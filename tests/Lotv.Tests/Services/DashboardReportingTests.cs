using Lotv.Api.Data;
using Lotv.Core.Models;
using Lotv.Core.Reporting;
using Lotv.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Tests.Services;

/// <summary>
/// Tests for dashboard aggregation and reporting query logic using EF InMemory.
/// Verifies the SQL-equivalent queries that IDashboardService and IReportingService implementations must satisfy.
/// </summary>
public class DashboardReportingTests : IDisposable
{
    private readonly LotvDbContext _db;

    public DashboardReportingTests()
    {
        var opts = new DbContextOptionsBuilder<LotvDbContext>()
            .UseInMemoryDatabase($"dash-{Guid.NewGuid():N}")
            .Options;
        _db = new LotvDbContext(opts);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<Donor> AddDonorAsync(int chapterId, string name = "Test Donor")
    {
        var d = _db.Donors.Add(new Donor
        {
            FirstName = name.Split(' ')[0], LastName = name.Contains(' ') ? name.Split(' ')[1] : "X",
            Email = $"{name.Replace(' ', '.')}@test.com", ChapterId = chapterId
        }).Entity;
        await _db.SaveChangesAsync();
        return d;
    }

    private async Task<Donation> AddDonationAsync(int donorId, int chapterId, decimal amount,
        DonationChannel channel = DonationChannel.Online, DateTime? date = null)
    {
        var d = _db.Donations.Add(new Donation
        {
            DonorId = donorId, ChapterId = chapterId,
            Amount = amount, Channel = channel,
            Date = date ?? DateTime.UtcNow,
            AllocationStatus = AllocationStatus.Unallocated
        }).Entity;
        await _db.SaveChangesAsync();
        return d;
    }

    // ── Total donations ────────────────────────────────────────────────────────

    [Fact]
    public async Task TotalDonations_SumOfAllDonations_MatchesSeedData()
    {
        var donor = await AddDonorAsync(1);
        await AddDonationAsync(donor.Id, 1, 100m);
        await AddDonationAsync(donor.Id, 1, 250m);
        await AddDonationAsync(donor.Id, 1, 50m);

        var total = await _db.Donations.Where(d => d.ChapterId == 1).SumAsync(d => d.Amount);

        Assert.Equal(400m, total);
    }

    [Fact]
    public async Task TotalDonations_ChapterScoped_ExcludesOtherChapters()
    {
        var d1 = await AddDonorAsync(1);
        var d2 = await AddDonorAsync(2);
        await AddDonationAsync(d1.Id, 1, 200m);
        await AddDonationAsync(d2.Id, 2, 500m);

        var ch1Total = await _db.Donations.Where(d => d.ChapterId == 1).SumAsync(d => d.Amount);

        Assert.Equal(200m, ch1Total);
    }

    [Fact]
    public async Task TotalDonations_HqRollup_SumsAllChapters()
    {
        var d1 = await AddDonorAsync(1);
        var d2 = await AddDonorAsync(2);
        await AddDonationAsync(d1.Id, 1, 200m);
        await AddDonationAsync(d2.Id, 2, 300m);

        var hqTotal = await _db.Donations.SumAsync(d => d.Amount);

        Assert.Equal(500m, hqTotal);
    }

    // ── Channel breakdown ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChannelBreakdown_GroupByChannel_ReturnsSumPerChannel()
    {
        var donor = await AddDonorAsync(1);
        await AddDonationAsync(donor.Id, 1, 100m, DonationChannel.Online);
        await AddDonationAsync(donor.Id, 1, 50m,  DonationChannel.Online);
        await AddDonationAsync(donor.Id, 1, 200m, DonationChannel.Check);

        var breakdown = await _db.Donations
            .Where(d => d.ChapterId == 1)
            .GroupBy(d => d.Channel)
            .Select(g => new { Channel = g.Key, Total = g.Sum(d => d.Amount) })
            .ToListAsync();

        var online = breakdown.First(b => b.Channel == DonationChannel.Online);
        var check  = breakdown.First(b => b.Channel == DonationChannel.Check);
        Assert.Equal(150m, online.Total);
        Assert.Equal(200m, check.Total);
    }

    [Fact]
    public async Task ChannelBreakdown_Percentage_SumsTo100()
    {
        var donor = await AddDonorAsync(1);
        await AddDonationAsync(donor.Id, 1, 300m, DonationChannel.Online);
        await AddDonationAsync(donor.Id, 1, 200m, DonationChannel.Check);
        await AddDonationAsync(donor.Id, 1, 100m, DonationChannel.Cash);

        var totals = await _db.Donations.Where(d => d.ChapterId == 1)
            .GroupBy(d => d.Channel)
            .Select(g => new { Total = g.Sum(d => d.Amount) })
            .ToListAsync();

        var grandTotal = totals.Sum(t => t.Total);
        var pcts = totals.Select(t => t.Total / grandTotal * 100).ToList();

        Assert.InRange(pcts.Sum(), 99.9m, 100.1m);
    }

    // ── Requests fulfilled ─────────────────────────────────────────────────────

    [Fact]
    public async Task RequestsFulfilled_CountsOnlyFulfilledStatus()
    {
        _db.Requests.AddRange(
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Fulfilled },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Fulfilled },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.New       },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Cancelled }
        );
        await _db.SaveChangesAsync();

        var fulfilled = await _db.Requests
            .CountAsync(r => r.ChapterId == 1 && r.Status == CaseStatus.Fulfilled);

        Assert.Equal(2, fulfilled);
    }

    // ── Date range filtering ───────────────────────────────────────────────────

    [Fact]
    public async Task DateRangeFilter_ExcludesDonationsOutsideRange()
    {
        var donor = await AddDonorAsync(1);
        var start = new DateTime(2026, 1, 1);
        var end   = new DateTime(2026, 1, 31);

        await AddDonationAsync(donor.Id, 1, 100m, date: new DateTime(2026, 1, 15));  // inside
        await AddDonationAsync(donor.Id, 1, 200m, date: new DateTime(2026, 2, 1));   // outside
        await AddDonationAsync(donor.Id, 1, 50m,  date: new DateTime(2025, 12, 31)); // outside

        var inRange = await _db.Donations
            .Where(d => d.ChapterId == 1 && d.Date >= start && d.Date <= end)
            .SumAsync(d => d.Amount);

        Assert.Equal(100m, inRange);
    }

    // ── Donation by amount band ────────────────────────────────────────────────

    [Fact]
    public async Task AmountBand_SmallGifts_CorrectlyBanded()
    {
        var donor = await AddDonorAsync(1);
        await AddDonationAsync(donor.Id, 1, 10m);   // < $25
        await AddDonationAsync(donor.Id, 1, 24m);   // < $25
        await AddDonationAsync(donor.Id, 1, 50m);   // $25–$99
        await AddDonationAsync(donor.Id, 1, 400m);  // $100–$499

        var donations = await _db.Donations.Where(d => d.ChapterId == 1).ToListAsync();

        var under25  = donations.Count(d => d.Amount < 25m);
        var mid      = donations.Count(d => d.Amount >= 25m && d.Amount < 100m);
        var over100  = donations.Count(d => d.Amount >= 100m && d.Amount < 500m);

        Assert.Equal(2, under25);
        Assert.Equal(1, mid);
        Assert.Equal(1, over100);
    }

    // ── People helped (PeopleHelped = distinct families with fulfilled requests) ──

    [Fact]
    public async Task PeopleHelped_CountsDistinctFamilies()
    {
        _db.Requests.AddRange(
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Fulfilled },
            new PackageRequest { FamilyId = 1, ChapterId = 1, Status = CaseStatus.Fulfilled }, // same family — should count once
            new PackageRequest { FamilyId = 2, ChapterId = 1, Status = CaseStatus.Fulfilled },
            new PackageRequest { FamilyId = 3, ChapterId = 1, Status = CaseStatus.New }       // not fulfilled
        );
        await _db.SaveChangesAsync();

        var peopleHelped = await _db.Requests
            .Where(r => r.ChapterId == 1 && r.Status == CaseStatus.Fulfilled)
            .Select(r => r.FamilyId)
            .Distinct()
            .CountAsync();

        Assert.Equal(2, peopleHelped);
    }

    // ── ImpactSummary construction ─────────────────────────────────────────────

    [Fact]
    public void ImpactSummary_HqRollup_ChapterIdIsNull()
    {
        var summary = new ImpactSummary(
            ChapterId:           null,
            TotalMoneyRaised:    50000m,
            TotalMoneyAllocated: 45000m,
            PeopleHelped:        120,
            RequestsFulfilled:   85,
            ActiveVolunteers:    12,
            PeriodStart:         new DateTime(2026, 1, 1),
            PeriodEnd:           new DateTime(2026, 12, 31)
        );

        Assert.Null(summary.ChapterId);
        Assert.Equal(50000m, summary.TotalMoneyRaised);
        Assert.True(summary.TotalMoneyAllocated <= summary.TotalMoneyRaised);
    }

    [Fact]
    public void ImpactSummary_ChapterScoped_ChapterIdIsSet()
    {
        var summary = new ImpactSummary(
            ChapterId:           5,
            TotalMoneyRaised:    10000m,
            TotalMoneyAllocated: 9500m,
            PeopleHelped:        20,
            RequestsFulfilled:   18,
            ActiveVolunteers:    4,
            PeriodStart:         new DateTime(2026, 1, 1),
            PeriodEnd:           new DateTime(2026, 12, 31)
        );

        Assert.Equal(5, summary.ChapterId);
    }

    // ── Receipt content (model-level) ──────────────────────────────────────────

    [Fact]
    public async Task Receipt_DonorEmail_IsRequiredField()
    {
        var donor = await AddDonorAsync(1, "Receipt Donor");
        var loaded = await _db.Donors.FindAsync(donor.Id);
        Assert.False(string.IsNullOrWhiteSpace(loaded!.Email));
    }

    [Fact]
    public async Task Receipt_DonationDate_IsRecorded()
    {
        var donor    = await AddDonorAsync(1);
        var donation = await AddDonationAsync(donor.Id, 1, 150m, date: new DateTime(2026, 3, 15));
        var loaded   = await _db.Donations.FindAsync(donation.Id);
        Assert.Equal(new DateTime(2026, 3, 15), loaded!.Date.Date);
    }

    [Fact]
    public async Task Receipt_TaxYear_MatchesDonationYear()
    {
        var donor    = await AddDonorAsync(1);
        var donation = await AddDonationAsync(donor.Id, 1, 100m, date: new DateTime(2025, 12, 31));
        var loaded   = await _db.Donations.FindAsync(donation.Id);
        Assert.Equal(2025, loaded!.Date.Year);
    }

    // ── Year-end statement (all donations in a year) ───────────────────────────

    [Fact]
    public async Task YearEndStatement_IncludesAllDonationsForYear()
    {
        var donor = await AddDonorAsync(1);
        await AddDonationAsync(donor.Id, 1, 100m, date: new DateTime(2025, 1, 15));
        await AddDonationAsync(donor.Id, 1, 200m, date: new DateTime(2025, 6, 10));
        await AddDonationAsync(donor.Id, 1, 50m,  date: new DateTime(2026, 1, 1)); // different year

        var year2025 = await _db.Donations
            .Where(d => d.DonorId == donor.Id && d.Date.Year == 2025)
            .ToListAsync();

        Assert.Equal(2, year2025.Count);
        Assert.Equal(300m, year2025.Sum(d => d.Amount));
    }
}
