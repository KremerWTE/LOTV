using System.Text.RegularExpressions;

namespace Lotv.Core.Models;

public enum DataIssueSeverity
{
    /// <summary>Worth a second look but usable as is.</summary>
    Warning,
    /// <summary>Something is missing or clearly wrong; staff should reach out before relying on it.</summary>
    Problem
}

public record DataIssue(string Field, string Message, DataIssueSeverity Severity);

/// <summary>What staff should do about a family's record: the problems found and how to reach the family.</summary>
public record FamilyDataReport(List<DataIssue> Issues, string Advice)
{
    public bool NeedsAttention => Issues.Count > 0;
    public bool HasProblems => Issues.Any(i => i.Severity == DataIssueSeverity.Problem);
}

/// <summary>
/// Checks a family record for names that look wrong (numbers, placeholders, single letters) and for
/// missing or malformed contact and address details, so staff can call or email to fix them.
/// Pure and side-effect free; runs wherever the family is shown.
/// </summary>
public static class FamilyDataQuality
{
    private static readonly string[] PlaceholderWords =
        ["test", "testing", "asdf", "qwerty", "xxx", "unknown", "none", "n/a", "na", "tbd", "sample", "dummy", "fake", "lorem", "ipsum", "firstname", "lastname", "name"];

    private static readonly Regex NameChars = new(@"^[\p{L}][\p{L}\p{M} .'’\-]*$", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
    private static readonly Regex UsZip = new(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled);

    public static FamilyDataReport Check(Family f)
    {
        var issues = new List<DataIssue>();

        CheckName(issues, "First parent's first name", f.Parent1FirstName, required: true);
        CheckName(issues, "First parent's last name", f.Parent1LastName, required: true);
        if (!string.IsNullOrWhiteSpace(f.Parent2FirstName) || !string.IsNullOrWhiteSpace(f.Parent2LastName))
        {
            CheckName(issues, "Second parent's first name", f.Parent2FirstName, required: true);
            CheckName(issues, "Second parent's last name", f.Parent2LastName, required: false);
        }

        var emailOk = !string.IsNullOrWhiteSpace(f.Email) && Email.IsMatch(f.Email.Trim());
        if (string.IsNullOrWhiteSpace(f.Email))
            issues.Add(new("Email", "No email address on file.", DataIssueSeverity.Problem));
        else if (!emailOk)
            issues.Add(new("Email", $"The email address \"{f.Email}\" doesn't look valid.", DataIssueSeverity.Problem));

        var digits = new string((f.Phone ?? "").Where(char.IsDigit).ToArray());
        var phoneOk = digits.Length >= 10;
        if (string.IsNullOrWhiteSpace(f.Phone))
            issues.Add(new("Phone", "No phone number on file.", DataIssueSeverity.Warning));
        else if (!phoneOk)
            issues.Add(new("Phone", $"The phone number \"{f.Phone}\" is too short.", DataIssueSeverity.Warning));

        if (string.IsNullOrWhiteSpace(f.StreetAddress))
            issues.Add(new("Street address", "No street address, so the package can't be shipped.", DataIssueSeverity.Problem));
        if (string.IsNullOrWhiteSpace(f.City))
            issues.Add(new("City", "No city on the address.", DataIssueSeverity.Problem));
        if (string.IsNullOrWhiteSpace(f.State))
            issues.Add(new("State", "No state on the address.", DataIssueSeverity.Problem));
        else if (f.State.Trim().Length != 2)
            issues.Add(new("State", $"The state \"{f.State}\" isn't a two-letter code.", DataIssueSeverity.Warning));
        if (string.IsNullOrWhiteSpace(f.Zip))
            issues.Add(new("Zip", "No zip code on the address.", DataIssueSeverity.Problem));
        else if (!UsZip.IsMatch(f.Zip.Trim()))
            issues.Add(new("Zip", $"The zip code \"{f.Zip}\" isn't a valid 5-digit zip.", DataIssueSeverity.Warning));

        if (f.DateOfLoss is { } loss && loss.Date > DateTime.UtcNow.Date)
            issues.Add(new("Date of loss", "The date of loss is in the future.", DataIssueSeverity.Warning));

        return new FamilyDataReport(issues, AdviceFor(issues.Count > 0, emailOk, phoneOk));
    }

    private static string AdviceFor(bool hasIssues, bool emailOk, bool phoneOk)
    {
        if (!hasIssues) return "";
        return (emailOk, phoneOk) switch
        {
            (true, true)   => "Call or email the family to confirm and fix these details.",
            (true, false)  => "Email the family to confirm and fix these details.",
            (false, true)  => "Call the family to confirm and fix these details.",
            _              => "There's no working email or phone for this family. Check the original request or ask the person who referred them.",
        };
    }

    private static void CheckName(List<DataIssue> issues, string field, string? value, bool required)
    {
        var name = (value ?? "").Trim();
        if (name.Length == 0)
        {
            if (required) issues.Add(new(field, $"{field} is missing.", DataIssueSeverity.Problem));
            return;
        }
        if (name.Any(char.IsDigit))
            issues.Add(new(field, $"\"{name}\" has numbers in it, so it doesn't look like a real name.", DataIssueSeverity.Problem));
        else if (!NameChars.IsMatch(name))
            issues.Add(new(field, $"\"{name}\" has unusual characters, so it may be a typo.", DataIssueSeverity.Problem));
        else if (name.Replace(".", "").Replace(" ", "").Length < 2)
            issues.Add(new(field, $"\"{name}\" is only one letter.", DataIssueSeverity.Problem));
        else if (name.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Any(w => PlaceholderWords.Contains(w.ToLowerInvariant())))
            issues.Add(new(field, $"\"{name}\" looks like a placeholder, not a real name.", DataIssueSeverity.Problem));
        else if (name.Length >= 4 && name.Distinct().Count() == 1)
            issues.Add(new(field, $"\"{name}\" is the same letter repeated.", DataIssueSeverity.Problem));
        else if (name.Length > 3 && !name.Any(c => "aeiouyAEIOUY".Contains(c)))
            issues.Add(new(field, $"\"{name}\" has no vowels, so it may be a typo.", DataIssueSeverity.Warning));
    }
}
