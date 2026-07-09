using Microsoft.AspNetCore.SignalR.Client;

namespace Lotv.Web.Services;

/// <summary>
/// Manages the SignalR connection to AuctionHub.
/// Allows anonymous viewing — no JWT required.
/// Call JoinEventAsync(id) after connecting to subscribe to a specific event's auction.
/// </summary>
public class AuctionSignalRService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly string _apiBaseUrl;

    // ── Events (components subscribe) ─────────────────────────────────────────
    public event Func<int, decimal, string?, int, Task>? OnBidPlaced;      // itemId, newHigh, bidderName, totalBids
    public event Func<int, decimal?, string?, string, Task>? OnItemClosed; // itemId, winningBid, winnerName, status
    public event Func<int, int, Task>? OnAuctionClosed;                    // eventId, itemsClosed

    public HubConnectionState State => _hub?.State ?? HubConnectionState.Disconnected;

    public AuctionSignalRService(IConfiguration config)
    {
        _apiBaseUrl = config["ApiBaseUrl"] ?? "http://localhost:5275";
    }

    public async Task ConnectAsync()
    {
        if (_hub?.State == HubConnectionState.Connected) return;

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_apiBaseUrl}/hubs/auction")   // anonymous — no access token
            .WithAutomaticReconnect()
            .Build();

        _hub.On<int, decimal, string?, int>("BidPlaced",
            async (itemId, newHigh, bidder, total) =>
            {
                if (OnBidPlaced is not null) await OnBidPlaced(itemId, newHigh, bidder, total);
            });

        _hub.On<int, decimal?, string?, string>("ItemClosed",
            async (itemId, winBid, winName, status) =>
            {
                if (OnItemClosed is not null) await OnItemClosed(itemId, winBid, winName, status);
            });

        _hub.On<int, int>("AuctionClosed",
            async (eventId, count) =>
            {
                if (OnAuctionClosed is not null) await OnAuctionClosed(eventId, count);
            });

        await _hub.StartAsync();
    }

    public async Task JoinEventAsync(int eventId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.SendAsync("JoinEvent", eventId);
    }

    public async Task LeaveEventAsync(int eventId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.SendAsync("LeaveEvent", eventId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
