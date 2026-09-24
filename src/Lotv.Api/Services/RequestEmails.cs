using System.Net;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;

namespace Lotv.Api.Services;

/// <summary>
/// The emails sent as a prayer care package request moves along - to the family (or whoever
/// referred them) and to Whitney and the team. Content only: <see cref="RequestNotifier"/> decides
/// who gets what and when. Every value is HTML-encoded; team emails deliberately leave out the
/// reason for the request and the family's story.
/// </summary>
public static class RequestEmails
{
    public record Email(string Subject, string Html);

    // ── Family / referrer ────────────────────────────────────────────────────

    /// <summary>Sent to whoever submitted the form: the family, or the person who referred them.</summary>
    public static Email Received(string submitterFirstName, bool forSelf, string familyDisplayName)
    {
        var greeting = $"<p style=\"{P}\">Dear {E(submitterFirstName)},</p>";
        var body = forSelf
            ? greeting +
              $"<p style=\"{P}\">Thank you for reaching out to Lily of the Valley Ministry. Your request for a Prayer Care Package has been received, and you and your family are being held in our prayers.</p>" +
              Steps("A member of our team will review your request within 1&ndash;2 business days.",
                    "A volunteer will be assigned to assemble your package with care.",
                    "Your package ships directly to you at no cost. We will email you when it is on its way.")
            : greeting +
              $"<p style=\"{P}\">Thank you for thinking of {E(familyDisplayName)}. Your request for a Prayer Care Package has been received, and they are being held in our prayers.</p>" +
              Steps("A member of our team will review the request within 1&ndash;2 business days.",
                    "We will reach out to the family with care, and a volunteer will assemble their package.",
                    "We will let you know once the package has been delivered.");
        return new("Your Prayer Care Package Request Has Been Received",
            Wrap("We've received your request", "Your request has been received", body,
                 "You are receiving this because a Prayer Care Package was requested through our website."));
    }

    public static Email Shipped(string familyFirstNames, string? trackingNumber)
    {
        var tracking = string.IsNullOrWhiteSpace(trackingNumber) ? "" :
            $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f0f7ff;border-left:4px solid #1a4a6b;border-radius:4px;margin:0 0 20px\"><tr><td style=\"padding:14px 18px;color:#1a4a6b\">Tracking number<br><strong style=\"font-size:17px\">{E(trackingNumber)}</strong></td></tr></table>";
        var body =
            $"<p style=\"{P}\">Dear {E(familyFirstNames)},</p>" +
            $"<p style=\"{P}\">Your Prayer Care Package has shipped and is on its way to you. It was put together by a volunteer who is keeping you close in prayer.</p>" +
            tracking +
            $"<p style=\"{P}\">If it doesn't arrive within a week or two, or anything is not right, just reply to this email and we will take care of it.</p>";
        return new("Your Prayer Care Package Is On Its Way",
            Wrap("Your package is on its way", "Your package is on its way", body,
                 "You are receiving this because a Prayer Care Package was requested for your family."));
    }

    public static Email Completed(string familyFirstNames)
    {
        var body =
            $"<p style=\"{P}\">Dear {E(familyFirstNames)},</p>" +
            $"<p style=\"{P}\">Your Prayer Care Package has been delivered. We hope it brings a measure of comfort in a very hard season, and we want you to know that you and your family are not forgotten.</p>" +
            $"<p style=\"{P}\">Your name stays in our prayers, and our team may reach out from time to time to check in. If you would ever like to talk, need something more, or want to share how your package arrived, simply reply to this email.</p>" +
            $"<p style=\"{P}\">With love and prayers,<br>The Lily of the Valley Ministry team</p>";
        return new("Your Prayer Care Package Has Been Delivered",
            Wrap("Your package has been delivered", "Your package has been delivered", body,
                 "You are receiving this because a Prayer Care Package was requested for your family."));
    }

    /// <summary>Thank-you to the person who referred a family, once the package has been delivered.</summary>
    public static Email ReferrerCompleted(string referrerFirstName, string familyDisplayName)
    {
        var body =
            $"<p style=\"{P}\">Dear {E(referrerFirstName)},</p>" +
            $"<p style=\"{P}\">The Prayer Care Package you requested for {E(familyDisplayName)} has been delivered. Thank you for caring enough to reach out on their behalf &mdash; it made a real difference.</p>" +
            $"<p style=\"{P}\">We are continuing to hold them in prayer, and you are welcome to keep them in yours as well.</p>" +
            $"<p style=\"{P}\">With gratitude,<br>The Lily of the Valley Ministry team</p>";
        return new("The Prayer Care Package You Requested Has Been Delivered",
            Wrap("The package has been delivered", "The package has been delivered", body,
                 "You are receiving this because you requested a Prayer Care Package for a family."));
    }

    // ── Whitney and the team ─────────────────────────────────────────────────

    public record TeamRequest(
        int RequestId, string FamilyName, string? Location, string SubmittedBy, string? AssignedTo,
        string CaseUrl, string? TrackingNumber = null, DateTime? ShippedDate = null, DateTime? RequestedAt = null,
        DateTime? CompletedAt = null, string? DuplicateReason = null);

    public static Email TeamNewRequest(TeamRequest r)
    {
        var assignment = string.IsNullOrWhiteSpace(r.AssignedTo)
            ? "<strong style=\"color:#b45309\">Not assigned yet</strong> &mdash; it is waiting in the Unassigned Queue."
            : $"Assigned to <strong>{E(r.AssignedTo)}</strong>.";
        var body =
            $"<p style=\"{P}\">A new Prayer Care Package request came in.</p>" +
            Facts(("Family", r.FamilyName), ("Location", r.Location), ("Submitted", r.SubmittedBy)) +
            (r.DuplicateReason is null ? "" :
                $"<p style=\"{P};background:#fff7ed;border-left:4px solid #b45309;padding:12px 16px\"><strong>Possible duplicate:</strong> {E(r.DuplicateReason)}<br>It is on hold in Duplicate Review until someone confirms it.</p>") +
            $"<p style=\"{P}\">{assignment}</p>" +
            Button("View this request", r.CaseUrl);
        return new(r.DuplicateReason is null ? "New Prayer Care Package Request" : "New Prayer Care Package Request — Possible Duplicate",
            Wrap("New request", "New prayer care request", body, TeamFooter));
    }

    public static Email TeamShipped(TeamRequest r)
    {
        var body =
            $"<p style=\"{P}\">A Prayer Care Package has shipped.</p>" +
            Facts(("Family", r.FamilyName), ("Location", r.Location), ("Assigned to", r.AssignedTo),
                  ("Tracking number", r.TrackingNumber), ("Shipped", r.ShippedDate?.ToString("MMM d, yyyy"))) +
            $"<p style=\"{P}\">The family has been emailed that it is on its way.</p>" +
            Button("View this request", r.CaseUrl);
        return new($"Package shipped — {r.FamilyName}", Wrap("Package shipped", "Package shipped", body, TeamFooter));
    }

    public static Email TeamCompleted(TeamRequest r)
    {
        string? took = r.RequestedAt is { } from && r.CompletedAt is { } to
            ? $"{Math.Max(0, (int)Math.Round((to - from).TotalDays))} days from request to delivery" : null;
        var body =
            $"<p style=\"{P}\">A Prayer Care Package request has been completed.</p>" +
            Facts(("Family", r.FamilyName), ("Location", r.Location), ("Volunteer", r.AssignedTo), ("Turnaround", took)) +
            $"<p style=\"{P}\">The family has been emailed that their package was delivered" +
            $"{(r.SubmittedBy.StartsWith("Referred by", StringComparison.Ordinal) ? ", and the person who referred them has been thanked" : "")}. " +
            "Bereavement follow-up touchpoints, where they apply, continue on the Bereavement Follow-Up page.</p>" +
            Button("View this request", r.CaseUrl);
        return new($"Package completed — {r.FamilyName}", Wrap("Package completed", "Package completed", body, TeamFooter));
    }

    // ── Sample emails for the dashboard preview page ─────────────────────────

    public record Preview(string Key, string Audience, string Name, string When, string Subject, string Html);

    public static List<Preview> Previews()
    {
        var team = new TeamRequest(1234, "Mary & Daniel Example", "Chicago, IL", "Referred by Jane Friend", "Claire Hoffman",
            "https://example.org/admin/cases/1234", "9400 1112 0000 1234 5678 90", new DateTime(2026, 9, 18),
            new DateTime(2026, 9, 10), new DateTime(2026, 9, 24));
        Preview P(string key, string audience, string name, string when, Email e) => new(key, audience, name, when, e.Subject, e.Html);
        const string F = "Family";
        const string T = "Whitney & the team";
        return
        [
            P("received-self",      F, "Request received (family submitted it)",  "Right after the form is submitted", Received("Mary", true, "Mary & Daniel")),
            P("received-referral",  F, "Request received (someone referred them)", "Right after the form is submitted, to the person who referred", Received("Jane", false, "Mary & Daniel Example")),
            P("shipped",            F, "Package shipped",                          "When the request is marked Shipped", Shipped("Mary and Daniel", "9400 1112 0000 1234 5678 90")),
            P("completed",          F, "Package delivered",                        "When the request is marked Fulfilled", Completed("Mary and Daniel")),
            P("referrer-completed", F, "Thank-you to the person who referred",     "When a referred request is marked Fulfilled", ReferrerCompleted("Jane", "Mary & Daniel Example")),
            P("team-new",           T, "New request",                              "Right after the form is submitted", TeamNewRequest(team with { AssignedTo = null })),
            P("team-new-duplicate", T, "New request - possible duplicate",         "Right after the form is submitted, when it matches an existing family", TeamNewRequest(team with { AssignedTo = null, DuplicateReason = "Same email address as existing family #12 (Mary & Daniel Example)" })),
            P("team-shipped",       T, "Package shipped",                          "When the request is marked Shipped", TeamShipped(team)),
            P("team-completed",     T, "Package completed",                        "When the request is marked Fulfilled", TeamCompleted(team)),
        ];
    }

    // ── HTML building blocks ─────────────────────────────────────────────────

    private const string P = "color:#444;line-height:1.7;margin:0 0 16px;font-size:16px";
    private const string TeamFooter = "Internal notification for the LOTV team. Reasons for requests and family stories are not included in email.";

    public static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Steps(params string[] steps) =>
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f0f7ff;border-left:4px solid #1a4a6b;border-radius:4px;margin:0 0 20px\"><tr><td style=\"padding:14px 20px\">" +
        "<p style=\"margin:0 0 6px;color:#1a4a6b;font-weight:bold\">What happens next</p>" +
        "<ol style=\"color:#555;margin:0;padding-left:20px;line-height:1.8\">" + string.Concat(steps.Select(s => $"<li>{s}</li>")) + "</ol></td></tr></table>";

    private static string Facts(params (string Label, string? Value)[] rows) =>
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 0 16px;border:1px solid #e3e8f0;border-radius:6px\">" +
        string.Concat(rows.Where(r => !string.IsNullOrWhiteSpace(r.Value)).Select(r =>
            $"<tr><td style=\"padding:8px 14px;color:#7a8aa3;font-size:13px;width:130px;border-bottom:1px solid #eef1f6\">{E(r.Label)}</td><td style=\"padding:8px 14px;color:#1f2a3d;font-size:15px;border-bottom:1px solid #eef1f6\">{E(r.Value)}</td></tr>")) +
        "</table>";

    private static string Button(string label, string url) =>
        $"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr><td style=\"background:#1a4a6b;border-radius:6px\"><a href=\"{E(url)}\" style=\"display:inline-block;padding:12px 26px;color:#ffffff;text-decoration:none;font-weight:bold;font-size:15px\">{E(label)}</a></td></tr></table>";

    private static string Wrap(string preheader, string heading, string bodyHtml, string footer) =>
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
        $"<title>{E(heading)}</title></head>" +
        "<body style=\"margin:0;padding:0;background:#f4f7fb;font-family:Georgia,serif\">" +
        $"<div style=\"display:none;max-height:0;overflow:hidden;color:#f4f7fb\">{E(preheader)}</div>" +
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f7fb;padding:32px 0\"><tr><td align=\"center\">" +
        "<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08)\">" +
        "<tr><td style=\"background:#1a4a6b;padding:28px 40px;text-align:center\">" +
        "<div style=\"font-size:28px;line-height:1\">&#127803;</div>" +
        "<h1 style=\"color:#ffffff;margin:8px 0 0;font-size:24px;letter-spacing:.5px\">Lily of the Valley Ministry</h1>" +
        "<p style=\"color:#a8c8e8;margin:6px 0 0;font-size:14px\">Bringing comfort to grieving families</p></td></tr>" +
        $"<tr><td style=\"padding:36px 40px 28px\"><h2 style=\"color:#1a4a6b;margin:0 0 18px;font-size:22px\">{E(heading)}</h2>{bodyHtml}</td></tr>" +
        "<tr><td style=\"background:#f8fafc;padding:18px 40px;text-align:center;color:#8291a8;font-size:12px;line-height:1.6\">" +
        $"{E(footer)}<br>Lily of the Valley Ministry &middot; info@lotvministry.org</td></tr>" +
        "</table></td></tr></table></body></html>";
}

/// <summary>Decides who is emailed at each stage of a request, and sends it.</summary>
public static class RequestNotifier
{
    /// <summary>The team list (Whitney and the others): comma/semicolon separated, set in Notifications:IntakeTeamEmails.</summary>
    public static string[] TeamEmails(IConfiguration cfg)
    {
        var raw = cfg["Notifications:IntakeTeamEmails"] ?? cfg["Notifications:IntakeStaffEmail"] ?? "info@lotvministry.org";
        return raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Absolute link into the staff portal (emails are read outside the app, so relative links break).</summary>
    public static string WebUrl(IConfiguration cfg, string path) =>
        (cfg["App:WebBaseUrl"] ?? "https://lotv.wte.net").TrimEnd('/') + path;

    private static string Names(Family f) =>
        string.IsNullOrWhiteSpace(f.Parent2FirstName) ? f.Parent1FirstName : $"{f.Parent1FirstName} and {f.Parent2FirstName}";

    private static string? Location(Family f) =>
        string.Join(", ", new[] { f.City, f.State }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } l ? l : null;

    private static string SubmittedBy(PackageRequest r) =>
        r.IsForSelf ? "By the family, for themselves"
        : $"Referred by {(string.IsNullOrWhiteSpace(r.ReferrerName) ? "someone" : r.ReferrerName)}";

    private static RequestEmails.TeamRequest Team(IConfiguration cfg, PackageRequest r, Family f, string? duplicateReason = null) => new(
        r.Id, f.FullName, Location(f), SubmittedBy(r), r.AssignedTo, WebUrl(cfg, $"/admin/cases/{r.Id}"),
        r.TrackingNumber, r.ShippedDate, r.CreatedAt, DateTime.UtcNow, duplicateReason);

    private static void ToTeam(INotificationService notify, IConfiguration cfg, RequestEmails.Email e)
    {
        foreach (var to in TeamEmails(cfg)) _ = notify.SendEmailAsync(to, "LOTV Team", e.Subject, e.Html);
    }

    /// <summary>After the public form is submitted: confirm to the submitter, alert the team.</summary>
    public static void NewRequest(INotificationService notify, IConfiguration cfg, PackageRequest r, Family f,
        string? referrerFirstName, string? referrerEmail, string? duplicateReason)
    {
        // Confirmation goes to whoever actually submitted the form. If someone referred the family, the
        // family (who may not know a package is coming) is not emailed - staff make that first contact.
        var submitterEmail = r.IsForSelf ? f.Email : (referrerEmail ?? f.Email);
        var submitterName = r.IsForSelf ? f.Parent1FirstName : (referrerFirstName ?? f.Parent1FirstName);
        if (!string.IsNullOrWhiteSpace(submitterEmail))
        {
            var e = RequestEmails.Received(string.IsNullOrWhiteSpace(submitterName) ? "Friend" : submitterName, r.IsForSelf, f.FullName);
            _ = notify.SendEmailAsync(submitterEmail, submitterName ?? "Friend", e.Subject, e.Html);
        }
        ToTeam(notify, cfg, RequestEmails.TeamNewRequest(Team(cfg, r, f, duplicateReason)));
    }

    public static void Shipped(INotificationService notify, IConfiguration cfg, PackageRequest r)
    {
        if (r.Family is not { } f) return;
        if (!string.IsNullOrWhiteSpace(f.Email))
        {
            var e = RequestEmails.Shipped(Names(f), r.TrackingNumber);
            _ = notify.SendEmailAsync(f.Email, f.FullName, e.Subject, e.Html);
        }
        ToTeam(notify, cfg, RequestEmails.TeamShipped(Team(cfg, r, f)));
    }

    public static void Completed(INotificationService notify, IConfiguration cfg, PackageRequest r)
    {
        if (r.Family is not { } f) return;
        if (!string.IsNullOrWhiteSpace(f.Email))
        {
            var e = RequestEmails.Completed(Names(f));
            _ = notify.SendEmailAsync(f.Email, f.FullName, e.Subject, e.Html);
        }
        if (!r.IsForSelf && !string.IsNullOrWhiteSpace(r.ReferrerEmail))
        {
            var first = (r.ReferrerName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Friend";
            var e = RequestEmails.ReferrerCompleted(first, f.FullName);
            _ = notify.SendEmailAsync(r.ReferrerEmail, r.ReferrerName ?? "Friend", e.Subject, e.Html);
        }
        ToTeam(notify, cfg, RequestEmails.TeamCompleted(Team(cfg, r, f)));
    }
}
