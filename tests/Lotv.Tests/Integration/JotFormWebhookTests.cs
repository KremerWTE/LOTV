using System.Net;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// Tests for the JotForm prayer-package-request webhook (/api/v1/webhooks/jotform).
/// Reproduces the real, pre-existing data-loss bug found 2026-08-05 — the
/// "Children for Bracelet" label was never in knownLabels, so its answer glued
/// onto the end of the "Your Story" field and ChildrenInitials came through null —
/// plus the 2026-08-11 desync against the live form's actual current labels
/// (Recipient's Address, "Quaterly Grief Support", "How did you hear",
/// "Date of Recent Loss") that broke parsing further.
/// </summary>
[Collection("Integration")]
public class JotFormWebhookTests
{
    private readonly LotvApiFactory _factory;

    public JotFormWebhookTests(LotvApiFactory factory) => _factory = factory;

    private async Task SeedActiveChapterAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        if (!await db.Chapters.AnyAsync())
        {
            db.Chapters.Add(new Chapter
            {
                Name = "Test Chapter", City = "Springfield", State = "IL", IsActive = true,
                MaxActiveCasesPerVolunteer = 6, AcceptanceWindowHours = 24, UrgentAcceptanceWindowHours = 4
            });
            await db.SaveChangesAsync();
        }
    }

    // Mirrors JotForm's real "pretty" shape for form 261395566857171 as of the
    // 2026-08-11 live-question pull: comma-separated "Label:value" pairs using
    // the form's actual current text (including trailing-space labels).
    private const string RealisticPretty =
        "Prayer Care Package Options:For Someone Else, " +
        "Husband's Name:John Smith, " +
        "Email:john@example.com, " +
        "Phone Number:5551234567, " +
        "Wife's Name:Jane Smith, " +
        "Recipient's Address:123 Main St, Springfield, IL 62704, " +
        "Reason for Prayer Package Request:Miscarriage, " +
        "Date of Recent Loss: :06/01/2026, " +
        "Quaterly Grief Support :Yes, " +
        "Faith Tradition :Catholic, " +
        "Diocese:Test Diocese, " +
        "Parish:Test Parish, " +
        "How did you hear :Friend, " +
        "Would you like us to mention that this package is from you or prefer to remain anonymous?:Remain Anonymous, " +
        "Include a custom message to your recipient::Sending love, " +
        "Please Share With Us, As Much As You're Comfortable, Your Story::We are so sorry for your loss., " +
        "Children for Bracelet: We would like to include a personalized bracelet in your Prayer Care Package. " +
        "Please share the initials of all your children in birth order, including those in heaven. If your child " +
        "was not named or if you're experiencing infertility, we will place special Heart beads on your bracelet." +
        ":A.S., " +
        "Opt-in Communications:Join Our Newsletter, " +
        "Requester Name:Jane Smith, " +
        "Requester Email:jane@example.com, " +
        "Requester Phone :5559876543, " +
        "Requester Address :123 Main St, Springfield, IL 62704";

    private static MultipartFormDataContent BuildForm(string submissionId, string pretty)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(submissionId), "submissionID");
        form.Add(new StringContent(pretty), "pretty");
        return form;
    }

    [Fact]
    public async Task Webhook_RealisticSubmission_ReturnsOk()
    {
        await SeedActiveChapterAsync();
        var client = _factory.CreateClient();

        var resp = await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-001", RealisticPretty));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Webhook_RealisticSubmission_DoesNotLoseChildrenInitials()
    {
        await SeedActiveChapterAsync();
        var client = _factory.CreateClient();

        await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-002", RealisticPretty));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.OrderByDescending(f => f.Id).FirstOrDefaultAsync();

        Assert.NotNull(family);
        Assert.Equal("A.S.", family!.ChildrenInitials);
    }

    [Fact]
    public async Task Webhook_RealisticSubmission_CopiesChildrenInitialsOntoRequest()
    {
        // KanbanCard.razor reads Request.ChildrenInitials, a separate field from
        // Family.ChildrenInitials — this was never copied over, so real intake
        // (JotForm or the public /apply form) silently dropped the bracelet
        // initials off the Kanban card even though Family had the data.
        await SeedActiveChapterAsync();
        var client = _factory.CreateClient();

        await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-006", RealisticPretty));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var request = await db.Requests.OrderByDescending(r => r.Id).FirstOrDefaultAsync();

        Assert.NotNull(request);
        Assert.Equal("A.S.", request!.ChildrenInitials);
    }

    [Fact]
    public async Task Webhook_RealisticSubmission_DoesNotCorruptStoryField()
    {
        await SeedActiveChapterAsync();
        var client = _factory.CreateClient();

        await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-003", RealisticPretty));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.OrderByDescending(f => f.Id).FirstOrDefaultAsync();

        Assert.NotNull(family);
        Assert.Equal("We are so sorry for your loss.", family!.Story);
        Assert.DoesNotContain("bracelet", family.Story, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_RealisticSubmission_ParsesDateOfLoss()
    {
        await SeedActiveChapterAsync();
        var client = _factory.CreateClient();

        await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-004", RealisticPretty));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.OrderByDescending(f => f.Id).FirstOrDefaultAsync();

        Assert.NotNull(family);
        Assert.Equal(new DateTime(2026, 6, 1), family!.DateOfLoss);
    }

    [Fact]
    public async Task Webhook_RealisticSubmission_ParsesRecipientAddress()
    {
        await SeedActiveChapterAsync();
        var client = _factory.CreateClient();

        await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-005", RealisticPretty));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.OrderByDescending(f => f.Id).FirstOrDefaultAsync();

        Assert.NotNull(family);
        Assert.Equal("123 Main St", family!.StreetAddress);
        Assert.Equal("Springfield", family.City);
        Assert.Equal("IL", family.State);
        Assert.Equal("62704", family.Zip);
    }

    [Fact]
    public async Task Webhook_DuplicateSubmissionId_IsIgnored()
    {
        await SeedActiveChapterAsync();
        var client = _factory.CreateClient();

        await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-dup", RealisticPretty));
        await client.PostAsync("/api/v1/webhooks/jotform", BuildForm("sub-dup", RealisticPretty));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        // Families table is shared across tests in this collection, so assert
        // against the submission-scoped WebhookEvents row instead of a count
        // that other tests' fixture data would also contribute to.
        var count = await db.WebhookEvents.CountAsync(w => w.Source == "jotform" && w.ExternalId == "sub-dup");

        Assert.Equal(1, count);
    }
}
