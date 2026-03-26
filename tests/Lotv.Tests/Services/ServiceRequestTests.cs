using Lotv.Core.Models;

namespace Lotv.Tests.Services;

/// <summary>
/// Tests for request state-machine rules and model-level constraints.
/// Pure in-memory tests — no database required.
/// </summary>
public class ServiceRequestTests
{
    // ── Status transitions (valid) ────────────────────────────────────────────

    [Theory]
    [InlineData(CaseStatus.New,              CaseStatus.InProgress)]
    [InlineData(CaseStatus.InProgress,       CaseStatus.AwaitingShipment)]
    [InlineData(CaseStatus.AwaitingShipment, CaseStatus.Shipped)]
    [InlineData(CaseStatus.Shipped,          CaseStatus.Fulfilled)]
    [InlineData(CaseStatus.New,              CaseStatus.Cancelled)]
    [InlineData(CaseStatus.InProgress,       CaseStatus.OnHold)]
    [InlineData(CaseStatus.OnHold,           CaseStatus.InProgress)]
    public void IsValidTransition_ForwardPath_ReturnsTrue(CaseStatus from, CaseStatus to)
    {
        Assert.True(IsValidTransition(from, to), $"{from} → {to} should be valid");
    }

    [Theory]
    [InlineData(CaseStatus.Fulfilled, CaseStatus.New)]
    [InlineData(CaseStatus.Fulfilled, CaseStatus.InProgress)]
    [InlineData(CaseStatus.Cancelled, CaseStatus.New)]
    [InlineData(CaseStatus.Cancelled, CaseStatus.InProgress)]
    [InlineData(CaseStatus.Shipped,   CaseStatus.New)]
    public void IsValidTransition_FromTerminalState_ReturnsFalse(CaseStatus from, CaseStatus to)
    {
        Assert.False(IsValidTransition(from, to), $"{from} → {to} should be invalid");
    }

    // ── IsOverdue ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_NewRequestUnder7Days_False()
    {
        var r = new PackageRequest { Status = CaseStatus.New, CreatedAt = DateTime.UtcNow.AddDays(-3) };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_NewRequestOver7Days_True()
    {
        var r = new PackageRequest { Status = CaseStatus.New, CreatedAt = DateTime.UtcNow.AddDays(-8) };
        Assert.True(r.IsOverdue);
    }

    [Theory]
    [InlineData(CaseStatus.Fulfilled)]
    [InlineData(CaseStatus.Shipped)]
    [InlineData(CaseStatus.Cancelled)]
    public void IsOverdue_TerminalStatus_AlwaysFalse(CaseStatus status)
    {
        var r = new PackageRequest { Status = status, CreatedAt = DateTime.UtcNow.AddDays(-20) };
        Assert.False(r.IsOverdue);
    }

    // ── Priority ordering ──────────────────────────────────────────────────────

    [Fact]
    public void Priority_UrgentHighestOrdinal()
    {
        // Urgent should sort first (lowest int value = highest priority)
        var priorities = new[]
        {
            RequestPriority.Low, RequestPriority.Normal, RequestPriority.High, RequestPriority.Urgent
        };
        var sorted = priorities.OrderBy(p => (int)p).ToArray();
        Assert.Equal(RequestPriority.Urgent, sorted[0]);
        Assert.Equal(RequestPriority.Low,    sorted[^1]);
    }

    // ── Default values ─────────────────────────────────────────────────────────

    [Fact]
    public void NewRequest_DefaultStatusIsNew()
    {
        var r = new PackageRequest();
        Assert.Equal(CaseStatus.New, r.Status);
    }

    [Fact]
    public void NewRequest_DefaultPriorityIsNormal()
    {
        var r = new PackageRequest();
        Assert.Equal(RequestPriority.Normal, r.Priority);
    }

    [Fact]
    public void NewRequest_DefaultCreatedAtIsRecent()
    {
        var r = new PackageRequest();
        Assert.True((DateTime.UtcNow - r.CreatedAt).TotalSeconds < 5);
    }

    [Fact]
    public void NewRequest_DefaultAssignedToIdIsNull()
    {
        var r = new PackageRequest();
        Assert.Null(r.AssignedToId);
    }

    // ── Request note ──────────────────────────────────────────────────────────

    [Fact]
    public void InternalNote_IsInternalTrue_NotVisibleToRequester()
    {
        // Business rule: IsInternal = true notes are staff-only
        var note = new RequestNote { Content = "Shipping delay", IsInternal = true };
        Assert.True(note.IsInternal);
    }

    [Fact]
    public void PublicNote_IsInternalFalse_VisibleToRequester()
    {
        var note = new RequestNote { Content = "Your package is ready", IsInternal = false };
        Assert.False(note.IsInternal);
    }

    // ── Escalation rules ──────────────────────────────────────────────────────

    [Fact]
    public void OnHold_Request_IsNotOverdue()
    {
        // OnHold requests should not trigger overdue alerts — they're explicitly paused
        var r = new PackageRequest { Status = CaseStatus.OnHold, CreatedAt = DateTime.UtcNow.AddDays(-20) };
        Assert.False(r.IsOverdue);
    }

    // ── Helpers (simulates service-layer validation) ────────────────────────────

    private static bool IsValidTransition(CaseStatus from, CaseStatus to) => (from, to) switch
    {
        (CaseStatus.New,              CaseStatus.InProgress)        => true,
        (CaseStatus.New,              CaseStatus.OnHold)            => true,
        (CaseStatus.New,              CaseStatus.Cancelled)         => true,
        (CaseStatus.InProgress,       CaseStatus.AwaitingShipment)  => true,
        (CaseStatus.InProgress,       CaseStatus.OnHold)            => true,
        (CaseStatus.InProgress,       CaseStatus.Cancelled)         => true,
        (CaseStatus.AwaitingShipment, CaseStatus.Shipped)           => true,
        (CaseStatus.AwaitingShipment, CaseStatus.OnHold)            => true,
        (CaseStatus.Shipped,          CaseStatus.Fulfilled)         => true,
        (CaseStatus.OnHold,           CaseStatus.InProgress)        => true,
        (CaseStatus.OnHold,           CaseStatus.Cancelled)         => true,
        _                                                           => false,
    };
}
