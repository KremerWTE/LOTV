using Lotv.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Lotv.Api.Hubs;

/// <summary>
/// Real-time hub for broadcasting request state changes to chapter staff.
/// Group strategy: "chapter-{chapterId}" per chapter, "hq" for HQAdmin.
/// </summary>
[Authorize]
public class RequestsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var role = user?.FindFirstValue("role");
        var chapterIdClaim = user?.FindFirstValue("chapterId");

        if (role == nameof(UserRole.HQAdmin))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "hq");
        }
        else if (int.TryParse(chapterIdClaim, out var chapterId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chapter-{chapterId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = Context.User;
        var role = user?.FindFirstValue("role");
        var chapterIdClaim = user?.FindFirstValue("chapterId");

        if (role == nameof(UserRole.HQAdmin))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "hq");
        }
        else if (int.TryParse(chapterIdClaim, out var chapterId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chapter-{chapterId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}

// ── Typed client interface for strongly-typed hub calls ───────────────────────
public interface IRequestsClient
{
    Task CaseStatusChanged(int caseId, string newStatus, string updatedBy);
    Task CaseAssigned(int caseId, int volunteerId, string volunteerName);
    Task CaseCreated(int caseId, string familyName, string reason);
    Task CaseEscalated(int caseId, string reason);
    Task OverdueAlert(int caseId, int ageInDays);
    Task DonationReceived(int donationId, decimal amount, string channel);
}
