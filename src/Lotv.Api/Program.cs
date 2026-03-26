using System.Text;
using System.Threading.RateLimiting;
using Lotv.Api.Auth;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.SignalR;
using Lotv.Api.Data;
using Lotv.Api.Hubs;
using Lotv.Api.Services;
using Lotv.Core.Models;
using Lotv.Core.Services;
using Lotv.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// ── Logging (Serilog) ─────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    var template = ctx.HostingEnvironment.IsDevelopment()
        ? "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        : "{Timestamp:o} [{Level:u3}] {SourceContext}: {Message:j}{NewLine}{Exception}";

    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
       .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
       .Enrich.FromLogContext()
       .WriteTo.Console(outputTemplate: template,
           restrictedToMinimumLevel: LogEventLevel.Information);
});

// ── Database ──────────────────────────────────────────────────────────────────
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<LotvDbContext>(o =>
        o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=lotv-dev.db"));
}
else
{
    builder.Services.AddDbContext<LotvDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<LotvIdentityUser, IdentityRole>(o =>
{
    o.Password.RequiredLength         = 12;   // OWASP A07: enforce strong passwords
    o.Password.RequireUppercase        = true;
    o.Password.RequireDigit            = true;
    o.Password.RequireNonAlphanumeric  = true;
    o.Lockout.AllowedForNewUsers       = true;
    o.Lockout.MaxFailedAccessAttempts  = 5;
    o.Lockout.DefaultLockoutTimeSpan   = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<LotvDbContext>()
.AddDefaultTokenProviders();

// ── JWT Authentication ────────────────────────────────────────────────────────
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    // Keep JWT claim names as-is (don't map "role" → ClaimTypes.Role URI)
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
    // Allow SignalR to pass token via query string
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.Request.Path.StartsWithSegments("/hubs"))
                ctx.Token = token;
            return Task.CompletedTask;
        }
    };
});

// ── Authorization Policies ────────────────────────────────────────────────────
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("HQAdmin",       p => p.RequireClaim("role", nameof(UserRole.HQAdmin)));
    o.AddPolicy("ChapterAdmin",  p => p.RequireClaim("role", nameof(UserRole.HQAdmin), nameof(UserRole.ChapterAdmin)));
    o.AddPolicy("Staff",         p => p.RequireClaim("role", nameof(UserRole.HQAdmin), nameof(UserRole.ChapterAdmin), nameof(UserRole.ChapterStaff)));
    o.AddPolicy("Volunteer",     p => p.RequireClaim("role", nameof(UserRole.HQAdmin), nameof(UserRole.ChapterAdmin), nameof(UserRole.ChapterStaff), nameof(UserRole.Volunteer)));
    o.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IChapterContextService, ChapterContextService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAutoAssignmentService, AutoAssignmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IScheduledReportService, ScheduledReportService>();
builder.Services.AddScoped<IFinancialAuditService, FinancialAuditService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddSingleton<IMockDataService, MockDataService>();      // legacy mock service
builder.Services.AddHostedService<ScheduledReportBackgroundService>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Infrastructure ────────────────────────────────────────────────────────────
// Accept string enum names in JSON (e.g. "Volunteer" instead of 3)
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["https://localhost:7000", "http://localhost:5000", "https://localhost:7001", "http://localhost:5001"];

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));   // required for SignalR

// ── Rate Limiting ─────────────────────────────────────────────────────────────
// Limits are permissive in Development (avoids test-run throttling).
var isDev = builder.Environment.IsDevelopment();

builder.Services.AddRateLimiter(o =>
{
    // Auth endpoints: brute-force protection
    o.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit          = isDev ? 10_000 : 10,
            Window               = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0
        }));

    // Payment webhook: Stripe can retry on 429
    o.AddPolicy("payment", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit          = isDev ? 10_000 : 30,
            Window               = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0
        }));

    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LotvDbContext>("database");

var app = builder.Build();

// ── Dev seed data (MOCK DATA — Development only, skipped when Testing:SkipSeed=true) ──
if (app.Environment.IsDevelopment() && !app.Configuration.GetValue<bool>("Testing:SkipSeed"))
{
    using var scope = app.Services.CreateScope();
    var seedDb = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
    await DevSeedData.SeedAsync(seedDb);
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Security response headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options", "DENY");
    ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    ctx.Response.Headers.Append("X-XSS-Protection", "0"); // disabled in favour of CSP
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'none'; script-src 'none'; connect-src 'self'; " +
        "frame-ancestors 'none'; form-action 'self'");
    if (!app.Environment.IsDevelopment())
        ctx.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
    await next();
});

app.UseCors();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ── Health check (public — no auth required) ──────────────────────────────────
app.MapHealthChecks("/health").AllowAnonymous();

// ── Apply DB schema on startup ────────────────────────────────────────────────
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
    if (app.Environment.IsDevelopment())
        db.Database.EnsureCreated();    // fast for SQLite dev; skips migration table
    else
        db.Database.Migrate();          // runs pending EF Core migrations in production
}

// ── SignalR Hubs ──────────────────────────────────────────────────────────────
app.MapHub<RequestsHub>("/hubs/requests");
app.MapHub<AuctionHub>("/hubs/auction");

// ─────────────────────────────────────────────────────────────────────────────
// API v1 Endpoints
// ─────────────────────────────────────────────────────────────────────────────

// ── Public Intake (anonymous — rate-limited) ──────────────────────────────────
var publicIntake = app.MapGroup("/api/v1/public").WithTags("Public").AllowAnonymous().RequireRateLimiting("auth");

// Family intake: creates a Family record + PackageRequest and triggers auto-assignment
publicIntake.MapPost("/apply", async (PublicApplyRequest body, LotvDbContext db, IAutoAssignmentService autoAssign) =>
{
    if (string.IsNullOrWhiteSpace(body.Family.Parent1FirstName) ||
        string.IsNullOrWhiteSpace(body.Family.Parent1LastName) ||
        string.IsNullOrWhiteSpace(body.Family.Email))
        return Results.BadRequest(new { error = "First name, last name, and email are required." });

    body.Family.CreatedAt = DateTime.UtcNow;
    db.Families.Add(body.Family);
    await db.SaveChangesAsync();

    var req = new PackageRequest
    {
        FamilyId  = body.Family.Id,
        ChapterId = body.Family.ChapterId,
        Reason    = body.Family.Reason,
        Status    = CaseStatus.New,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    db.Requests.Add(req);
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = req.Id, ActorId = "public", ActorName = "Public Intake Form",
        ActivityType = ActivityType.Created, Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    await autoAssign.TryAutoAssignAsync(req.Id);

    return Results.Created($"/api/v1/requests/{req.Id}", new { familyId = body.Family.Id, requestId = req.Id });
});

// Donation intake: creates Donor + Donation records
publicIntake.MapPost("/give", async (PublicGiveRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Donor.FirstName) ||
        string.IsNullOrWhiteSpace(body.Donor.LastName) ||
        string.IsNullOrWhiteSpace(body.Donor.Email))
        return Results.BadRequest(new { error = "First name, last name, and email are required." });
    if (body.Donation.Amount <= 0)
        return Results.BadRequest(new { error = "Donation amount must be greater than zero." });

    body.Donor.CreatedAt = DateTime.UtcNow;
    db.Donors.Add(body.Donor);
    await db.SaveChangesAsync();

    body.Donation.DonorId = body.Donor.Id;
    body.Donation.ChapterId = body.Donor.ChapterId;
    body.Donation.Date = DateTime.UtcNow;
    db.Donations.Add(body.Donation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/donations/{body.Donation.Id}", new { donorId = body.Donor.Id, donationId = body.Donation.Id });
});

// Volunteer signup: creates a Volunteer record in Onboarding status
publicIntake.MapPost("/volunteer", async (Volunteer vol, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(vol.FirstName) ||
        string.IsNullOrWhiteSpace(vol.LastName) ||
        string.IsNullOrWhiteSpace(vol.Email))
        return Results.BadRequest(new { error = "First name, last name, and email are required." });

    vol.Status     = VolunteerStatus.Onboarding;
    vol.JoinedDate = DateTime.UtcNow;
    db.Volunteers.Add(vol);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/volunteers/{vol.Id}", vol);
});

// ── Auth ──────────────────────────────────────────────────────────────────────
var auth = app.MapGroup("/api/v1/auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting("auth");

auth.MapPost("/register", async (RegisterRequest req, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = new LotvIdentityUser
    {
        UserName = req.Email,
        Email = req.Email,
        FirstName = req.FirstName,
        LastName = req.LastName,
        Role = req.Role,
        ChapterId = req.ChapterId
    };
    var result = await userMgr.CreateAsync(user, req.Password);
    if (!result.Succeeded)
        return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
    return Results.Created($"/api/v1/users/{user.Id}", new { user.Id, user.Email });
});

auth.MapPost("/login", async (LoginRequest req, UserManager<LotvIdentityUser> userMgr,
    JwtTokenService tokenSvc, LotvDbContext db) =>
{
    var user = await userMgr.FindByEmailAsync(req.Email);
    if (user is null || !await userMgr.CheckPasswordAsync(user, req.Password))
        return Results.Unauthorized();
    if (!user.IsActive)
        return Results.Forbid();

    user.LastLoginAt = DateTime.UtcNow;
    var accessToken = tokenSvc.CreateAccessToken(user);
    var refreshToken = tokenSvc.CreateRefreshToken(user.Id);
    db.RefreshTokens.Add(refreshToken);
    await db.SaveChangesAsync();

    return Results.Ok(new { accessToken, refreshToken = refreshToken.Token, user.Role, user.ChapterId });
});

auth.MapPost("/refresh", async (RefreshRequest req, LotvDbContext db,
    UserManager<LotvIdentityUser> userMgr, JwtTokenService tokenSvc) =>
{
    var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == req.RefreshToken);
    if (stored is null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
        return Results.Unauthorized();

    var user = await userMgr.FindByIdAsync(stored.UserId);
    if (user is null || !user.IsActive) return Results.Unauthorized();

    // Rotate token
    stored.IsRevoked = true;
    stored.RevokedAt = DateTime.UtcNow;
    var newRefresh = tokenSvc.CreateRefreshToken(user.Id);
    stored.ReplacedByToken = newRefresh.Token;
    db.RefreshTokens.Add(newRefresh);
    await db.SaveChangesAsync();

    return Results.Ok(new { accessToken = tokenSvc.CreateAccessToken(user), refreshToken = newRefresh.Token });
});

auth.MapPost("/logout", async (RefreshRequest req, LotvDbContext db) =>
{
    var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == req.RefreshToken);
    if (token is not null)
    {
        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    return Results.Ok();
}).RequireAuthorization();

// ── Cases / Requests ──────────────────────────────────────────────────────────
var cases = app.MapGroup("/api/v1/requests").WithTags("Requests").RequireAuthorization("Staff");

cases.MapGet("/", async (LotvDbContext db, IChapterContextService ctx,
    string? status, string? priority, bool? overdue) =>
{
    var q = db.Requests.Include(r => r.Family).AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue)
        q = q.Where(r => r.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<CaseStatus>(status, true, out var cs))
        q = q.Where(r => r.Status == cs);
    if (!string.IsNullOrEmpty(priority) && Enum.TryParse<RequestPriority>(priority, true, out var rp))
        q = q.Where(r => r.Priority == rp);
    if (overdue == true)
        q = q.Where(r => r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled
                      && r.CreatedAt < DateTime.UtcNow.AddDays(-7));
    return await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
});

cases.MapGet("/queue", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var q = db.Requests.Where(r => r.AssignedToId == null && r.Status == CaseStatus.New);
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue)
        q = q.Where(r => r.ChapterId == ctx.ChapterId.Value);
    return await q.OrderBy(r => r.CreatedAt).ToListAsync();
});

cases.MapGet("/overdue", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var cutoff = DateTime.UtcNow.AddDays(-7);
    var q = db.Requests.Where(r => r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled && r.CreatedAt < cutoff);
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue)
        q = q.Where(r => r.ChapterId == ctx.ChapterId.Value);
    return await q.OrderBy(r => r.CreatedAt).ToListAsync();
});

cases.MapGet("/{id:int}", async (int id, LotvDbContext db, IChapterContextService ctx) =>
{
    var r = await db.Requests.Include(r => r.Family).FirstOrDefaultAsync(r => r.Id == id);
    if (r is null) return Results.NotFound();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue && r.ChapterId != ctx.ChapterId.Value) return Results.Forbid();
    return Results.Ok(r);
});

cases.MapPost("/", async (PackageRequest req, LotvDbContext db, IChapterContextService ctx,
    IAutoAssignmentService autoAssign, IHubContext<RequestsHub> hub) =>
{
    req.ChapterId = ctx.ChapterId ?? req.ChapterId;
    req.CreatedAt = DateTime.UtcNow;
    req.UpdatedAt = DateTime.UtcNow;
    db.Requests.Add(req);
    await db.SaveChangesAsync();

    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = req.Id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.Created, Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    await hub.Clients.Group($"chapter-{req.ChapterId}")
        .SendAsync("CaseCreated", req.Id, req.Family?.FullName ?? "Unknown", req.Reason.ToString());

    await autoAssign.TryAutoAssignAsync(req.Id);

    return Results.Created($"/api/v1/requests/{req.Id}", req);
}).RequireAuthorization("Authenticated");

cases.MapPut("/{id:int}/status", async (int id, StatusUpdateRequest body, LotvDbContext db,
    IChapterContextService ctx, IHubContext<RequestsHub> hub) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    if (!ctx.IsHqAdmin && r.ChapterId != ctx.ChapterId) return Results.Forbid();
    var old = r.Status;
    r.Status = body.Status;
    r.UpdatedAt = DateTime.UtcNow;
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.StatusChanged,
        OldValue = old.ToString(), NewValue = body.Status.ToString(), Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    await hub.Clients.Group($"chapter-{r.ChapterId}").SendAsync("CaseStatusChanged", id, body.Status.ToString(), ctx.UserId);
    return Results.Ok(r);
});

cases.MapPut("/{id:int}/assign", async (int id, AssignRequest body, LotvDbContext db,
    IChapterContextService ctx, IHubContext<RequestsHub> hub) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    var vol = await db.Volunteers.FindAsync(body.VolunteerId);
    if (vol is null) return Results.NotFound(new { message = "Volunteer not found" });
    r.AssignedToId = vol.Id;
    r.AssignedTo = vol.FullName;
    r.Status = CaseStatus.InProgress;
    r.UpdatedAt = DateTime.UtcNow;
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.Assigned, NewValue = vol.FullName, Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    await hub.Clients.Group($"chapter-{r.ChapterId}").SendAsync("CaseAssigned", id, vol.Id, vol.FullName);
    return Results.Ok(r);
});

cases.MapPut("/{id:int}/priority", async (int id, PriorityRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Priority = body.Priority; r.UpdatedAt = DateTime.UtcNow;
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.PriorityChanged, NewValue = body.Priority.ToString(), Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(r);
});

cases.MapPut("/{id:int}/due-date", async (int id, DueDateRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.DueDate = body.DueDate; r.UpdatedAt = DateTime.UtcNow;
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.DueDateSet, NewValue = body.DueDate.ToString("O"), Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(r);
});

cases.MapPatch("/{id:int}", async (int id, RequestPatchRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    if (!ctx.IsHqAdmin && r.ChapterId != ctx.ChapterId) return Results.Forbid();
    if (body.TrackingNumber is not null) r.TrackingNumber = body.TrackingNumber;
    if (body.ShippedDate.HasValue) r.ShippedDate = body.ShippedDate;
    if (body.InternalNotes is not null) r.InternalNotes = body.InternalNotes;
    r.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("Staff");

cases.MapPost("/{id:int}/accept", async (int id, LotvDbContext db, IChapterContextService ctx) =>
{
    var assignment = await db.RequestAssignments.FirstOrDefaultAsync(a => a.RequestId == id && a.Status == AssignmentStatus.Pending);
    if (assignment is null) return Results.NotFound();
    assignment.Status = AssignmentStatus.Accepted;
    assignment.AcceptedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization("Volunteer");

cases.MapPost("/{id:int}/decline", async (int id, DeclineRequest body, LotvDbContext db,
    IChapterContextService ctx, IAutoAssignmentService autoAssign) =>
{
    var assignment = await db.RequestAssignments.FirstOrDefaultAsync(a => a.RequestId == id && a.Status == AssignmentStatus.Pending);
    if (assignment is null) return Results.NotFound();
    assignment.Status = AssignmentStatus.Declined;
    assignment.DeclinedAt = DateTime.UtcNow;
    assignment.DeclineReason = body.Reason;
    await db.SaveChangesAsync();
    await autoAssign.HandleDeclineAsync(id, assignment.AssignedToId, body.Reason ?? "");
    return Results.Ok();
}).RequireAuthorization("Volunteer");

cases.MapPost("/{id:int}/escalate", async (int id, EscalateRequest body, LotvDbContext db,
    IChapterContextService ctx, IHubContext<RequestsHub> hub) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = CaseStatus.OnHold; r.UpdatedAt = DateTime.UtcNow;
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.Escalated, Details = body.Reason, Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    await hub.Clients.Group($"chapter-{r.ChapterId}").SendAsync("CaseEscalated", id, body.Reason);
    return Results.Ok(r);
});

cases.MapPost("/{id:int}/fulfill", async (int id, FulfillRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = CaseStatus.Fulfilled; r.UpdatedAt = DateTime.UtcNow;
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.Fulfilled, Details = body.Notes, Timestamp = DateTime.UtcNow
    });
    if (r.AssignedToId.HasValue)
    {
        var vol = await db.Volunteers.FindAsync(r.AssignedToId.Value);
        if (vol is not null) { vol.TotalCasesFulfilled++; vol.ActiveCases = Math.Max(0, vol.ActiveCases - 1); }
    }
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("Volunteer");

// SMS check-in: volunteer pings their location/status on an active assignment
cases.MapPost("/{id:int}/checkin", async (int id, LotvDbContext db, IChapterContextService ctx,
    ISmsService sms, SmsCheckInRequest body) =>
{
    var r = await db.Requests.FindAsync(id);
    if (r is null) return Results.NotFound();
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.NoteAdded,
        Details = $"Volunteer checked in. {(string.IsNullOrWhiteSpace(body.Note) ? "" : body.Note)}",
        Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    // Notify the volunteer by SMS to confirm check-in
    if (!string.IsNullOrWhiteSpace(body.VolunteerPhone))
    {
        var firstName = ctx.UserId ?? "Volunteer";
        await sms.ConfirmCheckInAsync(body.VolunteerPhone, firstName, id);
    }
    return Results.Ok();
}).RequireAuthorization("Volunteer");

// SMS log viewer (staff)
cases.MapGet("/sms-log", async (LotvDbContext db, int? caseId, int page = 1, int pageSize = 50) =>
{
    var q = db.SmsLogs.AsQueryable();
    if (caseId.HasValue) q = q.Where(s => s.CaseId == caseId);
    var total = await q.CountAsync();
    var rows  = await q.OrderByDescending(s => s.SentAt)
        .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    return Results.Ok(new { total, page, pageSize, rows });
}).RequireAuthorization("Staff");

cases.MapGet("/{id:int}/notes", async (int id, LotvDbContext db) =>
    await db.RequestNotes.Where(n => n.RequestId == id).OrderBy(n => n.CreatedAt).ToListAsync());

cases.MapPost("/{id:int}/notes", async (int id, NoteRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    var note = new RequestNote { RequestId = id, AuthorId = ctx.UserId, AuthorName = ctx.UserId, Content = body.Content, IsInternal = body.IsInternal };
    db.RequestNotes.Add(note);
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = id, ActorId = ctx.UserId, ActorName = ctx.UserId,
        ActivityType = ActivityType.NoteAdded, Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/requests/{id}/notes/{note.Id}", note);
}).RequireAuthorization("Volunteer");

cases.MapGet("/{id:int}/activity", async (int id, LotvDbContext db) =>
    await db.RequestActivities.Where(a => a.RequestId == id).OrderBy(a => a.Timestamp).ToListAsync());

cases.MapPost("/{id:int}/auto-assign", async (int id, IAutoAssignmentService svc) =>
{
    var result = await svc.TryAutoAssignAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(new { result.Error });
});

cases.MapGet("/{id:int}/candidates", async (int id, IAutoAssignmentService svc) =>
    await svc.GetScoresAsync(id));

// ── Families ──────────────────────────────────────────────────────────────────
var families = app.MapGroup("/api/v1/families").WithTags("Families").RequireAuthorization("Staff");

families.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? search) =>
{
    var q = db.Families.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(f => f.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(search)) q = q.Where(f => f.Parent1LastName.Contains(search) || f.Email.Contains(search));
    return await q.OrderByDescending(f => f.CreatedAt).ToListAsync();
});

families.MapGet("/{id:int}", async (int id, LotvDbContext db, IChapterContextService ctx) =>
{
    var f = await db.Families.FindAsync(id);
    if (f is null) return Results.NotFound();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue && f.ChapterId != ctx.ChapterId.Value) return Results.Forbid();
    return Results.Ok(f);
});

families.MapPost("/", async (Family family, LotvDbContext db, IChapterContextService ctx) =>
{
    family.ChapterId = ctx.ChapterId ?? family.ChapterId;
    family.CreatedAt = DateTime.UtcNow;
    db.Families.Add(family);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/families/{family.Id}", family);
});

families.MapPut("/{id:int}", async (int id, Family family, LotvDbContext db) =>
{
    if (!await db.Families.AnyAsync(f => f.Id == id)) return Results.NotFound();
    family.Id = id;
    db.Families.Update(family);
    await db.SaveChangesAsync();
    return Results.Ok(family);
});

// ── Volunteers ────────────────────────────────────────────────────────────────
var volunteers = app.MapGroup("/api/v1/volunteers").WithTags("Volunteers").RequireAuthorization("Staff");

volunteers.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.Volunteers.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(v => v.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<VolunteerStatus>(status, true, out var vs)) q = q.Where(v => v.Status == vs);
    return await q.OrderBy(v => v.LastName).ToListAsync();
});

volunteers.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.Volunteers.FindAsync(id) is Volunteer v ? Results.Ok(v) : Results.NotFound());

volunteers.MapGet("/available", async (int requestId, LotvDbContext db, IAutoAssignmentService svc) =>
    await svc.GetScoresAsync(requestId));

volunteers.MapPost("/", async (Volunteer v, LotvDbContext db, IChapterContextService ctx) =>
{
    v.ChapterId = ctx.ChapterId ?? v.ChapterId;
    v.JoinedDate = DateTime.UtcNow;
    db.Volunteers.Add(v);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/volunteers/{v.Id}", v);
});

volunteers.MapPut("/{id:int}", async (int id, Volunteer v, LotvDbContext db) =>
{
    if (!await db.Volunteers.AnyAsync(x => x.Id == id)) return Results.NotFound();
    v.Id = id;
    db.Volunteers.Update(v);
    await db.SaveChangesAsync();
    return Results.Ok(v);
});

// ── Donors ────────────────────────────────────────────────────────────────────
var donors = app.MapGroup("/api/v1/donors").WithTags("Donors").RequireAuthorization("Staff");

donors.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? search, bool maskAnonymous = false) =>
{
    var q = db.Donors.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(d => d.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(search)) q = q.Where(d => d.LastName.Contains(search) || d.Email.Contains(search));
    var list = await q.OrderBy(d => d.LastName).ToListAsync();
    // maskAnonymous=true is used by report generators — replaces names but keeps record for counting
    if (maskAnonymous)
        foreach (var d in list.Where(d => d.IsAnonymous))
            { d.FirstName = "Anonymous"; d.LastName = "Donor"; d.Email = ""; d.Phone = null; }
    return list;
});

donors.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.Donors.FindAsync(id) is Donor d ? Results.Ok(d) : Results.NotFound());

donors.MapGet("/{id:int}/contributions", async (int id, LotvDbContext db) =>
    await db.Donations.Where(d => d.DonorId == id).OrderByDescending(d => d.Date).ToListAsync());

donors.MapPost("/", async (Donor donor, LotvDbContext db, IChapterContextService ctx) =>
{
    donor.ChapterId = ctx.ChapterId ?? donor.ChapterId;
    donor.CreatedAt = DateTime.UtcNow;
    db.Donors.Add(donor);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/donors/{donor.Id}", donor);
});

donors.MapPut("/{id:int}", async (int id, Donor donor, LotvDbContext db) =>
{
    if (!await db.Donors.AnyAsync(d => d.Id == id)) return Results.NotFound();
    donor.Id = id;
    db.Donors.Update(donor);
    await db.SaveChangesAsync();
    return Results.Ok(donor);
});

donors.MapPatch("/{id:int}/privacy", async (int id, DonorPrivacyRequest body, LotvDbContext db) =>
{
    var donor = await db.Donors.FindAsync(id);
    if (donor is null) return Results.NotFound();
    donor.IsAnonymous = body.IsAnonymous;
    await db.SaveChangesAsync();
    return Results.Ok(new { donor.Id, donor.IsAnonymous });
}).RequireAuthorization("ChapterAdmin");

// ── Donations ─────────────────────────────────────────────────────────────────
var donations = app.MapGroup("/api/v1/donations").WithTags("Donations").RequireAuthorization("Staff");

donations.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, int? donorId, string? channel) =>
{
    var q = db.Donations.Include(d => d.Donor).AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(d => d.ChapterId == ctx.ChapterId.Value);
    if (donorId.HasValue) q = q.Where(d => d.DonorId == donorId.Value);
    if (!string.IsNullOrEmpty(channel) && Enum.TryParse<DonationChannel>(channel, true, out var ch)) q = q.Where(d => d.Channel == ch);
    return await q.OrderByDescending(d => d.Date).ToListAsync();
});

donations.MapPost("/", async (Donation donation, LotvDbContext db, IChapterContextService ctx, IHubContext<RequestsHub> hub) =>
{
    donation.ChapterId = ctx.ChapterId ?? donation.ChapterId;
    donation.Date = donation.Date == default ? DateTime.UtcNow : donation.Date;
    db.Donations.Add(donation);
    await db.SaveChangesAsync();
    await hub.Clients.Group($"chapter-{donation.ChapterId}").SendAsync("DonationReceived", donation.Id, donation.Amount, donation.Channel.ToString());
    return Results.Created($"/api/v1/donations/{donation.Id}", donation);
});

donations.MapPut("/{id:int}", async (int id, Donation donation, LotvDbContext db) =>
{
    if (!await db.Donations.AnyAsync(d => d.Id == id)) return Results.NotFound();
    donation.Id = id;
    db.Donations.Update(donation);
    await db.SaveChangesAsync();
    return Results.Ok(donation);
});

// Receipt — returns HTML for a single donation; used by DonationConfirm.razor download button
donations.MapGet("/{id:int}/receipt", async (int id, IReceiptService receipts) =>
{
    var (found, html) = await receipts.GetReceiptHtmlAsync(id);
    return found
        ? Results.Content(html!, "text/html")
        : Results.NotFound();
});

// Year-end giving statement
donations.MapGet("/year-end/{donorId:int}/{year:int}", async (int donorId, int year, IReceiptService receipts) =>
{
    var (found, html) = await receipts.GetYearEndHtmlAsync(donorId, year);
    return found
        ? Results.Content(html!, "text/html")
        : Results.NotFound();
});

// ── Allocations ───────────────────────────────────────────────────────────────
var allocs = app.MapGroup("/api/v1/allocations").WithTags("Allocations").RequireAuthorization("Staff");

allocs.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.FundAllocations.Include(a => a.Donation).AsQueryable();
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<AllocationStatus>(status, true, out var s)) q = q.Where(a => a.Status == s);
    return await q.ToListAsync();
});

allocs.MapPost("/", async (FundAllocation alloc, LotvDbContext db, IFinancialAuditService audit, HttpContext http) =>
{
    alloc.Status    = AllocationStatus.PendingReview;  // always start in review
    alloc.CreatedAt = DateTime.UtcNow;
    db.FundAllocations.Add(alloc);
    await db.SaveChangesAsync();
    var actor = http.User.FindFirst("sub")?.Value ?? "system";
    await audit.LogAllocationCreatedAsync(alloc, actor, http.Connection.RemoteIpAddress?.ToString());
    return Results.Created($"/api/v1/allocations/{alloc.Id}", alloc);
}).RequireAuthorization("ChapterAdmin");

allocs.MapPost("/{id:int}/approve", async (int id, ApproveAllocationRequest body, LotvDbContext db, IFinancialAuditService audit, HttpContext http) =>
{
    var alloc = await db.FundAllocations.FindAsync(id);
    if (alloc is null) return Results.NotFound();
    if (alloc.Status != AllocationStatus.PendingReview)
        return Results.BadRequest("Only PendingReview allocations can be approved.");
    alloc.Status     = AllocationStatus.Allocated;
    alloc.ApprovedBy = body.ApprovedBy;
    alloc.ApprovedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    var actor = http.User.FindFirst("sub")?.Value ?? "system";
    await audit.LogAllocationApprovedAsync(alloc, actor, http.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(alloc);
}).RequireAuthorization("ChapterAdmin");

allocs.MapPost("/{id:int}/reject", async (int id, RejectAllocationRequest body, LotvDbContext db, IFinancialAuditService audit, HttpContext http) =>
{
    var alloc = await db.FundAllocations.FindAsync(id);
    if (alloc is null) return Results.NotFound();
    if (alloc.Status == AllocationStatus.Allocated)
        return Results.BadRequest("Approved allocations cannot be rejected — contact HQ Admin.");
    alloc.Status = AllocationStatus.Unallocated;
    await db.SaveChangesAsync();
    var actor = http.User.FindFirst("sub")?.Value ?? "system";
    await audit.LogAllocationRejectedAsync(alloc, actor, body.Reason, http.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(alloc);
}).RequireAuthorization("ChapterAdmin");

allocs.MapPut("/{id:int}", async (int id, FundAllocation alloc, LotvDbContext db, IFinancialAuditService audit, HttpContext http) =>
{
    if (!await db.FundAllocations.AnyAsync(a => a.Id == id)) return Results.NotFound();
    alloc.Id = id;
    db.FundAllocations.Update(alloc);
    await db.SaveChangesAsync();
    var actor = http.User.FindFirst("sub")?.Value ?? "system";
    await audit.LogAsync("AllocationUpdated", "FundAllocation", id.ToString(), actor,
        $"Status={alloc.Status} Amount={alloc.Amount:C}",
        http.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(alloc);
}).RequireAuthorization("ChapterAdmin");

// ── Workload ──────────────────────────────────────────────────────────────────
var workload = app.MapGroup("/api/v1/workload").WithTags("Workload").RequireAuthorization("Staff");

workload.MapGet("/", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var q = db.Volunteers.Where(v => v.Status == VolunteerStatus.Active);
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(v => v.ChapterId == ctx.ChapterId.Value);
    return await q.Select(v => new
    {
        v.Id, v.FullName, Role = v.Role.ToString(), v.ActiveCases, v.TotalCasesFulfilled
    }).ToListAsync();
});

// ── Events ────────────────────────────────────────────────────────────────────
var events = app.MapGroup("/api/v1/events").WithTags("Events").RequireAuthorization("Staff");

events.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.Events.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(e => e.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<EventStatus>(status, true, out var es)) q = q.Where(e => e.Status == es);
    return await q.OrderByDescending(e => e.Date).ToListAsync();
});

events.MapGet("/upcoming", async (LotvDbContext db) =>
    await db.Events.Where(e => e.Date >= DateTime.UtcNow && e.Status == EventStatus.Published)
        .OrderBy(e => e.Date).Take(10).ToListAsync()).AllowAnonymous();

events.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.Events.FindAsync(id) is MinistryEvent e ? Results.Ok(e) : Results.NotFound());

events.MapPost("/", async (MinistryEvent evt, LotvDbContext db, IChapterContextService ctx) =>
{
    evt.ChapterId = ctx.ChapterId ?? evt.ChapterId;
    evt.CreatedAt = DateTime.UtcNow;
    evt.CreatedBy = ctx.UserId;
    db.Events.Add(evt);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/events/{evt.Id}", evt);
});

events.MapPut("/{id:int}", async (int id, MinistryEvent evt, LotvDbContext db) =>
{
    if (!await db.Events.AnyAsync(e => e.Id == id)) return Results.NotFound();
    evt.Id = id;
    db.Events.Update(evt);
    await db.SaveChangesAsync();
    return Results.Ok(evt);
});

events.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var evt = await db.Events.FindAsync(id);
    if (evt is null) return Results.NotFound();
    evt.Status = EventStatus.Cancelled;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("ChapterAdmin");

events.MapGet("/{id:int}/attendees", async (int id, LotvDbContext db) =>
{
    var attendees = await db.EventAttendees
        .Include(a => a.Donor)
        .Where(a => a.EventId == id)
        .ToListAsync();
    // Respect donor anonymity preference — mask name but keep record for capacity/revenue
    foreach (var a in attendees.Where(a => a.Donor?.IsAnonymous == true))
        if (a.Donor is not null)
            { a.Donor.FirstName = "Anonymous"; a.Donor.LastName = "Donor"; a.Donor.Email = ""; }
    return Results.Ok(attendees);
});

events.MapPost("/{id:int}/attendees", async (int id, EventAttendee attendee, LotvDbContext db) =>
{
    attendee.EventId = id;
    attendee.RegisteredAt = DateTime.UtcNow;
    db.EventAttendees.Add(attendee);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/events/{id}/attendees/{attendee.Id}", attendee);
});

events.MapPut("/{id:int}/attendees/{attendeeId:int}/checkin", async (int id, int attendeeId, LotvDbContext db) =>
{
    var a = await db.EventAttendees.FindAsync(attendeeId);
    if (a is null) return Results.NotFound();
    a.CheckedIn = true; a.CheckedInAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(a);
});

// QR code image for a specific attendee ticket (SVG, staff-facing)
events.MapGet("/{id:int}/attendees/{attendeeId:int}/qr", async (int id, int attendeeId, LotvDbContext db) =>
{
    var a = await db.EventAttendees.FindAsync(attendeeId);
    if (a is null) return Results.NotFound();

    using var qrGenerator = new QRCoder.QRCodeGenerator();
    var data = qrGenerator.CreateQrCode(a.TicketCode, QRCoder.QRCodeGenerator.ECCLevel.M);
    var svgCode = new QRCoder.SvgQRCode(data);
    var svg = svgCode.GetGraphic(new System.Drawing.Size(200, 200), "#1a2e5c", "#ffffff", true);

    return Results.Content(svg, "image/svg+xml");
}).RequireAuthorization("Staff");

// QR scan endpoint: look up attendee by ticket code and mark checked in
events.MapPost("/{id:int}/scan", async (int id, QrScanRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Code))
        return Results.BadRequest(new { error = "Ticket code is required." });

    var a = await db.EventAttendees.Include(a => a.Donor)
        .FirstOrDefaultAsync(a => a.EventId == id && a.TicketCode == body.Code);
    if (a is null) return Results.NotFound(new { error = "Ticket not found for this event." });
    if (a.CheckedIn)
        return Results.Conflict(new { error = "Ticket already checked in.", attendee = a, checkedInAt = a.CheckedInAt });

    a.CheckedIn = true; a.CheckedInAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Check-in successful.", attendee = a });
}).RequireAuthorization("Staff");

events.MapGet("/{id:int}/revenue", async (int id, LotvDbContext db) =>
{
    var tickets = await db.EventAttendees.Where(a => a.EventId == id).SumAsync(a => (decimal?)a.AmountPaid) ?? 0m;
    var auction = await db.AuctionItems.Where(i => i.EventId == id).SumAsync(i => (decimal?)i.WinningBid) ?? 0m;
    return Results.Ok(new { tickets, auction, total = tickets + auction });
});

events.MapGet("/{id:int}/auction", async (int id, LotvDbContext db) =>
    await db.AuctionItems.Include(i => i.Bids).Where(i => i.EventId == id).ToListAsync());

events.MapPost("/{id:int}/auction", async (int id, SilentAuctionItem item, LotvDbContext db) =>
{
    item.EventId = id;
    db.AuctionItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/events/{id}/auction/{item.Id}", item);
});

events.MapPost("/{id:int}/auction/{itemId:int}/bid", async (int id, int itemId, BidRequest body,
    LotvDbContext db, IHubContext<AuctionHub> auctionHub) =>
{
    var item = await db.AuctionItems.Include(i => i.Bids).FirstOrDefaultAsync(i => i.Id == itemId);
    if (item is null || item.Status != AuctionItemStatus.Available)
        return Results.BadRequest(new { message = "Item not available" });
    if (body.BidAmount <= (item.Bids.Any() ? item.Bids.Max(b => b.BidAmount) : item.StartingBid - 0.01m))
        return Results.BadRequest(new { message = "Bid must exceed the current high bid." });

    var bid = new AuctionBid
    {
        AuctionItemId = itemId,
        BidderId      = body.BidderId,
        BidAmount     = body.BidAmount,
        BidTime       = DateTime.UtcNow
    };
    db.AuctionBids.Add(bid);
    await db.SaveChangesAsync();

    // Broadcast to all clients watching this event's auction
    var newHigh    = item.Bids.Max(b => b.BidAmount);
    var totalBids  = item.Bids.Count;
    await auctionHub.Clients.Group($"auction-{id}")
        .SendAsync("BidPlaced", itemId, newHigh, (string?)null, totalBids);

    return Results.Ok(bid);
}).AllowAnonymous();   // public event attendees may bid

events.MapPost("/{id:int}/auction/close", async (int id, LotvDbContext db,
    IChapterContextService ctx, IHubContext<AuctionHub> auctionHub) =>
{
    var items = await db.AuctionItems.Include(i => i.Bids).Where(i => i.EventId == id).ToListAsync();
    foreach (var item in items)
    {
        if (!item.Bids.Any()) { item.Status = AuctionItemStatus.Unsold; continue; }
        var winning  = item.Bids.OrderByDescending(b => b.BidAmount).First();
        item.WinningBid = winning.BidAmount;
        item.WinnerId   = winning.BidderId;
        item.Status     = AuctionItemStatus.Sold;
    }
    await db.SaveChangesAsync();

    // Notify all connected viewers
    await auctionHub.Clients.Group($"auction-{id}")
        .SendAsync("AuctionClosed", id, items.Count);

    return Results.Ok(new { closed = items.Count });
}).RequireAuthorization("ChapterAdmin");

// ── Dioceses ──────────────────────────────────────────────────────────────────
var dioceses = app.MapGroup("/api/v1/dioceses").WithTags("Dioceses").RequireAuthorization("Staff");

dioceses.MapGet("/", async (LotvDbContext db) => await db.Dioceses.OrderBy(d => d.Name).ToListAsync());
dioceses.MapPost("/", async (Diocese d, LotvDbContext db) => { db.Dioceses.Add(d); await db.SaveChangesAsync(); return Results.Created($"/api/v1/dioceses/{d.Id}", d); }).RequireAuthorization("ChapterAdmin");
dioceses.MapPut("/{id:int}", async (int id, Diocese d, LotvDbContext db) => { d.Id = id; db.Dioceses.Update(d); await db.SaveChangesAsync(); return Results.Ok(d); }).RequireAuthorization("ChapterAdmin");
dioceses.MapGet("/{id:int}/donors", async (int id, LotvDbContext db) => await db.Donors.Where(d => d.DioceseId == id).ToListAsync());

// ── Dashboard ─────────────────────────────────────────────────────────────────
var dashboard = app.MapGroup("/api/v1/dashboard").WithTags("Dashboard").RequireAuthorization("Staff");

dashboard.MapGet("/stats", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var chapterId = ctx.ChapterId;
    var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    var lastMonth = startOfMonth.AddMonths(-1);

    var openCases = await db.Requests.CountAsync(r => (!chapterId.HasValue || r.ChapterId == chapterId) && r.Status == CaseStatus.New || r.Status == CaseStatus.InProgress);
    var overdue = await db.Requests.CountAsync(r => (!chapterId.HasValue || r.ChapterId == chapterId) && r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled && r.CreatedAt < DateTime.UtcNow.AddDays(-7));
    var donationsThisMonth = await db.Donations.Where(d => (!chapterId.HasValue || d.ChapterId == chapterId) && d.Date >= startOfMonth).SumAsync(d => (decimal?)d.Amount) ?? 0m;
    var donationsLastMonth = await db.Donations.Where(d => (!chapterId.HasValue || d.ChapterId == chapterId) && d.Date >= lastMonth && d.Date < startOfMonth).SumAsync(d => (decimal?)d.Amount) ?? 0m;
    var activeVolunteers = await db.Volunteers.CountAsync(v => (!chapterId.HasValue || v.ChapterId == chapterId) && v.Status == VolunteerStatus.Active);

    return Results.Ok(new { openCases, overdue, donationsThisMonth, donationsLastMonth, activeVolunteers });
});

dashboard.MapGet("/donations/by-channel", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var q = db.Donations.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(d => d.ChapterId == ctx.ChapterId.Value);
    var total = await q.SumAsync(d => (decimal?)d.Amount) ?? 1m;
    return await q.GroupBy(d => d.Channel)
        .Select(g => new { Channel = g.Key.ToString(), TotalAmount = g.Sum(d => d.Amount), GiftCount = g.Count(), Percentage = Math.Round((double)g.Sum(d => d.Amount) / (double)total * 100, 1) })
        .ToListAsync();
});

dashboard.MapGet("/donations/by-person", async (LotvDbContext db, IChapterContextService ctx, string? search, int page = 1, int pageSize = 25) =>
{
    var q = db.Donations.Include(d => d.Donor).AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(d => d.ChapterId == ctx.ChapterId.Value);
    var rows = await q.GroupBy(d => new { d.DonorId, d.Donor!.FirstName, d.Donor.LastName, d.Donor.DioceseName, d.Donor.City, d.Donor.State })
        .Select(g => new { g.Key.DonorId, Name = g.Key.FirstName + " " + g.Key.LastName, g.Key.DioceseName, g.Key.City, g.Key.State, TotalAmount = g.Sum(d => d.Amount), GiftCount = g.Count(), AverageGift = g.Average(d => d.Amount) })
        .OrderByDescending(r => r.TotalAmount)
        .Skip((page - 1) * pageSize).Take(pageSize)
        .ToListAsync();
    return Results.Ok(rows);
});

// ── Chapters ─────────────────────────────────────────────────────────────────
var chapters = app.MapGroup("/api/v1/chapters").WithTags("Chapters").RequireAuthorization("HQAdmin");

chapters.MapGet("/", async (LotvDbContext db) => await db.Chapters.OrderBy(c => c.Name).ToListAsync());
chapters.MapPost("/", async (Chapter c, LotvDbContext db) => { db.Chapters.Add(c); await db.SaveChangesAsync(); return Results.Created($"/api/v1/chapters/{c.Id}", c); });
chapters.MapPut("/{id:int}", async (int id, Chapter c, LotvDbContext db) => { c.Id = id; db.Chapters.Update(c); await db.SaveChangesAsync(); return Results.Ok(c); });

// ── Users ─────────────────────────────────────────────────────────────────────
var users = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

users.MapGet("/me", async (IChapterContextService ctx, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = await userMgr.FindByIdAsync(ctx.UserId);
    return user is null ? Results.NotFound() : Results.Ok(new { user.Id, user.Email, user.FullName, user.Role, user.ChapterId });
});

users.MapGet("/", async (UserManager<LotvIdentityUser> userMgr) =>
    userMgr.Users.Select(u => new { u.Id, u.Email, u.FullName, u.Role, u.ChapterId, u.IsActive }).ToList()
).RequireAuthorization("ChapterAdmin");

users.MapPut("/{id}/role", async (string id, RoleChangeRequest body, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = await userMgr.FindByIdAsync(id);
    if (user is null) return Results.NotFound();
    user.Role = body.Role; user.ChapterId = body.ChapterId;
    await userMgr.UpdateAsync(user);
    return Results.Ok();
}).RequireAuthorization("ChapterAdmin");

// ── HQ Dashboard ──────────────────────────────────────────────────────────────
app.MapGet("/api/v1/dashboard/hq", async (LotvDbContext db) =>
{
    var chapters = await db.Chapters.Where(c => c.IsActive).ToListAsync();
    var cutoff   = DateTime.UtcNow.AddDays(-7);
    var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    var rows = new List<Lotv.Core.Reporting.ChapterSummaryRow>();
    foreach (var ch in chapters)
    {
        var open      = await db.Requests.CountAsync(r => r.ChapterId == ch.Id && r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled);
        var overdue   = await db.Requests.CountAsync(r => r.ChapterId == ch.Id && r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled && r.CreatedAt < cutoff);
        var fulfilled = await db.Requests.CountAsync(r => r.ChapterId == ch.Id && r.Status == CaseStatus.Fulfilled && r.UpdatedAt >= startOfMonth);
        var donations = await db.Donations.Where(d => d.ChapterId == ch.Id).SumAsync(d => (decimal?)d.Amount) ?? 0m;
        var volunteers = await db.Volunteers.CountAsync(v => v.ChapterId == ch.Id && v.Status == VolunteerStatus.Active);
        rows.Add(new(ch.Id, ch.Name, open, overdue, fulfilled, donations, volunteers));
    }
    return Results.Ok(rows);
}).WithTags("Dashboard").RequireAuthorization("HQAdmin");

// ── Resource Inventory ────────────────────────────────────────────────────────
var inventory = app.MapGroup("/api/v1/inventory").WithTags("Inventory").RequireAuthorization("Staff");

inventory.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? category) =>
{
    var q = db.ResourceItems.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(r => r.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(category) && Enum.TryParse<ResourceCategory>(category, true, out var cat))
        q = q.Where(r => r.Category == cat);
    return await q.OrderBy(r => r.Name).ToListAsync();
});

inventory.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.ResourceItems.FindAsync(id) is ResourceItem r ? Results.Ok(r) : Results.NotFound());

inventory.MapPost("/", async (ResourceItem item, LotvDbContext db, IChapterContextService ctx) =>
{
    item.ChapterId = ctx.ChapterId ?? item.ChapterId;
    item.CreatedAt = DateTime.UtcNow;
    item.UpdatedAt = DateTime.UtcNow;
    db.ResourceItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/inventory/{item.Id}", item);
}).RequireAuthorization("ChapterAdmin");

inventory.MapPut("/{id:int}", async (int id, ResourceItem item, LotvDbContext db) =>
{
    if (!await db.ResourceItems.AnyAsync(r => r.Id == id)) return Results.NotFound();
    item.Id = id;
    item.UpdatedAt = DateTime.UtcNow;
    db.ResourceItems.Update(item);
    await db.SaveChangesAsync();
    return Results.Ok(item);
}).RequireAuthorization("ChapterAdmin");

inventory.MapPatch("/{id:int}/adjust", async (int id, InventoryAdjustRequest body, LotvDbContext db) =>
{
    var item = await db.ResourceItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    item.QuantityOnHand  = Math.Max(0, item.QuantityOnHand + body.QuantityDelta);
    item.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(item);
}).RequireAuthorization("ChapterAdmin");

// ── Audit ─────────────────────────────────────────────────────────────────────
app.MapGet("/api/v1/audit", async (LotvDbContext db, IChapterContextService ctx, int page = 1, int pageSize = 50) =>
{
    var q = db.AuditEntries.AsQueryable();
    return await q.OrderByDescending(a => a.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
}).WithTags("Audit").RequireAuthorization("ChapterAdmin");

// Audit CSV export — for accountants and compliance review
app.MapGet("/api/v1/audit/export", async (HttpContext http, LotvDbContext db, IChapterContextService ctx,
    DateTime? from, DateTime? to) =>
{
    var q = db.AuditEntries.AsQueryable();
    if (from.HasValue) q = q.Where(a => a.Timestamp >= from.Value);
    if (to.HasValue)   q = q.Where(a => a.Timestamp <= to.Value);
    q = q.Where(a => a.Entity == "FundAllocation");
    var rows = await q.OrderBy(a => a.Timestamp).ToListAsync();

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("Timestamp,Action,EntityId,Actor,Details,IpAddress");
    foreach (var r in rows)
    {
        var details = (r.Details ?? "").Replace("\"", "\"\"");
        sb.AppendLine($"{r.Timestamp:o},{r.Action},{r.EntityId},{r.UserName},\"{details}\",{r.IpAddress}");
    }

    var filename = $"lotv-audit-{DateTime.UtcNow:yyyyMMdd}.csv";
    http.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";
    return Results.Content(sb.ToString(), "text/csv; charset=utf-8");
}).WithTags("Audit").RequireAuthorization("ChapterAdmin");

// ── Wish List ─────────────────────────────────────────────────────────────────
var wishlist = app.MapGroup("/api/v1/wishlist").WithTags("Wish List");

wishlist.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status, string? category) =>
{
    var q = db.WishListItems.Include(w => w.Family).AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(w => w.ChapterId == ctx.ChapterId);
    if (Enum.TryParse<WishListStatus>(status, true, out var s)) q = q.Where(w => w.Status == s);
    if (Enum.TryParse<WishListCategory>(category, true, out var c)) q = q.Where(w => w.Category == c);
    return Results.Ok(await q.OrderByDescending(w => w.CreatedAt).ToListAsync());
}).RequireAuthorization("Staff");

wishlist.MapGet("/open", async (LotvDbContext db, IChapterContextService ctx) =>
{
    // Public-facing open items (family details anonymised)
    var q = db.WishListItems
        .Where(w => w.Status == WishListStatus.Open || w.Status == WishListStatus.PartiallyFulfilled);
    if (ctx.ChapterId.HasValue) q = q.Where(w => w.ChapterId == ctx.ChapterId);
    var items = await q.OrderBy(w => w.Category).ThenBy(w => w.CreatedAt).ToListAsync();
    return Results.Ok(items.Select(w => new
    {
        w.Id, w.Title, w.Description, Category = w.Category.ToDisplayName(),
        w.QuantityRequested, w.QuantityFulfilled, w.QuantityRemaining, w.Status
    }));
}).AllowAnonymous();

wishlist.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.WishListItems.Include(w => w.Family).FirstOrDefaultAsync(w => w.Id == id)
        is WishListItem w ? Results.Ok(w) : Results.NotFound()).RequireAuthorization("Staff");

wishlist.MapPost("/", async (WishListItem item, LotvDbContext db, IChapterContextService ctx) =>
{
    item.Id        = 0;
    item.CreatedAt = DateTime.UtcNow;
    if (ctx.ChapterId.HasValue) item.ChapterId = ctx.ChapterId.Value;
    db.WishListItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/wishlist/{item.Id}", item);
}).RequireAuthorization("Staff");

wishlist.MapPost("/{id:int}/fulfill", async (int id, FulfillWishListRequest body, LotvDbContext db) =>
{
    var item = await db.WishListItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    item.QuantityFulfilled = Math.Min(item.QuantityRequested, item.QuantityFulfilled + body.Quantity);
    item.FulfilledByDonorId = body.DonorId;
    item.Status = item.IsFullyFulfilled ? WishListStatus.Fulfilled
                : item.QuantityFulfilled > 0 ? WishListStatus.PartiallyFulfilled
                : item.Status;
    if (item.IsFullyFulfilled) item.FulfilledAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(item);
}).AllowAnonymous();

wishlist.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var item = await db.WishListItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    item.Status = WishListStatus.Cancelled;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("ChapterAdmin");

// ── Recurring Donations ───────────────────────────────────────────────────────
var recurring = app.MapGroup("/api/v1/recurring").WithTags("Recurring Donations").RequireAuthorization("Staff");

recurring.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.RecurringDonations.Include(r => r.Donor).AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(r => r.ChapterId == ctx.ChapterId);
    if (Enum.TryParse<RecurringStatus>(status, true, out var s)) q = q.Where(r => r.Status == s);
    var items = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
    return Results.Ok(items);
});

recurring.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.RecurringDonations.Include(r => r.Donor).FirstOrDefaultAsync(r => r.Id == id)
        is RecurringDonation r ? Results.Ok(r) : Results.NotFound());

recurring.MapPost("/", async (RecurringDonation r, LotvDbContext db, IChapterContextService ctx) =>
{
    r.Id = 0;
    r.CreatedAt = DateTime.UtcNow;
    if (ctx.ChapterId.HasValue) r.ChapterId = ctx.ChapterId.Value;
    db.RecurringDonations.Add(r);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/recurring/{r.Id}", r);
}).RequireAuthorization("ChapterAdmin");

recurring.MapPut("/{id:int}", async (int id, RecurringDonation body, LotvDbContext db) =>
{
    var existing = await db.RecurringDonations.FindAsync(id);
    if (existing is null) return Results.NotFound();
    existing.Amount         = body.Amount;
    existing.Frequency      = body.Frequency;
    existing.NextChargeDate = body.NextChargeDate;
    existing.Status         = body.Status;
    existing.EndsOn         = body.EndsOn;
    existing.Campaign       = body.Campaign;
    existing.Notes          = body.Notes;
    await db.SaveChangesAsync();
    return Results.Ok(existing);
}).RequireAuthorization("ChapterAdmin");

recurring.MapPost("/{id:int}/pause", async (int id, LotvDbContext db) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Paused;
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("ChapterAdmin");

recurring.MapPost("/{id:int}/cancel", async (int id, LotvDbContext db) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Cancelled;
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("ChapterAdmin");

// ── Pledges ───────────────────────────────────────────────────────────────────
var pledges = app.MapGroup("/api/v1/pledges").WithTags("Pledges").RequireAuthorization("Staff");

pledges.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.DonorPledges.Include(p => p.Donor).AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(p => p.ChapterId == ctx.ChapterId);
    if (Enum.TryParse<PledgeStatus>(status, true, out var s)) q = q.Where(p => p.Status == s);
    var items = await q.OrderByDescending(p => p.CreatedAt).ToListAsync();
    return Results.Ok(items);
});

pledges.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.DonorPledges.Include(p => p.Donor).FirstOrDefaultAsync(p => p.Id == id)
        is DonorPledge p ? Results.Ok(p) : Results.NotFound());

pledges.MapPost("/", async (DonorPledge pledge, LotvDbContext db, IChapterContextService ctx) =>
{
    pledge.Id        = 0;
    pledge.CreatedAt = DateTime.UtcNow;
    if (ctx.ChapterId.HasValue) pledge.ChapterId = ctx.ChapterId.Value;
    db.DonorPledges.Add(pledge);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/pledges/{pledge.Id}", pledge);
}).RequireAuthorization("ChapterAdmin");

pledges.MapPut("/{id:int}", async (int id, DonorPledge body, LotvDbContext db) =>
{
    var existing = await db.DonorPledges.FindAsync(id);
    if (existing is null) return Results.NotFound();
    existing.PledgedAmount   = body.PledgedAmount;
    existing.FulfilledAmount = body.FulfilledAmount;
    existing.TargetDate      = body.TargetDate;
    existing.Status          = body.Status;
    existing.Campaign        = body.Campaign;
    existing.Notes           = body.Notes;
    existing.UpdatedAt       = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(existing);
}).RequireAuthorization("ChapterAdmin");

pledges.MapPost("/{id:int}/apply", async (int id, ApplyPledgePaymentRequest body, LotvDbContext db) =>
{
    var pledge = await db.DonorPledges.FindAsync(id);
    if (pledge is null) return Results.NotFound();
    pledge.FulfilledAmount += body.Amount;
    pledge.UpdatedAt = DateTime.UtcNow;
    if (pledge.IsFulfilled) pledge.Status = PledgeStatus.Fulfilled;
    await db.SaveChangesAsync();
    return Results.Ok(pledge);
}).RequireAuthorization("ChapterAdmin");

pledges.MapGet("/overdue", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var cutoff = DateTime.UtcNow;
    var q = db.DonorPledges
        .Include(p => p.Donor)
        .Where(p => p.Status == PledgeStatus.Active && p.TargetDate < cutoff
                    && p.FulfilledAmount < p.PledgedAmount);
    if (ctx.ChapterId.HasValue) q = q.Where(p => p.ChapterId == ctx.ChapterId);
    return Results.Ok(await q.OrderBy(p => p.TargetDate).ToListAsync());
});

// ── Public Partner API (API-key auth) ─────────────────────────────────────────
// Authentication: pass the raw key in the X-Api-Key header.
// The middleware hashes it (SHA-256) and looks up ApiKeys table.

static async Task<bool> ValidateApiKey(HttpContext http, LotvDbContext db, ApiKeyScope minScope)
{
    if (!http.Request.Headers.TryGetValue("X-Api-Key", out var raw) || string.IsNullOrEmpty(raw))
    {
        http.Response.StatusCode = 401;
        await http.Response.WriteAsync("{\"error\":\"X-Api-Key header required\"}");
        return false;
    }
    var hash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw.ToString()!))).ToLowerInvariant();
    var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);
    if (key is null || !key.IsValid || key.Scope < minScope)
    {
        http.Response.StatusCode = 403;
        await http.Response.WriteAsync("{\"error\":\"Invalid or insufficient API key\"}");
        return false;
    }
    key.LastUsedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return true;
}

var publicApi = app.MapGroup("/api/public/v1").WithTags("Public API");

// GET /api/public/v1/impact — aggregate impact summary (no key required — truly public)
publicApi.MapGet("/impact", async (LotvDbContext db) =>
{
    var totalDonations   = await db.Donations.SumAsync(d => d.Amount);
    var peopleHelped     = await db.Requests.CountAsync(r => r.Status == CaseStatus.Fulfilled);
    var activeVolunteers = await db.Volunteers.CountAsync(v => v.Status == VolunteerStatus.Active);
    var openRequests     = await db.Requests.CountAsync(r =>
        r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled);
    return Results.Ok(new { totalDonations, peopleHelped, activeVolunteers, openRequests,
        generatedAt = DateTime.UtcNow });
}).AllowAnonymous();

// GET /api/public/v1/chapters — list active chapters (no key required)
publicApi.MapGet("/chapters", async (LotvDbContext db) =>
    Results.Ok(await db.Chapters
        .Where(c => c.IsActive)
        .Select(c => new { c.Id, c.Name, c.City, c.State })
        .ToListAsync())
).AllowAnonymous();

// GET /api/public/v1/wishlist — open wish-list items (no key required)
publicApi.MapGet("/wishlist", async (LotvDbContext db, string? category) =>
{
    var q = db.WishListItems
        .Where(w => w.Status == WishListStatus.Open || w.Status == WishListStatus.PartiallyFulfilled);
    if (Enum.TryParse<WishListCategory>(category, true, out var cat))
        q = q.Where(w => w.Category == cat);
    var items = await q.OrderBy(w => w.Category).ToListAsync();
    return Results.Ok(items.Select(w => new
    {
        w.Id, w.Title, w.Description, Category = w.Category.ToDisplayName(),
        w.QuantityRequested, w.QuantityFulfilled, w.QuantityRemaining
    }));
}).AllowAnonymous();

// POST /api/public/v1/requests — submit service request (key required, Write scope)
publicApi.MapPost("/requests", async (HttpContext http, LotvDbContext db, PublicIntakeRequest body) =>
{
    if (!await ValidateApiKey(http, db, ApiKeyScope.Write)) return Results.Empty;
    if (string.IsNullOrWhiteSpace(body.FamilyLastName) || body.ChapterId <= 0)
        return Results.BadRequest(new { error = "FamilyLastName and ChapterId are required." });
    var family = new Family
    {
        Parent1LastName = body.FamilyLastName, City = body.City ?? "", State = body.State ?? "",
        ChapterId = body.ChapterId, CreatedAt = DateTime.UtcNow
    };
    db.Families.Add(family);
    await db.SaveChangesAsync();
    var request = new PackageRequest
    {
        FamilyId = family.Id, ChapterId = body.ChapterId,
        Reason = body.Reason, InternalNotes = body.Notes,
        Status = CaseStatus.New, CreatedAt = DateTime.UtcNow
    };
    db.Requests.Add(request);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/requests/{request.Id}",
        new { requestId = request.Id, familyId = family.Id, status = "New" });
}).AllowAnonymous();

// POST /api/public/v1/donations — record a donation (key required, Write scope)
publicApi.MapPost("/donations", async (HttpContext http, LotvDbContext db, PublicDonationRequest body) =>
{
    if (!await ValidateApiKey(http, db, ApiKeyScope.Write)) return Results.Empty;
    if (body.Amount <= 0 || string.IsNullOrWhiteSpace(body.DonorEmail))
        return Results.BadRequest(new { error = "Amount and DonorEmail are required." });
    var donor = await db.Donors.FirstOrDefaultAsync(d => d.Email == body.DonorEmail);
    if (donor is null)
    {
        donor = new Donor
        {
            Email = body.DonorEmail, FirstName = body.DonorFirstName ?? "",
            LastName = body.DonorLastName ?? "", ChapterId = body.ChapterId,
            CreatedAt = DateTime.UtcNow
        };
        db.Donors.Add(donor);
        await db.SaveChangesAsync();
    }
    var donation = new Donation
    {
        DonorId = donor.Id, Amount = body.Amount, Date = DateTime.UtcNow,
        Channel = DonationChannel.Online, ChapterId = body.ChapterId,
        StripePaymentIntentId = body.StripePaymentIntentId,
        ContributionStatus = ContributionStatus.Processed
    };
    db.Donations.Add(donation);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/donations/{donation.Id}",
        new { donationId = donation.Id, donorId = donor.Id });
}).AllowAnonymous();

// ── API Key Management (HQAdmin) ──────────────────────────────────────────────
var apiKeys = app.MapGroup("/api/v1/apikeys").WithTags("API Keys").RequireAuthorization("HQAdmin");

apiKeys.MapGet("/", async (LotvDbContext db) =>
    Results.Ok(await db.ApiKeys.OrderByDescending(k => k.CreatedAt).ToListAsync()));

apiKeys.MapPost("/", async (CreateApiKeyRequest body, LotvDbContext db) =>
{
    // Generate a cryptographically random key and return it once (unhashed)
    var rawKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    var hash   = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
    var key = new ApiKey
    {
        KeyHash = hash, PartnerName = body.PartnerName,
        ContactEmail = body.ContactEmail, ChapterId = body.ChapterId,
        Scope = body.Scope, ExpiresAt = body.ExpiresAt, CreatedAt = DateTime.UtcNow
    };
    db.ApiKeys.Add(key);
    await db.SaveChangesAsync();
    // Return the raw key once — it cannot be recovered after this response
    return Results.Ok(new { key.Id, key.PartnerName, rawKey, note = "Store this key securely — it will not be shown again." });
});

apiKeys.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var key = await db.ApiKeys.FindAsync(id);
    if (key is null) return Results.NotFound();
    key.IsActive = false;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ── Payments (Stripe webhook) ─────────────────────────────────────────────────
app.MapPost("/api/v1/payments/webhook", async (HttpRequest request) =>
{
    // Stripe signature verification and webhook processing happens here
    // TODO: implement with Stripe.net SDK
    return Results.Ok();
}).WithTags("Payments").AllowAnonymous().RequireRateLimiting("payment");

// ─────────────────────────────────────────────────────────────────────────────
// Legacy routes — Development only (OWASP A05: remove in staging/production)
// ─────────────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    var mock = app.MapGroup("/api").AllowAnonymous();
    mock.MapGet("/families",   (IMockDataService svc) => svc.GetFamilies()).WithTags("Legacy");
    mock.MapGet("/volunteers", (IMockDataService svc) => svc.GetVolunteers()).WithTags("Legacy");
    mock.MapGet("/donors",     (IMockDataService svc) => svc.GetDonors()).WithTags("Legacy");
    mock.MapGet("/donations",  (IMockDataService svc) => svc.GetDonations()).WithTags("Legacy");
    mock.MapGet("/events",     (IMockDataService svc) => svc.GetEvents()).WithTags("Legacy");
    mock.MapGet("/cases",      (IMockDataService svc) => svc.GetRequests()).WithTags("Legacy");
    mock.MapGet("/parishes",   (IMockDataService svc) => svc.GetParishes()).WithTags("Legacy");
    mock.MapGet("/dioceses",   (IMockDataService svc) => svc.GetDioceses()).WithTags("Legacy");
    mock.MapGet("/allocations",(IMockDataService svc) => svc.GetAllocations()).WithTags("Legacy");
    mock.MapGet("/audit",      (IMockDataService svc) => svc.GetAuditLog()).WithTags("Legacy");
    mock.MapGet("/stats/dashboard", (IMockDataService svc) => svc.GetDashboardStats()).WithTags("Legacy");
    mock.MapPost("/cases", (PackageRequest req, IMockDataService svc) =>
    {
        svc.AddRequest(req); return Results.Created($"/api/cases/{req.Id}", req);
    }).WithTags("Legacy");
    mock.MapPut("/cases/{id:int}", (int id, PackageRequest req, IMockDataService svc) =>
    {
        req.Id = id; req.UpdatedAt = DateTime.UtcNow; svc.UpdateRequest(req); return Results.Ok(req);
    }).WithTags("Legacy");
    mock.MapPost("/audit", (AuditLogRequest req, IMockDataService svc) =>
    {
        svc.LogAction(req.UserName, req.Action, req.Entity, req.EntityId, req.Details); return Results.Ok();
    }).WithTags("Legacy");
}

app.Run();

// ─────────────────────────────────────────────────────────────────────────────
// Request/response records
// ─────────────────────────────────────────────────────────────────────────────
record PublicApplyRequest(Family Family);
record PublicGiveRequest(Donor Donor, Donation Donation);
record RegisterRequest(string Email, string Password, string FirstName, string LastName, UserRole Role, int? ChapterId);
record LoginRequest(string Email, string Password);
record RefreshRequest(string RefreshToken);
record StatusUpdateRequest(CaseStatus Status);
record AssignRequest(int VolunteerId);
record PriorityRequest(RequestPriority Priority);
record DueDateRequest(DateTime DueDate);
record DeclineRequest(string? Reason);
record EscalateRequest(string Reason);
record FulfillRequest(string? Notes);
record NoteRequest(string Content, bool IsInternal = true);
record BidRequest(int BidderId, decimal BidAmount);
record RoleChangeRequest(UserRole Role, int? ChapterId);
record AuditLogRequest(string UserName, string Action, string Entity, string? EntityId, string? Details);
record RequestPatchRequest(string? TrackingNumber, DateTime? ShippedDate, string? InternalNotes);
record ApproveAllocationRequest(string ApprovedBy);
record RejectAllocationRequest(string Reason);
record DonorPrivacyRequest(bool IsAnonymous);
record InventoryAdjustRequest(int QuantityDelta, string? Reason);
record ApplyPledgePaymentRequest(decimal Amount);
record FulfillWishListRequest(int Quantity, string? DonorId);
record SmsCheckInRequest(string? VolunteerPhone, string? Note);
record QrScanRequest(string Code);
record PublicIntakeRequest(string FamilyLastName, int ChapterId, PackageReason Reason,
    string? City, string? State, string? Notes);
record PublicDonationRequest(decimal Amount, string DonorEmail, int ChapterId,
    string? DonorFirstName, string? DonorLastName, string? StripePaymentIntentId);
record CreateApiKeyRequest(string PartnerName, string? ContactEmail, int? ChapterId,
    ApiKeyScope Scope, DateTime? ExpiresAt);

// Required for WebApplicationFactory in integration tests
public partial class Program { }
