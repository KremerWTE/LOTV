using Lotv.Api.Data;
using Lotv.Core.Models;

namespace Lotv.Tests.Domain;

/// <summary>
/// The intake form is brought up to date every time it is served: a "Child's name" question for a loss, and Spanish text for
/// every built-in item — without ever overwriting Spanish that staff wrote.
/// </summary>
public class FormUpgradesTests
{
    private static IntakeFormDefinition Fresh() => FormUpgrades.Apply(FormDefinitions.Default(FormDefinitions.PrayerCareIntakeKey));

    [Fact]
    public void TheChildNameQuestion_IsAddedOnce_OnlyForALoss_AfterTheDateOfLoss()
    {
        var def = Fresh();
        var q = Assert.Single(def.Fields, f => f.Key == "childName");
        Assert.False(q.Required);
        Assert.False(q.Standard);
        var rule = Assert.Single(q.ShowWhen);
        Assert.Equal("reason", rule.Field);
        Assert.Contains("Miscarriage", rule.In!);
        Assert.DoesNotContain("Infertility", rule.In!);
        Assert.Equal("dateOfLoss", def.Fields[def.Fields.IndexOf(q) - 1].Key);

        FormUpgrades.Apply(def);   // applying again changes nothing
        Assert.Single(def.Fields, f => f.Key == "childName");
    }

    [Fact]
    public void AnUpgradedDefinition_StillPassesValidation()
    {
        var errors = Fresh().Validate(FormDefinitions.ReasonValues);
        Assert.Empty(errors);
    }

    [Fact]
    public void EveryBuiltInQuestionAndChoice_GetsSpanish()
    {
        var def = Fresh();
        Assert.False(string.IsNullOrWhiteSpace(def.Es!["title"]));
        Assert.False(string.IsNullOrWhiteSpace(def.Confirmation.Es!["body"]));
        foreach (var f in def.Fields.Where(f => f.Visible))
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Es?.GetValueOrDefault("label")), $"No Spanish for '{f.Label}'");
            foreach (var o in f.Options)
                Assert.False(string.IsNullOrWhiteSpace(o.Es?.GetValueOrDefault("label")), $"No Spanish for choice '{o.Label}' of '{f.Key}'");
        }
    }

    [Fact]
    public void SpanishThatStaffWrote_IsNeverOverwritten_AndOnlyBlanksAreFilled()
    {
        var def = FormDefinitions.Default(FormDefinitions.PrayerCareIntakeKey);
        def.Es = new() { ["title"] = "Mi título propio", ["intro"] = "   " };
        FormUpgrades.Apply(def);
        Assert.Equal("Mi título propio", def.Es["title"]);
        Assert.StartsWith("Para solicitar", def.Es["intro"]);   // a blank was filled
    }

    [Fact]
    public void ACustomQuestion_WithNoSpanish_IsLeftAlone_SoVisitorsSeeTheEnglish()
    {
        var def = FormDefinitions.Default(FormDefinitions.PrayerCareIntakeKey);
        def.Fields.Add(new IntakeFormField { Id = "custom-1", Key = "custom1", Type = "text", Label = "Anything else?", Width = "full" });
        FormUpgrades.Apply(def);
        Assert.Null(def.Fields.Single(f => f.Key == "custom1").Es);
    }

    [Fact]
    public void ASavedFormWithoutTheQuestion_GetsIt_WithoutAReasonField_AtTheEnd()
    {
        var def = new IntakeFormDefinition { Title = "T", Fields = [new IntakeFormField { Id = "a", Key = "custom1", Type = "text", Label = "A" }] };
        FormUpgrades.Apply(def);
        Assert.Equal("childName", def.Fields[^1].Key);
    }

    [Theory]
    [InlineData("Child's name: Grace", "Grace")]
    [InlineData("Quarterly Grief Support requested: Yes | Child's name: Grace Ann | Opted in to: Newsletter", "Grace Ann")]
    [InlineData("Parent 2 email: a@b.com | Child's name:   Luke  ", "Luke")]
    [InlineData("No name here | Other: x", null)]
    [InlineData("Child's name: ", null)]
    [InlineData(null, null)]
    public void TheChildName_IsReadOutOfTheIntakeNotes(string? notes, string? expected) =>
        Assert.Equal(expected, FormUpgrades.ChildNameFrom(notes));

    [Fact]
    public void Validation_RejectsUnknownSpanishKeys_AndOverlongText()
    {
        var def = Fresh();
        def.Es!["colour"] = "x";
        def.Fields[0].Es!["label"] = new string('x', 3001);
        var errors = def.Validate(FormDefinitions.ReasonValues);
        Assert.Contains(errors, e => e.Contains("unknown Spanish text 'colour'"));
        Assert.Contains(errors, e => e.Contains("too long"));
    }
}
