using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

/// <summary>
/// Generates a shipping label when a package case reaches the Packing stage. The one seam a real
/// carrier integration (platform not yet confirmed) needs to fill: swap the registered implementation
/// for one that calls a real carrier API and returns a real tracking number, carrier and label file.
/// </summary>
public interface IShippingLabelGenerator
{
    Task<ShippingLabel> GenerateAsync(PackageRequest request);
}
