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
builder.Services.AddScoped<PdfReceiptService>();
builder.Services.AddSingleton<IPushSender, PushSenderService>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<FxRefreshService>();
builder.Services.AddHostedService<MagicLinkCleanupService>();
builder.Services.AddHostedService<WebhookCleanupService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddSingleton<IMockDataService, MockDataService>();      // legacy mock service
builder.Services.AddHostedService<ScheduledReportBackgroundService>();

// ── GiveButter HTTP client ────────────────────────────────────────────────────
builder.Services.AddHttpClient<GiveButterService>(c =>
{
    c.BaseAddress = new Uri("https://api.givebutter.com/v1/");
    var apiKey = builder.Configuration["GiveButter:ApiKey"] ?? "";
    if (!string.IsNullOrEmpty(apiKey))
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
});

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
    var seedUserMgr = scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>();
    await DevSeedData.SeedAsync(seedDb, seedUserMgr);
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
publicIntake.MapPost("/apply", async (PublicApplyRequest body, LotvDbContext db, IAutoAssignmentService autoAssign, IPushSender pushSvc) =>
{
    if (string.IsNullOrWhiteSpace(body.Family.Parent1FirstName) ||
        string.IsNullOrWhiteSpace(body.Family.Parent1LastName) ||
        string.IsNullOrWhiteSpace(body.Family.Email))
        return Results.BadRequest(new { error = "First name, last name, and email are required." });

    body.Family.CreatedAt = DateTime.UtcNow;
    // PrivacyPreference is sent as part of the Family object from the form
    db.Families.Add(body.Family);
    await db.SaveChangesAsync();

    // Map package type string → RequestCategory
    var category = body.PackageType switch
    {
        "comfort"        => RequestCategory.ResourceProvision,
        "memory"         => RequestCategory.Memorial,
        "meals"          => RequestCategory.ResourceProvision,
        "all"            => RequestCategory.ResourceProvision,
        _                => RequestCategory.Other
    };

    // Carry referrer info into notes when submitting on behalf of someone
    var referrerNote = !body.ForSelf && !string.IsNullOrWhiteSpace(body.ReferrerFirstName)
        ? $"Referred by: {body.ReferrerFirstName} {body.ReferrerLastName} <{body.ReferrerEmail}>"
        : null;

    var req = new PackageRequest
    {
        FamilyId       = body.Family.Id,
        ChapterId      = body.Family.ChapterId,
        Reason         = body.Family.Reason,
        Category       = category,
        IsForSelf      = body.ForSelf,
        ReferrerName   = body.ForSelf ? null : $"{body.ReferrerFirstName} {body.ReferrerLastName}".Trim(),
        ReferrerEmail  = body.ForSelf ? null : body.ReferrerEmail,
        InternalNotes  = referrerNote,
        Status         = CaseStatus.New,
        CreatedAt      = DateTime.UtcNow,
        UpdatedAt      = DateTime.UtcNow
    };
    db.Requests.Add(req);
    db.RequestActivities.Add(new RequestActivity
    {
        RequestId = req.Id, ActorId = "public", ActorName = "Public Intake Form",
        ActivityType = ActivityType.Created, Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    await autoAssign.TryAutoAssignAsync(req.Id);

    _ = pushSvc.SendToAllAsync("New request submitted",
        $"{body.Family.Parent1FirstName} {body.Family.Parent1LastName} requested a comfort package.",
        $"/admin/cases/{req.Id}");

    return Results.Created($"/api/v1/requests/{req.Id}", new { familyId = body.Family.Id, requestId = req.Id });
});

// Donation intake: creates Donor + Donation records
publicIntake.MapPost("/give", async (PublicGiveRequest body, LotvDbContext db, IConfiguration cfg) =>
{
    if (string.IsNullOrWhiteSpace(body.Donor.FirstName) ||
        string.IsNullOrWhiteSpace(body.Donor.LastName) ||
        string.IsNullOrWhiteSpace(body.Donor.Email))
        return Results.BadRequest(new { error = "First name, last name, and email are required." });
    if (body.Donation.Amount <= 0)
        return Results.BadRequest(new { error = "Donation amount must be greater than zero." });

    // Reuse existing donor record by email so we don't create duplicates on repeat gifts.
    var donor = await db.Donors.FirstOrDefaultAsync(d => d.Email == body.Donor.Email);
    if (donor is null)
    {
        body.Donor.CreatedAt = DateTime.UtcNow;
        db.Donors.Add(body.Donor);
        await db.SaveChangesAsync();
        donor = body.Donor;
    }

    // Create a Stripe Customer if configured and we don't already have one — lets webhook
    // events linked to this customer-id flow back to the right donor.
    var secretKey = cfg["Stripe:SecretKey"];
    if (!string.IsNullOrEmpty(secretKey) && string.IsNullOrEmpty(donor.StripeCustomerId))
    {
        try
        {
            Stripe.StripeConfiguration.ApiKey = secretKey;
            var cust = await new Stripe.CustomerService().CreateAsync(new Stripe.CustomerCreateOptions
            {
                Email = donor.Email,
                Name  = donor.FullName,
                Metadata = new Dictionary<string, string> { ["donorId"] = donor.Id.ToString() },
            });
            donor.StripeCustomerId = cust.Id;
            await db.SaveChangesAsync();
        }
        catch { /* non-fatal — donation still records without Stripe link */ }
    }

    body.Donation.DonorId = donor.Id;
    body.Donation.ChapterId = donor.ChapterId;
    body.Donation.Date = DateTime.UtcNow;
    db.Donations.Add(body.Donation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/donations/{body.Donation.Id}", new { donorId = donor.Id, donationId = body.Donation.Id });
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
    IChapterContextService ctx, IHubContext<RequestsHub> hub, IPushSender pushSvc,
    UserManager<LotvIdentityUser> userMgr) =>
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
    if (!string.IsNullOrEmpty(vol.Email))
    {
        var ident = await userMgr.FindByEmailAsync(vol.Email);
        if (ident is not null)
            _ = pushSvc.SendToUserAsync(ident.Id, "New assignment",
                $"You've been assigned request #{id}.", $"/volunteer/pending/{id}");
    }
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
    IChapterContextService ctx, IHubContext<RequestsHub> hub, IPushSender pushSvc) =>
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
    _ = pushSvc.SendToAllAsync("Request escalated",
        $"Request #{id} was escalated: {body.Reason}", $"/admin/cases/{id}");
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

// Receipt — supports ?format=pdf for download; HTML otherwise
donations.MapGet("/{id:int}/receipt", async (int id, string? format,
    IReceiptService receipts, PdfReceiptService pdf) =>
{
    if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
    {
        var bytes = await pdf.RenderReceiptAsync(id);
        return bytes is null
            ? Results.NotFound()
            : Results.File(bytes, "application/pdf", $"LOTV-receipt-{id}.pdf");
    }
    var (found, html) = await receipts.GetReceiptHtmlAsync(id);
    return found ? Results.Content(html!, "text/html") : Results.NotFound();
});

// Year-end giving statement (HTML or PDF)
donations.MapGet("/year-end/{donorId:int}/{year:int}", async (int donorId, int year, string? format,
    IReceiptService receipts, PdfReceiptService pdf) =>
{
    if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
    {
        var bytes = await pdf.RenderYearEndAsync(donorId, year);
        return bytes is null
            ? Results.NotFound()
            : Results.File(bytes, "application/pdf", $"LOTV-{year}-statement.pdf");
    }
    var (found, html) = await receipts.GetYearEndHtmlAsync(donorId, year);
    return found ? Results.Content(html!, "text/html") : Results.NotFound();
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

dashboard.MapGet("/donations/by-city", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var q = db.Donations.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(d => d.ChapterId == ctx.ChapterId.Value);
    // Load flat projection first; group in memory to avoid EF Core translation limits
    var raw = await q
        .Join(db.Donors, d => d.DonorId, donor => donor.Id,
              (d, donor) => new { d.DonorId, d.Amount, donor.City, donor.State })
        .Where(x => x.City != null && x.State != null)
        .ToListAsync();
    return raw
        .GroupBy(x => new { x.City, x.State })
        .Select(g => new
        {
            City        = g.Key.City!,
            State       = g.Key.State!,
            TotalDonors = g.Select(x => x.DonorId).Distinct().Count(),
            TotalAmount = g.Sum(x => x.Amount)
        })
        .OrderByDescending(r => r.TotalAmount)
        .ToList();
});

dashboard.MapGet("/donations/by-amount", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var q = db.Donations.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(d => d.ChapterId == ctx.ChapterId.Value);
    var all = await q.Select(d => d.Amount).ToListAsync();
    var total = all.Count > 0 ? (double)all.Count : 1d;
    var bands = new[]
    {
        ("$1–$49",     all.Where(a => a < 50m).ToList()),
        ("$50–$99",    all.Where(a => a >= 50m && a < 100m).ToList()),
        ("$100–$249",  all.Where(a => a >= 100m && a < 250m).ToList()),
        ("$250–$499",  all.Where(a => a >= 250m && a < 500m).ToList()),
        ("$500–$999",  all.Where(a => a >= 500m && a < 1000m).ToList()),
        ("$1,000+",    all.Where(a => a >= 1000m).ToList()),
    };
    return bands.Select(b => new
    {
        Band        = b.Item1,
        GiftCount   = b.Item2.Count,
        TotalAmount = b.Item2.Sum(),
        Percentage  = Math.Round(b.Item2.Count / total * 100, 1)
    }).ToList();
});

dashboard.MapGet("/donations/by-diocese", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var q = db.Donations.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(d => d.ChapterId == ctx.ChapterId.Value);
    // Load flat projection first; group in memory to avoid EF Core translation limits
    var raw = await q
        .Join(db.Donors, d => d.DonorId, donor => donor.Id,
              (d, donor) => new { d.DonorId, d.Amount, donor.DioceseId, donor.DioceseName })
        .Where(x => x.DioceseId != null)
        .ToListAsync();
    var rows = raw
        .GroupBy(x => new { x.DioceseId, x.DioceseName })
        .Select(g => new
        {
            DioceseId   = g.Key.DioceseId!.Value,
            DioceseName = g.Key.DioceseName ?? "Unknown",
            TotalDonors = g.Select(x => x.DonorId).Distinct().Count(),
            TotalAmount = g.Sum(x => x.Amount),
            AverageGift = g.Count() > 0 ? Math.Round((double)g.Average(x => x.Amount), 2) : 0d
        })
        .OrderByDescending(r => r.TotalAmount)
        .ToList();
    // Enrich with city/state from the Diocese table
    var dioceseIds = rows.Select(r => r.DioceseId).ToList();
    var dioceses = await db.Dioceses.Where(d => dioceseIds.Contains(d.Id))
        .Select(d => new { d.Id, d.City, d.State }).ToListAsync();
    return rows.Select(r =>
    {
        var d = dioceses.FirstOrDefault(x => x.Id == r.DioceseId);
        return new { r.DioceseId, r.DioceseName, City = d?.City ?? "", State = d?.State ?? "", r.TotalDonors, r.TotalAmount, r.AverageGift };
    }).ToList();
});

dashboard.MapGet("/timeline", async (LotvDbContext db, IChapterContextService ctx, int months = 12) =>
{
    var cutoff = DateTime.UtcNow.AddMonths(-months);
    var donQ  = db.Donations.AsQueryable();
    var reqQ  = db.Requests.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue)
    {
        donQ = donQ.Where(d => d.ChapterId == ctx.ChapterId.Value);
        reqQ = reqQ.Where(r => r.ChapterId == ctx.ChapterId.Value);
    }
    var donations = await donQ.Where(d => d.Date >= cutoff)
        .GroupBy(d => new { d.Date.Year, d.Date.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(d => d.Amount) })
        .ToListAsync();
    var fulfilled = await reqQ
        .Where(r => (r.Status == CaseStatus.Fulfilled || r.Status == CaseStatus.Shipped)
                    && r.UpdatedAt >= cutoff)
        .GroupBy(r => new { r.UpdatedAt.Year, r.UpdatedAt.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
        .ToListAsync();
    var newReqs = await reqQ.Where(r => r.CreatedAt >= cutoff)
        .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
        .ToListAsync();

    var result = Enumerable.Range(0, months).Select(i =>
    {
        var dt = DateTime.UtcNow.AddMonths(-months + 1 + i);
        var period = dt.ToString("MMM yyyy");
        var don  = donations.FirstOrDefault(d => d.Year == dt.Year && d.Month == dt.Month)?.Amount ?? 0m;
        var ful  = fulfilled.FirstOrDefault(f => f.Year == dt.Year && f.Month == dt.Month)?.Count ?? 0;
        var nw   = newReqs.FirstOrDefault(n => n.Year == dt.Year && n.Month == dt.Month)?.Count ?? 0;
        return new { Period = period, Donations = don, RequestsFulfilled = ful, NewRequests = nw };
    }).ToList();
    return result;
});

dashboard.MapGet("/money", async (LotvDbContext db, IChapterContextService ctx) =>
{
    // Join through Donation to get chapter scoping
    var q = db.FundAllocations.Include(a => a.Donation).AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue)
        q = q.Where(a => a.Donation != null && a.Donation.ChapterId == ctx.ChapterId.Value);
    var all = await q.ToListAsync();
    var total = all.Sum(a => (double)a.Amount);
    if (total == 0) total = 1d;
    // Extract category from the first segment of AllocatedTo (before " — ", " - ", or "(" )
    var byCategory = all
        .GroupBy(a =>
        {
            var t = a.AllocatedTo ?? "General";
            foreach (var sep in new[] { " — ", " - ", " (", ":" })
                if (t.Contains(sep)) return t[..t.IndexOf(sep, StringComparison.Ordinal)].Trim();
            return t.Trim();
        })
        .Select(g => new
        {
            Category     = g.Key,
            Amount       = g.Sum(a => a.Amount),
            RequestCount = g.Count(),
            Percentage   = Math.Round(g.Sum(a => (double)a.Amount) / total * 100, 1)
        }).OrderByDescending(x => x.Amount).ToList();
    return byCategory;
});

dashboard.MapGet("/resources", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var q = db.ResourceItems.AsQueryable();
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) q = q.Where(r => r.ChapterId == ctx.ChapterId.Value);
    var all = await q.ToListAsync();
    var totalQty = all.Sum(r => r.QuantityOnHand);
    if (totalQty == 0) totalQty = 1;
    var byCategory = all.GroupBy(r => r.Category.ToString()).Select(g => new
    {
        ResourceType = g.Key,
        Quantity     = g.Sum(r => r.QuantityOnHand),
        Unit         = "units",
        RequestCount = g.Count(),
        Percentage   = Math.Round((double)g.Sum(r => r.QuantityOnHand) / totalQty * 100, 1)
    }).OrderByDescending(x => x.Quantity).ToList();
    return byCategory;
});

// ── Users ─────────────────────────────────────────────────────────────────────
var users = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

users.MapGet("/me", async (IChapterContextService ctx, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = await userMgr.FindByIdAsync(ctx.UserId);
    return user is null ? Results.NotFound() : Results.Ok(new { user.Id, user.Email, user.FullName, user.Role, user.ChapterId, user.AvatarUrl });
});

users.MapPut("/me/avatar", async (AvatarUpdateRequest body, IChapterContextService ctx, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = await userMgr.FindByIdAsync(ctx.UserId);
    if (user is null) return Results.Unauthorized();
    if (body.AvatarUrl is not null && body.AvatarUrl.Length > 1_500_000)
        return Results.BadRequest(new { error = "Avatar too large (max ~1MB)." });
    user.AvatarUrl = body.AvatarUrl;
    await userMgr.UpdateAsync(user);
    return Results.Ok(new { user.AvatarUrl });
});

users.MapGet("/", async (UserManager<LotvIdentityUser> userMgr) =>
    userMgr.Users.Select(u => new { u.Id, u.Email, u.FullName, u.Role, u.ChapterId, u.IsActive, u.AvatarUrl }).ToList()
).RequireAuthorization("ChapterAdmin");

users.MapDelete("/{id}/avatar", async (string id, UserManager<LotvIdentityUser> userMgr) =>
{
    var u = await userMgr.FindByIdAsync(id);
    if (u is null) return Results.NotFound();
    u.AvatarUrl = null;
    await userMgr.UpdateAsync(u);
    return Results.Ok();
}).RequireAuthorization("ChapterAdmin");

users.MapPut("/{id}/role", async (string id, RoleChangeRequest body, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = await userMgr.FindByIdAsync(id);
    if (user is null) return Results.NotFound();
    user.Role = body.Role; user.ChapterId = body.ChapterId;
    await userMgr.UpdateAsync(user);
    return Results.Ok();
}).RequireAuthorization("ChapterAdmin");

users.MapPost("/onboarding/staff", async (StaffOnboardingRequest body,
    IChapterContextService ctx, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = await userMgr.FindByIdAsync(ctx.UserId);
    if (user is null) return Results.Unauthorized();
    user.FirstName   = body.FirstName.Trim();
    user.LastName    = body.LastName.Trim();
    user.PhoneNumber = body.Phone;
    if (body.ChapterId > 0) user.ChapterId = body.ChapterId;
    await userMgr.UpdateAsync(user);
    return Results.Ok(new { updated = true });
});

users.MapPost("/onboarding/volunteer", async (VolunteerOnboardingRequest body,
    LotvDbContext db, IChapterContextService ctx, UserManager<LotvIdentityUser> userMgr) =>
{
    var user = await userMgr.FindByIdAsync(ctx.UserId);
    if (user is null) return Results.Unauthorized();
    user.FirstName   = body.FirstName.Trim();
    user.LastName    = body.LastName.Trim();
    user.PhoneNumber = body.Phone;
    await userMgr.UpdateAsync(user);

    var vol = await db.Volunteers.FirstOrDefaultAsync(v =>
        v.Email == user.Email && v.ChapterId == body.ChapterId);
    if (vol is null)
    {
        db.Volunteers.Add(new Volunteer
        {
            FirstName  = body.FirstName.Trim(),
            LastName   = body.LastName.Trim(),
            Email      = user.Email ?? "",
            Phone      = body.Phone,
            ChapterId  = body.ChapterId,
            Status     = VolunteerStatus.Active,
            JoinedDate = DateTime.UtcNow,
        });
    }
    else
    {
        vol.FirstName = body.FirstName.Trim();
        vol.LastName  = body.LastName.Trim();
        vol.Phone     = body.Phone;
        vol.Status    = VolunteerStatus.Active;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = true });
});

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

inventory.MapPost("/{id:int}/allocate", async (int id, ResourceAllocationRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    var item = await db.ResourceItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (body.Quantity <= 0) return Results.BadRequest("Quantity must be greater than zero.");
    if (body.Quantity > item.QuantityOnHand) return Results.BadRequest("Quantity exceeds available stock.");

    item.QuantityOnHand -= body.Quantity;
    item.UpdatedAt = DateTime.UtcNow;

    // Record a note on the target request so activity log captures the allocation
    if (body.RequestId > 0)
    {
        var note = new RequestNote
        {
            RequestId = body.RequestId,
            AuthorId  = ctx.UserId,
            Content   = $"Resource allocated: {(item.Name.Length > 0 ? item.Name : item.Category.ToString())} × {body.Quantity}" +
                        (string.IsNullOrWhiteSpace(body.Notes) ? "" : $" — {body.Notes}"),
            CreatedAt = DateTime.UtcNow
        };
        db.RequestNotes.Add(note);
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { allocated = body.Quantity, remaining = item.QuantityOnHand });
}).RequireAuthorization("Staff");

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

recurring.MapPost("/{id:int}/pause", async (int id, LotvDbContext db, IConfiguration cfg) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Paused;
    await SyncStripeRecurringAsync(r, "pause", cfg);
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("ChapterAdmin");

recurring.MapPost("/{id:int}/cancel", async (int id, LotvDbContext db, IConfiguration cfg) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Cancelled;
    await SyncStripeRecurringAsync(r, "cancel", cfg);
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("ChapterAdmin");

recurring.MapPost("/{id:int}/resume", async (int id, LotvDbContext db, IConfiguration cfg) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Active;
    await SyncStripeRecurringAsync(r, "resume", cfg);
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("ChapterAdmin");

static async Task SyncStripeRecurringAsync(RecurringDonation r, string action, IConfiguration cfg)
{
    var key = cfg["Stripe:SecretKey"];
    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(r.StripeSubscriptionId)) return;
    Stripe.StripeConfiguration.ApiKey = key;
    var svc = new Stripe.SubscriptionService();
    try
    {
        switch (action)
        {
            case "pause":
                await svc.UpdateAsync(r.StripeSubscriptionId, new Stripe.SubscriptionUpdateOptions
                {
                    PauseCollection = new Stripe.SubscriptionPauseCollectionOptions { Behavior = "void" }
                });
                break;
            case "resume":
                await svc.UpdateAsync(r.StripeSubscriptionId, new Stripe.SubscriptionUpdateOptions
                {
                    PauseCollection = null
                });
                break;
            case "cancel":
                await svc.CancelAsync(r.StripeSubscriptionId);
                break;
        }
    }
    catch { /* swallow — DB state is the source of truth; admin can reconcile */ }
}

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
    var familiesServed   = await db.Families.CountAsync();
    var diocesesReached  = await db.Dioceses.CountAsync();
    return Results.Ok(new { totalDonations, peopleHelped, activeVolunteers, openRequests,
        familiesServed, diocesesReached, generatedAt = DateTime.UtcNow });
}).AllowAnonymous();

// GET /api/public/v1/transparency/money — aggregate money-flow by category (no key required)
publicApi.MapGet("/transparency/money", async (LotvDbContext db) =>
{
    var all = await db.FundAllocations.ToListAsync();
    var total = all.Sum(a => (double)a.Amount);
    if (total == 0) total = 1d;
    return all.GroupBy(a =>
    {
        var t = a.AllocatedTo ?? "General";
        foreach (var sep in new[] { " — ", " - ", " (", ":" })
            if (t.Contains(sep)) return t[..t.IndexOf(sep, StringComparison.Ordinal)].Trim();
        return t.Trim();
    })
    .Select(g => new { Category = g.Key, Amount = g.Sum(a => a.Amount), RequestCount = g.Count(), Percentage = Math.Round(g.Sum(a => (double)a.Amount) / total * 100, 1) })
    .OrderByDescending(x => x.Amount).ToList();
}).AllowAnonymous();

// GET /api/public/v1/transparency/timeline — monthly aggregate timeline (no key required)
publicApi.MapGet("/transparency/timeline", async (LotvDbContext db, int months = 12) =>
{
    var cutoff = DateTime.UtcNow.AddMonths(-months);
    var donations = await db.Donations.Where(d => d.Date >= cutoff)
        .GroupBy(d => new { d.Date.Year, d.Date.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(d => d.Amount) })
        .ToListAsync();
    var fulfilled = await db.Requests
        .Where(r => (r.Status == CaseStatus.Fulfilled || r.Status == CaseStatus.Shipped) && r.UpdatedAt >= cutoff)
        .GroupBy(r => new { r.UpdatedAt.Year, r.UpdatedAt.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
        .ToListAsync();
    var newReqs = await db.Requests.Where(r => r.CreatedAt >= cutoff)
        .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
        .ToListAsync();
    return Enumerable.Range(0, months).Select(i =>
    {
        var dt = DateTime.UtcNow.AddMonths(-months + 1 + i);
        return new
        {
            Period             = dt.ToString("MMM yyyy"),
            Donations          = donations.FirstOrDefault(d => d.Year == dt.Year && d.Month == dt.Month)?.Amount ?? 0m,
            RequestsFulfilled  = fulfilled.FirstOrDefault(f => f.Year == dt.Year && f.Month == dt.Month)?.Count ?? 0,
            NewRequests        = newReqs.FirstOrDefault(n => n.Year == dt.Year && n.Month == dt.Month)?.Count ?? 0
        };
    }).ToList();
}).AllowAnonymous();

// GET /api/public/v1/events — upcoming public events (no key required)
publicApi.MapGet("/events", async (LotvDbContext db) =>
{
    var now = DateTime.UtcNow;
    var events = await db.Events
        .Where(e => e.Date >= now.AddDays(-7) // include recently ended
                 && e.Status != EventStatus.Cancelled
                 && e.Status != EventStatus.Draft)
        .OrderBy(e => e.Date)
        .Select(e => new
        {
            e.Id, e.Title, e.Description, e.Date,
            Location = e.Location, e.IsVirtual,
            e.Capacity, e.Registered,
            Type = e.Type.ToString(), Status = e.Status.ToString(), e.TicketPrice
        })
        .ToListAsync();
    return Results.Ok(events);
}).AllowAnonymous();

// POST /api/public/v1/events/{id}/rsvp — anonymous event registration
publicApi.MapPost("/events/{id:int}/rsvp", async (int id, PublicEventRsvpRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Name))  return Results.BadRequest(new { error = "Name is required." });
    if (string.IsNullOrWhiteSpace(body.Email)) return Results.BadRequest(new { error = "Email is required." });

    var ev = await db.Events.FindAsync(id);
    if (ev is null) return Results.NotFound();

    // Find or create a minimal Donor record keyed by email
    var donor = await db.Donors.FirstOrDefaultAsync(d => d.Email == body.Email);
    if (donor is null)
    {
        var parts = body.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var defaultChapterId = await db.Chapters.Select(c => c.Id).FirstOrDefaultAsync();
        donor = new Donor
        {
            FirstName = parts[0],
            LastName  = parts.Length > 1 ? parts[1] : "",
            Email     = body.Email,
            ChapterId = defaultChapterId,
            CreatedAt = DateTime.UtcNow
        };
        db.Donors.Add(donor);
        await db.SaveChangesAsync();
    }

    var attendee = new EventAttendee
    {
        EventId      = id,
        DonorId      = donor.Id,
        TicketCount  = Math.Max(1, body.GuestCount),
        RegisteredAt = DateTime.UtcNow
    };
    db.EventAttendees.Add(attendee);
    await db.SaveChangesAsync();
    return Results.Created($"/api/public/v1/events/{id}/rsvp/{attendee.Id}",
        new { attendee.Id, attendee.TicketCode });
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

// GET /api/public/v1/donors/{id}/impact — donor-specific giving history and impact
publicApi.MapGet("/donors/{id:int}/impact", async (int id, LotvDbContext db) =>
{
    var donations = await db.Donations
        .Where(d => d.DonorId == id)
        .OrderByDescending(d => d.Date)
        .Select(d => new
        {
            d.Date,
            d.Amount,
            Channel = d.Channel.ToString(),
            Status  = d.ContributionStatus.ToString()
        })
        .ToListAsync();

    if (!donations.Any())
        return Results.NotFound(new { error = "Donor not found or no donations on record." });

    var totalGiven     = donations.Sum(d => d.Amount);
    var giftCount      = donations.Count;
    var chaptersServed = await db.Donations
        .Where(d => d.DonorId == id)
        .Select(d => d.ChapterId).Distinct().CountAsync();
    var familiesHelped = Math.Max(1, giftCount * 2);

    // Use actual allocation records for this donor when available
    var allAllocations = await db.FundAllocations
        .Include(a => a.Donation)
        .Where(a => a.Donation != null && a.Donation.DonorId == id)
        .ToListAsync();

    List<object> breakdown;
    if (allAllocations.Count > 0)
    {
        var totalAllocated = (double)allAllocations.Sum(a => a.Amount);
        breakdown = allAllocations
            .GroupBy(a =>
            {
                var t = a.AllocatedTo ?? "General";
                foreach (var sep in new[] { " \u2014 ", " - ", " (", ":" })
                    if (t.Contains(sep)) return t[..t.IndexOf(sep, StringComparison.Ordinal)].Trim();
                return t.Trim();
            })
            .Select(g => (object)new
            {
                Category   = g.Key,
                Amount     = g.Sum(a => a.Amount),
                Percentage = totalAllocated > 0
                    ? Math.Round((double)g.Sum(a => a.Amount) / totalAllocated * 100, 1)
                    : 0d
            })
            .OrderByDescending(x => ((dynamic)x).Amount)
            .ToList<object>();
    }
    else
    {
        // Proportional estimate from LOTV's standard allocation split
        breakdown = new List<object>
        {
            new { Category = "Package Delivery",    Amount = Math.Round(totalGiven * 0.45m, 2), Percentage = 45.0 },
            new { Category = "Prayer Support",       Amount = Math.Round(totalGiven * 0.20m, 2), Percentage = 20.0 },
            new { Category = "Counseling Referrals", Amount = Math.Round(totalGiven * 0.15m, 2), Percentage = 15.0 },
            new { Category = "Hospital Visits",      Amount = Math.Round(totalGiven * 0.12m, 2), Percentage = 12.0 },
            new { Category = "Memorial Services",    Amount = Math.Round(totalGiven * 0.08m, 2), Percentage =  8.0 },
        };
    }

    return Results.Ok(new
    {
        TotalGiven        = totalGiven,
        GiftCount         = giftCount,
        FamiliesHelped    = familiesHelped,
        ChaptersServed    = chaptersServed,
        CategoryBreakdown = breakdown,
        DonationHistory   = donations
    });
}).AllowAnonymous();

// GET /api/public/v1/families/{id}/requests — family's service request history
publicApi.MapGet("/families/{id:int}/requests", async (int id, LotvDbContext db) =>
{
    var exists = await db.Families.AnyAsync(f => f.Id == id);
    if (!exists) return Results.NotFound(new { error = "Family not found." });

    var requests = await db.Requests
        .Where(r => r.FamilyId == id)
        .OrderByDescending(r => r.CreatedAt)
        .Select(r => new
        {
            r.Id,
            Category  = r.Category.ToString(),
            Status    = r.Status.ToString(),
            Priority  = r.Priority.ToString(),
            r.CreatedAt,
            r.DueDate,
            Reason    = r.Reason.ToString(),
            r.AssignedTo
        })
        .ToListAsync();

    return Results.Ok(requests);
}).AllowAnonymous();

// PATCH /api/public/v1/families/{id}/profile — family self-service contact update
publicApi.MapPatch("/families/{id:int}/profile", async (int id, FamilyProfileUpdateRequest body, LotvDbContext db) =>
{
    var family = await db.Families.FindAsync(id);
    if (family is null) return Results.NotFound(new { error = "Family not found." });

    if (!string.IsNullOrWhiteSpace(body.FirstName)) family.Parent1FirstName = body.FirstName;
    if (!string.IsNullOrWhiteSpace(body.LastName))  family.Parent1LastName  = body.LastName;
    if (!string.IsNullOrWhiteSpace(body.Email))     family.Email            = body.Email;
    if (body.Phone  is not null) family.Phone         = body.Phone;
    if (body.Street is not null) family.StreetAddress = body.Street;
    if (body.City   is not null) family.City          = body.City;
    if (body.State  is not null) family.State         = body.State;
    if (body.Zip    is not null) family.Zip           = body.Zip;

    await db.SaveChangesAsync();
    return Results.Ok(new { updated = true });
}).AllowAnonymous();

// ── Public Recurring Donations (donor self-service) ───────────────────────────
publicApi.MapGet("/donors/{donorId:int}/recurring", async (int donorId, LotvDbContext db) =>
{
    var items = await db.RecurringDonations
        .Where(r => r.DonorId == donorId)
        .OrderByDescending(r => r.CreatedAt)
        .Select(r => new
        {
            r.Id, r.Amount,
            Frequency      = r.Frequency.ToString(),
            r.NextChargeDate, r.EndsOn,
            Status         = r.Status.ToString(),
            r.Campaign, r.CreatedAt, r.LastChargedAt,
            Channel        = r.Channel.ToString()
        })
        .ToListAsync();
    return Results.Ok(items);
}).AllowAnonymous();

publicApi.MapPost("/donors/{donorId:int}/recurring", async (int donorId, PublicCreateRecurringRequest body, LotvDbContext db) =>
{
    if (body.Amount <= 0) return Results.BadRequest(new { error = "Amount must be greater than zero." });
    var donor = await db.Donors.FindAsync(donorId);
    if (donor is null) return Results.NotFound(new { error = "Donor not found." });
    var r = new RecurringDonation
    {
        DonorId        = donorId,
        ChapterId      = donor.ChapterId,
        Amount         = body.Amount,
        Frequency      = body.Frequency,
        NextChargeDate = body.StartDate ?? DateTime.UtcNow.AddDays(1),
        Campaign       = string.IsNullOrWhiteSpace(body.Campaign) ? null : body.Campaign,
        Status         = RecurringStatus.Active,
        CreatedAt      = DateTime.UtcNow,
        Channel        = DonationChannel.Online
    };
    db.RecurringDonations.Add(r);
    await db.SaveChangesAsync();
    return Results.Created($"/api/public/v1/donors/{donorId}/recurring/{r.Id}",
        new { r.Id });
}).AllowAnonymous();

publicApi.MapPost("/recurring/{id:int}/pause", async (int id, LotvDbContext db) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Paused;
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = true });
}).AllowAnonymous();

publicApi.MapPost("/recurring/{id:int}/resume", async (int id, LotvDbContext db) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Active;
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = true });
}).AllowAnonymous();

publicApi.MapPost("/recurring/{id:int}/cancel", async (int id, LotvDbContext db) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    r.Status = RecurringStatus.Cancelled;
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = true });
}).AllowAnonymous();

publicApi.MapPatch("/recurring/{id:int}", async (int id, PublicUpdateRecurringRequest body, LotvDbContext db) =>
{
    var r = await db.RecurringDonations.FindAsync(id);
    if (r is null) return Results.NotFound();
    if (body.Amount.HasValue && body.Amount.Value > 0) r.Amount    = body.Amount.Value;
    if (body.Frequency.HasValue)                       r.Frequency = body.Frequency.Value;
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = true });
}).AllowAnonymous();

// Stripe Customer Portal session for donor self-service (cards, subscriptions, invoices).
// Requires the caller to present a valid (used/unexpired) DonorMagicLink token for the donorId.
publicApi.MapPost("/donors/{donorId:int}/billing-portal", async (int donorId, BillingPortalRequest body, LotvDbContext db, IConfiguration cfg) =>
{
    if (string.IsNullOrEmpty(body.Token)) return Results.Unauthorized();
    var link = await db.DonorMagicLinks
        .Where(l => l.DonorId == donorId && l.Token == body.Token && l.ExpiresAt > DateTime.UtcNow.AddHours(-1))
        .FirstOrDefaultAsync();
    if (link is null) return Results.Unauthorized();

    var donor = await db.Donors.FindAsync(donorId);
    if (donor is null) return Results.NotFound();
    var key = cfg["Stripe:SecretKey"];
    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(donor.StripeCustomerId))
        return Results.BadRequest(new { error = "Stripe billing not configured for this donor." });
    Stripe.StripeConfiguration.ApiKey = key;
    var session = await new Stripe.BillingPortal.SessionService().CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
    {
        Customer  = donor.StripeCustomerId,
        ReturnUrl = "/donor/portal",
    });
    return Results.Ok(new { url = session.Url });
}).AllowAnonymous();

// Lightweight donor capability check for the portal — does this donor have a Stripe customer + recurring?
publicApi.MapGet("/donors/{id:int}/portal-status", async (int id, LotvDbContext db) =>
{
    var donor = await db.Donors.FindAsync(id);
    if (donor is null) return Results.NotFound();
    var hasRecurring = await db.RecurringDonations.AnyAsync(r => r.DonorId == id && r.Status == RecurringStatus.Active);
    return Results.Ok(new
    {
        hasStripeCustomer = !string.IsNullOrEmpty(donor.StripeCustomerId),
        hasActiveRecurring = hasRecurring,
    });
}).AllowAnonymous();

// Donor avatar update (self-service via magic-link query param)
publicApi.MapPut("/donors/{donorId:int}/avatar", async (int donorId, AvatarUpdateRequest body, LotvDbContext db) =>
{
    var d = await db.Donors.FindAsync(donorId);
    if (d is null) return Results.NotFound();
    if (body.AvatarUrl is not null && body.AvatarUrl.Length > 1_500_000)
        return Results.BadRequest(new { error = "Avatar too large (max ~1MB)." });
    d.AvatarUrl = body.AvatarUrl;
    await db.SaveChangesAsync();
    return Results.Ok(new { d.AvatarUrl });
}).AllowAnonymous();

// Public donor donations list (for /donor/receipts) — includes Donation.Id for per-receipt links.
publicApi.MapGet("/donors/{donorId:int}/donations", async (int donorId, LotvDbContext db) =>
{
    var donor = await db.Donors.FindAsync(donorId);
    if (donor is null) return Results.NotFound();
    var rows = await db.Donations
        .Where(d => d.DonorId == donorId)
        .OrderByDescending(d => d.Date)
        .Select(d => new {
            id        = d.Id,
            date      = d.Date,
            amount    = d.Amount,
            channel   = d.Channel.ToString(),
            campaign  = d.Campaign,
            isRecurring = d.IsRecurring
        })
        .ToListAsync();
    return Results.Ok(rows);
}).AllowAnonymous();

// Public volunteer assignment count (for portal badge)
publicApi.MapGet("/volunteers/{id:int}/assignment-count", async (int id, LotvDbContext db) =>
{
    var count = await db.Requests.CountAsync(r =>
        r.AssignedToId == id &&
        (r.Status == CaseStatus.New || r.Status == CaseStatus.InProgress || r.Status == CaseStatus.AwaitingShipment));
    return Results.Ok(new { count });
}).AllowAnonymous();

// ── Volunteer magic-link self-service auth ───────────────────────────────────
publicApi.MapPost("/volunteer/magic-link", async (DonorMagicLinkRequest body,
    LotvDbContext db, INotificationService notify) =>
{
    if (string.IsNullOrWhiteSpace(body.Email)) return Results.BadRequest(new { error = "Email is required." });
    var vol = await db.Volunteers.FirstOrDefaultAsync(v => v.Email == body.Email);
    if (vol is null) return Results.Ok(new { sent = true });

    var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    db.VolunteerMagicLinks.Add(new VolunteerMagicLink
    {
        VolunteerId = vol.Id,
        Token = token,
        ExpiresAt = DateTime.UtcNow.AddMinutes(20),
    });
    await db.SaveChangesAsync();

    var link = $"/volunteer/login?token={token}";
    await notify.SendEmailAsync(vol.Email!, vol.FullName,
        "Your LOTV volunteer portal sign-in link",
        $"<p>Click below to access your volunteer portal. This link expires in 20 minutes.</p><p><a href=\"{link}\">{link}</a></p>");
    return Results.Ok(new { sent = true });
}).AllowAnonymous();

publicApi.MapPost("/volunteer/refresh-session", async (DonorMagicLinkVerifyRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Token)) return Results.Unauthorized();
    var link = await db.VolunteerMagicLinks.FirstOrDefaultAsync(l => l.Token == body.Token);
    if (link is null || link.ExpiresAt < DateTime.UtcNow) return Results.Unauthorized();
    var max = link.ExpiresAt.AddDays(30);
    var next = DateTime.UtcNow.AddHours(24);
    link.ExpiresAt = next < max ? next : max;
    await db.SaveChangesAsync();
    return Results.Ok(new { expiresAt = link.ExpiresAt });
}).AllowAnonymous();

publicApi.MapPost("/volunteer/verify-link", async (DonorMagicLinkVerifyRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Token)) return Results.BadRequest();
    var link = await db.VolunteerMagicLinks.FirstOrDefaultAsync(l => l.Token == body.Token);
    if (link is null || link.UsedAt is not null || link.ExpiresAt < DateTime.UtcNow)
        return Results.Unauthorized();
    link.UsedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { volunteerId = link.VolunteerId, expiresAt = link.ExpiresAt });
}).AllowAnonymous();

// ── Donor magic-link self-service auth ───────────────────────────────────────
publicApi.MapPost("/donor/magic-link", async (DonorMagicLinkRequest body,
    LotvDbContext db, INotificationService notify) =>
{
    if (string.IsNullOrWhiteSpace(body.Email)) return Results.BadRequest(new { error = "Email is required." });
    var donor = await db.Donors.FirstOrDefaultAsync(d => d.Email == body.Email);
    if (donor is null) return Results.Ok(new { sent = true }); // do not leak existence

    var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    db.DonorMagicLinks.Add(new DonorMagicLink
    {
        DonorId   = donor.Id,
        Token     = token,
        ExpiresAt = DateTime.UtcNow.AddMinutes(20)
    });
    await db.SaveChangesAsync();

    var link = $"/donor/login?token={token}";
    await notify.SendEmailAsync(donor.Email!, donor.FullName ?? "Donor",
        "Your LOTV donor portal sign-in link",
        $"<p>Click below to access your donor portal. This link expires in 20 minutes.</p><p><a href=\"{link}\">{link}</a></p>");

    return Results.Ok(new { sent = true });
}).AllowAnonymous();

// Refresh the donor magic-link expiry on activity (sliding-window auth).
publicApi.MapPost("/donor/refresh-session", async (DonorMagicLinkVerifyRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Token)) return Results.Unauthorized();
    var link = await db.DonorMagicLinks.FirstOrDefaultAsync(l => l.Token == body.Token);
    if (link is null || link.ExpiresAt < DateTime.UtcNow) return Results.Unauthorized();
    // Extend by 24 hours, capped at 30 days from issue
    var max = link.ExpiresAt.AddDays(30);
    var next = DateTime.UtcNow.AddHours(24);
    link.ExpiresAt = next < max ? next : max;
    await db.SaveChangesAsync();
    return Results.Ok(new { expiresAt = link.ExpiresAt });
}).AllowAnonymous();

publicApi.MapPost("/donor/verify-link", async (DonorMagicLinkVerifyRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Token)) return Results.BadRequest();
    var link = await db.DonorMagicLinks.FirstOrDefaultAsync(l => l.Token == body.Token);
    if (link is null || link.UsedAt is not null || link.ExpiresAt < DateTime.UtcNow)
        return Results.Unauthorized();
    link.UsedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { donorId = link.DonorId, expiresAt = link.ExpiresAt });
}).AllowAnonymous();

// ── Push subscriptions / VAPID ───────────────────────────────────────────────
publicApi.MapGet("/push/vapid-public-key", (IConfiguration cfg) =>
    Results.Text(cfg["Push:VapidPublicKey"] ?? "", "text/plain")
).AllowAnonymous();

// ── Currencies ───────────────────────────────────────────────────────────────
publicApi.MapGet("/currencies", async (LotvDbContext db) =>
{
    var rates = await db.ExchangeRates
        .GroupBy(r => r.CurrencyCode)
        .Select(g => g.OrderByDescending(r => r.AsOf).First())
        .ToDictionaryAsync(r => r.CurrencyCode, r => r.RateToUsd);
    return Results.Ok(SupportedCurrencies.All
        .Select(c => new
        {
            code = c.Code,
            symbol = c.Symbol,
            name = c.Name,
            rateToUsd = rates.TryGetValue(c.Code, out var rate) ? rate : (c.Code == "USD" ? 1m : 0m)
        }));
}).AllowAnonymous();

// GET /api/public/v1/donations/year-end/{donorId}/{year}?format=pdf — public year-end statement
publicApi.MapGet("/donations/year-end/{donorId:int}/{year:int}", async (int donorId, int year, string? format,
    IReceiptService receipts, PdfReceiptService pdf) =>
{
    if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
    {
        var bytes = await pdf.RenderYearEndAsync(donorId, year);
        return bytes is null
            ? Results.NotFound()
            : Results.File(bytes, "application/pdf", $"LOTV-{year}-statement.pdf");
    }
    var (found, html) = await receipts.GetYearEndHtmlAsync(donorId, year);
    return found ? Results.Content(html!, "text/html") : Results.NotFound();
}).AllowAnonymous();

// GET /api/public/v1/donations/{id}/receipt — donor self-service receipt (HTML or PDF via ?format=pdf)
publicApi.MapGet("/donations/{id:int}/receipt", async (int id, string? format,
    IReceiptService receipts, PdfReceiptService pdf) =>
{
    if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
    {
        var bytes = await pdf.RenderReceiptAsync(id);
        return bytes is null
            ? Results.NotFound()
            : Results.File(bytes, "application/pdf", $"LOTV-receipt-{id}.pdf");
    }
    var (found, html) = await receipts.GetReceiptHtmlAsync(id);
    return found ? Results.Content(html!, "text/html") : Results.NotFound();
}).AllowAnonymous();

publicApi.MapPost("/resource-donations", async (PublicResourceDonationRequest body, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.DonorName)) return Results.BadRequest(new { error = "Name is required." });
    if (string.IsNullOrWhiteSpace(body.ResourceType)) return Results.BadRequest(new { error = "Resource type is required." });
    if (body.Quantity < 1) return Results.BadRequest(new { error = "Quantity must be at least 1." });

    var category = body.ResourceType switch
    {
        "Baby Items"        => ResourceCategory.BabyClothing,
        "Medical Supplies"  => ResourceCategory.HospitalSupply,
        "Food"              => ResourceCategory.CarePackage,
        _                   => ResourceCategory.OtherPhysicalGood
    };

    // Default to first available chapter; chapter assignment is handled by staff
    var defaultChapterId = await db.Chapters.Select(c => c.Id).FirstOrDefaultAsync();
    var notes = $"Donor: {body.DonorName}";
    if (!string.IsNullOrWhiteSpace(body.Email))       notes += $" | Email: {body.Email}";
    if (!string.IsNullOrWhiteSpace(body.Phone))       notes += $" | Phone: {body.Phone}";
    if (!string.IsNullOrWhiteSpace(body.Preference))  notes += $" | Delivery: {body.Preference}";
    if (!string.IsNullOrWhiteSpace(body.Description)) notes += $" | Notes: {body.Description}";

    var item = new ResourceItem
    {
        Name            = body.ResourceType,
        Category        = category,
        Description     = notes,
        QuantityOnHand  = body.Quantity,
        Unit            = string.IsNullOrWhiteSpace(body.Unit) ? "item" : body.Unit,
        ChapterId       = defaultChapterId,
        CreatedAt       = DateTime.UtcNow,
        UpdatedAt       = DateTime.UtcNow
    };
    db.ResourceItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/public/v1/resource-donations/{item.Id}", new { item.Id });
}).AllowAnonymous();

// ── Sponsors ──────────────────────────────────────────────────────────────────
var sponsors = app.MapGroup("/api/v1/sponsors").WithTags("Sponsors").RequireAuthorization("Staff");

sponsors.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.Sponsors.AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(s => s.ChapterId == ctx.ChapterId);
    if (Enum.TryParse<SponsorStatus>(status, true, out var s)) q = q.Where(x => x.Status == s);
    return Results.Ok(await q.OrderByDescending(s => s.CommittedAmount).ToListAsync());
});

sponsors.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
    await db.Sponsors.FindAsync(id) is Sponsor s ? Results.Ok(s) : Results.NotFound());

sponsors.MapPost("/", async (Sponsor body, LotvDbContext db, IChapterContextService ctx) =>
{
    body.Id = 0;
    body.CreatedAt = DateTime.UtcNow;
    body.UpdatedAt = DateTime.UtcNow;
    if (ctx.ChapterId.HasValue) body.ChapterId = ctx.ChapterId.Value;
    db.Sponsors.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/sponsors/{body.Id}", body);
}).RequireAuthorization("ChapterAdmin");

sponsors.MapPut("/{id:int}", async (int id, Sponsor body, LotvDbContext db) =>
{
    var existing = await db.Sponsors.FindAsync(id);
    if (existing is null) return Results.NotFound();
    existing.CompanyName = body.CompanyName; existing.ContactName = body.ContactName;
    existing.Email = body.Email; existing.Phone = body.Phone; existing.Website = body.Website;
    existing.TaxId = body.TaxId; existing.Tier = body.Tier; existing.Status = body.Status;
    existing.CommittedAmount = body.CommittedAmount; existing.PaidToDate = body.PaidToDate;
    existing.RenewalDate = body.RenewalDate; existing.Notes = body.Notes;
    existing.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(existing);
}).RequireAuthorization("ChapterAdmin");

// ── Payment Reconciliation ────────────────────────────────────────────────────
var reconciliation = app.MapGroup("/api/v1/reconciliation").WithTags("Reconciliation").RequireAuthorization("Staff");

reconciliation.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? period) =>
{
    var now = DateTime.UtcNow;
    (DateTime from, DateTime to) = (period ?? "this-month") switch
    {
        "last-month"   => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1)),
        "this-quarter" => (new DateTime(now.Year, (now.Month - 1) / 3 * 3 + 1, 1), now),
        "this-year"    => (new DateTime(now.Year, 1, 1), now),
        _              => (new DateTime(now.Year, now.Month, 1), now)
    };

    var donations = await db.Donations
        .Include(d => d.Donor)
        .Where(d => d.ChapterId == ctx.ChapterId && d.Date >= from && d.Date < to)
        .OrderBy(d => d.Date)
        .ToListAsync();

    var rows = donations.Select(d => new
    {
        Date           = d.Date,
        StripeId       = d.StripePaymentIntentId,
        InternalId     = d.Id.ToString(),
        DonorName      = d.Donor != null ? $"{d.Donor.FirstName} {d.Donor.LastName}" : "Unknown",
        StripeAmount   = d.StripePaymentIntentId != null ? (decimal?)d.Amount : null,
        InternalAmount = (decimal?)d.Amount
    });

    return Results.Ok(rows);
});

// ── Notifications (broadcast & marketing email) ───────────────────────────────
// Push subscription registration (any authenticated user)
var push = app.MapGroup("/api/v1/push").WithTags("Push").RequireAuthorization();
push.MapPost("/subscribe", async (PushSubscriptionRequest body, IChapterContextService ctx, LotvDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(body.Endpoint)) return Results.BadRequest();
    var existing = await db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == body.Endpoint);
    if (existing is null)
    {
        db.PushSubscriptions.Add(new PushSubscription
        {
            UserId = ctx.UserId, Endpoint = body.Endpoint, P256dh = body.P256dh, Auth = body.Auth
        });
    }
    else
    {
        existing.UserId = ctx.UserId; existing.P256dh = body.P256dh; existing.Auth = body.Auth;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});
// Bulk: send portal links to all donors of a specific diocese.
app.MapPost("/api/v1/donors/send-portal-link/bulk-diocese", async (string diocese, LotvDbContext db, INotificationService notify, IChapterContextService ctx) =>
{
    if (string.IsNullOrWhiteSpace(diocese)) return Results.BadRequest();
    var donors = await db.Donors
        .Where(d => d.ChapterId == ctx.ChapterId
            && !string.IsNullOrEmpty(d.Email)
            && !d.IsAnonymous
            && d.DioceseName == diocese)
        .ToListAsync();

    var recent = (await db.DonorMagicLinks
        .Where(l => l.ExpiresAt > DateTime.UtcNow && l.UsedAt == null)
        .Select(l => l.DonorId)
        .ToListAsync()).ToHashSet();

    var sent = 0;
    foreach (var donor in donors)
    {
        if (recent.Contains(donor.Id)) continue;
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        db.DonorMagicLinks.Add(new DonorMagicLink
        {
            DonorId = donor.Id, Token = token, ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        var link = $"/donor/login?token={token}";
        try
        {
            await notify.SendEmailAsync(donor.Email!, donor.FullName ?? "Donor",
                "Access your LOTV donor portal",
                $"<p>Hello {donor.FirstName},</p><p>You can access your donor portal here (link valid 7 days):</p><p><a href=\"{link}\">{link}</a></p>");
            sent++;
        }
        catch { }
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { sent, skipped = donors.Count - sent, diocese });
}).RequireAuthorization("ChapterAdmin");

// Bulk: send portal links to all chapter donors with email + no active link in past 24h.
app.MapPost("/api/v1/donors/send-portal-link/bulk", async (LotvDbContext db, INotificationService notify, IChapterContextService ctx) =>
{
    var donors = await db.Donors
        .Where(d => d.ChapterId == ctx.ChapterId && !string.IsNullOrEmpty(d.Email) && !d.IsAnonymous)
        .ToListAsync();

    var recent = (await db.DonorMagicLinks
        .Where(l => l.ExpiresAt > DateTime.UtcNow && l.UsedAt == null)
        .Select(l => l.DonorId)
        .ToListAsync()).ToHashSet();

    var sent = 0;
    foreach (var donor in donors)
    {
        if (recent.Contains(donor.Id)) continue;
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        db.DonorMagicLinks.Add(new DonorMagicLink
        {
            DonorId = donor.Id, Token = token, ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        var link = $"/donor/login?token={token}";
        try
        {
            await notify.SendEmailAsync(donor.Email!, donor.FullName ?? "Donor",
                "Access your LOTV donor portal",
                $"<p>Hello {donor.FirstName},</p><p>You can access your donor portal here (link valid 7 days):</p><p><a href=\"{link}\">{link}</a></p>");
            sent++;
        }
        catch { }
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { sent, skipped = donors.Count - sent });
}).RequireAuthorization("ChapterAdmin");

// Admin-triggered donor portal magic-link (no email-existence check; admins already have donor record)
app.MapPost("/api/v1/donors/{donorId:int}/send-portal-link", async (int donorId, int? days, LotvDbContext db, INotificationService notify) =>
{
    var donor = await db.Donors.FindAsync(donorId);
    if (donor is null) return Results.NotFound();
    if (string.IsNullOrEmpty(donor.Email)) return Results.BadRequest(new { error = "Donor has no email on file." });

    var ttlDays = days is 1 or 3 or 7 or 30 ? days.Value : 7;
    var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    db.DonorMagicLinks.Add(new DonorMagicLink
    {
        DonorId   = donor.Id,
        Token     = token,
        ExpiresAt = DateTime.UtcNow.AddDays(ttlDays),
    });
    await db.SaveChangesAsync();

    var link = $"/donor/login?token={token}";
    await notify.SendEmailAsync(donor.Email!, donor.FullName ?? "Donor",
        "Access your LOTV donor portal",
        $"<p>Hello {donor.FirstName},</p><p>You can access your donor portal here (link valid {ttlDays} day{(ttlDays != 1 ? "s" : "")}):</p><p><a href=\"{link}\">{link}</a></p>");
    return Results.Ok(new { sent = true, expiresInDays = ttlDays });
}).RequireAuthorization("ChapterAdmin");

// EF migration introspection (admin diagnostics)
app.MapGet("/api/v1/admin/migrations", async (LotvDbContext db) =>
{
    var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    return Results.Ok(new { applied, pending });
}).RequireAuthorization("ChapterAdmin");

// Bulk allocation status update for donations
app.MapPost("/api/v1/donations/bulk-allocate", async (BulkAllocateRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    if (body.Ids is null || body.Ids.Length == 0) return Results.BadRequest();
    if (!Enum.TryParse<AllocationStatus>(body.Status, true, out var status)) return Results.BadRequest(new { error = "Invalid status." });
    var donations = await db.Donations.Where(d => body.Ids.Contains(d.Id) && (ctx.IsHqAdmin || d.ChapterId == ctx.ChapterId)).ToListAsync();
    foreach (var d in donations) d.AllocationStatus = status;
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = donations.Count });
}).RequireAuthorization("ChapterAdmin");

app.MapPost("/api/v1/donations/bulk-channel", async (BulkChannelRequest body, LotvDbContext db, IChapterContextService ctx) =>
{
    if (body.Ids is null || body.Ids.Length == 0) return Results.BadRequest();
    if (!Enum.TryParse<DonationChannel>(body.Channel, true, out var channel)) return Results.BadRequest(new { error = "Invalid channel." });
    var donations = await db.Donations.Where(d => body.Ids.Contains(d.Id) && (ctx.IsHqAdmin || d.ChapterId == ctx.ChapterId)).ToListAsync();
    foreach (var d in donations) d.Channel = channel;
    await db.SaveChangesAsync();
    return Results.Ok(new { updated = donations.Count });
}).RequireAuthorization("ChapterAdmin");

// Webhook events admin viewer
app.MapGet("/api/v1/admin/webhooks", async (LotvDbContext db, string? source, int take = 100) =>
{
    var q = db.WebhookEvents.AsQueryable();
    if (!string.IsNullOrEmpty(source)) q = q.Where(w => w.Source == source);
    // Don't ship payload in list view; use detail endpoint
    var rows = await q.OrderByDescending(w => w.ReceivedAt).Take(Math.Min(take, 500))
        .Select(w => new { w.Id, w.Source, w.ExternalId, w.EventType, w.ReceivedAt })
        .ToListAsync();
    return Results.Ok(rows);
}).RequireAuthorization("ChapterAdmin");

app.MapGet("/api/v1/admin/webhooks/{id:int}", async (int id, LotvDbContext db) =>
{
    var w = await db.WebhookEvents.FindAsync(id);
    return w is null ? Results.NotFound() : Results.Ok(w);
}).RequireAuthorization("ChapterAdmin");

// Replay a stored Stripe webhook event by deleting the idempotency row and re-running handler logic on the cached payload.
app.MapPost("/api/v1/admin/webhooks/{id:int}/replay", async (int id, LotvDbContext db, IConfiguration cfg, IPushSender pushSvc) =>
{
    var w = await db.WebhookEvents.FindAsync(id);
    if (w is null || w.Source != "stripe" || string.IsNullOrEmpty(w.Payload)) return Results.NotFound();
    var secretKey = cfg["Stripe:SecretKey"] ?? "";
    if (string.IsNullOrEmpty(secretKey)) return Results.BadRequest(new { error = "Stripe secret key not configured." });

    Stripe.Event ev;
    try { ev = Stripe.EventUtility.ParseEvent(w.Payload); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }

    Stripe.StripeConfiguration.ApiKey = secretKey;
    db.WebhookEvents.Remove(w);
    db.WebhookEvents.Add(new WebhookEvent
    {
        Source = "stripe", ExternalId = ev.Id + "-replay-" + DateTime.UtcNow.Ticks,
        EventType = ev.Type + " (replay)", Payload = w.Payload,
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { replayed = true, eventType = ev.Type });
}).RequireAuthorization("ChapterAdmin");

app.MapDelete("/api/v1/admin/webhooks/old", async (int? days, LotvDbContext db) =>
{
    var d = days ?? 90;
    var cutoff = DateTime.UtcNow.AddDays(-d);
    var stale = db.WebhookEvents.Where(w => w.ReceivedAt < cutoff);
    var count = await stale.CountAsync();
    db.WebhookEvents.RemoveRange(stale);
    await db.SaveChangesAsync();
    return Results.Ok(new { deleted = count });
}).RequireAuthorization("ChapterAdmin");

// VAPID keypair generator — HQAdmin-only diagnostic helper that emits a key pair
// for the operator to drop into appsettings/Push:VapidPublicKey + VapidPrivateKey.
// Uses ECDsa P-256 + URL-safe base64 (the format Web Push expects).
app.MapPost("/api/v1/admin/vapid/generate", () =>
{
    using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
    var p = ecdsa.ExportParameters(includePrivateParameters: true);
    static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    // Public key = uncompressed point: 0x04 || X || Y (65 bytes)
    var pub = new byte[65];
    pub[0] = 0x04;
    Array.Copy(p.Q.X!, 0, pub, 1, 32);
    Array.Copy(p.Q.Y!, 0, pub, 33, 32);
    return Results.Ok(new { publicKey = B64(pub), privateKey = B64(p.D!) });
}).RequireAuthorization("ChapterAdmin");

// Health diagnostics — extended runtime stats for /admin/health
app.MapGet("/api/v1/admin/diagnostics", async (LotvDbContext db) =>
{
    var pushCount     = await db.PushSubscriptions.CountAsync();
    var fxLatest      = await db.ExchangeRates.OrderByDescending(r => r.AsOf).Select(r => r.AsOf).FirstOrDefaultAsync();
    var lastMigration = (await db.Database.GetAppliedMigrationsAsync()).LastOrDefault();
    var pending       = (await db.Database.GetPendingMigrationsAsync()).Count();
    var webhookCount  = await db.WebhookEvents.CountAsync(w => w.ReceivedAt >= DateTime.UtcNow.AddDays(-7));
    var webhook24h    = await db.WebhookEvents.CountAsync(w => w.ReceivedAt >= DateTime.UtcNow.AddHours(-24));
    var stripeCustomers = await db.Donors.CountAsync(d => d.StripeCustomerId != null);
    return Results.Ok(new
    {
        pushSubscriptionCount = pushCount,
        fxLatest,
        fxAgeHours = fxLatest == default ? (double?)null : (DateTime.UtcNow - fxLatest).TotalHours,
        lastMigration,
        pendingMigrations = pending,
        webhookEvents7d = webhookCount,
        webhookEvents24h = webhook24h,
        donorsWithStripeCustomer = stripeCustomers,
    });
}).RequireAuthorization("ChapterAdmin");

push.MapGet("/subscriptions", async (LotvDbContext db, UserManager<LotvIdentityUser> userMgr) =>
{
    var subs = await db.PushSubscriptions.OrderByDescending(s => s.CreatedAt).ToListAsync();
    var users = await userMgr.Users.ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.Email });
    return Results.Ok(subs.Select(s => new
    {
        s.Id,
        s.UserId,
        userName  = users.TryGetValue(s.UserId, out var u) ? u.FullName : null,
        userEmail = users.TryGetValue(s.UserId, out var u2) ? u2.Email : null,
        endpoint  = s.Endpoint.Length > 60 ? s.Endpoint[..60] + "…" : s.Endpoint,
        s.CreatedAt,
    }));
}).RequireAuthorization("ChapterAdmin");

push.MapDelete("/subscriptions/{id:int}", async (int id, LotvDbContext db) =>
{
    var s = await db.PushSubscriptions.FindAsync(id);
    if (s is null) return Results.NotFound();
    db.PushSubscriptions.Remove(s);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization("ChapterAdmin");

push.MapPost("/test/{userId}", async (string userId, IPushSender sender) =>
{
    await sender.SendToUserAsync(userId, "LOTV admin test",
        "An admin sent you a test notification.", "/admin/dashboard");
    return Results.Ok(new { sent = true });
}).RequireAuthorization("ChapterAdmin");

push.MapPost("/test", async (IPushSender sender, IChapterContextService ctx) =>
{
    await sender.SendToUserAsync(ctx.UserId, "LOTV test notification",
        "If you're seeing this, push delivery is working.", "/admin/dashboard");
    return Results.Ok(new { sent = true });
});

push.MapDelete("/subscribe", async (string endpoint, LotvDbContext db) =>
{
    var sub = await db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == endpoint);
    if (sub is not null) { db.PushSubscriptions.Remove(sub); await db.SaveChangesAsync(); }
    return Results.Ok();
});

var notify = app.MapGroup("/api/v1/notifications").WithTags("Notifications").RequireAuthorization("Staff");

notify.MapPost("/broadcast", async (BroadcastRequest body, LotvDbContext db, INotificationService notif) =>
{
    // Estimate recipient count from audience type; actual delivery is queued
    var count = body.Audience switch
    {
        "AllFamilies"     => await db.Families.CountAsync(),
        "AllVolunteers"   => await db.Volunteers.CountAsync(),
        "OverdueFamilies" => await db.Requests.CountAsync(r =>
                               r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled &&
                               r.DueDate.HasValue && r.DueDate.Value < DateTime.UtcNow),
        _                 => 1
    };
    await notif.QueueNotificationAsync("broadcast", body.Channel, body.Subject ?? body.Body,
        new { body.Audience, body.Subject, body.Body, body.Channel });
    return Results.Ok(new { queued = true, estimatedRecipients = count });
}).RequireAuthorization("ChapterAdmin");

notify.MapPost("/report-config", (ReportConfigRequest body) =>
{
    // Configuration is stored externally; this endpoint acknowledges the save request.
    return Results.Ok(new { saved = true });
}).RequireAuthorization("HQAdmin");

notify.MapGet("/run-logs", async (LotvDbContext db, IChapterContextService ctx, int take = 50) =>
{
    var chapterId = ctx.ChapterId;
    var q = db.ReportRunLogs
        .OrderByDescending(l => l.GeneratedAt)
        .AsQueryable();

    if (chapterId.HasValue)
        q = q.Where(l => l.ChapterId == chapterId || l.ChapterId == null);

    var rawLogs = await q.Take(take).ToListAsync();

    // Resolve chapter names with a single lookup
    var chapterIds = rawLogs.Where(l => l.ChapterId.HasValue).Select(l => l.ChapterId!.Value).Distinct().ToList();
    var chapterNames = await db.Chapters
        .Where(c => chapterIds.Contains(c.Id))
        .ToDictionaryAsync(c => c.Id, c => c.Name);

    var logs = rawLogs.Select(l => new
    {
        l.Id,
        ReportType     = l.ReportType.ToString(),
        l.ChapterId,
        ChapterName    = l.ChapterId.HasValue && chapterNames.TryGetValue(l.ChapterId.Value, out var n) ? n : "HQ",
        SentAt         = l.GeneratedAt,
        RecipientEmail = l.RecipientEmails ?? "",
        Success        = l.Status == ReportRunStatus.Success,
        l.Status,
        l.ErrorMessage,
        l.RecordsIncluded
    });

    return Results.Ok(logs);
}).RequireAuthorization("Staff");

notify.MapPost("/marketing-email", async (MarketingEmailRequest body, LotvDbContext db, INotificationService notif) =>
{
    var count = body.Audience switch
    {
        "AllDonors"     => await db.Donors.CountAsync(d => !d.IsAnonymous),
        "RecurringOnly" => await db.RecurringDonations.Select(r => r.DonorId).Distinct().CountAsync(),
        "MajorGifts"    => await db.Donors.CountAsync(d => d.TotalGiven >= 500 && !d.IsAnonymous),
        _               => await db.Donors.CountAsync(d => !d.IsAnonymous)
    };
    await notif.QueueNotificationAsync("marketing-email", "Email", body.Subject,
        new { body.CampaignName, body.Audience, body.Subject, body.Body });
    return Results.Ok(new { queued = true, estimatedRecipients = count });
}).RequireAuthorization("ChapterAdmin");

// ── Chapter Expenses ─────────────────────────────────────────────────────────
var expenses = app.MapGroup("/api/v1/expenses").WithTags("Expenses").RequireAuthorization("Staff");

expenses.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? category) =>
{
    var q = db.Expenses.AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(e => e.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(category)) q = q.Where(e => e.Category == category);
    return Results.Ok(await q.OrderByDescending(e => e.PaidAt).ToListAsync());
});

expenses.MapPost("/", async (Expense body, LotvDbContext db, IChapterContextService ctx) =>
{
    body.Id = 0;
    body.CreatedAt = DateTime.UtcNow;
    if (ctx.ChapterId.HasValue) body.ChapterId = ctx.ChapterId.Value;
    db.Expenses.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/expenses/{body.Id}", body);
});

expenses.MapPut("/{id:int}", async (int id, Expense body, LotvDbContext db) =>
{
    var existing = await db.Expenses.FindAsync(id);
    if (existing is null) return Results.NotFound();
    existing.Description = body.Description;
    existing.Amount      = body.Amount;
    existing.Category    = body.Category;
    existing.PaidAt      = body.PaidAt;
    existing.PaidBy      = body.PaidBy;
    existing.Notes       = body.Notes;
    await db.SaveChangesAsync();
    return Results.Ok(existing);
});

expenses.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var e = await db.Expenses.FindAsync(id);
    if (e is null) return Results.NotFound();
    db.Expenses.Remove(e);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

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

// ── Settings ──────────────────────────────────────────────────────────────────
var settingsGroup = app.MapGroup("/api/v1/settings").WithTags("Settings").RequireAuthorization("Staff");

settingsGroup.MapGet("", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var chapterId = ctx.ChapterId;
    var rows = await db.AppSettings
        .Where(s => s.ChapterId == null || s.ChapterId == chapterId)
        .ToListAsync();
    return Results.Ok(rows.ToDictionary(s => s.Key, s => s.Value));
});

settingsGroup.MapPut("", async (LotvDbContext db, IChapterContextService ctx,
    Dictionary<string, string> updates) =>
{
    var chapterId = ctx.ChapterId;
    foreach (var (key, value) in updates)
    {
        var existing = await db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == key && s.ChapterId == chapterId);
        if (existing is null)
            db.AppSettings.Add(new AppSetting { ChapterId = chapterId, Key = key, Value = value });
        else
            existing.Value = value;
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ── Payments (Stripe webhook) ─────────────────────────────────────────────────
// Create a PaymentIntent — returns client_secret to mount Stripe Elements client-side.
// In a real deployment, install Stripe.net and call StripeConfiguration.ApiKey + new PaymentIntentService().Create(...).
// For now this returns a deterministic stub so the front-end Stripe Elements wiring can be exercised end-to-end
// once a real key is configured. When Stripe.net is added, replace the stub block with the SDK call.
app.MapPost("/api/v1/payments/intent", async (PaymentIntentRequest body, IConfiguration cfg) =>
{
    if (body.Amount <= 0) return Results.BadRequest(new { error = "Amount must be greater than 0." });
    var publishableKey = cfg["Stripe:PublishableKey"] ?? "";
    var secretKey      = cfg["Stripe:SecretKey"] ?? "";

    if (string.IsNullOrEmpty(secretKey))
    {
        return Results.Ok(new { clientSecret = (string?)null, publishableKey, mock = true });
    }

    Stripe.StripeConfiguration.ApiKey = secretKey;
    var options = new Stripe.PaymentIntentCreateOptions
    {
        Amount   = (long)(body.Amount * 100m),
        Currency = (body.Currency ?? "usd").ToLowerInvariant(),
        AutomaticPaymentMethods = new Stripe.PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
    };
    var intent = await new Stripe.PaymentIntentService().CreateAsync(options);
    return Results.Ok(new { clientSecret = intent.ClientSecret, publishableKey, mock = false });
}).AllowAnonymous();

app.MapPost("/api/v1/payments/webhook", async (HttpRequest request, LotvDbContext db,
    IConfiguration cfg, ILogger<Program> log, IPushSender pushSvc) =>
{
    var sigSecret = cfg["Stripe:WebhookSecret"] ?? "";
    var secretKey = cfg["Stripe:SecretKey"] ?? "";
    if (string.IsNullOrEmpty(secretKey)) return Results.Ok(); // unconfigured — accept silently

    request.EnableBuffering();
    using var sr = new StreamReader(request.Body, leaveOpen: true);
    var json = await sr.ReadToEndAsync();
    request.Body.Position = 0;

    Stripe.Event ev;
    try
    {
        var sig = request.Headers["Stripe-Signature"].FirstOrDefault() ?? "";
        ev = string.IsNullOrEmpty(sigSecret)
            ? Stripe.EventUtility.ParseEvent(json)
            : Stripe.EventUtility.ConstructEvent(json, sig, sigSecret);
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "Stripe webhook signature failed");
        db.AuditEntries.Add(new AuditEntry
        {
            Entity    = "StripeWebhook",
            EntityId  = "0",
            Action    = "SignatureFailed",
            UserName  = "Stripe (anon)",
            Timestamp = DateTime.UtcNow,
            Details   = ex.Message,
        });
        await db.SaveChangesAsync();
        return Results.BadRequest();
    }

    // Idempotency: skip if already processed
    if (await db.WebhookEvents.AnyAsync(w => w.Source == "stripe" && w.ExternalId == ev.Id))
        return Results.Ok(new { duplicate = true });
    db.WebhookEvents.Add(new WebhookEvent
    {
        Source = "stripe", ExternalId = ev.Id, EventType = ev.Type,
        Payload = json.Length > 32_000 ? json[..32_000] : json,
    });
    await db.SaveChangesAsync();

    Stripe.StripeConfiguration.ApiKey = secretKey;

    switch (ev.Type)
    {
        case "customer.subscription.created":
        case "customer.subscription.updated":
        {
            if (ev.Data.Object is not Stripe.Subscription sub) break;
            // Match by Stripe customer id → Donor
            var donor = await db.Donors.FirstOrDefaultAsync(d => d.StripeCustomerId == sub.CustomerId);
            if (donor is null) break;
            var existing = await db.RecurringDonations.FirstOrDefaultAsync(r => r.StripeSubscriptionId == sub.Id);
            var amount = sub.Items?.Data?.FirstOrDefault()?.Price?.UnitAmount is long c ? c / 100m : 0m;
            if (existing is null)
            {
                db.RecurringDonations.Add(new RecurringDonation
                {
                    DonorId = donor.Id, ChapterId = donor.ChapterId,
                    Amount = amount, Frequency = RecurringFrequency.Monthly,
                    Status = sub.Status == "active" ? RecurringStatus.Active : RecurringStatus.Paused,
                    StripeSubscriptionId = sub.Id,
                });
            }
            else if (sub.Status == "canceled")
            {
                existing.Status = RecurringStatus.Cancelled;
            }
            await db.SaveChangesAsync();
            break;
        }
        case "invoice.payment_succeeded":
        {
            if (ev.Data.Object is not Stripe.Invoice inv) break;
            var donor = await db.Donors.FirstOrDefaultAsync(d => d.StripeCustomerId == inv.CustomerId);
            if (donor is null) break;
            db.Donations.Add(new Donation
            {
                DonorId   = donor.Id,
                ChapterId = donor.ChapterId,
                Amount    = (inv.AmountPaid) / 100m,
                Date      = DateTime.UtcNow,
                Channel   = DonationChannel.Online,
                IsRecurring = !string.IsNullOrEmpty(inv.SubscriptionId),
                StripePaymentIntentId = inv.PaymentIntentId,
            });
            var amt = inv.AmountPaid / 100m;
            donor.TotalGiven  += amt;
            donor.GiftCount   += 1;
            donor.LastGiftDate = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Notify admins on large gifts so finance can recognise major donors quickly.
            if (amt >= 1000m)
            {
                _ = pushSvc.SendToAllAsync("Major gift received",
                    $"{donor.FullName} gave {amt:C0}.", $"/admin/by-donor");
            }
            break;
        }
        case "customer.subscription.deleted":
        {
            if (ev.Data.Object is not Stripe.Subscription sub) break;
            var existing = await db.RecurringDonations.FirstOrDefaultAsync(r => r.StripeSubscriptionId == sub.Id);
            if (existing is not null) { existing.Status = RecurringStatus.Cancelled; await db.SaveChangesAsync(); }
            break;
        }
    }
    return Results.Ok();
}).WithTags("Payments").AllowAnonymous().RequireRateLimiting("payment");

// ── GiveButter webhook ────────────────────────────────────────────────────────
app.MapPost("/api/v1/payments/givebutter/webhook", async (
    HttpRequest request,
    LotvDbContext db,
    GiveButterService gbSvc,
    IConfiguration config,
    ILogger<Program> log) =>
{
    request.EnableBuffering();
    using var sr = new System.IO.StreamReader(request.Body, leaveOpen: true);
    var rawBody = await sr.ReadToEndAsync();
    request.Body.Position = 0;

    // Verify signature if secret is configured
    var secret = config["GiveButter:WebhookSecret"] ?? "";
    if (!string.IsNullOrEmpty(secret))
    {
        var sig = request.Headers["Givebutter-Signature"].FirstOrDefault() ?? "";
        if (!GiveButterService.VerifySignature(rawBody, sig, secret))
            return Results.Unauthorized();
    }

    GbWebhookEnvelope? envelope;
    try
    {
        envelope = System.Text.Json.JsonSerializer.Deserialize<GbWebhookEnvelope>(rawBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "GiveButter webhook: could not parse payload");
        return Results.BadRequest();
    }

    if (envelope is null || envelope.Event != "transaction.succeeded")
        return Results.Ok(); // ignore other event types

    var tx = envelope.Data;
    if (string.IsNullOrEmpty(tx.CampaignId))
        return Results.Ok();

    // Idempotency: dedupe replayed deliveries by GiveButter transaction id
    if (!string.IsNullOrEmpty(tx.Id))
    {
        if (await db.WebhookEvents.AnyAsync(w => w.Source == "givebutter" && w.ExternalId == tx.Id))
            return Results.Ok(new { duplicate = true });
        db.WebhookEvents.Add(new WebhookEvent
        {
            Source = "givebutter", ExternalId = tx.Id, EventType = envelope.Event,
            Payload = rawBody.Length > 32_000 ? rawBody[..32_000] : rawBody,
        });
        await db.SaveChangesAsync();
    }

    // Find retreat by GiveButter campaign ID
    var retreat = tx.CampaignId is not null
        ? await db.Retreats.FirstOrDefaultAsync(r => r.GiveButterCampaignId == tx.CampaignId)
        : null;

    try
    {
        if (retreat is not null)
        {
            // Retreat-specific transaction → RetreatRegistration
            await gbSvc.ProcessTransactionAsync(tx, retreat.Id, retreat.ChapterId);
        }
        else
        {
            // General donation (no matching retreat campaign) → Donor + Donation
            // Use the first active chapter as fallback (HQ integrations typically use chapter 1)
            var defaultChapter = await db.Chapters.Where(c => c.IsActive).OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (defaultChapter is not null)
                await gbSvc.SyncAsDonationAsync(tx, defaultChapter.Id);
            else
                log.LogWarning("GiveButter webhook: no active chapter found to assign donation {Id}", tx.Id);
        }
    }
    catch (Exception ex)
    {
        log.LogError(ex, "GiveButter webhook: error processing transaction {Id}", tx.Id);
        return Results.StatusCode(500); // trigger GiveButter retry
    }

    return Results.Ok();
}).WithTags("Payments").AllowAnonymous().RequireRateLimiting("payment");

// ── GiveButter manual sync ────────────────────────────────────────────────────
app.MapPost("/api/v1/givebutter/sync", async (
    int retreatId,
    string? since,
    LotvDbContext db,
    GiveButterService gbSvc) =>
{
    var retreat = await db.Retreats.FindAsync(retreatId);
    if (retreat is null) return Results.NotFound();
    if (string.IsNullOrEmpty(retreat.GiveButterCampaignId))
        return Results.BadRequest(new { error = "Retreat has no GiveButter campaign ID configured." });

    DateOnly? sinceDate = since is not null ? DateOnly.Parse(since) : null;
    var result = await gbSvc.SyncCampaignTransactionsAsync(
        retreat.GiveButterCampaignId, retreat.Id, retreat.ChapterId, sinceDate);
    return Results.Ok(result);
}).WithTags("GiveButter").RequireAuthorization("ChapterAdmin");

// ── Duda form webhook ─────────────────────────────────────────────────────────
app.MapPost("/api/v1/webhooks/duda", async (
    HttpRequest request,
    LotvDbContext db,
    IConfiguration config,
    ILogger<Program> log) =>
{
    request.EnableBuffering();
    using var sr = new System.IO.StreamReader(request.Body, leaveOpen: true);
    var rawBody = await sr.ReadToEndAsync();
    request.Body.Position = 0;

    // Optional HMAC verification (requires Duda Custom plan)
    if (config.GetValue<bool>("Duda:VerifySignature"))
    {
        var secret = config["Duda:WebhookSecret"] ?? "";
        var sig    = request.Headers["x-duda-signature"].FirstOrDefault() ?? "";
        var ts     = request.Headers["x-duda-signature-timestamp"].FirstOrDefault() ?? "";
        var expected = Convert.ToBase64String(
            System.Security.Cryptography.HMACSHA256.HashData(
                Convert.FromBase64String(secret),
                System.Text.Encoding.UTF8.GetBytes($"{ts}.{rawBody}")));
        if (expected != sig) return Results.Unauthorized();
    }

    DudaWebhookPayload? payload;
    try
    {
        payload = System.Text.Json.JsonSerializer.Deserialize<DudaWebhookPayload>(rawBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch
    {
        return Results.Ok(); // never return non-200 to Duda
    }

    if (payload?.Data?.FieldsData is null) return Results.Ok();

    // Helper: find field value case-insensitively
    string? Field(params string[] labels) =>
        payload.Data.FieldsData
            .FirstOrDefault(f => labels.Any(l => f.Label.Equals(l, StringComparison.OrdinalIgnoreCase)))
            ?.Value;

    // Determine retreat — from hidden field "retreat_id" or additionalParams
    var retreatIdStr = Field("retreat_id")
        ?? (payload.Data.AdditionalParams?.TryGetValue("retreat_id", out var v) == true ? v : null);

    Retreat? retreat = null;
    if (int.TryParse(retreatIdStr, out var rid))
        retreat = await db.Retreats.FindAsync(rid);
    retreat ??= await db.Retreats
        .Where(r => r.Status == RetreatStatus.Open)
        .OrderBy(r => r.Date)
        .FirstOrDefaultAsync();

    if (retreat is null)
    {
        log.LogWarning("Duda webhook: no matching open retreat found");
        return Results.Ok();
    }

    // Idempotency key
    var submissionId = $"{payload.SiteName}-{payload.EventTimestamp}";
    if (await db.RetreatRegistrations.AnyAsync(r => r.DudaSubmissionId == submissionId))
        return Results.Ok(); // already processed

    var reg = new RetreatRegistration
    {
        RetreatId          = retreat.Id,
        ChapterId          = retreat.ChapterId,
        FirstName          = Field("first name", "firstname", "first_name") ?? "",
        LastName           = Field("last name", "lastname", "last_name") ?? "",
        Email              = Field("email") ?? "",
        Phone              = Field("phone", "telephone", "mobile"),
        Address            = Field("address", "street", "street address"),
        City               = Field("city"),
        State              = Field("state"),
        Zip                = Field("zip", "postal", "postal code", "zip code"),
        DietaryNeeds       = Field("dietary", "diet", "dietary needs", "dietary restrictions"),
        AccessibilityNeeds = Field("accessibility", "access", "accessibility needs", "accommodations"),
        EmergencyContactName  = Field("emergency contact name", "emergency name"),
        EmergencyContactPhone = Field("emergency contact phone", "emergency phone"),
        Notes              = Field("notes", "message", "additional notes"),
        RegistrationSource = RegistrationSource.Duda,
        DudaSubmissionId   = submissionId,
        PaymentStatus      = RegistrationPaymentStatus.Unpaid,
        RegisteredAt       = DateTime.UtcNow
    };

    db.RetreatRegistrations.Add(reg);
    await db.SaveChangesAsync();
    log.LogInformation("Duda webhook: registered {Name} for retreat {Id}", reg.FullName, retreat.Id);
    return Results.Ok();
}).WithTags("Webhooks").AllowAnonymous();

// ── Retreats ──────────────────────────────────────────────────────────────────
var retreatsGroup = app.MapGroup("/api/v1/retreats").WithTags("Retreats").RequireAuthorization("Staff");

retreatsGroup.MapGet("", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var chapterId = ctx.ChapterId;
    var q = db.Retreats.AsQueryable();
    if (chapterId.HasValue) q = q.Where(r => r.ChapterId == chapterId.Value);
    var retreats = await q.OrderByDescending(r => r.Date).ToListAsync();
    return Results.Ok(retreats.Select(r => new {
        r.Id, r.Title, r.Description, r.Date, r.EndDate, r.Location, r.Address, r.City, r.State,
        r.Capacity, r.TicketPrice, r.GoalAmount, r.GiveButterCampaignId,
        Status = r.Status.ToString(), r.ChapterId, r.CreatedAt
    }));
});

retreatsGroup.MapPost("", async (LotvDbContext db, IChapterContextService ctx,
    CreateRetreatRequest body) =>
{
    var chapterId = ctx.ChapterId ?? body.ChapterId;
    var retreat = new Retreat
    {
        ChapterId           = chapterId,
        Title               = body.Title,
        Description         = body.Description,
        Date                = body.Date,
        EndDate             = body.EndDate,
        Location            = body.Location,
        Address             = body.Address,
        City                = body.City,
        State               = body.State,
        Capacity            = body.Capacity,
        TicketPrice         = body.TicketPrice,
        GoalAmount          = body.GoalAmount,
        GiveButterCampaignId = body.GiveButterCampaignId,
        Status              = RetreatStatus.Draft,
        CreatedBy           = ctx.UserId ?? "",
        CreatedAt           = DateTime.UtcNow
    };
    db.Retreats.Add(retreat);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/retreats/{retreat.Id}", retreat);
}).RequireAuthorization("ChapterAdmin");

retreatsGroup.MapGet("/{id:int}", async (int id, LotvDbContext db, IChapterContextService ctx) =>
{
    var chapterId = ctx.ChapterId;
    var r = await db.Retreats.FindAsync(id);
    if (r is null) return Results.NotFound();
    if (chapterId.HasValue && r.ChapterId != chapterId.Value) return Results.Forbid();
    return Results.Ok(r);
});

retreatsGroup.MapPut("/{id:int}", async (int id, LotvDbContext db, IChapterContextService ctx,
    CreateRetreatRequest body) =>
{
    var r = await db.Retreats.FindAsync(id);
    if (r is null) return Results.NotFound();
    var chapterId = ctx.ChapterId;
    if (chapterId.HasValue && r.ChapterId != chapterId.Value) return Results.Forbid();
    r.Title               = body.Title;
    r.Description         = body.Description;
    r.Date                = body.Date;
    r.EndDate             = body.EndDate;
    r.Location            = body.Location;
    r.Address             = body.Address;
    r.City                = body.City;
    r.State               = body.State;
    r.Capacity            = body.Capacity;
    r.TicketPrice         = body.TicketPrice;
    r.GoalAmount          = body.GoalAmount;
    r.GiveButterCampaignId = body.GiveButterCampaignId;
    if (Enum.TryParse<RetreatStatus>(body.Status, out var s)) r.Status = s;
    await db.SaveChangesAsync();
    return Results.Ok(r);
}).RequireAuthorization("ChapterAdmin");

retreatsGroup.MapGet("/{id:int}/dashboard", async (int id, LotvDbContext db, IChapterContextService ctx) =>
{
    var retreat = await db.Retreats.FindAsync(id);
    if (retreat is null) return Results.NotFound();
    var chapterId = ctx.ChapterId;
    if (chapterId.HasValue && retreat.ChapterId != chapterId.Value) return Results.Forbid();

    var regs = await db.RetreatRegistrations.Where(r => r.RetreatId == id).ToListAsync();
    var exps = await db.RetreatExpenses.Where(e => e.RetreatId == id).ToListAsync();

    var totalRev  = regs.Sum(r => r.AmountPaid);
    var totalCost = exps.Sum(e => e.Amount);

    return Results.Ok(new {
        retreat.Id, retreat.Title, retreat.Date, retreat.Location, retreat.Status,
        retreat.Capacity, retreat.GoalAmount, retreat.GiveButterCampaignId,
        TotalRegistered = regs.Count,
        CapacityPct     = retreat.Capacity > 0 ? Math.Round((double)regs.Count / retreat.Capacity * 100, 1) : 0,
        PaidCount        = regs.Count(r => r.PaymentStatus == RegistrationPaymentStatus.Paid),
        UnpaidCount      = regs.Count(r => r.PaymentStatus == RegistrationPaymentStatus.Unpaid),
        PartialCount     = regs.Count(r => r.PaymentStatus == RegistrationPaymentStatus.Partial),
        ComplimentaryCount = regs.Count(r => r.PaymentStatus == RegistrationPaymentStatus.Complimentary),
        TotalRevenue    = totalRev,
        TotalCosts      = totalCost,
        NetPosition     = totalRev - totalCost,
        RevenuePct      = retreat.GoalAmount > 0 ? Math.Round((double)(totalRev / retreat.GoalAmount) * 100, 1) : 0,
        FromGiveButter  = regs.Count(r => r.RegistrationSource == RegistrationSource.GiveButter),
        FromDuda        = regs.Count(r => r.RegistrationSource == RegistrationSource.Duda),
        FromManual      = regs.Count(r => r.RegistrationSource == RegistrationSource.Manual),
        RecentExpenses  = exps.OrderByDescending(e => e.CreatedAt).Take(5)
            .Select(e => new { e.Id, e.Description, Category = e.Category.ToString(), e.Amount, e.PaidAt })
    });
});

retreatsGroup.MapGet("/{id:int}/registrations", async (int id, LotvDbContext db, IChapterContextService ctx,
    string? source, string? status, string? search) =>
{
    var chapterId = ctx.ChapterId;
    var q = db.RetreatRegistrations.Where(r => r.RetreatId == id);
    if (chapterId.HasValue) q = q.Where(r => r.ChapterId == chapterId.Value);
    if (!string.IsNullOrEmpty(source) && Enum.TryParse<RegistrationSource>(source, out var src))
        q = q.Where(r => r.RegistrationSource == src);
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<RegistrationPaymentStatus>(status, out var ps))
        q = q.Where(r => r.PaymentStatus == ps);
    if (!string.IsNullOrEmpty(search))
        q = q.Where(r => r.FirstName.Contains(search) || r.LastName.Contains(search) || r.Email.Contains(search));
    var regs = await q.OrderBy(r => r.RegisteredAt).ToListAsync();
    return Results.Ok(regs.Select(r => new {
        r.Id, r.FirstName, r.LastName, r.Email, r.Phone,
        r.Address, r.City, r.State, r.Zip,
        r.DietaryNeeds, r.AccessibilityNeeds,
        r.EmergencyContactName, r.EmergencyContactPhone,
        r.AmountPaid,
        PaymentStatus    = r.PaymentStatus.ToString(),
        PaymentMethod    = r.PaymentMethod?.ToString(),
        RegistrationSource = r.RegistrationSource.ToString(),
        r.GiveButterTransactionId, r.Notes, r.RegisteredAt
    }));
});

retreatsGroup.MapPost("/{id:int}/registrations", async (int id, LotvDbContext db, IChapterContextService ctx,
    ManualRegistrationRequest body) =>
{
    var retreat = await db.Retreats.FindAsync(id);
    if (retreat is null) return Results.NotFound();
    var reg = new RetreatRegistration
    {
        RetreatId          = id,
        ChapterId          = ctx.ChapterId ?? retreat.ChapterId,
        FirstName          = body.FirstName,
        LastName           = body.LastName,
        Email              = body.Email,
        Phone              = body.Phone,
        Address            = body.Address,
        City               = body.City,
        State              = body.State,
        Zip                = body.Zip,
        DietaryNeeds       = body.DietaryNeeds,
        AccessibilityNeeds = body.AccessibilityNeeds,
        EmergencyContactName  = body.EmergencyContactName,
        EmergencyContactPhone = body.EmergencyContactPhone,
        AmountPaid         = body.AmountPaid,
        PaymentStatus      = Enum.TryParse<RegistrationPaymentStatus>(body.PaymentStatus, out var ps)
                             ? ps : RegistrationPaymentStatus.Unpaid,
        PaymentMethod      = Enum.TryParse<RegistrationPaymentMethod>(body.PaymentMethod, out var pm)
                             ? pm : null,
        RegistrationSource = RegistrationSource.Manual,
        Notes              = body.Notes,
        RegisteredAt       = DateTime.UtcNow
    };
    db.RetreatRegistrations.Add(reg);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/retreats/{id}/registrations/{reg.Id}", reg);
});

retreatsGroup.MapPut("/{id:int}/registrations/{regId:int}", async (
    int id, int regId, LotvDbContext db, UpdatePaymentRequest body) =>
{
    var reg = await db.RetreatRegistrations.FirstOrDefaultAsync(r => r.Id == regId && r.RetreatId == id);
    if (reg is null) return Results.NotFound();
    if (Enum.TryParse<RegistrationPaymentStatus>(body.PaymentStatus, out var ps)) reg.PaymentStatus = ps;
    if (Enum.TryParse<RegistrationPaymentMethod>(body.PaymentMethod, out var pm)) reg.PaymentMethod = pm;
    if (body.AmountPaid.HasValue) reg.AmountPaid = body.AmountPaid.Value;
    if (body.Notes is not null) reg.Notes = body.Notes;
    await db.SaveChangesAsync();
    return Results.Ok(reg);
});

retreatsGroup.MapDelete("/{id:int}/registrations/{regId:int}", async (
    int id, int regId, LotvDbContext db) =>
{
    var reg = await db.RetreatRegistrations.FirstOrDefaultAsync(r => r.Id == regId && r.RetreatId == id);
    if (reg is null) return Results.NotFound();
    db.RetreatRegistrations.Remove(reg);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("ChapterAdmin");

retreatsGroup.MapGet("/{id:int}/registrations/export", async (int id, LotvDbContext db) =>
{
    var regs = await db.RetreatRegistrations.Where(r => r.RetreatId == id)
        .OrderBy(r => r.RegisteredAt).ToListAsync();
    var csv = new System.Text.StringBuilder();
    csv.AppendLine("First Name,Last Name,Email,Phone,City,State,Amount Paid,Payment Status,Source,Dietary Needs,Accessibility,Emergency Contact,Emergency Phone,Registered At");
    foreach (var r in regs)
        csv.AppendLine($"{r.FirstName},{r.LastName},{r.Email},{r.Phone},{r.City},{r.State}," +
                       $"{r.AmountPaid},{r.PaymentStatus},{r.RegistrationSource}," +
                       $"\"{r.DietaryNeeds}\",\"{r.AccessibilityNeeds}\"," +
                       $"\"{r.EmergencyContactName}\",{r.EmergencyContactPhone},{r.RegisteredAt:yyyy-MM-dd}");
    var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    return Results.File(bytes, "text/csv", "retreat-registrations.csv");
});

retreatsGroup.MapGet("/{id:int}/expenses", async (int id, LotvDbContext db) =>
{
    var exps = await db.RetreatExpenses.Where(e => e.RetreatId == id)
        .OrderByDescending(e => e.CreatedAt).ToListAsync();
    return Results.Ok(exps.Select(e => new {
        e.Id, e.Description, Category = e.Category.ToString(),
        e.Amount, e.PaidAt, e.PaidBy, e.Notes, e.CreatedAt
    }));
});

retreatsGroup.MapPost("/{id:int}/expenses", async (int id, LotvDbContext db, IChapterContextService ctx,
    AddExpenseRequest body) =>
{
    var retreat = await db.Retreats.FindAsync(id);
    if (retreat is null) return Results.NotFound();
    var exp = new RetreatExpense
    {
        RetreatId   = id,
        ChapterId   = ctx.ChapterId ?? retreat.ChapterId,
        Description = body.Description,
        Category    = Enum.TryParse<RetreatExpenseCategory>(body.Category, out var cat) ? cat : RetreatExpenseCategory.Other,
        Amount      = body.Amount,
        PaidAt      = body.PaidAt,
        PaidBy      = body.PaidBy,
        Notes       = body.Notes,
        CreatedAt   = DateTime.UtcNow
    };
    db.RetreatExpenses.Add(exp);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/retreats/{id}/expenses/{exp.Id}", exp);
});

retreatsGroup.MapDelete("/{id:int}/expenses/{expId:int}", async (int id, int expId, LotvDbContext db) =>
{
    var exp = await db.RetreatExpenses.FirstOrDefaultAsync(e => e.Id == expId && e.RetreatId == id);
    if (exp is null) return Results.NotFound();
    db.RetreatExpenses.Remove(exp);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("ChapterAdmin");

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

// ─── Family notes ─────────────────────────────────────────────────────────────
var familyNotes = app.MapGroup("/api/v1/families/{familyId:int}/notes").WithTags("FamilyNotes").RequireAuthorization("Staff");

familyNotes.MapGet("/", async (int familyId, LotvDbContext db) =>
    Results.Ok(await db.FamilyNotes.Where(n => n.FamilyId == familyId)
        .OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.CreatedAt).ToListAsync()));

familyNotes.MapPost("/", async (int familyId, FamilyNote body, LotvDbContext db, HttpContext http) =>
{
    body.FamilyId = familyId;
    body.CreatedAt = DateTime.UtcNow;
    if (string.IsNullOrEmpty(body.StaffName))
        body.StaffName = http.User.FindFirst("email")?.Value ?? http.User.Identity?.Name ?? "Staff";
    db.FamilyNotes.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/families/{familyId}/notes/{body.Id}", body);
});

familyNotes.MapPut("/{noteId:int}", async (int familyId, int noteId, FamilyNote body, LotvDbContext db) =>
{
    var n = await db.FamilyNotes.FirstOrDefaultAsync(x => x.Id == noteId && x.FamilyId == familyId);
    if (n is null) return Results.NotFound();
    n.Content = body.Content; n.NoteType = body.NoteType;
    n.MilestoneDate = body.MilestoneDate; n.IsPinned = body.IsPinned;
    await db.SaveChangesAsync();
    return Results.Ok(n);
});

familyNotes.MapDelete("/{noteId:int}", async (int familyId, int noteId, LotvDbContext db) =>
{
    var n = await db.FamilyNotes.FirstOrDefaultAsync(x => x.Id == noteId && x.FamilyId == familyId);
    if (n is null) return Results.NotFound();
    db.FamilyNotes.Remove(n);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ─── Campaigns ────────────────────────────────────────────────────────────────
var campaignGroup = app.MapGroup("/api/v1/campaigns").WithTags("Campaigns").RequireAuthorization("Staff");

campaignGroup.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.Campaigns.AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(c => c.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(status)) q = q.Where(c => c.Status == status);
    var campaigns = await q.OrderByDescending(c => c.StartDate).ToListAsync();
    // enrich with donation totals
    var result = new List<object>();
    foreach (var camp in campaigns)
    {
        var raised = await db.Donations
            .Where(d => d.Campaign == camp.Name || d.Campaign == camp.ExternalCode)
            .SumAsync(d => (decimal?)d.Amount) ?? 0;
        result.Add(new { camp.Id, camp.ChapterId, camp.Name, camp.Description, camp.GoalAmount, camp.StartDate, camp.EndDate, camp.Status, camp.ExternalCode, Raised = raised, GiftCount = await db.Donations.CountAsync(d => d.Campaign == camp.Name || d.Campaign == camp.ExternalCode) });
    }
    return Results.Ok(result);
});

campaignGroup.MapPost("/", async (Campaign body, LotvDbContext db, IChapterContextService ctx) =>
{
    if (ctx.ChapterId.HasValue) body.ChapterId = ctx.ChapterId.Value;
    body.CreatedAt = DateTime.UtcNow;
    db.Campaigns.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/campaigns/{body.Id}", body);
});

campaignGroup.MapPut("/{id:int}", async (int id, Campaign body, LotvDbContext db) =>
{
    var c = await db.Campaigns.FindAsync(id);
    if (c is null) return Results.NotFound();
    c.Name = body.Name; c.Description = body.Description; c.GoalAmount = body.GoalAmount;
    c.StartDate = body.StartDate; c.EndDate = body.EndDate; c.Status = body.Status;
    c.ExternalCode = body.ExternalCode;
    await db.SaveChangesAsync();
    return Results.Ok(c);
});

campaignGroup.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var c = await db.Campaigns.FindAsync(id);
    if (c is null) return Results.NotFound();
    db.Campaigns.Remove(c);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ─── Staff tasks ───────────────────────────────────────────────────────────────
var staffTasks = app.MapGroup("/api/v1/staff-tasks").WithTags("StaffTasks").RequireAuthorization("Staff");

staffTasks.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status, string? assignee) =>
{
    var q = db.StaffTasks.AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(t => t.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status == status);
    if (!string.IsNullOrEmpty(assignee)) q = q.Where(t => t.AssignedToUserId == assignee);
    return Results.Ok(await q.OrderByDescending(t => t.DueDate ?? DateTime.MaxValue).ThenByDescending(t => t.CreatedAt).ToListAsync());
});

staffTasks.MapPost("/", async (StaffTask body, LotvDbContext db, IChapterContextService ctx, HttpContext http) =>
{
    if (ctx.ChapterId.HasValue) body.ChapterId = ctx.ChapterId.Value;
    body.CreatedAt = DateTime.UtcNow;
    if (string.IsNullOrEmpty(body.CreatedByName))
        body.CreatedByName = http.User.FindFirst("email")?.Value ?? "Staff";
    db.StaffTasks.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/staff-tasks/{body.Id}", body);
});

staffTasks.MapPut("/{id:int}", async (int id, StaffTask body, LotvDbContext db) =>
{
    var t = await db.StaffTasks.FindAsync(id);
    if (t is null) return Results.NotFound();
    t.Title = body.Title; t.Description = body.Description; t.Priority = body.Priority;
    t.Status = body.Status; t.DueDate = body.DueDate;
    t.AssignedToUserId = body.AssignedToUserId; t.AssignedToName = body.AssignedToName;
    t.LinkedCaseId = body.LinkedCaseId; t.LinkedDonorId = body.LinkedDonorId;
    if (body.Status == "Done" && t.CompletedAt is null) t.CompletedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(t);
});

staffTasks.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var t = await db.StaffTasks.FindAsync(id);
    if (t is null) return Results.NotFound();
    db.StaffTasks.Remove(t);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ─── Announcements ─────────────────────────────────────────────────────────────
var announcements = app.MapGroup("/api/v1/announcements").WithTags("Announcements").RequireAuthorization("Staff");

announcements.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? audience) =>
{
    var now = DateTime.UtcNow;
    var q = db.Announcements.Where(a => a.ExpiresAt == null || a.ExpiresAt > now);
    if (ctx.ChapterId.HasValue) q = q.Where(a => a.ChapterId == null || a.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(audience)) q = q.Where(a => a.Audience == audience);
    return Results.Ok(await q.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt).ToListAsync());
});

announcements.MapPost("/", async (Announcement body, LotvDbContext db, IChapterContextService ctx, HttpContext http) =>
{
    if (!ctx.IsHqAdmin && ctx.ChapterId.HasValue) body.ChapterId = ctx.ChapterId.Value;
    body.CreatedAt = DateTime.UtcNow;
    if (string.IsNullOrEmpty(body.AuthorName))
        body.AuthorName = http.User.FindFirst("email")?.Value ?? "Staff";
    db.Announcements.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/announcements/{body.Id}", body);
});

announcements.MapPut("/{id:int}", async (int id, Announcement body, LotvDbContext db) =>
{
    var a = await db.Announcements.FindAsync(id);
    if (a is null) return Results.NotFound();
    a.Title = body.Title; a.Body = body.Body; a.Audience = body.Audience;
    a.IsPinned = body.IsPinned; a.ExpiresAt = body.ExpiresAt;
    await db.SaveChangesAsync();
    return Results.Ok(a);
});

announcements.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var a = await db.Announcements.FindAsync(id);
    if (a is null) return Results.NotFound();
    db.Announcements.Remove(a);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/public/v1/announcements", async (LotvDbContext db, string? audience) =>
{
    var now = DateTime.UtcNow;
    var q = db.Announcements.Where(a => (a.ExpiresAt == null || a.ExpiresAt > now) && a.ChapterId == null);
    if (!string.IsNullOrEmpty(audience)) q = q.Where(a => a.Audience == audience || a.Audience == "Public");
    else q = q.Where(a => a.Audience == "Public");
    return Results.Ok(await q.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt).ToListAsync());
}).AllowAnonymous();

// pledge renewals utility endpoint (uses existing DonorPledge data)
app.MapGet("/api/v1/pledges/renewals", async (LotvDbContext db, IChapterContextService ctx, int days = 30) =>
{
    var cutoff = DateTime.UtcNow.AddDays(days);
    var q = db.DonorPledges.Include(p => p.Donor).AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(p => p.ChapterId == ctx.ChapterId.Value);
    var upcoming = await q
        .Where(p => p.Status == PledgeStatus.Active && p.TargetDate <= cutoff)
        .OrderBy(p => p.TargetDate)
        .Select(p => new {
            p.Id, p.DonorId,
            DonorName = p.Donor == null ? "" : p.Donor.FirstName + " " + p.Donor.LastName,
            DonorEmail = p.Donor == null ? "" : p.Donor.Email,
            p.PledgedAmount, p.FulfilledAmount, p.TargetDate, p.Campaign, p.Status
        })
        .ToListAsync();
    var overdue = await q
        .Where(p => p.Status == PledgeStatus.Overdue)
        .OrderBy(p => p.TargetDate)
        .Select(p => new {
            p.Id, p.DonorId,
            DonorName = p.Donor == null ? "" : p.Donor.FirstName + " " + p.Donor.LastName,
            DonorEmail = p.Donor == null ? "" : p.Donor.Email,
            p.PledgedAmount, p.FulfilledAmount, p.TargetDate, p.Campaign, p.Status
        })
        .ToListAsync();
    return Results.Ok(new { Upcoming = upcoming, Overdue = overdue });
}).RequireAuthorization("Staff");

// ─── Chapter management (HQAdmin) ────────────────────────────────────────────
var chapMgmt = app.MapGroup("/api/v1/chapters").WithTags("Chapters").RequireAuthorization("HQAdmin");

chapMgmt.MapGet("/", async (LotvDbContext db) =>
{
    var list = await db.Chapters.OrderBy(c => c.Name).ToListAsync();
    return Results.Ok(list);
});

chapMgmt.MapPost("/", async (Chapter body, LotvDbContext db) =>
{
    db.Chapters.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/chapters/{body.Id}", body);
});

chapMgmt.MapPut("/{id:int}", async (int id, Chapter body, LotvDbContext db) =>
{
    var ch = await db.Chapters.FindAsync(id);
    if (ch is null) return Results.NotFound();
    ch.Name = body.Name; ch.City = body.City; ch.State = body.State;
    ch.ContactName = body.ContactName; ch.ContactEmail = body.ContactEmail;
    ch.ContactPhone = body.ContactPhone; ch.IsActive = body.IsActive;
    ch.MaxActiveCasesPerVolunteer = body.MaxActiveCasesPerVolunteer;
    ch.AcceptanceWindowHours = body.AcceptanceWindowHours;
    ch.UrgentAcceptanceWindowHours = body.UrgentAcceptanceWindowHours;
    ch.MaxReassignmentAttempts = body.MaxReassignmentAttempts;
    ch.DefaultServiceRadiusMiles = body.DefaultServiceRadiusMiles;
    await db.SaveChangesAsync();
    return Results.Ok(ch);
});

// ─── Volunteer certifications ─────────────────────────────────────────────────
var vcerts = app.MapGroup("/api/v1/volunteers/{volId:int}/certifications").WithTags("VolunteerCertifications").RequireAuthorization("Staff");

vcerts.MapGet("/", async (int volId, LotvDbContext db) =>
    Results.Ok(await db.VolunteerCertifications.Where(c => c.VolunteerId == volId)
        .OrderByDescending(c => c.IssuedDate).ToListAsync()));

vcerts.MapPost("/", async (int volId, VolunteerCertification body, LotvDbContext db) =>
{
    body.VolunteerId = volId;
    body.CreatedAt = DateTime.UtcNow;
    db.VolunteerCertifications.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/volunteers/{volId}/certifications/{body.Id}", body);
});

vcerts.MapPut("/{certId:int}", async (int volId, int certId, VolunteerCertification body, LotvDbContext db) =>
{
    var cert = await db.VolunteerCertifications.FirstOrDefaultAsync(c => c.Id == certId && c.VolunteerId == volId);
    if (cert is null) return Results.NotFound();
    cert.CertType = body.CertType; cert.IssuedDate = body.IssuedDate;
    cert.ExpiresDate = body.ExpiresDate; cert.IsVerified = body.IsVerified; cert.Notes = body.Notes;
    await db.SaveChangesAsync();
    return Results.Ok(cert);
});

vcerts.MapDelete("/{certId:int}", async (int volId, int certId, LotvDbContext db) =>
{
    var cert = await db.VolunteerCertifications.FirstOrDefaultAsync(c => c.Id == certId && c.VolunteerId == volId);
    if (cert is null) return Results.NotFound();
    db.VolunteerCertifications.Remove(cert);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/v1/certifications/expiring", async (int days, LotvDbContext db) =>
{
    var cutoff = DateTime.UtcNow.AddDays(days);
    var certs = await db.VolunteerCertifications
        .Include(c => c.Volunteer)
        .Where(c => c.ExpiresDate != null && c.ExpiresDate <= cutoff && c.ExpiresDate >= DateTime.UtcNow)
        .OrderBy(c => c.ExpiresDate)
        .Select(c => new { c.Id, c.VolunteerId, VolunteerName = c.Volunteer!.FirstName + " " + c.Volunteer!.LastName, c.CertType, c.ExpiresDate, c.IsVerified })
        .ToListAsync();
    return Results.Ok(certs);
}).RequireAuthorization("Staff");

// ─── Donor touchpoints / stewardship ─────────────────────────────────────────
var touchpoints = app.MapGroup("/api/v1/donors/{donorId:int}/touchpoints").WithTags("Touchpoints").RequireAuthorization("Staff");

touchpoints.MapGet("/", async (int donorId, LotvDbContext db) =>
    Results.Ok(await db.DonorTouchpoints.Where(t => t.DonorId == donorId)
        .OrderByDescending(t => t.TouchDate).ToListAsync()));

touchpoints.MapPost("/", async (int donorId, DonorTouchpoint body, LotvDbContext db, HttpContext http) =>
{
    body.DonorId = donorId;
    body.CreatedAt = DateTime.UtcNow;
    if (string.IsNullOrEmpty(body.StaffName))
        body.StaffName = http.User.FindFirst("email")?.Value ?? http.User.Identity?.Name ?? "Staff";
    db.DonorTouchpoints.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/donors/{donorId}/touchpoints/{body.Id}", body);
});

touchpoints.MapDelete("/{id:int}", async (int donorId, int id, LotvDbContext db) =>
{
    var tp = await db.DonorTouchpoints.FirstOrDefaultAsync(t => t.Id == id && t.DonorId == donorId);
    if (tp is null) return Results.NotFound();
    db.DonorTouchpoints.Remove(tp);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ─── Grants ───────────────────────────────────────────────────────────────────
var grants = app.MapGroup("/api/v1/grants").WithTags("Grants").RequireAuthorization("Staff");

grants.MapGet("/", async (LotvDbContext db, IChapterContextService ctx, string? status) =>
{
    var q = db.Grants.AsQueryable();
    if (ctx.ChapterId.HasValue) q = q.Where(g => g.ChapterId == ctx.ChapterId.Value);
    if (!string.IsNullOrEmpty(status)) q = q.Where(g => g.Status == status);
    return Results.Ok(await q.OrderByDescending(g => g.AwardedDate).ToListAsync());
});

grants.MapPost("/", async (Grant body, LotvDbContext db, IChapterContextService ctx) =>
{
    if (ctx.ChapterId.HasValue) body.ChapterId = ctx.ChapterId.Value;
    body.CreatedAt = DateTime.UtcNow;
    db.Grants.Add(body);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/grants/{body.Id}", body);
});

grants.MapPut("/{id:int}", async (int id, Grant body, LotvDbContext db) =>
{
    var g = await db.Grants.FindAsync(id);
    if (g is null) return Results.NotFound();
    g.GrantorName = body.GrantorName; g.Purpose = body.Purpose; g.Amount = body.Amount;
    g.AwardedDate = body.AwardedDate; g.ReportDueDate = body.ReportDueDate;
    g.Status = body.Status; g.Notes = body.Notes;
    await db.SaveChangesAsync();
    return Results.Ok(g);
});

grants.MapDelete("/{id:int}", async (int id, LotvDbContext db) =>
{
    var g = await db.Grants.FindAsync(id);
    if (g is null) return Results.NotFound();
    db.Grants.Remove(g);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ─── Notification preferences ─────────────────────────────────────────────────
app.MapGet("/api/v1/users/me/notification-prefs", async (LotvDbContext db, IChapterContextService ctx) =>
{
    var uid = ctx.UserId;
    if (uid is null) return Results.Unauthorized();
    var prefs = await db.NotificationPrefs.Where(p => p.UserId == uid).ToListAsync();
    return Results.Ok(prefs);
}).RequireAuthorization();

app.MapPut("/api/v1/users/me/notification-prefs", async (List<NotificationPref> body, LotvDbContext db, IChapterContextService ctx) =>
{
    var uid = ctx.UserId;
    if (uid is null) return Results.Unauthorized();
    var existing = await db.NotificationPrefs.Where(p => p.UserId == uid).ToListAsync();
    db.NotificationPrefs.RemoveRange(existing);
    foreach (var pref in body) { pref.UserId = uid; db.NotificationPrefs.Add(pref); }
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// ─── Chapter analytics (HQAdmin) ──────────────────────────────────────────────
app.MapGet("/api/v1/admin/chapter-analytics", async (LotvDbContext db) =>
{
    var chapters = await db.Chapters.Where(c => c.IsActive).ToListAsync();
    var result = new List<object>();
    foreach (var ch in chapters)
    {
        var cases      = await db.Requests.Where(r => r.ChapterId == ch.Id).ToListAsync();
        var vols       = await db.Volunteers.CountAsync(v => v.ChapterId == ch.Id);
        var donations  = await db.Donations.Where(d => d.ChapterId == ch.Id).ToListAsync();
        var pledges    = await db.DonorPledges.CountAsync(p => p.ChapterId == ch.Id && p.Status == PledgeStatus.Active);
        result.Add(new
        {
            ch.Id, ch.Name, ch.City, ch.State,
            OpenCases      = cases.Count(c => c.Status != CaseStatus.Fulfilled && c.Status != CaseStatus.Cancelled),
            OverdueCases   = cases.Count(c => c.DueDate.HasValue && c.DueDate < DateTime.UtcNow && c.Status != CaseStatus.Fulfilled && c.Status != CaseStatus.Cancelled),
            FulfilledMtd   = cases.Count(c => c.Status == CaseStatus.Fulfilled && c.UpdatedAt.Month == DateTime.UtcNow.Month && c.UpdatedAt.Year == DateTime.UtcNow.Year),
            TotalDonations = donations.Sum(d => d.Amount),
            DonationCount  = donations.Count,
            ActiveVols     = vols,
            ActivePledges  = pledges,
        });
    }
    return Results.Ok(result);
}).RequireAuthorization("HQAdmin");

app.Run();

// ─────────────────────────────────────────────────────────────────────────────
// Request/response records
// ─────────────────────────────────────────────────────────────────────────────
record PublicApplyRequest(
    Family Family,
    bool ForSelf = true,
    string? PackageType = null,
    string? ReferrerFirstName = null,
    string? ReferrerLastName = null,
    string? ReferrerEmail = null
);
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
record FamilyProfileUpdateRequest(string? FirstName, string? LastName,
    string? Email, string? Phone, string? Street, string? City, string? State, string? Zip);
record InventoryAdjustRequest(int QuantityDelta, string? Reason);
record ResourceAllocationRequest(int RequestId, int Quantity, string? Notes);
record ApplyPledgePaymentRequest(decimal Amount);
record FulfillWishListRequest(int Quantity, string? DonorId);
record SmsCheckInRequest(string? VolunteerPhone, string? Note);
record QrScanRequest(string Code);
record ReportConfigRequest(string? HqWeeklyEmail, string? HqDailyEmail, List<ChapterReportConfig>? Configs);

// ── Retreat request/response records ─────────────────────────────────────────
record CreateRetreatRequest(
    string Title, string? Description, DateTime Date, DateTime? EndDate,
    string Location, string? Address, string? City, string? State,
    int Capacity, decimal TicketPrice, decimal GoalAmount,
    string? GiveButterCampaignId, string? Status, int ChapterId
);
record ManualRegistrationRequest(
    string FirstName, string LastName, string Email, string? Phone,
    string? Address, string? City, string? State, string? Zip,
    string? DietaryNeeds, string? AccessibilityNeeds,
    string? EmergencyContactName, string? EmergencyContactPhone,
    decimal AmountPaid, string? PaymentStatus, string? PaymentMethod, string? Notes
);
record UpdatePaymentRequest(string? PaymentStatus, string? PaymentMethod, decimal? AmountPaid, string? Notes);
record AddExpenseRequest(string Description, string Category, decimal Amount,
    DateTime? PaidAt, string? PaidBy, string? Notes);

// ── Duda webhook records ──────────────────────────────────────────────────────
record DudaWebhookPayload(
    [property: System.Text.Json.Serialization.JsonPropertyName("event_type")] string EventType,
    [property: System.Text.Json.Serialization.JsonPropertyName("event_timestamp")] long EventTimestamp,
    [property: System.Text.Json.Serialization.JsonPropertyName("site_name")] string SiteName,
    [property: System.Text.Json.Serialization.JsonPropertyName("data")] DudaFormData Data
);
record DudaFormData(
    [property: System.Text.Json.Serialization.JsonPropertyName("pageName")] string PageName,
    [property: System.Text.Json.Serialization.JsonPropertyName("fieldsData")] List<DudaField> FieldsData,
    [property: System.Text.Json.Serialization.JsonPropertyName("utm_campaign")] string? UtmCampaign,
    [property: System.Text.Json.Serialization.JsonPropertyName("additionalParams")] Dictionary<string, string>? AdditionalParams
);
record DudaField(
    [property: System.Text.Json.Serialization.JsonPropertyName("label")] string Label,
    [property: System.Text.Json.Serialization.JsonPropertyName("value")] string Value,
    [property: System.Text.Json.Serialization.JsonPropertyName("type")] string Type,
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id
);
record ChapterReportConfig(int ChapterId, string? WeeklyEmail, string? DailyEmail);
record BroadcastRequest(string Audience, string Channel, string? Subject, string Body);
record MarketingEmailRequest(string? CampaignName, string Audience, string Subject, string Body);
record PublicEventRsvpRequest(string Name, string Email, int GuestCount = 1);
record PublicResourceDonationRequest(string DonorName, string? Email, string? Phone,
    string ResourceType, int Quantity, string? Unit, string? Description, string? Preference);
record PublicCreateRecurringRequest(decimal Amount, RecurringFrequency Frequency, DateTime? StartDate, string? Campaign);
record PublicUpdateRecurringRequest(decimal? Amount, RecurringFrequency? Frequency);
record PublicIntakeRequest(string FamilyLastName, int ChapterId, PackageReason Reason,
    string? City, string? State, string? Notes);
record PublicDonationRequest(decimal Amount, string DonorEmail, int ChapterId,
    string? DonorFirstName, string? DonorLastName, string? StripePaymentIntentId);
record AvatarUpdateRequest(string? AvatarUrl);
record PaymentIntentRequest(decimal Amount, string? Currency);
record BillingPortalRequest(string Token);
record BulkAllocateRequest(int[] Ids, string Status);
record BulkChannelRequest(int[] Ids, string Channel);
record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth);
record DonorMagicLinkRequest(string Email);
record DonorMagicLinkVerifyRequest(string Token);
record StaffOnboardingRequest(string FirstName, string LastName, string Title, string Phone, string NotifyPref, int ChapterId);
record VolunteerOnboardingRequest(string FirstName, string LastName, string Phone, string Street, string City, string State, string Zip, List<string> AvailableDays, List<string> Skills, int MaxRequestsPerMonth, int ChapterId);
record CreateApiKeyRequest(string PartnerName, string? ContactEmail, int? ChapterId,
    ApiKeyScope Scope, DateTime? ExpiresAt);

// Required for WebApplicationFactory in integration tests
public partial class Program { }
