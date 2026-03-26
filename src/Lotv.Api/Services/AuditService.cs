using Lotv.Api.Data;
using Lotv.Core.Models;

namespace Lotv.Api.Services;

/// <summary>
/// Records immutable audit entries for financial allocation events.
/// Entries are append-only — never updated or deleted — providing a tamper-evident trail.
/// </summary>
public interface IFinancialAuditService
{
    Task LogAsync(string action, string entity, string entityId, string actor, string? details = null, string? ipAddress = null);
    Task LogAllocationCreatedAsync(FundAllocation alloc, string actor, string? ip = null);
    Task LogAllocationApprovedAsync(FundAllocation alloc, string actor, string? ip = null);
    Task LogAllocationRejectedAsync(FundAllocation alloc, string actor, string reason, string? ip = null);
}

public class FinancialAuditService(LotvDbContext db) : IFinancialAuditService
{
    public async Task LogAsync(string action, string entity, string entityId, string actor, string? details = null, string? ipAddress = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            Action    = action,
            Entity    = entity,
            EntityId  = entityId,
            UserName  = actor,
            Details   = details,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public Task LogAllocationCreatedAsync(FundAllocation alloc, string actor, string? ip = null) =>
        LogAsync(
            action:    "AllocationCreated",
            entity:    "FundAllocation",
            entityId:  alloc.Id.ToString(),
            actor:     actor,
            details:   $"DonationId={alloc.DonationId} AllocatedTo=\"{alloc.AllocatedTo}\" Amount={alloc.Amount:C} Status={alloc.Status}",
            ipAddress: ip);

    public Task LogAllocationApprovedAsync(FundAllocation alloc, string actor, string? ip = null) =>
        LogAsync(
            action:    "AllocationApproved",
            entity:    "FundAllocation",
            entityId:  alloc.Id.ToString(),
            actor:     actor,
            details:   $"DonationId={alloc.DonationId} AllocatedTo=\"{alloc.AllocatedTo}\" Amount={alloc.Amount:C} ApprovedBy={alloc.ApprovedBy}",
            ipAddress: ip);

    public Task LogAllocationRejectedAsync(FundAllocation alloc, string actor, string reason, string? ip = null) =>
        LogAsync(
            action:    "AllocationRejected",
            entity:    "FundAllocation",
            entityId:  alloc.Id.ToString(),
            actor:     actor,
            details:   $"DonationId={alloc.DonationId} Amount={alloc.Amount:C} Reason=\"{reason}\"",
            ipAddress: ip);
}
