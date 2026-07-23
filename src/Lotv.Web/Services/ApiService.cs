using Lotv.Core.Models;
using Lotv.Core.Reporting;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lotv.Web.Services;

/// <summary>
/// Typed HTTP client for the LOTV API. Injects JWT Bearer token on every request.
/// Falls back gracefully when the API is unavailable (returns empty lists).
/// </summary>
public class ApiService
{
    private readonly HttpClient _http;
    private readonly JwtAuthStateProvider _authState;
    private readonly AuthService _auth;

    // The API serializes enums as strings (ConfigureHttpJsonOptions in Lotv.Api/Program.cs
    // adds JsonStringEnumConverter). System.Net.Http.Json's default options do NOT include
    // that converter, so without this, any ReadFromJsonAsync<T> for a type with an enum
    // property (PackageRequest.Status, .Reason, .Category, ...) throws a JsonException that
    // GetAsync's catch-all silently swallows into an empty list/null — the request looks
    // like it succeeded (200 OK) but the page renders as if there were no data at all.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiService(HttpClient http, JwtAuthStateProvider authState, AuthService auth)
    {
        _http = http;
        _authState = authState;
        _auth = auth;
    }

    // ── Requests / Cases ──────────────────────────────────────────────────────
    public Task<List<PackageRequest>> GetRequestsAsync(string? status = null, bool? overdue = null, int? familyId = null)
    {
        var qs = BuildQs(("status", status), ("overdue", overdue?.ToString().ToLower()), ("familyId", familyId?.ToString()));
        return GetListAsync<PackageRequest>($"/api/v1/requests{qs}");
    }

    public Task<List<PackageRequest>> GetRequestQueueAsync() =>
        GetListAsync<PackageRequest>("/api/v1/requests/queue");

    public Task<List<PackageRequest>> GetOverdueRequestsAsync() =>
        GetListAsync<PackageRequest>("/api/v1/requests/overdue");

    public Task<PackageRequest?> GetRequestAsync(int id) =>
        GetAsync<PackageRequest>($"/api/v1/requests/{id}");

    public async Task<PackageRequest?> CreateRequestAsync(PackageRequest req)
    {
        var resp = await AuthedPostAsync("/api/v1/requests", req);
        return resp?.IsSuccessStatusCode == true
            ? await resp.Content.ReadFromJsonAsync<PackageRequest>(JsonOpts) : null;
    }

    public Task<List<RequestNote>> GetRequestNotesAsync(int id) =>
        GetListAsync<RequestNote>($"/api/v1/requests/{id}/notes");

    public async Task<RequestNote?> CreateRequestNoteAsync(int id, RequestNote note)
    {
        var resp = await AuthedPostAsync($"/api/v1/requests/{id}/notes", note);
        return resp?.IsSuccessStatusCode == true
            ? await resp.Content.ReadFromJsonAsync<RequestNote>(JsonOpts) : null;
    }

    public Task<List<RequestActivity>> GetRequestActivityAsync(int id) =>
        GetListAsync<RequestActivity>($"/api/v1/requests/{id}/activity");

    // ── Families ─────────────────────────────────────────────────────────────
    public Task<List<Family>> GetFamiliesAsync(string? search = null) =>
        GetListAsync<Family>($"/api/v1/families{BuildQs(("search", search))}");

    public Task<Family?> GetFamilyAsync(int id) =>
        GetAsync<Family>($"/api/v1/families/{id}");

    public Task<List<PackageRequest>> GetDuplicateReviewQueueAsync() =>
        GetListAsync<PackageRequest>("/api/v1/families/duplicate-review");

    public async Task<bool> ResolveDuplicateReviewAsync(int requestId, string action)
    {
        var resp = await AuthedPostAsync($"/api/v1/families/duplicate-review/{requestId}/resolve", new { Action = action });
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Volunteers ────────────────────────────────────────────────────────────
    public Task<List<Volunteer>> GetVolunteersAsync(string? status = null) =>
        GetListAsync<Volunteer>($"/api/v1/volunteers{BuildQs(("status", status))}");

    public Task<List<VolunteerScoreResult>> GetVolunteerCandidatesAsync(int requestId) =>
        GetListAsync<VolunteerScoreResult>($"/api/v1/requests/{requestId}/candidates");

    // ── Donors ────────────────────────────────────────────────────────────────
    public Task<List<Donor>> GetDonorsAsync(string? search = null) =>
        GetListAsync<Donor>($"/api/v1/donors{BuildQs(("search", search))}");

    public Task<Donor?> GetDonorAsync(int id) =>
        GetAsync<Donor>($"/api/v1/donors/{id}");

    public Task<List<Donation>> GetDonorContributionsAsync(int donorId) =>
        GetListAsync<Donation>($"/api/v1/donors/{donorId}/contributions");

    // ── Donations ─────────────────────────────────────────────────────────────
    public Task<List<Donation>> GetDonationsAsync(int? donorId = null, string? channel = null) =>
        GetListAsync<Donation>($"/api/v1/donations{BuildQs(("donorId", donorId?.ToString()), ("channel", channel))}");

    // ── Events ────────────────────────────────────────────────────────────────
    public Task<List<MinistryEvent>> GetEventsAsync(string? status = null) =>
        GetListAsync<MinistryEvent>($"/api/v1/events{BuildQs(("status", status))}");

    public Task<MinistryEvent?> GetEventAsync(int id) =>
        GetAsync<MinistryEvent>($"/api/v1/events/{id}");

    public Task<List<EventAttendee>> GetAttendeesAsync(int eventId) =>
        GetListAsync<EventAttendee>($"/api/v1/events/{eventId}/attendees");

    public Task<List<SilentAuctionItem>> GetAuctionItemsAsync(int eventId) =>
        GetListAsync<SilentAuctionItem>($"/api/v1/events/{eventId}/auction");

    public Task<EventRevenueDto?> GetEventRevenueAsync(int eventId) =>
        GetAsync<EventRevenueDto>($"/api/v1/events/{eventId}/revenue");

    public async Task<EventAttendee?> CreateAttendeeAsync(int eventId, EventAttendee attendee)
    {
        var resp = await AuthedPostAsync($"/api/v1/events/{eventId}/attendees", attendee);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<EventAttendee>(JsonOpts) : null;
    }

    public async Task<bool> CheckInAttendeeAsync(int eventId, int attendeeId)
    {
        var resp = await AuthedPutAsync($"/api/v1/events/{eventId}/attendees/{attendeeId}/checkin", new { });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<CheckinScanResult?> ScanTicketAsync(int eventId, string code)
    {
        var resp = await AuthedPostAsync($"/api/v1/events/{eventId}/scan", new { Code = code });
        if (resp is null) return null;
        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadFromJsonAsync<ScanOkBody>(JsonOpts);
            var name = body?.Attendee?.Donor?.FullName;
            var tickets = body?.Attendee?.TicketCount ?? 1;
            return new CheckinScanResult(true, "Checked in!", name, tickets);
        }
        if ((int)resp.StatusCode == 409)
        {
            var body = await resp.Content.ReadFromJsonAsync<ScanErrorBody>(JsonOpts);
            var name = body?.Attendee?.Donor?.FullName;
            var tickets = body?.Attendee?.TicketCount ?? 1;
            return new CheckinScanResult(false, body?.Error ?? "Already checked in.", name, tickets);
        }
        var err = await resp.Content.ReadFromJsonAsync<ScanErrorBody>(JsonOpts);
        return new CheckinScanResult(false, err?.Error ?? "Ticket not found.", null, 0);
    }

    private record ScanOkBody(string? Message, EventAttendee? Attendee);
    private record ScanErrorBody(string? Error, EventAttendee? Attendee, DateTime? CheckedInAt);

    public async Task<SilentAuctionItem?> CreateAuctionItemAsync(int eventId, SilentAuctionItem item)
    {
        var resp = await AuthedPostAsync($"/api/v1/events/{eventId}/auction", item);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<SilentAuctionItem>(JsonOpts) : null;
    }

    public async Task<bool> PlaceBidAsync(int eventId, int itemId, int bidderId, decimal amount)
    {
        var resp = await AuthedPostAsync($"/api/v1/events/{eventId}/auction/{itemId}/bid", new { BidderId = bidderId, BidAmount = amount });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> CloseAuctionAsync(int eventId)
    {
        var resp = await AuthedPostAsync($"/api/v1/events/{eventId}/auction/close", new { });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> CancelEventAsync(int eventId)
    {
        SetAuthHeader();
        try { return (await _http.DeleteAsync($"/api/v1/events/{eventId}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    // ── Allocations ───────────────────────────────────────────────────────────
    public Task<List<FundAllocation>> GetAllocationsAsync(string? status = null) =>
        GetListAsync<FundAllocation>($"/api/v1/allocations{BuildQs(("status", status))}");

    // ── Parishes & Dioceses ───────────────────────────────────────────────────
    public Task<List<Parish>> GetParishesAsync() =>
        GetListAsync<Parish>("/api/parishes");   // legacy route

    public Task<List<Diocese>> GetDiocesesAsync() =>
        GetListAsync<Diocese>("/api/v1/dioceses");

    // ── Workload ──────────────────────────────────────────────────────────────
    public Task<List<WorkloadRowDto>> GetWorkloadAsync() =>
        GetListAsync<WorkloadRowDto>("/api/v1/workload");

    // ── Dashboard stats ───────────────────────────────────────────────────────
    public Task<DashboardStatsDto?> GetDashboardStatsAsync() =>
        GetAsync<DashboardStatsDto>("/api/v1/dashboard/stats");

    public Task<List<ChapterSummaryRow>> GetHqSummaryAsync() =>
        GetListAsync<ChapterSummaryRow>("/api/v1/dashboard/hq");

    public Task<List<MoneyFlowCategoryDto>> GetMoneyFlowAsync() =>
        GetListAsync<MoneyFlowCategoryDto>("/api/v1/dashboard/money");

    public Task<List<ResourceFlowTypeDto>> GetResourceFlowAsync() =>
        GetListAsync<ResourceFlowTypeDto>("/api/v1/dashboard/resources");

    public Task<List<TimelinePointDto>> GetTimelineAsync() =>
        GetListAsync<TimelinePointDto>("/api/v1/dashboard/timeline");

    // Public endpoints (no auth required)
    public Task<PublicImpactDto?> GetPublicImpactAsync() =>
        GetAsync<PublicImpactDto>("/api/public/v1/impact");

    public Task<List<MoneyFlowCategoryDto>> GetPublicMoneyFlowAsync() =>
        GetListAsync<MoneyFlowCategoryDto>("/api/public/v1/transparency/money");

    public Task<List<TimelinePointDto>> GetPublicTimelineAsync() =>
        GetListAsync<TimelinePointDto>("/api/public/v1/transparency/timeline");

    public Task<List<PublicWishListItemDto>> GetPublicWishListAsync(string? category = null) =>
        GetListAsync<PublicWishListItemDto>($"/api/public/v1/wishlist{(category != null ? $"?category={Uri.EscapeDataString(category)}" : "")}");

    public Task<List<PublicEventDto>> GetPublicEventsAsync() =>
        GetListAsync<PublicEventDto>("/api/public/v1/events");

    public Task<List<PublicChapterDto>> GetPublicChaptersAsync() =>
        GetListAsync<PublicChapterDto>("/api/public/v1/chapters");

    public Task<PublicDonorImpactDto?> GetPublicDonorImpactAsync(int donorId) =>
        GetAsync<PublicDonorImpactDto>($"/api/public/v1/donors/{donorId}/impact");

    public Task<List<PublicDonationRow>> GetPublicDonorDonationsAsync(int donorId) =>
        GetListAsync<PublicDonationRow>($"/api/public/v1/donors/{donorId}/donations");

    public Task<DonorPortalStatusDto?> GetDonorPortalStatusAsync(int donorId) =>
        GetAsync<DonorPortalStatusDto>($"/api/public/v1/donors/{donorId}/portal-status");
    public record DonorPortalStatusDto(bool HasStripeCustomer, bool HasActiveRecurring);

    public async Task<string?> CreateBillingPortalUrlAsync(int donorId, string token)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/public/v1/donors/{donorId}/billing-portal",
                new { Token = token });
            if (!resp.IsSuccessStatusCode) return null;
            var doc = await resp.Content.ReadFromJsonAsync<BillingPortalDto>(JsonOpts);
            return doc?.Url;
        }
        catch { return null; }
    }
    private record BillingPortalDto(string Url);

    public async Task<BulkLinkResult?> SendBulkPortalLinksAsync()
    {
        try
        {
            var resp = await _http.PostAsync("/api/v1/donors/send-portal-link/bulk", null);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<BulkLinkResult>(JsonOpts);
        }
        catch { return null; }
    }
    public async Task<BulkLinkResult?> SendBulkPortalLinksByDioceseAsync(string diocese)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/v1/donors/send-portal-link/bulk-diocese?diocese={Uri.EscapeDataString(diocese)}", null);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<BulkLinkResult>(JsonOpts);
        }
        catch { return null; }
    }
    public record BulkLinkResult(int Sent, int Skipped);

    public async Task<bool> SendDonorPortalLinkAsync(int donorId, int days = 7)
    {
        try { var r = await _http.PostAsync($"/api/v1/donors/{donorId}/send-portal-link?days={days}", null); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> UpdateDonorAvatarAsync(int donorId, string? avatarUrl)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync($"/api/public/v1/donors/{donorId}/avatar",
                new { AvatarUrl = avatarUrl });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public Task<List<PublicFamilyRequestDto>> GetPublicFamilyRequestsAsync(int familyId) =>
        GetListAsync<PublicFamilyRequestDto>($"/api/public/v1/families/{familyId}/requests");

    // ── Sponsors ──────────────────────────────────────────────────────────────
    public Task<List<SponsorDto>> GetSponsorsAsync(string? status = null) =>
        GetListAsync<SponsorDto>($"/api/v1/sponsors{BuildQs(("status", status))}");

    public async Task<SponsorDto?> CreateSponsorAsync(string company, string contact, string email,
        string? phone, string? website, string? taxId, string tier, decimal committed, DateTime? renewal, string? notes)
    {
        var resp = await AuthedPostAsync("/api/v1/sponsors", new
        {
            CompanyName = company, ContactName = contact, Email = email, Phone = phone,
            Website = website, TaxId = taxId, Tier = tier, CommittedAmount = committed,
            RenewalDate = renewal, Notes = notes, Status = "Active"
        });
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<SponsorDto>(JsonOpts) : null;
    }

    // ── Notifications ─────────────────────────────────────────────────────────
    public Task<List<ReportRunLogDto>> GetReportRunLogsAsync(int take = 50) =>
        GetListAsync<ReportRunLogDto>($"/api/v1/notifications/run-logs?take={take}");

    public async Task<bool> SaveReportConfigAsync(object configs, string? hqWeeklyEmail, string? hqDailyEmail)
    {
        var resp = await AuthedPostAsync("/api/v1/notifications/report-config",
            new { HqWeeklyEmail = hqWeeklyEmail, HqDailyEmail = hqDailyEmail, Configs = configs });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<int> BroadcastNotificationAsync(string audience, string channel, string? subject, string body)
    {
        try
        {
            var resp = await AuthedPostAsync("/api/v1/notifications/broadcast",
                new { Audience = audience, Channel = channel, Subject = subject, Body = body });
            if (resp?.IsSuccessStatusCode != true) return 0;
            using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("estimatedRecipients").GetInt32();
        }
        catch { return 0; }
    }

    public async Task<int> SendMarketingEmailAsync(string? campaignName, string audience, string subject, string body)
    {
        try
        {
            var resp = await AuthedPostAsync("/api/v1/notifications/marketing-email",
                new { CampaignName = campaignName, Audience = audience, Subject = subject, Body = body });
            if (resp?.IsSuccessStatusCode != true) return 0;
            using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("estimatedRecipients").GetInt32();
        }
        catch { return 0; }
    }

    public Task<List<WishListItem>> GetWishListAsync(string? status = null, string? category = null) =>
        GetListAsync<WishListItem>($"/api/v1/wishlist{BuildQs(("status", status), ("category", category))}");

    public async Task<bool> FulfillWishListItemAsync(int id, int quantity, string? donorId = null)
    {
        var resp = await AuthedPostAsync($"/api/v1/wishlist/{id}/fulfill",
            new { Quantity = quantity, DonorId = donorId });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<WishListItem?> CreateWishListItemAsync(WishListItem item)
    {
        var resp = await AuthedPostAsync("/api/v1/wishlist", item);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<WishListItem>(JsonOpts) : null;
    }

    public async Task<bool> CancelWishListItemAsync(int id)
    {
        SetAuthHeader();
        try { return (await _http.DeleteAsync($"/api/v1/wishlist/{id}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> PublicEventRsvpAsync(int eventId, string name, string email, int guestCount)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/public/v1/events/{eventId}/rsvp",
                new { Name = name, Email = email, GuestCount = guestCount });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> CreateResourceDonationAsync(string donorName, string? email, string? phone,
        string resourceType, int quantity, string? unit, string? description, string? preference)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/public/v1/resource-donations",
                new { DonorName = donorName, Email = email, Phone = phone,
                      ResourceType = resourceType, Quantity = quantity, Unit = unit,
                      Description = description, Preference = preference });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public Task<List<RecurringScheduleDto>> GetDonorRecurringAsync(int donorId) =>
        GetListAsync<RecurringScheduleDto>($"/api/public/v1/donors/{donorId}/recurring");

    public async Task<int?> CreateDonorRecurringAsync(int donorId, decimal amount, string frequency, DateTime? startDate, string? campaign)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/public/v1/donors/{donorId}/recurring",
                new { Amount = amount, Frequency = frequency, StartDate = startDate, Campaign = campaign });
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("id").GetInt32();
        }
        catch { return null; }
    }

    public async Task<bool> PauseDonorRecurringAsync(int id)
    {
        try { return (await _http.PostAsync($"/api/public/v1/recurring/{id}/pause", null))?.IsSuccessStatusCode == true; }
        catch { return false; }
    }

    public async Task<bool> ResumeDonorRecurringAsync(int id)
    {
        try { return (await _http.PostAsync($"/api/public/v1/recurring/{id}/resume", null))?.IsSuccessStatusCode == true; }
        catch { return false; }
    }

    public async Task<bool> CancelDonorRecurringAsync(int id)
    {
        try { return (await _http.PostAsync($"/api/public/v1/recurring/{id}/cancel", null))?.IsSuccessStatusCode == true; }
        catch { return false; }
    }

    public async Task<bool> UpdateDonorRecurringAsync(int id, decimal amount, string frequency)
    {
        try
        {
            var resp = await _http.PatchAsJsonAsync($"/api/public/v1/recurring/{id}",
                new { Amount = amount, Frequency = frequency });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> UpdateFamilyProfileAsync(int familyId,
        string firstName, string lastName, string email, string phone,
        string street, string city, string state, string zip)
    {
        try
        {
            var resp = await _http.PatchAsJsonAsync($"/api/public/v1/families/{familyId}/profile",
                new { FirstName = firstName, LastName = lastName, Email = email, Phone = phone,
                      Street = street, City = city, State = state, Zip = zip });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<string?> UpdateAvatarAsync(string? avatarUrl)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync("/api/v1/users/me/avatar", new { AvatarUrl = avatarUrl });
            if (!resp.IsSuccessStatusCode) return null;
            var doc = await resp.Content.ReadFromJsonAsync<AvatarResp>(JsonOpts);
            return doc?.AvatarUrl;
        }
        catch { return null; }
    }
    private record AvatarResp(string? AvatarUrl);

    // ── Volunteer magic-link auth ─────────────────────────────────────────
    public async Task<bool> RequestVolunteerMagicLinkAsync(string email)
    {
        try { return (await _http.PostAsJsonAsync("/api/public/v1/volunteer/magic-link", new { Email = email })).IsSuccessStatusCode; }
        catch { return false; }
    }
    public async Task<DateTime?> RefreshVolunteerSessionAsync(string token)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/public/v1/volunteer/refresh-session", new { Token = token });
            if (!resp.IsSuccessStatusCode) return null;
            var doc = await resp.Content.ReadFromJsonAsync<VolMagicLinkResp>(JsonOpts);
            return doc?.ExpiresAt;
        }
        catch { return null; }
    }

    public async Task<VolMagicLinkResp?> VerifyVolunteerMagicLinkAsync(string token)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/public/v1/volunteer/verify-link", new { Token = token });
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<VolMagicLinkResp>(JsonOpts);
        }
        catch { return null; }
    }
    public record VolMagicLinkResp(int VolunteerId, DateTime ExpiresAt);

    public async Task<int> GetVolunteerAssignmentCountAsync(int volunteerId)
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<VolCountDto>($"/api/public/v1/volunteers/{volunteerId}/assignment-count", JsonOpts);
            return resp?.Count ?? 0;
        }
        catch { return 0; }
    }
    private record VolCountDto(int Count);

    // ── Donor magic-link auth ─────────────────────────────────────────────
    public async Task<bool> RequestDonorMagicLinkAsync(string email)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/public/v1/donor/magic-link", new { Email = email });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
    public async Task<DateTime?> RefreshDonorSessionAsync(string token)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/public/v1/donor/refresh-session", new { Token = token });
            if (!resp.IsSuccessStatusCode) return null;
            var doc = await resp.Content.ReadFromJsonAsync<MagicLinkResp>(JsonOpts);
            return doc?.ExpiresAt;
        }
        catch { return null; }
    }

    public async Task<MagicLinkResp?> VerifyDonorMagicLinkAsync(string token)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/public/v1/donor/verify-link", new { Token = token });
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<MagicLinkResp>(JsonOpts);
        }
        catch { return null; }
    }
    public record MagicLinkResp(int DonorId, DateTime ExpiresAt);

    // ── Push subscription ─────────────────────────────────────────────────
    public Task<MigrationsDto?> GetMigrationsAsync() =>
        GetAsync<MigrationsDto>("/api/v1/admin/migrations");
    public record MigrationsDto(List<string> Applied, List<string> Pending);

    public Task<List<WebhookEventRow>> GetWebhookEventsAsync(string? source = null) =>
        GetListAsync<WebhookEventRow>($"/api/v1/admin/webhooks{(source is null ? "" : $"?source={source}")}");
    public record WebhookEventRow(int Id, string Source, string ExternalId, string EventType, DateTime ReceivedAt);

    public Task<WebhookEventDetail?> GetWebhookEventAsync(int id) =>
        GetAsync<WebhookEventDetail>($"/api/v1/admin/webhooks/{id}");

    public async Task<int> BulkAllocateAsync(int[] ids, string status)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/donations/bulk-allocate", new { Ids = ids, Status = status });
            if (!resp.IsSuccessStatusCode) return 0;
            var doc = await resp.Content.ReadFromJsonAsync<BulkAllocateResp>(JsonOpts);
            return doc?.Updated ?? 0;
        }
        catch { return 0; }
    }
    private record BulkAllocateResp(int Updated);

    public async Task<int> BulkChannelAsync(int[] ids, string channel)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/donations/bulk-channel", new { Ids = ids, Channel = channel });
            if (!resp.IsSuccessStatusCode) return 0;
            var doc = await resp.Content.ReadFromJsonAsync<BulkAllocateResp>(JsonOpts);
            return doc?.Updated ?? 0;
        }
        catch { return 0; }
    }

    public async Task<bool> ReplayWebhookAsync(int id)
    {
        try { return (await _http.PostAsync($"/api/v1/admin/webhooks/{id}/replay", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<int?> PruneWebhooksAsync(int days = 90)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/v1/admin/webhooks/old?days={days}");
            if (!resp.IsSuccessStatusCode) return null;
            var doc = await resp.Content.ReadFromJsonAsync<PruneResp>(JsonOpts);
            return doc?.Deleted;
        }
        catch { return null; }
    }
    private record PruneResp(int Deleted);
    public record WebhookEventDetail(int Id, string Source, string ExternalId, string EventType, DateTime ReceivedAt, string? Payload);

    public async Task<VapidKeyPair?> GenerateVapidKeysAsync()
    {
        try
        {
            var resp = await _http.PostAsync("/api/v1/admin/vapid/generate", null);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<VapidKeyPair>(JsonOpts);
        }
        catch { return null; }
    }
    public record VapidKeyPair(string PublicKey, string PrivateKey);

    public Task<DiagnosticsDto?> GetDiagnosticsAsync() =>
        GetAsync<DiagnosticsDto>("/api/v1/admin/diagnostics");
    public record DiagnosticsDto(int PushSubscriptionCount, DateTime? FxLatest, double? FxAgeHours,
        string? LastMigration, int PendingMigrations, int WebhookEvents7d, int DonorsWithStripeCustomer,
        int WebhookEvents24h);

    public Task<List<PushSubRow>> GetPushSubscriptionsAsync() =>
        GetListAsync<PushSubRow>("/api/v1/push/subscriptions");

    public async Task<bool> RevokePushSubscriptionAsync(int id)
    {
        try { var r = await _http.DeleteAsync($"/api/v1/push/subscriptions/{id}"); return r.IsSuccessStatusCode; }
        catch { return false; }
    }
    public record PushSubRow(int Id, string UserId, string? UserName, string? UserEmail, string Endpoint, DateTime CreatedAt);

    public async Task<bool> SendTestPushToUserAsync(string userId)
    {
        try { var r = await _http.PostAsync($"/api/v1/push/test/{userId}", null); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> SendTestPushAsync()
    {
        try { var r = await _http.PostAsync("/api/v1/push/test", null); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> RegisterPushAsync(string endpoint, string p256dh, string auth)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/push/subscribe",
                new { Endpoint = endpoint, P256dh = p256dh, Auth = auth });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
    public async Task<string?> GetVapidPublicKeyAsync()
    {
        try { return await _http.GetStringAsync("/api/public/v1/push/vapid-public-key"); }
        catch { return null; }
    }

    // ── Multi-currency ────────────────────────────────────────────────────
    public async Task<List<CurrencyDto>?> GetCurrenciesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<CurrencyDto>>("/api/public/v1/currencies", JsonOpts); }
        catch { return null; }
    }
    public record CurrencyDto(string Code, string Symbol, string Name, decimal RateToUsd);

    public async Task<PaymentIntentDto?> CreatePaymentIntentAsync(decimal amount, string currency = "usd")
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/payments/intent",
                new { Amount = amount, Currency = currency });
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PaymentIntentDto>(JsonOpts);
        }
        catch { return null; }
    }
    public record PaymentIntentDto(string? ClientSecret, string PublishableKey, bool Mock);

    public Task<List<ChannelBreakdownDto>> GetDonationsByChannelAsync() =>
        GetListAsync<ChannelBreakdownDto>("/api/v1/dashboard/donations/by-channel");

    public Task<List<PersonBreakdownDto>> GetDonationsByPersonAsync(int page = 1) =>
        GetListAsync<PersonBreakdownDto>($"/api/v1/dashboard/donations/by-person?page={page}");

    public Task<List<DonationByCityDto>> GetDonationsByCityAsync() =>
        GetListAsync<DonationByCityDto>("/api/v1/dashboard/donations/by-city");

    public Task<List<DonationByAmountBandDto>> GetDonationsByAmountAsync() =>
        GetListAsync<DonationByAmountBandDto>("/api/v1/dashboard/donations/by-amount");

    public Task<List<DonationByDioceseDto>> GetDonationsByDioceseAsync() =>
        GetListAsync<DonationByDioceseDto>("/api/v1/dashboard/donations/by-diocese");

    // ── Audit log ─────────────────────────────────────────────────────────────
    public Task<List<AuditEntry>> GetAuditLogAsync(int page = 1) =>
        GetListAsync<AuditEntry>($"/api/v1/audit?page={page}");

    // ── Requests mutations ─────────────────────────────────────────────────────
    public async Task<bool> UpdateRequestStatusAsync(int id, CaseStatus status)
    {
        var resp = await AuthedPutAsync($"/api/v1/requests/{id}/status", new { Status = status });
        return resp?.IsSuccessStatusCode == true;
    }

    private record ErrorBody(string? error);

    // Same endpoint as UpdateRequestStatusAsync, but surfaces the server's validation
    // message (invalid transition, missing tracking number, etc.) instead of just true/false.
    public async Task<(bool Ok, string? Error)> UpdateRequestStatusWithErrorAsync(int id, CaseStatus status)
    {
        var resp = await AuthedPutAsync($"/api/v1/requests/{id}/status", new { Status = status });
        if (resp is null) return (false, "Network error — please try again.");
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<ErrorBody>(JsonOpts);
            return (false, body?.error ?? "Failed to update status.");
        }
        catch { return (false, "Failed to update status."); }
    }

    public async Task<bool> AssignRequestAsync(int id, int volunteerId)
    {
        var resp = await AuthedPutAsync($"/api/v1/requests/{id}/assign", new { VolunteerId = volunteerId });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateRequestPriorityAsync(int id, RequestPriority priority)
    {
        var resp = await AuthedPutAsync($"/api/v1/requests/{id}/priority", new { Priority = priority });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateRequestDueDateAsync(int id, DateTime dueDate)
    {
        var resp = await AuthedPutAsync($"/api/v1/requests/{id}/due-date", new { DueDate = dueDate });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> PatchRequestAsync(int id, string? trackingNumber, DateTime? shippedDate, string? internalNotes)
    {
        var resp = await AuthedPatchAsync($"/api/v1/requests/{id}",
            new { TrackingNumber = trackingNumber, ShippedDate = shippedDate, InternalNotes = internalNotes });
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Packing list ──────────────────────────────────────────────────────────
    public Task<List<PackageContentItem>> GetPackageItemsAsync(int requestId) =>
        GetListAsync<PackageContentItem>($"/api/v1/requests/{requestId}/items");

    public async Task<(bool Ok, string? Error)> AddPackageItemAsync(int requestId, int resourceItemId, int quantity)
    {
        var resp = await AuthedPostAsync($"/api/v1/requests/{requestId}/items", new { ResourceItemId = resourceItemId, Quantity = quantity });
        if (resp is null) return (false, "Network error — please try again.");
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<ErrorBody>(JsonOpts);
            return (false, body?.error ?? "Failed to add item.");
        }
        catch { return (false, "Failed to add item."); }
    }

    public async Task<bool> TogglePackedAsync(int requestId, int itemId)
    {
        var resp = await AuthedPutAsync($"/api/v1/requests/{requestId}/items/{itemId}/pack", new { });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> RemovePackageItemAsync(int requestId, int itemId)
    {
        try { var r = await _http.DeleteAsync($"/api/v1/requests/{requestId}/items/{itemId}"); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> AddRequestNoteAsync(int id, string content, bool isInternal = true)
    {
        var resp = await AuthedPostAsync($"/api/v1/requests/{id}/notes", new { Content = content, IsInternal = isInternal });
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Families mutations ─────────────────────────────────────────────────────
    public async Task<Family?> CreateFamilyAsync(Family family)
    {
        var resp = await AuthedPostAsync("/api/v1/families", family);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Family>(JsonOpts) : null;
    }

    public async Task<Family?> UpdateFamilyAsync(int id, Family family)
    {
        var resp = await AuthedPutAsync($"/api/v1/families/{id}", family);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Family>(JsonOpts) : null;
    }

    // ── Volunteers mutations ───────────────────────────────────────────────────
    public async Task<Volunteer?> CreateVolunteerAsync(Volunteer vol)
    {
        var resp = await AuthedPostAsync("/api/v1/volunteers", vol);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Volunteer>(JsonOpts) : null;
    }

    public async Task<Volunteer?> UpdateVolunteerAsync(int id, Volunteer vol)
    {
        var resp = await AuthedPutAsync($"/api/v1/volunteers/{id}", vol);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Volunteer>(JsonOpts) : null;
    }

    // ── Donors mutations ───────────────────────────────────────────────────────
    public async Task<Donor?> CreateDonorAsync(Donor donor)
    {
        var resp = await AuthedPostAsync("/api/v1/donors", donor);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donor>(JsonOpts) : null;
    }

    public async Task<Donor?> UpdateDonorAsync(int id, Donor donor)
    {
        var resp = await AuthedPutAsync($"/api/v1/donors/{id}", donor);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donor>(JsonOpts) : null;
    }

    // ── Donations mutations ────────────────────────────────────────────────────
    public async Task<Donation?> CreateDonationAsync(Donation donation)
    {
        var resp = await AuthedPostAsync("/api/v1/donations", donation);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donation>(JsonOpts) : null;
    }

    public async Task<Donation?> UpdateDonationAsync(int id, Donation donation)
    {
        var resp = await AuthedPutAsync($"/api/v1/donations/{id}", donation);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donation>(JsonOpts) : null;
    }

    // ── Allocations mutations ──────────────────────────────────────────────────
    public async Task<FundAllocation?> CreateAllocationAsync(FundAllocation alloc)
    {
        var resp = await AuthedPostAsync("/api/v1/allocations", alloc);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<FundAllocation>(JsonOpts) : null;
    }

    public async Task<FundAllocation?> UpdateAllocationAsync(int id, FundAllocation alloc)
    {
        var resp = await AuthedPutAsync($"/api/v1/allocations/{id}", alloc);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<FundAllocation>(JsonOpts) : null;
    }

    public Task<List<ResourceItem>> GetInventoryAsync(string? category = null) =>
        GetListAsync<ResourceItem>($"/api/v1/inventory{BuildQs(("category", category))}");

    public async Task<bool> AllocateResourceAsync(int resourceId, int requestId, int quantity, string? notes)
    {
        var resp = await AuthedPostAsync($"/api/v1/inventory/{resourceId}/allocate",
            new { requestId, quantity, notes });
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Events mutations ───────────────────────────────────────────────────────
    public async Task<MinistryEvent?> CreateEventAsync(MinistryEvent evt)
    {
        var resp = await AuthedPostAsync("/api/v1/events", evt);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<MinistryEvent>(JsonOpts) : null;
    }

    public async Task<MinistryEvent?> UpdateEventAsync(int id, MinistryEvent evt)
    {
        var resp = await AuthedPutAsync($"/api/v1/events/{id}", evt);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<MinistryEvent>(JsonOpts) : null;
    }

    // ── Public Intake (no auth required) ─────────────────────────────────────
    public async Task<bool> PublicApplyAsync(
        Family family,
        bool forSelf,
        string? packageType,
        string? referrerFirstName,
        string? referrerLastName,
        string? referrerEmail)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/public/apply", new
            {
                family,
                forSelf,
                packageType,
                referrerFirstName,
                referrerLastName,
                referrerEmail
            });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> PublicGiveAsync(Donor donor, Donation donation)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/public/give", new { donor, donation });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> PublicVolunteerSignupAsync(Volunteer vol)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/public/volunteer", vol);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Settings ──────────────────────────────────────────────────────────────
    public async Task<Dictionary<string, string>> GetSettingsAsync()
    {
        var result = await GetAsync<Dictionary<string, string>>("/api/v1/settings");
        return result ?? new Dictionary<string, string>();
    }

    public async Task<bool> SaveSettingsAsync(Dictionary<string, string> settings)
    {
        var resp = await AuthedPutAsync("/api/v1/settings", settings);
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Users ─────────────────────────────────────────────────────────────────
    public Task<List<StaffUserDto>> GetUsersAsync() =>
        GetListAsync<StaffUserDto>("/api/v1/users");

    public async Task<bool> ClearUserAvatarAsync(string id)
    {
        try { var r = await _http.DeleteAsync($"/api/v1/users/{id}/avatar"); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> UpdateUserRoleAsync(string id, UserRole role, int? chapterId)
    {
        var resp = await AuthedPutAsync($"/api/v1/users/{id}/role", new { Role = role, ChapterId = chapterId });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<(bool Ok, string? Error)> UpdateUserEmailAsync(string id, string? email)
    {
        var resp = await AuthedPutAsync($"/api/v1/users/{id}/email", new { Email = email });
        if (resp is null) return (false, "Network error — please try again.");
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOpts);
            return (false, body is not null && body.TryGetValue("error", out var msg) ? msg : "Failed to update email.");
        }
        catch { return (false, "Failed to update email."); }
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private void SetAuthHeader()
    {
        var token = _authState.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        SetAuthHeader();
        try
        {
            var resp = await _http.GetAsync(url);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (await _auth.RefreshTokenAsync())
                {
                    SetAuthHeader();
                    resp = await _http.GetAsync(url);
                }
            }
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<T>(JsonOpts) : null;
        }
        catch { return null; }
    }

    private async Task<List<T>> GetListAsync<T>(string url)
    {
        var result = await GetAsync<List<T>>(url);
        return result ?? [];
    }

    private async Task<HttpResponseMessage?> AuthedPostAsync<T>(string url, T body)
    {
        SetAuthHeader();
        try { return await _http.PostAsJsonAsync(url, body); }
        catch { return null; }
    }

    private async Task<HttpResponseMessage?> AuthedPutAsync<T>(string url, T body)
    {
        SetAuthHeader();
        try { return await _http.PutAsJsonAsync(url, body); }
        catch { return null; }
    }

    private async Task<HttpResponseMessage?> AuthedPatchAsync<T>(string url, T body)
    {
        SetAuthHeader();
        try { return await _http.PatchAsJsonAsync(url, body); }
        catch { return null; }
    }

    private async Task<HttpResponseMessage?> AuthedDeleteAsync(string url)
    {
        SetAuthHeader();
        try { return await _http.DeleteAsync(url); }
        catch { return null; }
    }

    // ── Reconciliation ────────────────────────────────────────────────────────
    public Task<List<ReconciliationRowDto>> GetReconciliationAsync(string period) =>
        GetListAsync<ReconciliationRowDto>($"/api/v1/reconciliation?period={Uri.EscapeDataString(period)}");

    // ── Onboarding ────────────────────────────────────────────────────────────
    public async Task<bool> CompleteStaffOnboardingAsync(
        string firstName, string lastName, string title, string phone, string notifyPref, int chapterId)
    {
        var resp = await AuthedPostAsync("/api/v1/users/onboarding/staff",
            new { FirstName = firstName, LastName = lastName, Title = title,
                  Phone = phone, NotifyPref = notifyPref, ChapterId = chapterId });
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> CompleteVolunteerOnboardingAsync(
        string firstName, string lastName, string phone,
        string street, string city, string state, string zip,
        IEnumerable<string> availableDays, IEnumerable<string> skills,
        int maxRequestsPerMonth, int chapterId)
    {
        var resp = await AuthedPostAsync("/api/v1/users/onboarding/volunteer",
            new { FirstName = firstName, LastName = lastName, Phone = phone,
                  Street = street, City = city, State = state, Zip = zip,
                  AvailableDays = availableDays, Skills = skills,
                  MaxRequestsPerMonth = maxRequestsPerMonth, ChapterId = chapterId });
        return resp?.IsSuccessStatusCode == true;
    }

    private static string BuildQs(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs.Where(p => !string.IsNullOrEmpty(p.Value))
                         .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? "?" + qs : "";
    }

    // ── Retreats ──────────────────────────────────────────────────────────────
    public Task<List<RetreatListDto>> GetRetreatsAsync() =>
        GetListAsync<RetreatListDto>("/api/v1/retreats");

    public Task<RetreatListDto?> GetRetreatAsync(int id) =>
        GetAsync<RetreatListDto>($"/api/v1/retreats/{id}");

    public async Task<RetreatListDto?> CreateRetreatAsync(object body)
    {
        var resp = await AuthedPostAsync("/api/v1/retreats", body);
        return resp?.IsSuccessStatusCode == true
            ? await resp.Content.ReadFromJsonAsync<RetreatListDto>(JsonOpts) : null;
    }

    public async Task<bool> UpdateRetreatAsync(int id, object body)
    {
        var resp = await AuthedPutAsync($"/api/v1/retreats/{id}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public Task<RetreatDashboardDto?> GetRetreatDashboardAsync(int id) =>
        GetAsync<RetreatDashboardDto>($"/api/v1/retreats/{id}/dashboard");

    public Task<List<RetreatRegistrationDto>> GetRetreatRegistrationsAsync(
        int id, string? source = null, string? status = null, string? search = null)
    {
        var qs = BuildQs(("source", source), ("status", status), ("search", search));
        return GetListAsync<RetreatRegistrationDto>($"/api/v1/retreats/{id}/registrations{qs}");
    }

    public async Task<RetreatRegistrationDto?> AddManualRegistrationAsync(int id, object body)
    {
        var resp = await AuthedPostAsync($"/api/v1/retreats/{id}/registrations", body);
        return resp?.IsSuccessStatusCode == true
            ? await resp.Content.ReadFromJsonAsync<RetreatRegistrationDto>(JsonOpts) : null;
    }

    public async Task<bool> UpdateRegistrationPaymentAsync(int id, int regId, object body)
    {
        var resp = await AuthedPutAsync($"/api/v1/retreats/{id}/registrations/{regId}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteRegistrationAsync(int id, int regId)
    {
        SetAuthHeader();
        try { return (await _http.DeleteAsync($"/api/v1/retreats/{id}/registrations/{regId}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public Task<List<RetreatExpenseDto>> GetRetreatExpensesAsync(int id) =>
        GetListAsync<RetreatExpenseDto>($"/api/v1/retreats/{id}/expenses");

    public async Task<RetreatExpenseDto?> AddExpenseAsync(int id, object body)
    {
        var resp = await AuthedPostAsync($"/api/v1/retreats/{id}/expenses", body);
        return resp?.IsSuccessStatusCode == true
            ? await resp.Content.ReadFromJsonAsync<RetreatExpenseDto>(JsonOpts) : null;
    }

    public async Task<bool> DeleteExpenseAsync(int id, int expId)
    {
        SetAuthHeader();
        try { return (await _http.DeleteAsync($"/api/v1/retreats/{id}/expenses/{expId}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<GbSyncResultDto?> TriggerGiveButterSyncAsync(int retreatId, string? since = null)
    {
        var url = $"/api/v1/givebutter/sync?retreatId={retreatId}";
        if (since is not null) url += $"&since={since}";
        var resp = await AuthedPostAsync(url, new { });
        return resp?.IsSuccessStatusCode == true
            ? await resp.Content.ReadFromJsonAsync<GbSyncResultDto>(JsonOpts) : null;
    }

    // ── Chapter management ────────────────────────────────────────────────────
    public Task<List<Chapter>> GetChaptersAdminAsync() =>
        GetListAsync<Chapter>("/api/v1/chapters");

    public async Task<bool> CreateChapterAsync(Chapter body)
    {
        var resp = await AuthedPostAsync("/api/v1/chapters", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateChapterAsync(int id, Chapter body)
    {
        var resp = await AuthedPutAsync($"/api/v1/chapters/{id}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Volunteer certifications ──────────────────────────────────────────────
    public Task<List<VolCertDto>> GetAllCertificationsAsync() =>
        GetListAsync<VolCertDto>("/api/v1/certifications/expiring?days=365");

    public Task<List<VolCertDto>> GetVolunteerCertificationsAsync(int volId) =>
        GetListAsync<VolCertDto>($"/api/v1/volunteers/{volId}/certifications");

    public async Task<bool> CreateCertificationAsync(int volId, VolCertDto body)
    {
        var resp = await AuthedPostAsync($"/api/v1/volunteers/{volId}/certifications", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateCertificationAsync(int volId, int certId, VolCertDto body)
    {
        var resp = await AuthedPutAsync($"/api/v1/volunteers/{volId}/certifications/{certId}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Donor touchpoints ─────────────────────────────────────────────────────
    public Task<List<TouchpointDto>> GetDonorTouchpointsAsync(int donorId) =>
        GetListAsync<TouchpointDto>($"/api/v1/donors/{donorId}/touchpoints");

    public async Task<bool> CreateDonorTouchpointAsync(int donorId, TouchpointDto body)
    {
        var resp = await AuthedPostAsync($"/api/v1/donors/{donorId}/touchpoints", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteDonorTouchpointAsync(int donorId, int id)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/donors/{donorId}/touchpoints/{id}");
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Grants ────────────────────────────────────────────────────────────────
    public Task<List<GrantDto>> GetGrantsAsync(string? status = null)
    {
        var url = "/api/v1/grants" + (status is not null ? $"?status={status}" : "");
        return GetListAsync<GrantDto>(url);
    }

    public async Task<bool> CreateGrantAsync(GrantDto body)
    {
        var resp = await AuthedPostAsync("/api/v1/grants", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateGrantAsync(int id, GrantDto body)
    {
        var resp = await AuthedPutAsync($"/api/v1/grants/{id}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteGrantAsync(int id)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/grants/{id}");
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Notification preferences ──────────────────────────────────────────────
    public Task<List<NotifPrefDto>> GetNotificationPrefsAsync() =>
        GetListAsync<NotifPrefDto>("/api/v1/users/me/notification-prefs");

    public async Task<bool> SaveNotificationPrefsAsync(List<NotifPrefDto> prefs)
    {
        var resp = await AuthedPutAsync("/api/v1/users/me/notification-prefs", prefs);
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Chapter analytics ─────────────────────────────────────────────────────
    public Task<List<ChapterAnalyticsDto>> GetChapterAnalyticsAsync() =>
        GetListAsync<ChapterAnalyticsDto>("/api/v1/admin/chapter-analytics");

    // ── Inventory additional mutations ────────────────────────────────────────
    public Task<ResourceItem?> GetInventoryItemAsync(int id) =>
        GetAsync<ResourceItem>($"/api/v1/inventory/{id}");

    public async Task<ResourceItem?> CreateInventoryItemAsync(ResourceItem item)
    {
        var resp = await AuthedPostAsync("/api/v1/inventory", item);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<ResourceItem>(JsonOpts) : null;
    }

    public async Task<bool> UpdateInventoryItemAsync(int id, ResourceItem item)
    {
        var resp = await AuthedPutAsync($"/api/v1/inventory/{id}", item);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> AdjustInventoryAsync(int id, int delta, string reason)
    {
        var resp = await AuthedPatchAsync($"/api/v1/inventory/{id}/adjust", new { Delta = delta, Reason = reason });
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Pledges ───────────────────────────────────────────────────────────────
    public Task<List<DonorPledge>> GetPledgesAsync(string? status = null)
    {
        var url = "/api/v1/pledges" + (status is not null ? $"?status={status}" : "");
        return GetListAsync<DonorPledge>(url);
    }

    public async Task<DonorPledge?> CreatePledgeAsync(DonorPledge pledge)
    {
        var resp = await AuthedPostAsync("/api/v1/pledges", pledge);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<DonorPledge>(JsonOpts) : null;
    }

    public async Task<bool> UpdatePledgeAsync(int id, DonorPledge pledge)
    {
        var resp = await AuthedPutAsync($"/api/v1/pledges/{id}", pledge);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> ApplyPledgePaymentAsync(int id, decimal amount)
    {
        var resp = await AuthedPostAsync($"/api/v1/pledges/{id}/apply", new { Amount = amount });
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Recurring Donations (admin) ───────────────────────────────────────────
    public Task<List<RecurringDonation>> GetAllRecurringAsync(string? status = null)
    {
        var url = "/api/v1/recurring" + (status is not null ? $"?status={status}" : "");
        return GetListAsync<RecurringDonation>(url);
    }

    public async Task<bool> PauseRecurringAdminAsync(int id)
    {
        try { return (await _http.PostAsync($"/api/v1/recurring/{id}/pause", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> CancelRecurringAdminAsync(int id)
    {
        try { return (await _http.PostAsync($"/api/v1/recurring/{id}/cancel", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> ResumeRecurringAdminAsync(int id)
    {
        try { return (await _http.PostAsync($"/api/v1/recurring/{id}/resume", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    // ── SMS Log ───────────────────────────────────────────────────────────────
    public Task<List<SmsLogDto>> GetSmsLogAsync(int? caseId = null, int page = 1) =>
        GetListAsync<SmsLogDto>($"/api/v1/cases/sms-log{BuildQs(("caseId", caseId?.ToString()), ("page", page.ToString()))}");

    // ── Volunteer admin ───────────────────────────────────────────────────────
    public Task<Volunteer?> GetVolunteerByIdAsync(int id) =>
        GetAsync<Volunteer>($"/api/v1/volunteers/{id}");

    // ── Family notes ──────────────────────────────────────────────────────────
    public Task<List<FamilyNoteDto>> GetFamilyNotesAsync(int familyId) =>
        GetListAsync<FamilyNoteDto>($"/api/v1/families/{familyId}/notes");

    public async Task<bool> CreateFamilyNoteAsync(int familyId, FamilyNoteDto body)
    {
        var resp = await AuthedPostAsync($"/api/v1/families/{familyId}/notes", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteFamilyNoteAsync(int familyId, int noteId)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/families/{familyId}/notes/{noteId}");
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Campaigns ─────────────────────────────────────────────────────────────
    public Task<List<CampaignDto>> GetCampaignsAsync(string? status = null)
    {
        var url = "/api/v1/campaigns" + (status is not null ? $"?status={status}" : "");
        return GetListAsync<CampaignDto>(url);
    }

    public async Task<bool> CreateCampaignAsync(CampaignDto body)
    {
        var resp = await AuthedPostAsync("/api/v1/campaigns", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateCampaignAsync(int id, CampaignDto body)
    {
        var resp = await AuthedPutAsync($"/api/v1/campaigns/{id}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteCampaignAsync(int id)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/campaigns/{id}");
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Pledge renewals ───────────────────────────────────────────────────────
    public Task<PledgeRenewalsDto?> GetPledgeRenewalsAsync(int days = 30) =>
        GetAsync<PledgeRenewalsDto>($"/api/v1/pledges/renewals?days={days}");

    // ── Staff tasks ───────────────────────────────────────────────────────────
    public Task<List<StaffTaskDto>> GetStaffTasksAsync(string? status = null, string? assignee = null)
    {
        var qs = BuildQs(("status", status), ("assignee", assignee));
        return GetListAsync<StaffTaskDto>($"/api/v1/staff-tasks{qs}");
    }

    public async Task<bool> CreateStaffTaskAsync(StaffTaskDto body)
    {
        var resp = await AuthedPostAsync("/api/v1/staff-tasks", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateStaffTaskAsync(int id, StaffTaskDto body)
    {
        var resp = await AuthedPutAsync($"/api/v1/staff-tasks/{id}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteStaffTaskAsync(int id)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/staff-tasks/{id}");
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Chapter Expenses ──────────────────────────────────────────────────────
    public Task<List<Expense>> GetExpensesAsync(string? category = null)
    {
        var url = "/api/v1/expenses" + (category is not null ? $"?category={category}" : "");
        return GetListAsync<Expense>(url);
    }

    public async Task<Expense?> CreateExpenseAsync(Expense body)
    {
        var resp = await AuthedPostAsync("/api/v1/expenses", body);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Expense>(JsonOpts) : null;
    }

    public async Task<bool> UpdateExpenseAsync(int id, Expense body)
    {
        var resp = await AuthedPutAsync($"/api/v1/expenses/{id}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteExpenseAsync(int id)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/expenses/{id}");
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Year-end giving ───────────────────────────────────────────────────────
    public Task<YearEndSummaryDto?> GetYearEndSummaryAsync(int donorId, int year) =>
        GetAsync<YearEndSummaryDto>($"/api/v1/donations/year-end/{donorId}/{year}");

    // ── API Key management ────────────────────────────────────────────────────
    public Task<List<ApiKeyDto>> GetApiKeysAsync() =>
        GetListAsync<ApiKeyDto>("/api/v1/apikeys");

    public async Task<ApiKeyCreateResult?> CreateApiKeyAsync(string partnerName, string contactEmail, string scope, DateTime? expiresAt)
    {
        var resp = await AuthedPostAsync("/api/v1/apikeys",
            new { PartnerName = partnerName, ContactEmail = contactEmail, Scope = scope, ExpiresAt = expiresAt });
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<ApiKeyCreateResult>(JsonOpts) : null;
    }

    public async Task<bool> RevokeApiKeyAsync(int id)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/apikeys/{id}");
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Announcements ─────────────────────────────────────────────────────────
    public Task<List<AnnouncementDto>> GetAnnouncementsAsync() =>
        GetListAsync<AnnouncementDto>("/api/v1/announcements");

    public async Task<bool> CreateAnnouncementAsync(AnnouncementDto body)
    {
        var resp = await AuthedPostAsync("/api/v1/announcements", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> UpdateAnnouncementAsync(int id, AnnouncementDto body)
    {
        var resp = await AuthedPutAsync($"/api/v1/announcements/{id}", body);
        return resp?.IsSuccessStatusCode == true;
    }

    public async Task<bool> DeleteAnnouncementAsync(int id)
    {
        var resp = await AuthedDeleteAsync($"/api/v1/announcements/{id}");
        return resp?.IsSuccessStatusCode == true;
    }
}

// ── Response DTOs (API-specific shapes) ──────────────────────────────────────
public record ReportRunLogDto(
    int Id, string ReportType, int? ChapterId, string ChapterName,
    DateTime SentAt, string RecipientEmail, bool Success,
    string Status, string? ErrorMessage, int? RecordsIncluded);

public record PublicImpactDto(
    decimal TotalDonations, int PeopleHelped, int ActiveVolunteers, int OpenRequests,
    int FamiliesServed, int DiocesesReached);

public record PublicWishListItemDto(
    int Id, string Title, string? Description, string Category,
    int QuantityRequested, int QuantityFulfilled, int QuantityRemaining);

public record PublicEventDto(
    int Id, string Title, string? Description, DateTime Date,
    string Location, bool IsVirtual,
    int? Capacity, int Registered,
    string Type, string Status, decimal? TicketPrice);

public record PublicChapterDto(int Id, string Name, string City, string State);

public record PublicDonorImpactDto(
    decimal TotalGiven, int GiftCount, int FamiliesHelped, int ChaptersServed,
    List<DonorImpactCategoryDto> CategoryBreakdown,
    List<DonorDonationDto> DonationHistory);

public record DonorImpactCategoryDto(string Category, decimal Amount, double Percentage);

public record DonorDonationDto(DateTime Date, decimal Amount, string Channel, string Status);

public record PublicDonationRow(int Id, DateTime Date, decimal Amount, string Channel, string? Campaign, bool IsRecurring);

public record PublicFamilyRequestDto(
    int Id, string Category, string Status, string Priority,
    DateTime CreatedAt, DateTime? DueDate, string Reason, string? AssignedTo);

public record SponsorDto(
    int Id, string CompanyName, string Tier, string ContactName, string Email,
    string? Phone, string? Website, string? TaxId,
    decimal CommittedAmount, decimal PaidToDate,
    DateTime? RenewalDate, string Status, string? Notes);

public record RecurringScheduleDto(
    int Id, decimal Amount, string Frequency, DateTime NextChargeDate,
    DateTime? EndsOn, string Status, string? Campaign, DateTime CreatedAt,
    DateTime? LastChargedAt, string Channel);

public record DashboardStatsDto(
    int OpenCases, int Overdue,
    decimal DonationsThisMonth, decimal DonationsLastMonth, int ActiveVolunteers);

public record WorkloadRowDto(
    int Id, string FullName, string Role, int ActiveCases, int TotalCasesFulfilled,
    int OverdueCases, int Capacity);

public record ChannelBreakdownDto(
    string Channel, decimal TotalAmount, int GiftCount, double Percentage);

public record PersonBreakdownDto(
    int DonorId, string Name, string? DioceseName, string? City, string? State,
    decimal TotalAmount, int GiftCount, double AverageGift);

public record StaffUserDto(
    string Id, string? UserName, string? Email, string? FullName, UserRole Role, int? ChapterId, bool IsActive, string? AvatarUrl);

public record EventRevenueDto(decimal Tickets, decimal Auction, decimal Total);
public record CheckinScanResult(bool Success, string Message, string? AttendeeName, int TicketCount);

public record MoneyFlowCategoryDto(string Category, decimal Amount, int RequestCount, double Percentage);
public record ResourceFlowTypeDto(string ResourceType, int Quantity, string Unit, int RequestCount, double Percentage);
public record TimelinePointDto(string Period, decimal Donations, int RequestsFulfilled, int NewRequests);
public record DonationByCityDto(string City, string State, int TotalDonors, decimal TotalAmount);
public record DonationByAmountBandDto(string Band, int GiftCount, decimal TotalAmount, double Percentage);
public record DonationByDioceseDto(int DioceseId, string DioceseName, string City, string State, int TotalDonors, decimal TotalAmount, double AverageGift);

public record ReconciliationRowDto(
    DateTime Date, string? StripeId, string? InternalId, string? DonorName,
    decimal? StripeAmount, decimal? InternalAmount)
{
    public decimal Delta        => (StripeAmount ?? 0) - (InternalAmount ?? 0);
    public string RecordStatus  =>
        StripeId is null   ? "Internal Only" :
        InternalId is null ? "Stripe Only"   :
        Delta != 0         ? "Discrepancy"   :
                             "Matched";
}

// ── Retreat DTOs ──────────────────────────────────────────────────────────────
public record RetreatListDto(
    int Id, string Title, string? Description, DateTime Date, DateTime? EndDate,
    string Location, string? Address, string? City, string? State,
    int Capacity, decimal TicketPrice, decimal GoalAmount,
    string? GiveButterCampaignId, string Status, int ChapterId, DateTime CreatedAt
);

public record RetreatDashboardDto(
    int Id, string Title, DateTime Date, string Location, string Status,
    int Capacity, decimal GoalAmount, string? GiveButterCampaignId,
    int TotalRegistered, double CapacityPct,
    int PaidCount, int UnpaidCount, int PartialCount, int ComplimentaryCount,
    decimal TotalRevenue, decimal TotalCosts, decimal NetPosition, double RevenuePct,
    int FromGiveButter, int FromDuda, int FromManual,
    List<RetreatExpenseSummaryDto> RecentExpenses
);

public record RetreatExpenseSummaryDto(int Id, string Description, string Category, decimal Amount, DateTime? PaidAt);

public record RetreatRegistrationDto(
    int Id, string FirstName, string LastName, string Email, string? Phone,
    string? Address, string? City, string? State, string? Zip,
    string? DietaryNeeds, string? AccessibilityNeeds,
    string? EmergencyContactName, string? EmergencyContactPhone,
    decimal AmountPaid, string PaymentStatus, string? PaymentMethod,
    string RegistrationSource, string? GiveButterTransactionId,
    string? Notes, DateTime RegisteredAt
);

public record RetreatExpenseDto(
    int Id, string Description, string Category, decimal Amount,
    DateTime? PaidAt, string? PaidBy, string? Notes, DateTime CreatedAt
);

public record GbSyncResultDto(int Synced, int Skipped, List<string> Errors);

public record VolCertDto(int Id, int VolunteerId, string CertType, DateTime IssuedDate,
    DateTime? ExpiresDate, bool IsVerified, string? Notes);

public record TouchpointDto(int Id, int DonorId, string TouchType, string Notes,
    DateTime TouchDate, string StaffName);

public record GrantDto(int Id, int ChapterId, string GrantorName, string Purpose,
    decimal Amount, DateTime AwardedDate, DateTime? ReportDueDate, string Status, string? Notes);

public record NotifPrefDto(string EventType, bool EmailEnabled, bool PushEnabled);

public record ChapterAnalyticsDto(int Id, string Name, string City, string State,
    int OpenCases, int OverdueCases, int FulfilledMtd, decimal TotalDonations,
    int DonationCount, int ActiveVols, int ActivePledges);

public record FamilyNoteDto(int Id, int FamilyId, string NoteType, string Content,
    DateTime? MilestoneDate, string StaffName, bool IsPinned, DateTime CreatedAt);

public record CampaignDto(int Id, int ChapterId, string Name, string? Description,
    decimal GoalAmount, DateTime StartDate, DateTime? EndDate, string Status,
    string? ExternalCode, decimal Raised, int GiftCount);

public record PledgeRenewalItemDto(int Id, string DonorName, string? Email,
    decimal Amount, string Frequency, DateTime NextChargeDate, string Status);

public record PledgeRenewalsDto(List<PledgeRenewalItemDto> Upcoming, List<PledgeRenewalItemDto> Overdue);

public record StaffTaskDto(int Id, int ChapterId, string Title, string? Description,
    string AssignedToName, string CreatedByName, DateTime? DueDate,
    string Priority, string Status, int? LinkedCaseId, int? LinkedDonorId,
    DateTime CreatedAt, DateTime? CompletedAt);

public record AnnouncementDto(int Id, int? ChapterId, string Title, string Body,
    string Audience, bool IsPinned, DateTime? ExpiresAt, string AuthorName, DateTime CreatedAt);

public record SmsLogDto(int Id, string ToPhoneNumber, string MessageType, int? CaseId,
    string? UserId, string Body, bool Success, string? ProviderMessageId,
    string? ErrorMessage, DateTime SentAt);

public record YearEndSummaryDto(int DonorId, string DonorName, int Year, decimal TotalGiven,
    int GiftCount, List<YearEndGiftRow> Gifts);

public record YearEndGiftRow(DateTime Date, decimal Amount, string Channel, string? Campaign);

public record ApiKeyDto(int Id, string PartnerName, string ContactEmail, int? ChapterId,
    string Scope, bool IsActive, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastUsedAt);

public record ApiKeyCreateResult(int Id, string PartnerName, string RawKey, string Note);
