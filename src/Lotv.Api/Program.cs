using Lotv.Core.Models;
using Lotv.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMockDataService, MockDataService>();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("https://localhost:7000", "http://localhost:5000")
     .AllowAnyHeader()
     .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseHttpsRedirection();

// ── Families ──────────────────────────────────────────────────────────────────
var families = app.MapGroup("/api/families").WithTags("Families");

families.MapGet("/", (IMockDataService svc) => svc.GetFamilies());

families.MapGet("/{id:int}", (int id, IMockDataService svc) =>
    svc.GetFamily(id) is Family f ? Results.Ok(f) : Results.NotFound());

families.MapPost("/", (Family family, IMockDataService svc) =>
{
    svc.AddFamily(family);
    svc.LogAction("API", "Created", "Family", family.Id.ToString(), $"Added {family.FullName}");
    return Results.Created($"/api/families/{family.Id}", family);
});

families.MapPut("/{id:int}", (int id, Family family, IMockDataService svc) =>
{
    if (svc.GetFamily(id) is null) return Results.NotFound();
    family.Id = id;
    svc.UpdateFamily(family);
    svc.LogAction("API", "Updated", "Family", id.ToString(), $"Updated {family.FullName}");
    return Results.Ok(family);
});

// ── Cases ─────────────────────────────────────────────────────────────────────
var cases = app.MapGroup("/api/cases").WithTags("Cases");

cases.MapGet("/", (IMockDataService svc, string? status, int? familyId) =>
{
    var all = svc.GetRequests();
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<CaseStatus>(status, true, out var cs))
        all = all.Where(r => r.Status == cs).ToList();
    if (familyId.HasValue)
        all = all.Where(r => r.FamilyId == familyId.Value).ToList();
    return all;
});

cases.MapGet("/{id:int}", (int id, IMockDataService svc) =>
    svc.GetRequest(id) is PackageRequest r ? Results.Ok(r) : Results.NotFound());

cases.MapPost("/", (PackageRequest req, IMockDataService svc) =>
{
    if (req.AssignedTo is null)
    {
        var vol = svc.GetNextAvailableVolunteer();
        if (vol is not null) req.AssignedTo = vol.FullName;
    }
    req.CreatedAt = DateTime.UtcNow;
    svc.AddRequest(req);
    svc.LogAction("API", "Created", "PackageRequest", req.Id.ToString(), $"Case for family {req.FamilyId}");
    return Results.Created($"/api/cases/{req.Id}", req);
});

cases.MapPut("/{id:int}", (int id, PackageRequest req, IMockDataService svc) =>
{
    if (svc.GetRequest(id) is null) return Results.NotFound();
    req.Id        = id;
    req.UpdatedAt = DateTime.UtcNow;
    svc.UpdateRequest(req);
    svc.LogAction("API", "Updated", "PackageRequest", id.ToString(), $"Status → {req.Status}");
    return Results.Ok(req);
});

// ── Donors ────────────────────────────────────────────────────────────────────
var donors = app.MapGroup("/api/donors").WithTags("Donors");

donors.MapGet("/", (IMockDataService svc) => svc.GetDonors());

donors.MapGet("/{id:int}", (int id, IMockDataService svc) =>
    svc.GetDonor(id) is Donor d ? Results.Ok(d) : Results.NotFound());

donors.MapPost("/", (Donor donor, IMockDataService svc) =>
{
    donor.CreatedAt = DateTime.UtcNow;
    svc.AddDonor(donor);
    svc.LogAction("API", "Created", "Donor", donor.Id.ToString(), $"Added donor {donor.FullName}");
    return Results.Created($"/api/donors/{donor.Id}", donor);
});

donors.MapPut("/{id:int}", (int id, Donor donor, IMockDataService svc) =>
{
    if (svc.GetDonor(id) is null) return Results.NotFound();
    donor.Id = id;
    svc.UpdateDonor(donor);
    return Results.Ok(donor);
});

// ── Donations ─────────────────────────────────────────────────────────────────
var donations = app.MapGroup("/api/donations").WithTags("Donations");

donations.MapGet("/", (IMockDataService svc, int? donorId) =>
{
    var all = svc.GetDonations();
    return donorId.HasValue ? all.Where(d => d.DonorId == donorId.Value).ToList() : all;
});

donations.MapPost("/", (Donation donation, IMockDataService svc) =>
{
    donation.Date = donation.Date == default ? DateTime.UtcNow : donation.Date;
    svc.AddDonation(donation);
    svc.LogAction("API", "Created", "Donation", donation.Id.ToString(), $"{donation.Amount:C0} from donor {donation.DonorId}");
    return Results.Created($"/api/donations/{donation.Id}", donation);
});

// ── Volunteers ────────────────────────────────────────────────────────────────
var volunteers = app.MapGroup("/api/volunteers").WithTags("Volunteers");

volunteers.MapGet("/", (IMockDataService svc, string? status) =>
{
    var all = svc.GetVolunteers();
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<VolunteerStatus>(status, true, out var vs))
        all = all.Where(v => v.Status == vs).ToList();
    return all;
});

volunteers.MapGet("/next-available", (IMockDataService svc) =>
    svc.GetNextAvailableVolunteer() is Volunteer v ? Results.Ok(v) : Results.NotFound());

volunteers.MapPost("/", (Volunteer volunteer, IMockDataService svc) =>
{
    volunteer.JoinedDate = DateTime.UtcNow;
    svc.AddVolunteer(volunteer);
    svc.LogAction("API", "Created", "Volunteer", volunteer.Id.ToString(), $"Added {volunteer.FullName}");
    return Results.Created($"/api/volunteers/{volunteer.Id}", volunteer);
});

volunteers.MapPut("/{id:int}", (int id, Volunteer volunteer, IMockDataService svc) =>
{
    volunteer.Id = id;
    svc.UpdateVolunteer(volunteer);
    return Results.Ok(volunteer);
});

// ── Events ────────────────────────────────────────────────────────────────────
var events = app.MapGroup("/api/events").WithTags("Events");

events.MapGet("/", (IMockDataService svc, string? type) =>
{
    var all = svc.GetEvents();
    if (!string.IsNullOrEmpty(type) && Enum.TryParse<EventType>(type, true, out var et))
        all = all.Where(e => e.Type == et).ToList();
    return all;
});

events.MapPost("/", (MinistryEvent evt, IMockDataService svc) =>
{
    svc.AddEvent(evt);
    svc.LogAction("API", "Created", "MinistryEvent", evt.Id.ToString(), evt.Title);
    return Results.Created($"/api/events/{evt.Id}", evt);
});

// ── Parishes & Dioceses ───────────────────────────────────────────────────────
app.MapGet("/api/parishes", (IMockDataService svc) => svc.GetParishes()).WithTags("Reference");
app.MapGet("/api/dioceses", (IMockDataService svc) => svc.GetDioceses()).WithTags("Reference");

// ── Allocations ───────────────────────────────────────────────────────────────
var allocs = app.MapGroup("/api/allocations").WithTags("Allocations");

allocs.MapGet("/", (IMockDataService svc, string? status) =>
{
    var all = svc.GetAllocations();
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<AllocationStatus>(status, true, out var s))
        all = all.Where(a => a.Status == s).ToList();
    return all;
});

allocs.MapPut("/{id:int}", (int id, FundAllocation alloc, IMockDataService svc) =>
{
    alloc.Id = id;
    svc.UpdateAllocation(alloc);
    svc.LogAction("API", "Updated", "FundAllocation", id.ToString(), $"Status → {alloc.Status}");
    return Results.Ok(alloc);
});

// ── Audit ─────────────────────────────────────────────────────────────────────
var audit = app.MapGroup("/api/audit").WithTags("Audit");

audit.MapGet("/", (IMockDataService svc, string? user, string? entity) =>
{
    var log = svc.GetAuditLog();
    if (!string.IsNullOrEmpty(user))   log = log.Where(e => e.UserName.Contains(user, StringComparison.OrdinalIgnoreCase)).ToList();
    if (!string.IsNullOrEmpty(entity)) log = log.Where(e => e.Entity.Equals(entity, StringComparison.OrdinalIgnoreCase)).ToList();
    return log;
});

audit.MapPost("/", (AuditLogRequest req, IMockDataService svc) =>
{
    svc.LogAction(req.UserName, req.Action, req.Entity, req.EntityId, req.Details);
    return Results.Ok();
});

// ── Dashboard Stats ───────────────────────────────────────────────────────────
app.MapGet("/api/stats/dashboard", (IMockDataService svc) => svc.GetDashboardStats()).WithTags("Stats");

app.Run();

record AuditLogRequest(string UserName, string Action, string Entity, string? EntityId, string? Details);
