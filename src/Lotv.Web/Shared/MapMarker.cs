namespace Lotv.Web.Shared;

/// <summary>
/// Represents a single pin on a Leaflet map. State must be a 2-letter US postal code.
/// Value controls the circle radius (log-scaled). Tooltip appears in the popup body.
/// </summary>
public record MapMarker(string Label, string State, double Value, string Color = "#2d469d", string? Tooltip = null);
