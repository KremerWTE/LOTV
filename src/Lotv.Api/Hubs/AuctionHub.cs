using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Lotv.Api.Hubs;

/// <summary>
/// Real-time hub for silent auction live bidding.
/// Supports anonymous viewers (public event attendees) and authenticated staff.
/// Group strategy: "auction-{eventId}" — all subscribers for a specific event's auction.
/// </summary>
[AllowAnonymous]
public class AuctionHub : Hub
{
    /// <summary>Called by clients to subscribe to real-time updates for a specific event.</summary>
    public async Task JoinEvent(int eventId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"auction-{eventId}");

    /// <summary>Called by clients when leaving an auction view.</summary>
    public async Task LeaveEvent(int eventId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction-{eventId}");
}

/// <summary>Typed client interface for strongly-typed hub invocations.</summary>
public interface IAuctionClient
{
    /// <summary>Broadcast when a new bid is placed on an item.</summary>
    Task BidPlaced(int itemId, decimal newHighBid, string? bidderName, int totalBids);

    /// <summary>Broadcast when an individual item is closed.</summary>
    Task ItemClosed(int itemId, decimal? winningBid, string? winnerName, string status);

    /// <summary>Broadcast when the entire event auction is closed.</summary>
    Task AuctionClosed(int eventId, int itemsClosed);
}
