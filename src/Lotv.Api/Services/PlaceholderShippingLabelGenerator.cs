using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;

namespace Lotv.Api.Services;

/// <summary>
/// Phase-one stand-in for a real carrier integration (see IShippingLabelGenerator). The shipping
/// platform is still unconfirmed, so this only produces a placeholder tracking number — no carrier,
/// service level or label file — just enough to prove out the trigger and record shape.
/// </summary>
public class PlaceholderShippingLabelGenerator : IShippingLabelGenerator
{
    public Task<ShippingLabel> GenerateAsync(PackageRequest request) => Task.FromResult(new ShippingLabel
    {
        PackageRequestId = request.Id,
        TrackingNumber = $"PLACEHOLDER-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
        IsPlaceholder = true,
        GeneratedAt = DateTime.UtcNow
    });
}
