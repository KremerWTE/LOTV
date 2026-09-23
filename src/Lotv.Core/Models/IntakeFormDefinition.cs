using System.Text.RegularExpressions;

namespace Lotv.Core.Models;

/// <summary>
/// JSON shape of the public prayer care intake form. The static form page
/// (docs/duda-embed/prayer-care-intake.html) renders itself from this, and the
/// dashboard editor (Admin/FormEditor.razor) edits it. Property names are
/// camelCase on the wire.
/// </summary>
public class IntakeFormDefinition
{
    public int    Version          { get; set; } = 1;
    public string Title            { get; set; } = "";
    public string Intro            { get; set; } = "";
    public string SubmitLabel      { get; set; } = "Submit Request";
    public string FooterNote       { get; set; } = "";
    public string PackageType      { get; set; } = "Comfort";
    public int    ChapterId        { get; set; } = 1;
    public string ToggleLabelMe      { get; set; } = "This is for me";
    public string ToggleLabelSomeone { get; set; } = "This is for someone else";
    public string WhoIsThisForLabel  { get; set; } = "Who is this for?";
    public ConfirmationCopy Confirmation { get; set; } = new();
    public DonationCopy     Donation     { get; set; } = new();
    public List<IntakeFormField> Fields  { get; set; } = [];

    // ── Validation ───────────────────────────────────────────────────────────
    public static readonly string[] FieldTypes =
        ["text", "email", "tel", "date", "select", "textarea", "checkbox", "heading", "hint", "bracelet"];

    /// <summary>Standard fields feed fixed properties on the submitted Family/request, so they can't be removed or retyped.</summary>
    public static readonly IReadOnlyDictionary<string, string> StandardKeys = new Dictionary<string, string>
    {
        ["husbandFirst"] = "text",   ["wifeFirst"] = "text",
        ["husbandEmail"] = "email",  ["wifeEmail"] = "email",
        ["husbandPhone"] = "tel",    ["wifePhone"] = "tel",
        ["street"] = "text", ["apt"] = "text", ["city"] = "text", ["state"] = "text", ["zip"] = "text",
        ["mentionPreference"] = "select", ["reason"] = "select", ["dateOfLoss"] = "date",
        ["griefSupport"] = "select", ["faithTradition"] = "select",
        ["diocese"] = "text", ["parish"] = "text",
        ["howHeard"] = "select", ["howHeardOther"] = "text",
        ["customMessage"] = "textarea", ["story"] = "textarea",
        ["childrenInitials"] = "bracelet",
        ["requesterFirst"] = "text", ["requesterLast"] = "text",
        ["requesterEmail"] = "email", ["requesterPhone"] = "tel",
        ["optinNewsletter"] = "checkbox", ["optinPrayerNight"] = "checkbox",
    };

    /// <summary>Standard fields the public /apply endpoint refuses to accept without.</summary>
    public static readonly string[] AlwaysRequiredKeys = ["husbandFirst", "husbandEmail"];

    private static readonly Regex IdRx     = new(@"^[a-z0-9][a-z0-9-]{0,40}$", RegexOptions.Compiled);
    private static readonly Regex KeyRx    = new(@"^[A-Za-z][A-Za-z0-9]{0,40}$", RegexOptions.Compiled);
    private static readonly Regex WidgetRx = new(@"^[A-Za-z0-9]{1,60}$", RegexOptions.Compiled);
    private static readonly Regex AmountRx = new(@"^\d{1,6}$", RegexOptions.Compiled);

    /// <summary>Returns human-readable problems; empty when the definition is safe to store and render.</summary>
    public List<string> Validate(IReadOnlySet<string>? allowedReasonValues = null)
    {
        var errors = new List<string>();
        void Max(string name, string? v, int max)
        {
            if (v is not null && v.Length > max) errors.Add($"{name} is too long (max {max} characters).");
        }

        if (string.IsNullOrWhiteSpace(Title)) errors.Add("Form title is required.");
        Max("Title", Title, 200);   Max("Intro", Intro, 3000);
        Max("Submit button label", SubmitLabel, 80);  Max("Footer note", FooterNote, 1000);
        Max("'For me' toggle label", ToggleLabelMe, 120);
        Max("'For someone else' toggle label", ToggleLabelSomeone, 120);
        Max("'Who is this for' label", WhoIsThisForLabel, 200);
        Max("Confirmation title", Confirmation.Title, 200);  Max("Confirmation message", Confirmation.Body, 3000);
        Max("Donation title", Donation.Title, 200);          Max("Donation message", Donation.Body, 3000);
        if (string.IsNullOrWhiteSpace(SubmitLabel)) errors.Add("Submit button label is required.");
        if (ChapterId < 1) errors.Add("Chapter id must be a positive number.");

        if (Donation.Enabled)
        {
            if (!WidgetRx.IsMatch(Donation.WidgetId ?? "")) errors.Add("GiveButter widget id must be letters and numbers only.");
            if (!WidgetRx.IsMatch(Donation.Account ?? ""))  errors.Add("GiveButter account id must be letters and numbers only.");
            if (!string.IsNullOrEmpty(Donation.Amount) && !AmountRx.IsMatch(Donation.Amount))
                errors.Add("Suggested donation amount must be a whole number.");
        }

        if (Fields.Count is < 1 or > 80) { errors.Add("The form must have between 1 and 80 items."); return errors; }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Fields)
        {
            var name = string.IsNullOrWhiteSpace(f.Label) ? f.Id : f.Label;
            if (!IdRx.IsMatch(f.Id ?? "")) errors.Add($"Item '{name}' has an invalid id.");
            else if (!ids.Add(f.Id)) errors.Add($"Duplicate item id '{f.Id}'.");

            if (!FieldTypes.Contains(f.Type)) { errors.Add($"'{name}' has an unknown type '{f.Type}'."); continue; }
            var needsKey = f.Type is not ("heading" or "hint");
            if (needsKey)
            {
                if (!KeyRx.IsMatch(f.Key ?? "")) errors.Add($"'{name}' has an invalid key.");
                else if (!keys.Add(f.Key)) errors.Add($"Duplicate item key '{f.Key}'.");
            }
            if (f.Width is not ("half" or "full")) errors.Add($"'{name}' has an invalid width.");
            if (string.IsNullOrWhiteSpace(f.Label) && f.Type != "bracelet") errors.Add($"Every item needs a label or text (id '{f.Id}').");
            Max($"'{name}' label", f.Label, 2000);   Max($"'{name}' text for 'me'", f.LabelMe, 2000);
            Max($"'{name}' text for 'someone else'", f.LabelSomeone, 2000);
            Max($"'{name}' placeholder", f.Placeholder, 200);   Max($"'{name}' help text", f.Help, 500);
            Max($"'{name}' button label", f.ButtonLabel, 80);

            if (f.Standard != StandardKeys.ContainsKey(f.Key ?? ""))
                errors.Add(f.Standard
                    ? $"'{name}' is marked standard but '{f.Key}' is not a standard key."
                    : $"'{name}' uses the reserved key '{f.Key}'.");
            if (f.Standard && StandardKeys.TryGetValue(f.Key, out var stdType) && stdType != f.Type)
                errors.Add($"'{name}' must stay a {stdType} question.");

            if (f.Type == "select")
            {
                if (f.Options.Count is < 1 or > 60) errors.Add($"'{name}' needs between 1 and 60 choices.");
                var vals = new HashSet<string>(StringComparer.Ordinal);
                foreach (var o in f.Options)
                {
                    if (string.IsNullOrWhiteSpace(o.Value) || o.Value.Length > 100) errors.Add($"'{name}' has a choice with an empty or too-long value.");
                    else if (!vals.Add(o.Value)) errors.Add($"'{name}' has the choice '{o.Value}' twice.");
                    if (string.IsNullOrWhiteSpace(o.Label) || o.Label.Length > 200) errors.Add($"'{name}' has a choice with an empty or too-long label.");
                }
                if (f.Key == "reason" && allowedReasonValues is not null)
                    foreach (var o in f.Options.Where(o => !allowedReasonValues.Contains(o.Value)))
                        errors.Add($"Reason choice '{o.Value}' isn't a value the system understands.");
            }
        }

        // Standard fields must all still be present, and the ones the API can't do without must stay required + shown.
        foreach (var (key, type) in StandardKeys)
            if (!Fields.Any(x => x.Key == key && x.Standard))
                errors.Add($"The standard {type} question '{key}' can't be removed.");
        foreach (var key in AlwaysRequiredKeys)
        {
            var f = Fields.FirstOrDefault(x => x.Key == key);
            if (f is not null && (!f.Required || !f.Visible || f.ShowWhen.Count > 0))
                errors.Add($"'{f.Label}' must stay visible and required — the intake needs it to create the family record.");
        }

        var keyList = Fields.Where(f => !string.IsNullOrEmpty(f.Key)).Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var f in Fields)
        {
            if (f.ShowWhen.Count > 10) errors.Add($"'{f.Label}' has too many show/hide rules.");
            foreach (var c in f.ShowWhen)
            {
                var hasBranch = !string.IsNullOrEmpty(c.Branch);
                var hasField  = !string.IsNullOrEmpty(c.Field);
                if (hasBranch == hasField) { errors.Add($"A show/hide rule on '{f.Label}' must be about either who it's for or another question."); continue; }
                if (hasBranch && c.Branch is not ("me" or "someone")) errors.Add($"A rule on '{f.Label}' has an unknown 'who it's for' value.");
                if (hasField)
                {
                    if (c.Field == f.Key) errors.Add($"'{f.Label}' can't depend on itself.");
                    else if (!keyList.Contains(c.Field!)) errors.Add($"A rule on '{f.Label}' refers to a question that doesn't exist.");
                    if (!c.NotEmpty && (c.In is null || c.In.Count == 0)) errors.Add($"A rule on '{f.Label}' needs at least one value or 'has any answer'.");
                }
            }
        }
        return errors;
    }
}

public class ConfirmationCopy
{
    public string Title { get; set; } = "Your request has been received.";
    public string Body  { get; set; } = "";
}

public class DonationCopy
{
    public bool    Enabled   { get; set; } = true;
    public string  Title     { get; set; } = "";
    public string  Body      { get; set; } = "";
    public string  WidgetId  { get; set; } = "";
    public string  Account   { get; set; } = "";
    public string? Amount    { get; set; }
}

public class IntakeFormField
{
    public string  Id          { get; set; } = "";
    public string  Key         { get; set; } = "";
    public string  Type        { get; set; } = "text";
    public string  Label       { get; set; } = "";
    /// <summary>Overrides <see cref="Label"/> when the visitor picks "for me".</summary>
    public string? LabelMe        { get; set; }
    /// <summary>Overrides <see cref="Label"/> when the visitor picks "for someone else".</summary>
    public string? LabelSomeone   { get; set; }
    public string? Placeholder { get; set; }
    public string? Help        { get; set; }
    public bool    Required    { get; set; }
    public bool    Visible     { get; set; } = true;
    /// <summary>"half" packs two per row; "full" spans the row.</summary>
    public string  Width       { get; set; } = "half";
    public bool    Standard    { get; set; }
    /// <summary>Choice values are understood by the backend (e.g. reasons), so only labels/order may change.</summary>
    public bool    LockValues  { get; set; }
    /// <summary>Checkbox only: its label in the "Opted in to: ..." note.</summary>
    public string? OptinLabel  { get; set; }
    /// <summary>Bracelet only: the "add another" button text.</summary>
    public string? ButtonLabel        { get; set; }
    public string? ButtonLabelMe      { get; set; }
    public string? ButtonLabelSomeone { get; set; }
    public List<FormOption>    Options  { get; set; } = [];
    public List<FormCondition> ShowWhen { get; set; } = [];
}

public class FormOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

/// <summary>One show/hide rule. Set either <see cref="Branch"/> or <see cref="Field"/>; all rules on a field must hold.</summary>
public class FormCondition
{
    /// <summary>"me" or "someone" — show only for that choice of who the request is for.</summary>
    public string? Branch   { get; set; }
    /// <summary>Key of another question whose answer decides visibility.</summary>
    public string? Field    { get; set; }
    public List<string>? In { get; set; }
    public bool    NotEmpty { get; set; }
}

/// <summary>What the editor API returns: the definition plus whether it's the built-in default and who last saved it.</summary>
public class IntakeFormEnvelope
{
    public string   Key        { get; set; } = "";
    public bool     IsDefault  { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string?  UpdatedBy  { get; set; }
    public IntakeFormDefinition Definition { get; set; } = new();
}
