using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// One-time (and safe to repeat) catch-up for the change that makes every new donation create a pending Fund Allocation:
/// gives any older, unallocated donation a pending allocation too, so the Allocations queue reflects everything waiting
/// to be assigned rather than only donations received after the change.
/// </summary>
public static class AllocationBackfill
{
    public static async Task<int> RunAsync(LotvDbContext db)
    {
        var allocatedDonationIds = await db.FundAllocations.Select(a => a.DonationId).ToListAsync();
        var missing = await db.Donations
            .Where(d => d.AllocationStatus == AllocationStatus.Unallocated && !allocatedDonationIds.Contains(d.Id))
            .ToListAsync();
        if (missing.Count == 0) return 0;

        foreach (var d in missing)
        {
            d.AllocationStatus = AllocationStatus.PendingReview;
            db.FundAllocations.Add(new FundAllocation { DonationId = d.Id, Amount = d.Amount, Status = AllocationStatus.PendingReview, CreatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        return missing.Count;
    }
}
