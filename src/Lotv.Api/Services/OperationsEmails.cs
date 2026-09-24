using Lotv.Api.Data;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>
/// Emails around running the ministry day to day: telling a volunteer about their assignments, reminding the
/// team of bereavement follow-ups that are due, and asking a family to confirm details. Content only;
/// <see cref="OperationsNotifier"/> sends them. Values are HTML-encoded; no reasons or stories are included.
/// </summary>
public static class OperationsEmails
{
    private static string P => RequestEmails.ParagraphStyle;
    private static string E(string? s) => RequestEmails.E(s);

    public record VolunteerCase(int RequestId, string FamilyName, string? Location, string CaseUrl, DateTime? AcceptBy);

    /// <summary>To a volunteer when a request is assigned to them.</summary>
    public static RequestEmails.Email VolunteerAssigned(string volunteerFirstName, VolunteerCase c)
    {
        var body =
            $"<p style=\"{P}\">Hello {E(volunteerFirstName)},</p>" +
            $"<p style=\"{P}\">A Prayer Care Package request has been assigned to you. Thank you for caring for this family.</p>" +
            RequestEmails.Facts(("Family", c.FamilyName), ("Location", c.Location),
                                ("Please accept by", c.AcceptBy is { } d ? d.ToString("MMM d, yyyy h:mm tt") + " UTC" : null)) +
            RequestEmails.Button("Open the request", c.CaseUrl);
        return new($"New assignment — {c.FamilyName}",
            RequestEmails.Wrap("A request was assigned to you", "You have a new assignment", body, RequestEmails.TeamFooter));
    }

    /// <summary>To a volunteer when a request is taken back from them.</summary>
    public static RequestEmails.Email VolunteerUnassigned(string volunteerFirstName, string familyName)
    {
        var body =
            $"<p style=\"{P}\">Hello {E(volunteerFirstName)},</p>" +
            $"<p style=\"{P}\">The request for <strong>{E(familyName)}</strong> is no longer assigned to you. The team has taken it back to reassign, so there is nothing more for you to do on it.</p>" +
            $"<p style=\"{P}\">Thank you for your willingness to help.</p>";
        return new($"Assignment removed — {familyName}",
            RequestEmails.Wrap("A request was taken off your list", "Assignment removed", body, RequestEmails.TeamFooter));
    }

    public record DueItem(string FamilyName, string Milestone, DateTime DueDate, bool Overdue);

    /// <summary>To the team: bereavement follow-up touchpoints that are due soon or just passed.</summary>
    public static RequestEmails.Email BereavementDue(IReadOnlyList<DueItem> items, string pageUrl)
    {
        var rows = string.Concat(items.OrderBy(i => i.DueDate).Select(i =>
            $"<tr><td style=\"padding:8px 14px;border-bottom:1px solid #eef1f6\"><strong>{E(i.FamilyName)}</strong></td>" +
            $"<td style=\"padding:8px 14px;border-bottom:1px solid #eef1f6\">{E(i.Milestone)}</td>" +
            $"<td style=\"padding:8px 14px;border-bottom:1px solid #eef1f6;color:{(i.Overdue ? "#b45309" : "#1f2a3d")}\">{E(i.DueDate.ToString("MMM d, yyyy"))}{(i.Overdue ? " (overdue)" : "")}</td></tr>"));
        var body =
            $"<p style=\"{P}\">{items.Count} bereavement follow-up touchpoint{(items.Count == 1 ? " is" : "s are")} due or just past due and hasn't been marked sent.</p>" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 0 20px;border:1px solid #e3e8f0;border-radius:6px\">" +
            "<tr style=\"background:#f0f7ff;color:#1a4a6b\"><th align=\"left\" style=\"padding:8px 14px\">Family</th><th align=\"left\" style=\"padding:8px 14px\">Touchpoint</th><th align=\"left\" style=\"padding:8px 14px\">Due</th></tr>" +
            rows + "</table>" +
            RequestEmails.Button("Open Bereavement Follow-Up", pageUrl);
        return new($"Bereavement follow-up due ({items.Count})",
            RequestEmails.Wrap("Follow-ups are due", "Bereavement follow-ups are due", body, RequestEmails.TeamFooter));
    }

    /// <summary>To a family: please check we have your details right.</summary>
    public static RequestEmails.Email DetailsRequest(string familyFirstNames, IReadOnlyList<string> whatToCheck, IReadOnlyList<(string Label, string? Value)> onFile)
    {
        var body =
            $"<p style=\"{P}\">Dear {E(familyFirstNames)},</p>" +
            $"<p style=\"{P}\">Thank you for your Prayer Care Package request. So that we can send everything to the right place and reach you with care, could you please look over the details below and reply to let us know if anything needs to be corrected?</p>" +
            (whatToCheck.Count == 0 ? "" :
                $"<p style=\"{P};margin-bottom:6px\">We would especially like to confirm:</p><ul style=\"color:#444;line-height:1.8;margin:0 0 16px\">{string.Concat(whatToCheck.Select(w => $"<li>{E(w)}</li>"))}</ul>") +
            RequestEmails.Facts(onFile.ToArray()) +
            $"<p style=\"{P}\">Just reply to this email with any corrections. If you would rather talk, tell us a good time to call.</p>" +
            $"<p style=\"{P}\">With love and prayers,<br>The Lily of the Valley Ministry team</p>";
        return new("Please confirm your details — Lily of the Valley Ministry",
            RequestEmails.Wrap("Please check your details", "Could you check your details?", body,
                "You are receiving this because a Prayer Care Package was requested for your family."));
    }

    /// <summary>To a family: a Mother's Day or Father's Day card has been mailed to them.</summary>
    public static RequestEmails.Email CardSent(string recipientFirstName, MailingKind kind)
    {
        var holiday = MailingCycle.HolidayName(kind);
        var who = kind == MailingKind.FathersDay ? "a father" : "a mother";
        var body =
            $"<p style=\"{P}\">Dear {E(recipientFirstName)},</p>" +
            $"<p style=\"{P}\">We have mailed you a {E(holiday)} card. We know this day can carry mixed feelings for {who} who has lost a little one, and we wanted you to know you are remembered and loved.</p>" +
            $"<p style=\"{P}\">It should arrive in your mailbox within the next week or two. If it doesn't, or your address has changed, just reply to this email and we will make it right.</p>" +
            $"<p style=\"{P}\">With love and prayers,<br>The Lily of the Valley Ministry team</p>";
        return new($"A {holiday} Card Is on Its Way",
            RequestEmails.Wrap($"A {holiday} card is on its way", $"A {holiday} card is on its way", body,
                "You are receiving this because your family is on the Lily of the Valley Ministry card mailing list."));
    }

    /// <summary>The family-friendly names of the things a data check found wrong.</summary>
    public static List<string> FriendlyFields(IEnumerable<DataIssue> issues)
    {
        var list = new List<string>();
        foreach (var field in issues.Select(i => i.Field).Distinct())
        {
            var label = field.Contains("name", StringComparison.OrdinalIgnoreCase) ? "The spelling of your name"
                : field == "Email" ? "Your email address"
                : field == "Phone" ? "Your phone number"
                : field is "Street address" or "City" or "State" or "Zip" ? "Your mailing address"
                : field == "Date of loss" ? "The date of your loss"
                : null;
            if (label is not null && !list.Contains(label)) list.Add(label);
        }
        return list;
    }
}

/// <summary>Sends the <see cref="OperationsEmails"/>.</summary>
public static class OperationsNotifier
{
    private static string? Location(Family f) =>
        string.Join(", ", new[] { f.City, f.State }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } l ? l : null;

    public static void VolunteerAssigned(INotificationService notify, IConfiguration cfg, Volunteer v, PackageRequest r, Family? f, DateTime? acceptBy)
    {
        if (string.IsNullOrWhiteSpace(v.Email)) return;
        var e = OperationsEmails.VolunteerAssigned(v.FirstName,
            new(r.Id, f?.FullName ?? "a family", f is null ? null : Location(f), RequestNotifier.WebUrl(cfg, $"/admin/cases/{r.Id}"), acceptBy));
        _ = notify.SendEmailAsync(v.Email, v.FullName, e.Subject, e.Html);
    }

    public static void VolunteerUnassigned(INotificationService notify, Volunteer v, Family? f)
    {
        if (string.IsNullOrWhiteSpace(v.Email)) return;
        var e = OperationsEmails.VolunteerUnassigned(v.FirstName, f?.FullName ?? "a family");
        _ = notify.SendEmailAsync(v.Email, v.FullName, e.Subject, e.Html);
    }

    /// <summary>Tells the family a card was mailed. False (and nothing sent) when the entry has no family or the family has no valid email.</summary>
    public static bool CardSent(INotificationService notify, MailingListEntry entry, Family? family)
    {
        if (family is null || !FamilyDataQuality.Check(family).CanEmail) return false;
        var name = (entry.RecipientName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                   ?? (entry.Kind == MailingKind.FathersDay ? family.Parent1FirstName : FamilyParents.MomFirstName(family));
        var e = OperationsEmails.CardSent(name, entry.Kind);
        _ = notify.SendEmailAsync(family.Email, family.FullName, e.Subject, e.Html);
        return true;
    }

    /// <summary>Asks the family to check the details the data check flagged. False when there is nothing to ask or no valid email.</summary>
    public static bool DetailsRequest(INotificationService notify, Family f)
    {
        var report = FamilyDataQuality.Check(f);
        if (!report.NeedsAttention || !report.CanEmail) return false;
        var names = string.IsNullOrWhiteSpace(f.Parent2FirstName) ? f.Parent1FirstName : $"{f.Parent1FirstName} and {f.Parent2FirstName}";
        var address = string.Join(", ", new[] { f.StreetAddress, f.Apt, f.City, f.State, f.Zip }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var e = OperationsEmails.DetailsRequest(names, OperationsEmails.FriendlyFields(report.Issues),
        [
            ("Name", f.FullName), ("Email", f.Email), ("Phone", f.Phone), ("Address", address),
        ]);
        _ = notify.SendEmailAsync(f.Email, f.FullName, e.Subject, e.Html);
        return true;
    }
}

/// <summary>Finds bereavement follow-up touchpoints that are due and emails the team once about each.</summary>
public static class BereavementReminders
{
    /// <summary>How far ahead a touchpoint counts as "due soon".</summary>
    public const int DaysAhead = 7;
    /// <summary>Touchpoints overdue by more than this are left alone, so old records don't flood the first digest.</summary>
    public const int DaysBack = 30;

    public static async Task<int> SendDueAsync(LotvDbContext db, INotificationService notify, IConfiguration cfg, DateTime now)
    {
        var from = now.Date.AddDays(-DaysBack);
        var until = now.Date.AddDays(DaysAhead + 1);
        var due = await db.FollowUpMilestones.Include(m => m.FollowUpTracker)
            .Where(m => !m.BookSent && m.ReminderSentAt == null && m.DueDate != null && m.DueDate >= from && m.DueDate < until
                        && (m.FollowUpTracker == null || m.FollowUpTracker.Email == null || !m.FollowUpTracker.Email.EndsWith(".invalid")))
            .ToListAsync();
        if (due.Count == 0) return 0;

        var items = due.Select(m =>
        {
            var t = m.FollowUpTracker;
            var name = t is null ? "A family" : string.IsNullOrWhiteSpace(t.Parent2Name) ? t.Parent1Name : $"{t.Parent1Name} & {t.Parent2Name}";
            return new OperationsEmails.DueItem(name, m.Type.ToDisplayName(), m.DueDate!.Value, m.DueDate!.Value.Date < now.Date);
        }).ToList();

        var email = OperationsEmails.BereavementDue(items, RequestNotifier.WebUrl(cfg, "/admin/follow-up-trackers"));
        foreach (var to in RequestNotifier.TeamEmails(cfg)) _ = notify.SendEmailAsync(to, "LOTV Team", email.Subject, email.Html);

        foreach (var m in due) m.ReminderSentAt = now;
        await db.SaveChangesAsync();
        return due.Count;
    }
}

/// <summary>Checks for due bereavement follow-ups every few hours and emails the team about new ones.</summary>
public class BereavementReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BereavementReminderBackgroundService> _logger;

    public BereavementReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BereavementReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var count = await BereavementReminders.SendDueAsync(
                    scope.ServiceProvider.GetRequiredService<LotvDbContext>(),
                    scope.ServiceProvider.GetRequiredService<INotificationService>(),
                    scope.ServiceProvider.GetRequiredService<IConfiguration>(), DateTime.UtcNow);
                if (count > 0) _logger.LogInformation("Bereavement reminder sent for {Count} touchpoint(s).", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Bereavement reminder check failed.");
            }
            try { await Task.Delay(TimeSpan.FromHours(6), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
