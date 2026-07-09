using Microsoft.AspNetCore.SignalR.Client;

namespace Lotv.Web.Services;

/// <summary>
/// Manages the SignalR connection to RequestsHub.
/// On reconnect, fires OnReconnected so components can re-fetch state from the API.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly JwtAuthStateProvider _authState;
    private readonly string _apiBaseUrl;

    // ── Events (components subscribe to these) ────────────────────────────────
    public event Func<int, string, string, Task>? OnCaseStatusChanged;
    public event Func<int, int, string, Task>? OnCaseAssigned;
    public event Func<int, string, string, Task>? OnCaseCreated;
    public event Func<int, string, Task>? OnCaseEscalated;
    public event Func<int, int, Task>? OnOverdueAlert;
    public event Func<int, decimal, string, Task>? OnDonationReceived;
    public event Func<Task>? OnReconnected;

    public HubConnectionState State => _hub?.State ?? HubConnectionState.Disconnected;

    public SignalRService(JwtAuthStateProvider authState, IConfiguration config)
    {
        _authState = authState;
        _apiBaseUrl = config["ApiBaseUrl"] ?? "http://localhost:5275";
    }

    public async Task ConnectAsync()
    {
        if (_hub?.State == HubConnectionState.Connected) return;

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_apiBaseUrl}/hubs/requests", opts =>
            {
                opts.AccessTokenProvider = () =>
                    Task.FromResult(_authState.GetAccessToken());
            })
            .WithAutomaticReconnect()
            .Build();

        // ── Register server → client handlers ─────────────────────────────────
        _hub.On<int, string, string>("CaseStatusChanged",
            async (id, status, by) => { if (OnCaseStatusChanged is not null) await OnCaseStatusChanged(id, status, by); });

        _hub.On<int, int, string>("CaseAssigned",
            async (id, volId, name) => { if (OnCaseAssigned is not null) await OnCaseAssigned(id, volId, name); });

        _hub.On<int, string, string>("CaseCreated",
            async (id, family, reason) => { if (OnCaseCreated is not null) await OnCaseCreated(id, family, reason); });

        _hub.On<int, string>("CaseEscalated",
            async (id, reason) => { if (OnCaseEscalated is not null) await OnCaseEscalated(id, reason); });

        _hub.On<int, int>("OverdueAlert",
            async (id, days) => { if (OnOverdueAlert is not null) await OnOverdueAlert(id, days); });

        _hub.On<int, decimal, string>("DonationReceived",
            async (id, amount, channel) => { if (OnDonationReceived is not null) await OnDonationReceived(id, amount, channel); });

        // ── Reconnect: fire event so components re-fetch from API ──────────────
        _hub.Reconnected += async _ =>
        {
            if (OnReconnected is not null) await OnReconnected();
        };

        await _hub.StartAsync();
    }

    public async Task DisconnectAsync()
    {
        if (_hub is not null)
            await _hub.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
