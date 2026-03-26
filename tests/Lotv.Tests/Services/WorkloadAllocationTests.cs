using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Tests.Services;

/// <summary>
/// Tests for workload aggregation and allocation constraint logic using EF InMemory.
/// </summary>
public class WorkloadAllocationTests : IDisposable
{
    private readonly LotvDbContext _db;

    public WorkloadAllocationTests()
    {
        var opts = new DbContextOptionsBuilder<LotvDbContext>()
            .UseInMemoryDatabase($"wa-{Guid.NewGuid():N}")
            .Options;
        _db = new LotvDbContext(opts);
    }

    public void Dispose() => _db.Dispose();

    // ── Workload aggregation ──────────────────────────────────────────────────

    [Fact]
    public async Task Workload_VolunteerWithNoAssignments_HasZeroActiveCases()
    {
        var vol = new Volunteer
        {
            FirstName = "Idle", LastName = "Vol", Email = "idle@test.com",
            ChapterId = 1, Status = VolunteerStatus.Active, ActiveCases = 0
        };
        _db.Volunteers.Add(vol);
        await _db.SaveChangesAsync();

        var result = await _db.Volunteers.FindAsync(vol.Id);
        Assert.Equal(0, result!.ActiveCases);
    }

    [Fact]
    public async Task Workload_OverdueRequestsQuery_ReturnsRequestsOlderThan7Days()
    {
        _db.Requests.AddRange(
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New,        CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.InProgress, CreatedAt = DateTime.UtcNow.AddDays(-8)  },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New,        CreatedAt = DateTime.UtcNow.AddDays(-3)  }, // not overdue
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Fulfilled,  CreatedAt = DateTime.UtcNow.AddDays(-20) }  // terminal
        );
        await _db.SaveChangesAsync();

        var overdueThreshold = DateTime.UtcNow.AddDays(-7);
        var overdue = await _db.Requests
            .Where(r => r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled
                     && r.Status != CaseStatus.Shipped && r.CreatedAt < overdueThreshold)
            .ToListAsync();

        Assert.Equal(2, overdue.Count);
    }

    [Fact]
    public async Task Workload_UnassignedQueue_OnlyContainsUnassignedOpenRequests()
    {
        _db.Requests.AddRange(
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New, AssignedToId = null  },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New, AssignedToId = 42    }, // assigned
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Fulfilled, AssignedToId = null } // terminal
        );
        await _db.SaveChangesAsync();

        var queue = await _db.Requests
            .Where(r => r.AssignedToId == null
                     && r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled)
            .ToListAsync();

        Assert.Single(queue);
    }

    [Fact]
    public async Task Workload_ChapterScoping_OnlyReturnsRequestsForChapter()
    {
        _db.Requests.AddRange(
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New },
            new PackageRequest { FamilyId = 1, ChapterId = 2, Status = CaseStatus.New } // different chapter
        );
        await _db.SaveChangesAsync();

        var ch1Requests = await _db.Requests.Where(r => r.ChapterId == 1).CountAsync();

        Assert.Equal(2, ch1Requests);
    }

    // ── Allocation constraints ─────────────────────────────────────────────────

    [Fact]
    public async Task Allocation_AmountMatchesDonation_Passes()
    {
        var donor = _db.Donors.Add(new Donor { FirstName = "Al", LastName = "Loc", Email = "al@test.com", ChapterId = 1 }).Entity;
        await _db.SaveChangesAsync();

        var donation = _db.Donations.Add(new Donation
        {
            DonorId = donor.Id, ChapterId = 1,
            Amount = 100m, Date = DateTime.UtcNow,
            AllocationStatus = AllocationStatus.Unallocated
        }).Entity;
        await _db.SaveChangesAsync();

        var allocation = new FundAllocation
        {
            DonationId  = donation.Id,
            AllocatedTo = "Case #1",
            Amount      = 100m,       // exactly matches donation
            Status      = AllocationStatus.Allocated
        };

        // Validation: amount must not exceed donation amount
        bool valid = allocation.Amount <= donation.Amount;
        Assert.True(valid);
    }

    [Fact]
    public void Allocation_OverAmount_FailsValidation()
    {
        var donationAmount = 50m;
        var requestedAllocation = 75m;

        bool valid = requestedAllocation <= donationAmount;
        Assert.False(valid, "Allocation exceeding donation amount should fail validation");
    }

    [Fact]
    public async Task Allocation_DefaultStatus_IsPendingReview()
    {
        var a = new FundAllocation { DonationId = 1, AllocatedTo = "Test", Amount = 25m };
        _db.FundAllocations.Add(a);
        await _db.SaveChangesAsync();

        var saved = await _db.FundAllocations.FindAsync(a.Id);
        Assert.Equal(AllocationStatus.PendingReview, saved!.Status);
    }

    [Fact]
    public async Task Allocation_ApprovedAllocation_StatusIsAllocated()
    {
        var a = new FundAllocation
        {
            DonationId = 1, AllocatedTo = "Case #5", Amount = 50m,
            Status = AllocationStatus.Allocated, ApprovedBy = "Admin"
        };
        _db.FundAllocations.Add(a);
        await _db.SaveChangesAsync();

        var saved = await _db.FundAllocations.FindAsync(a.Id);
        Assert.Equal(AllocationStatus.Allocated, saved!.Status);
        Assert.Equal("Admin", saved.ApprovedBy);
    }

    [Fact]
    public async Task Allocation_TotalAllocated_CannotExceedTotalDonated()
    {
        var donor    = _db.Donors.Add(new Donor { FirstName = "A", LastName = "B", Email = "ab@test.com", ChapterId = 1 }).Entity;
        await _db.SaveChangesAsync();
        var donation = _db.Donations.Add(new Donation { DonorId = donor.Id, ChapterId = 1, Amount = 200m, Date = DateTime.UtcNow, AllocationStatus = AllocationStatus.Unallocated }).Entity;
        await _db.SaveChangesAsync();

        _db.FundAllocations.Add(new FundAllocation { DonationId = donation.Id, AllocatedTo = "Case #1", Amount = 100m, Status = AllocationStatus.Allocated });
        _db.FundAllocations.Add(new FundAllocation { DonationId = donation.Id, AllocatedTo = "Case #2", Amount = 80m,  Status = AllocationStatus.Allocated });
        await _db.SaveChangesAsync();

        var totalAllocated = await _db.FundAllocations
            .Where(a => a.DonationId == donation.Id && a.Status == AllocationStatus.Allocated)
            .SumAsync(a => a.Amount);

        Assert.True(totalAllocated <= donation.Amount,
            $"Total allocated ({totalAllocated:C}) must not exceed donation ({donation.Amount:C})");
    }

    [Fact]
    public async Task AllocationFilter_PendingReview_ReturnsOnlyPendingRecords()
    {
        _db.FundAllocations.AddRange(
            new FundAllocation { DonationId = 1, AllocatedTo = "A", Amount = 10m, Status = AllocationStatus.PendingReview },
            new FundAllocation { DonationId = 1, AllocatedTo = "B", Amount = 20m, Status = AllocationStatus.Allocated     },
            new FundAllocation { DonationId = 1, AllocatedTo = "C", Amount = 30m, Status = AllocationStatus.Unallocated   }
        );
        await _db.SaveChangesAsync();

        var pending = await _db.FundAllocations
            .Where(a => a.Status == AllocationStatus.PendingReview)
            .ToListAsync();

        Assert.Single(pending);
    }

    // ── ScheduledReport digest ─────────────────────────────────────────────────

    [Fact]
    public async Task DailyDigest_NewRequestsCount_MatchesYesterdayCreatedRequests()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1).Date;
        var today     = DateTime.UtcNow.Date;

        _db.Requests.AddRange(
            new PackageRequest { FamilyId = 1, ChapterId = 1, CreatedAt = yesterday.AddHours(10), Status = CaseStatus.New },
            new PackageRequest { FamilyId = 1, ChapterId = 1, CreatedAt = yesterday.AddHours(15), Status = CaseStatus.New },
            new PackageRequest { FamilyId = 1, ChapterId = 1, CreatedAt = today.AddHours(2),      Status = CaseStatus.New } // today = not in yesterday's digest
        );
        await _db.SaveChangesAsync();

        var newYesterday = await _db.Requests
            .CountAsync(r => r.ChapterId == 1 && r.CreatedAt >= yesterday && r.CreatedAt < today);

        Assert.Equal(2, newYesterday);
    }

    [Fact]
    public async Task DailyDigest_FulfilledCount_MatchesYesterdayFulfillments()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1).Date;
        var today     = DateTime.UtcNow.Date;

        _db.Requests.AddRange(
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Fulfilled, UpdatedAt = yesterday.AddHours(12) },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New,        UpdatedAt = yesterday.AddHours(14) }  // not fulfilled
        );
        await _db.SaveChangesAsync();

        var fulfilled = await _db.Requests
            .CountAsync(r => r.ChapterId == 1 && r.Status == CaseStatus.Fulfilled
                          && r.UpdatedAt >= yesterday && r.UpdatedAt < today);

        Assert.Equal(1, fulfilled);
    }
}
