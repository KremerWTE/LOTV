using Lotv.Core.Models;
using Lotv.Core.Services;

namespace Lotv.Tests;

public class MockDataServiceTests
{
    private static MockDataService CreateSvc() => new();

    // ── Families ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddFamily_AssignsId_AndAppearsInGetFamilies()
    {
        var svc    = CreateSvc();
        var before = svc.GetFamilies().Count;
        var family = new Family { Parent1FirstName = "Jane", Parent1LastName = "Doe", Email = "jane@example.com" };

        svc.AddFamily(family);

        Assert.True(family.Id > 0);
        Assert.Equal(before + 1, svc.GetFamilies().Count);
    }

    [Fact]
    public void GetFamily_ReturnsCorrectRecord()
    {
        var svc    = CreateSvc();
        var family = new Family { Parent1FirstName = "Anna", Parent1LastName = "Smith", Email = "anna@example.com" };
        svc.AddFamily(family);

        var result = svc.GetFamily(family.Id);

        Assert.NotNull(result);
        Assert.Equal("Anna", result.Parent1FirstName);
    }

    [Fact]
    public void GetFamily_UnknownId_ReturnsNull()
    {
        var svc = CreateSvc();
        Assert.Null(svc.GetFamily(99999));
    }

    [Fact]
    public void UpdateFamily_PersistsChanges()
    {
        var svc    = CreateSvc();
        var family = svc.GetFamilies().First();
        var orig   = family.Parent1FirstName;
        family.Parent1FirstName = "Updated";

        svc.UpdateFamily(family);

        var result = svc.GetFamily(family.Id);
        Assert.Equal("Updated", result?.Parent1FirstName);
        Assert.NotEqual(orig, result?.Parent1FirstName);
    }

    // ── Requests / Cases ──────────────────────────────────────────────────────

    [Fact]
    public void AddRequest_AssignsId_AndAppearsInGetRequests()
    {
        var svc    = CreateSvc();
        var before = svc.GetRequests().Count;
        var req    = new PackageRequest { FamilyId = 1, Reason = PackageReason.Miscarriage };

        svc.AddRequest(req);

        Assert.True(req.Id > 0);
        Assert.Equal(before + 1, svc.GetRequests().Count);
    }

    [Fact]
    public void AddRequest_IdsAreUnique()
    {
        var svc = CreateSvc();
        var r1  = new PackageRequest { FamilyId = 1 };
        var r2  = new PackageRequest { FamilyId = 2 };

        svc.AddRequest(r1);
        svc.AddRequest(r2);

        Assert.NotEqual(r1.Id, r2.Id);
    }

    [Fact]
    public void UpdateRequest_PersistsStatusChange()
    {
        var svc = CreateSvc();
        var req = svc.GetRequests().First(r => r.Status == CaseStatus.New);
        req.Status = CaseStatus.InProgress;

        svc.UpdateRequest(req);

        var result = svc.GetRequest(req.Id);
        Assert.Equal(CaseStatus.InProgress, result?.Status);
    }

    // ── Volunteer Round-Robin ──────────────────────────────────────────────────

    [Fact]
    public void GetNextAvailableVolunteer_ReturnsActiveVolunteer()
    {
        var svc = CreateSvc();
        var vol = svc.GetNextAvailableVolunteer();

        Assert.NotNull(vol);
        Assert.Equal(VolunteerStatus.Active, vol.Status);
    }

    [Fact]
    public void GetNextAvailableVolunteer_PrefersFewerOpenCases()
    {
        var svc        = CreateSvc();
        var volunteers = svc.GetVolunteers().Where(v => v.Status == VolunteerStatus.Active).ToList();
        Assert.True(volunteers.Count >= 2, "Need at least 2 active volunteers for this test");

        // Assign many cases to the first active volunteer
        var heavy = volunteers[0];
        for (int i = 0; i < 5; i++)
        {
            var r = new PackageRequest { FamilyId = 1, AssignedTo = heavy.FullName, Status = CaseStatus.New };
            svc.AddRequest(r);
        }

        var next = svc.GetNextAvailableVolunteer();
        Assert.NotEqual(heavy.FullName, next?.FullName);
    }

    [Fact]
    public void GetNextAvailableVolunteer_WhenNoActiveVolunteers_ReturnsNull()
    {
        var svc        = CreateSvc();
        var volunteers = svc.GetVolunteers().ToList(); // snapshot — GetVolunteers returns the live list
        foreach (var v in volunteers)
        {
            v.Status = VolunteerStatus.Inactive;
            svc.UpdateVolunteer(v);
        }

        var result = svc.GetNextAvailableVolunteer();
        Assert.Null(result);
    }

    // ── Donors ────────────────────────────────────────────────────────────────

    [Fact]
    public void AddDonor_AssignsId_AndAppearsInGetDonors()
    {
        var svc    = CreateSvc();
        var before = svc.GetDonors().Count;
        var donor  = new Donor { FirstName = "Mary", LastName = "Test", Email = "mary@test.com" };

        svc.AddDonor(donor);

        Assert.True(donor.Id > 0);
        Assert.Equal(before + 1, svc.GetDonors().Count);
    }

    [Fact]
    public void UpdateDonor_PersistsChanges()
    {
        var svc   = CreateSvc();
        var donor = svc.GetDonors().First();
        donor.TotalGiven += 500m;

        svc.UpdateDonor(donor);

        var result = svc.GetDonor(donor.Id);
        Assert.NotNull(result);
        Assert.Equal(donor.TotalGiven, result.TotalGiven);
    }

    // ── Donations ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddDonation_AssignsId_AndAppearsInGetDonations()
    {
        var svc    = CreateSvc();
        var before = svc.GetDonations().Count;
        var donor  = svc.GetDonors().First();
        var gift   = new Donation { DonorId = donor.Id, Amount = 100m, Date = DateTime.UtcNow, Channel = DonationChannel.Online };

        svc.AddDonation(gift);

        Assert.True(gift.Id > 0);
        Assert.Equal(before + 1, svc.GetDonations().Count);
    }

    [Fact]
    public void AddDonation_CanFilterByDonorId()
    {
        var svc   = CreateSvc();
        var donor = svc.GetDonors().First();
        svc.AddDonation(new Donation { DonorId = donor.Id, Amount = 50m, Date = DateTime.UtcNow, Channel = DonationChannel.Check });
        svc.AddDonation(new Donation { DonorId = donor.Id, Amount = 75m, Date = DateTime.UtcNow, Channel = DonationChannel.Online });

        var donorGifts = svc.GetDonations().Where(d => d.DonorId == donor.Id).ToList();
        Assert.True(donorGifts.Count >= 2);
    }

    // ── Volunteers ────────────────────────────────────────────────────────────

    [Fact]
    public void AddVolunteer_AppearsInGetVolunteers()
    {
        var svc    = CreateSvc();
        var before = svc.GetVolunteers().Count;
        var vol    = new Volunteer { FirstName = "Tom", LastName = "Helper", Email = "tom@test.com", JoinedDate = DateTime.UtcNow };

        svc.AddVolunteer(vol);

        Assert.True(vol.Id > 0);
        Assert.Equal(before + 1, svc.GetVolunteers().Count);
    }

    [Fact]
    public void UpdateVolunteer_PersistsStatusChange()
    {
        var svc = CreateSvc();
        var vol = svc.GetVolunteers().First(v => v.Status == VolunteerStatus.Active);
        vol.Status = VolunteerStatus.Inactive;

        svc.UpdateVolunteer(vol);

        var updated = svc.GetVolunteers().First(v => v.Id == vol.Id);
        Assert.Equal(VolunteerStatus.Inactive, updated.Status);
    }

    // ── Audit Log ─────────────────────────────────────────────────────────────

    [Fact]
    public void LogAction_AppearsInGetAuditLog()
    {
        var svc    = CreateSvc();
        var before = svc.GetAuditLog().Count;

        svc.LogAction("TestUser", "Created", "Family", "42", "Test entry");

        var log = svc.GetAuditLog();
        Assert.Equal(before + 1, log.Count);
        var entry = log.First(e => e.UserName == "TestUser" && e.EntityId == "42");
        Assert.Equal("Created", entry.Action);
        Assert.Equal("Family", entry.Entity);
        Assert.Equal("Test entry", entry.Details);
    }

    [Fact]
    public void GetAuditLog_ReturnsMostRecentFirst()
    {
        var svc = CreateSvc();
        svc.LogAction("User1", "Created", "Family", "1", null);
        svc.LogAction("User2", "Updated", "Family", "2", null);

        var log = svc.GetAuditLog();
        Assert.True(log[0].Timestamp >= log[1].Timestamp);
    }

    // ── Dashboard Stats ───────────────────────────────────────────────────────

    [Fact]
    public void GetDashboardStats_OpenCasesMatchesActual()
    {
        var svc   = CreateSvc();
        var stats = svc.GetDashboardStats();
        var actual = svc.GetRequests()
            .Count(r => r.Status is CaseStatus.New or CaseStatus.InProgress or CaseStatus.AwaitingShipment);

        Assert.Equal(actual, stats.OpenCases);
    }

    [Fact]
    public void GetDashboardStats_ActiveVolunteersMatchesActual()
    {
        var svc    = CreateSvc();
        var stats  = svc.GetDashboardStats();
        var actual = svc.GetVolunteers().Count(v => v.Status == VolunteerStatus.Active);

        Assert.Equal(actual, stats.ActiveVolunteers);
    }

    [Fact]
    public void GetDashboardStats_UpcomingEventsMatchesActual()
    {
        var svc    = CreateSvc();
        var stats  = svc.GetDashboardStats();
        var actual = svc.GetEvents().Count(e => e.Date >= DateTime.UtcNow);

        Assert.Equal(actual, stats.UpcomingEvents);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public void AddEvent_AppearsInGetEvents()
    {
        var svc    = CreateSvc();
        var before = svc.GetEvents().Count;
        var evt    = new MinistryEvent { Title = "Test Gala", Type = EventType.Gala, Date = DateTime.UtcNow.AddDays(30) };

        svc.AddEvent(evt);

        Assert.True(evt.Id > 0);
        Assert.Equal(before + 1, svc.GetEvents().Count);
    }

    // ── Allocations ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdateAllocation_PersistsStatusChange()
    {
        var svc   = CreateSvc();
        var alloc = svc.GetAllocations().First(a => a.Status == AllocationStatus.PendingReview);
        alloc.Status     = AllocationStatus.Allocated;
        alloc.ApprovedBy = "TestUser";
        alloc.ApprovedAt = DateTime.UtcNow;

        svc.UpdateAllocation(alloc);

        var updated = svc.GetAllocations().First(a => a.Id == alloc.Id);
        Assert.Equal(AllocationStatus.Allocated, updated.Status);
        Assert.Equal("TestUser", updated.ApprovedBy);
    }

    // ── Model Logic ───────────────────────────────────────────────────────────

    [Fact]
    public void PackageRequest_IsOverdue_WhenOlderThan7Days_AndNotFulfilled()
    {
        var req = new PackageRequest
        {
            Status    = CaseStatus.New,
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        };
        Assert.True(req.IsOverdue);
    }

    [Fact]
    public void PackageRequest_IsNotOverdue_WhenFulfilled()
    {
        var req = new PackageRequest
        {
            Status    = CaseStatus.Fulfilled,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void PackageRequest_IsNotOverdue_WhenRecentlyCreated()
    {
        var req = new PackageRequest
        {
            Status    = CaseStatus.New,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void Family_FullName_CombinesParentNames()
    {
        var f = new Family { Parent1FirstName = "Jane", Parent1LastName = "Doe" };
        Assert.Equal("Jane Doe", f.FullName);
    }

    [Fact]
    public void Donor_FullName_CombinesNames()
    {
        var d = new Donor { FirstName = "John", LastName = "Smith" };
        Assert.Equal("John Smith", d.FullName);
    }

    [Fact]
    public void Volunteer_FullName_CombinesNames()
    {
        var v = new Volunteer { FirstName = "Sarah", LastName = "Jones" };
        Assert.Equal("Sarah Jones", v.FullName);
    }
}
