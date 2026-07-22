namespace Lotv.Core.Models;

// Every status has at least one way forward and one way to be corrected (via OnHold),
// but arbitrary any-to-any jumps (e.g. New straight to Fulfilled, or un-cancelling
// directly into Shipped) aren't allowed — the intake -> assign -> ship -> fulfill
// order has to be walked, and terminal states (Fulfilled, Cancelled) require going
// through OnHold to be reopened rather than being editable in place.
public static class CaseStatusTransitions
{
    private static readonly Dictionary<CaseStatus, CaseStatus[]> Allowed = new()
    {
        [CaseStatus.New]              = [CaseStatus.InProgress, CaseStatus.OnHold, CaseStatus.Cancelled],
        // New is reachable from InProgress too — a volunteer declining their
        // assignment (VolunteerPending.razor) sends the case back to the
        // unassigned queue rather than through OnHold.
        [CaseStatus.InProgress]       = [CaseStatus.New, CaseStatus.AwaitingShipment, CaseStatus.Fulfilled, CaseStatus.OnHold, CaseStatus.Cancelled],
        // Fulfilled is reachable directly from AwaitingShipment too — some
        // packages are hand-delivered locally rather than formally shipped
        // (VolunteerMyAssignments.razor's "Mark Complete" action).
        [CaseStatus.AwaitingShipment] = [CaseStatus.Shipped, CaseStatus.Fulfilled, CaseStatus.OnHold, CaseStatus.Cancelled],
        [CaseStatus.Shipped]          = [CaseStatus.Fulfilled, CaseStatus.OnHold],
        [CaseStatus.OnHold]           = [CaseStatus.New, CaseStatus.InProgress, CaseStatus.AwaitingShipment, CaseStatus.Cancelled],
        [CaseStatus.Fulfilled]        = [CaseStatus.OnHold],
        [CaseStatus.Cancelled]        = [CaseStatus.OnHold],
    };

    public static bool IsValid(CaseStatus from, CaseStatus to) =>
        from == to || (Allowed.TryGetValue(from, out var next) && next.Contains(to));

    public static CaseStatus[] ValidNextStates(CaseStatus from) =>
        Allowed.GetValueOrDefault(from, []);
}
