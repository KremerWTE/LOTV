using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>The startup catch-up: an older, unallocated donation with no Fund Allocation gets one; nothing else is touched.</summary>
[Collection("Integration")]
public class AllocationBackfillTests
{
    private readonly LotvApiFactory _factory;
    public AllocationBackfillTests(LotvApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AnOlderUnallocatedDonation_GetsAPendingAllocation_ButAnAlreadyAllocatedOneIsLeftAlone()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var donor = new Donor { FirstName = "Old", LastName = "Donor", Email = $"old-{Guid.NewGuid():N}@test.com", ChapterId = 1, CreatedAt = DateTime.UtcNow };
        db.Donors.Add(donor);
        await db.SaveChangesAsync();
        var unallocated = new Donation { DonorId = donor.Id, Amount = 60m, ChapterId = 1, Channel = DonationChannel.Check, AllocationStatus = AllocationStatus.Unallocated, Date = DateTime.UtcNow };
        var alreadyAllocated = new Donation { DonorId = donor.Id, Amount = 90m, ChapterId = 1, Channel = DonationChannel.Online, AllocationStatus = AllocationStatus.Allocated, Date = DateTime.UtcNow };
        db.Donations.AddRange(unallocated, alreadyAllocated);
        await db.SaveChangesAsync();

        var count = await AllocationBackfill.RunAsync(db);
        Assert.Equal(1, count);
        Assert.Equal(1, await db.FundAllocations.CountAsync(a => a.DonationId == unallocated.Id));
        Assert.Equal(0, await db.FundAllocations.CountAsync(a => a.DonationId == alreadyAllocated.Id));
        Assert.Equal(AllocationStatus.PendingReview, (await db.Donations.AsNoTracking().SingleAsync(d => d.Id == unallocated.Id)).AllocationStatus);

        var again = await AllocationBackfill.RunAsync(db);
        Assert.Equal(0, again);   // safe to repeat
    }
}
