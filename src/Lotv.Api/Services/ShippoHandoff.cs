using Lotv.Api.Data;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>
/// Pushes a case to Shippo and records the outcome on its ShippingLabel row. Callers save changes.
/// Skips silently (returns false, no error recorded) when Shippo isn't configured, and never throws.
/// </summary>
public static class ShippoHandoff
{
    public static async Task<bool> PushAsync(LotvDbContext db, IShippoOrderClient shippo, PackageRequest request, ShippingLabel label)
    {
        if (!shippo.IsConfigured || label.ShippoOrderId is not null) return false;

        var family = request.Family ?? await db.Families.FindAsync(request.FamilyId);
        if (family is null) { label.ShippoSyncError = "Case has no family on file."; return false; }

        var result = await shippo.CreateOrderAsync(request, family);
        if (result.Success)
        {
            label.ShippoOrderId = result.OrderId;
            label.ShippoSyncedAt = DateTime.UtcNow;
            label.ShippoSyncError = null;
        }
        else label.ShippoSyncError = result.Error;
        return result.Success;
    }
}
