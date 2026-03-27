using Lotv.Core.Models;
using Lotv.Core.Reporting;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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

    public ApiService(HttpClient http, JwtAuthStateProvider authState, AuthService auth)
    {
        _http = http;
        _authState = authState;
        _auth = auth;
    }

    // ── Requests / Cases ──────────────────────────────────────────────────────
    public Task<List<PackageRequest>> GetRequestsAsync(string? status = null, bool? overdue = null)
    {
        var qs = BuildQs(("status", status), ("overdue", overdue?.ToString().ToLower()));
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
            ? await resp.Content.ReadFromJsonAsync<PackageRequest>() : null;
    }

    public Task<List<RequestNote>> GetRequestNotesAsync(int id) =>
        GetListAsync<RequestNote>($"/api/v1/requests/{id}/notes");

    public async Task<RequestNote?> CreateRequestNoteAsync(int id, RequestNote note)
    {
        var resp = await AuthedPostAsync($"/api/v1/requests/{id}/notes", note);
        return resp?.IsSuccessStatusCode == true
            ? await resp.Content.ReadFromJsonAsync<RequestNote>() : null;
    }

    public Task<List<RequestActivity>> GetRequestActivityAsync(int id) =>
        GetListAsync<RequestActivity>($"/api/v1/requests/{id}/activity");

    // ── Families ─────────────────────────────────────────────────────────────
    public Task<List<Family>> GetFamiliesAsync(string? search = null) =>
        GetListAsync<Family>($"/api/v1/families{BuildQs(("search", search))}");

    public Task<Family?> GetFamilyAsync(int id) =>
        GetAsync<Family>($"/api/v1/families/{id}");

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
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<EventAttendee>() : null;
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
            var body = await resp.Content.ReadFromJsonAsync<ScanOkBody>();
            var name = body?.Attendee?.Donor?.FullName;
            var tickets = body?.Attendee?.TicketCount ?? 1;
            return new CheckinScanResult(true, "Checked in!", name, tickets);
        }
        if ((int)resp.StatusCode == 409)
        {
            var body = await resp.Content.ReadFromJsonAsync<ScanErrorBody>();
            var name = body?.Attendee?.Donor?.FullName;
            var tickets = body?.Attendee?.TicketCount ?? 1;
            return new CheckinScanResult(false, body?.Error ?? "Already checked in.", name, tickets);
        }
        var err = await resp.Content.ReadFromJsonAsync<ScanErrorBody>();
        return new CheckinScanResult(false, err?.Error ?? "Ticket not found.", null, 0);
    }

    private record ScanOkBody(string? Message, EventAttendee? Attendee);
    private record ScanErrorBody(string? Error, EventAttendee? Attendee, DateTime? CheckedInAt);

    public async Task<SilentAuctionItem?> CreateAuctionItemAsync(int eventId, SilentAuctionItem item)
    {
        var resp = await AuthedPostAsync($"/api/v1/events/{eventId}/auction", item);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<SilentAuctionItem>() : null;
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
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<SponsorDto>() : null;
    }

    // ── Notifications ─────────────────────────────────────────────────────────
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
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<WishListItem>() : null;
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

    public async Task<bool> AddRequestNoteAsync(int id, string content, bool isInternal = true)
    {
        var resp = await AuthedPostAsync($"/api/v1/requests/{id}/notes", new { Content = content, IsInternal = isInternal });
        return resp?.IsSuccessStatusCode == true;
    }

    // ── Families mutations ─────────────────────────────────────────────────────
    public async Task<Family?> CreateFamilyAsync(Family family)
    {
        var resp = await AuthedPostAsync("/api/v1/families", family);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Family>() : null;
    }

    public async Task<Family?> UpdateFamilyAsync(int id, Family family)
    {
        var resp = await AuthedPutAsync($"/api/v1/families/{id}", family);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Family>() : null;
    }

    // ── Volunteers mutations ───────────────────────────────────────────────────
    public async Task<Volunteer?> CreateVolunteerAsync(Volunteer vol)
    {
        var resp = await AuthedPostAsync("/api/v1/volunteers", vol);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Volunteer>() : null;
    }

    public async Task<Volunteer?> UpdateVolunteerAsync(int id, Volunteer vol)
    {
        var resp = await AuthedPutAsync($"/api/v1/volunteers/{id}", vol);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Volunteer>() : null;
    }

    // ── Donors mutations ───────────────────────────────────────────────────────
    public async Task<Donor?> CreateDonorAsync(Donor donor)
    {
        var resp = await AuthedPostAsync("/api/v1/donors", donor);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donor>() : null;
    }

    public async Task<Donor?> UpdateDonorAsync(int id, Donor donor)
    {
        var resp = await AuthedPutAsync($"/api/v1/donors/{id}", donor);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donor>() : null;
    }

    // ── Donations mutations ────────────────────────────────────────────────────
    public async Task<Donation?> CreateDonationAsync(Donation donation)
    {
        var resp = await AuthedPostAsync("/api/v1/donations", donation);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donation>() : null;
    }

    public async Task<Donation?> UpdateDonationAsync(int id, Donation donation)
    {
        var resp = await AuthedPutAsync($"/api/v1/donations/{id}", donation);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<Donation>() : null;
    }

    // ── Allocations mutations ──────────────────────────────────────────────────
    public async Task<FundAllocation?> UpdateAllocationAsync(int id, FundAllocation alloc)
    {
        var resp = await AuthedPutAsync($"/api/v1/allocations/{id}", alloc);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<FundAllocation>() : null;
    }

    // ── Events mutations ───────────────────────────────────────────────────────
    public async Task<MinistryEvent?> CreateEventAsync(MinistryEvent evt)
    {
        var resp = await AuthedPostAsync("/api/v1/events", evt);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<MinistryEvent>() : null;
    }

    public async Task<MinistryEvent?> UpdateEventAsync(int id, MinistryEvent evt)
    {
        var resp = await AuthedPutAsync($"/api/v1/events/{id}", evt);
        return resp?.IsSuccessStatusCode == true ? await resp.Content.ReadFromJsonAsync<MinistryEvent>() : null;
    }

    // ── Public Intake (no auth required) ─────────────────────────────────────
    public async Task<bool> PublicApplyAsync(Family family)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/public/apply", new { family });
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

    // ── Users ─────────────────────────────────────────────────────────────────
    public Task<List<StaffUserDto>> GetUsersAsync() =>
        GetListAsync<StaffUserDto>("/api/v1/users");

    public async Task<bool> UpdateUserRoleAsync(string id, UserRole role, int? chapterId)
    {
        var resp = await AuthedPutAsync($"/api/v1/users/{id}/role", new { Role = role, ChapterId = chapterId });
        return resp?.IsSuccessStatusCode == true;
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
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<T>() : null;
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

    private static string BuildQs(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs.Where(p => !string.IsNullOrEmpty(p.Value))
                         .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? "?" + qs : "";
    }
}

// ── Response DTOs (API-specific shapes) ──────────────────────────────────────
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
    int Id, string FullName, string Role, int ActiveCases, int TotalCasesFulfilled);

public record ChannelBreakdownDto(
    string Channel, decimal TotalAmount, int GiftCount, double Percentage);

public record PersonBreakdownDto(
    int DonorId, string Name, string? DioceseName, string? City, string? State,
    decimal TotalAmount, int GiftCount, double AverageGift);

public record StaffUserDto(
    string Id, string? Email, string? FullName, UserRole Role, int? ChapterId, bool IsActive);

public record EventRevenueDto(decimal Tickets, decimal Auction, decimal Total);
public record CheckinScanResult(bool Success, string Message, string? AttendeeName, int TicketCount);

public record MoneyFlowCategoryDto(string Category, decimal Amount, int RequestCount, double Percentage);
public record ResourceFlowTypeDto(string ResourceType, int Quantity, string Unit, int RequestCount, double Percentage);
public record TimelinePointDto(string Period, decimal Donations, int RequestsFulfilled, int NewRequests);
public record DonationByCityDto(string City, string State, int TotalDonors, decimal TotalAmount);
public record DonationByAmountBandDto(string Band, int GiftCount, decimal TotalAmount, double Percentage);
public record DonationByDioceseDto(int DioceseId, string DioceseName, string City, string State, int TotalDonors, decimal TotalAmount, double AverageGift);
