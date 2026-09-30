using System.Net;
using System.Net.Http.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// PATCH /api/public/v1/recurring/{id}, POST .../pause|resume|cancel, and PUT /donors/{id}/avatar were
/// AllowAnonymous with no ownership check at all — same pattern as the family-profile PATCH fixed earlier.
/// Unlike families, donors already have a real magic-link session (DonorMagicLink: token + expiry), so the
/// fix here is a live-session check, not a ConfirmEmail workaround. A signed-in staff caller (the Admin/
/// Donor*.razor pages reuse these same endpoints) sends its Bearer token and skips the session check.
/// </summary>
[Collection("Integration")]
public class DonorSelfServiceSecurityTests
{
    private readonly LotvApiFactory _factory;
    public DonorSelfServiceSecurityTests(LotvApiFactory factory) => _factory = factory;

    private async Task<(int ChapterId, Donor Donor, string Token)> SeedDonorWithSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"DonorSec {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        var donor = new Donor { FirstName = "Dee", LastName = "Nor", Email = $"donorsec-{Guid.NewGuid():N}@test.com", ChapterId = chapter.Id };
        db.Donors.Add(donor);
        await db.SaveChangesAsync();
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        db.DonorMagicLinks.Add(new DonorMagicLink { DonorId = donor.Id, Token = token, ExpiresAt = DateTime.UtcNow.AddMinutes(20) });
        await db.SaveChangesAsync();
        return (chapter.Id, donor, token);
    }

    private async Task<int> SeedRecurringAsync(int donorId, int chapterId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var r = new RecurringDonation { DonorId = donorId, ChapterId = chapterId, Amount = 25m, Frequency = RecurringFrequency.Monthly, NextChargeDate = DateTime.UtcNow.AddDays(1), Status = RecurringStatus.Active, CreatedAt = DateTime.UtcNow };
        db.RecurringDonations.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    private async Task<string> AdminTokenAsync()
    {
        var client = _factory.CreateClient();
        var email = $"donorsectest-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1DonorSec!", FirstName = "Ad", LastName = "Min", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1DonorSec!" });
        return (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
    }

    [Fact]
    public async Task Pause_AnonymousWithoutToken_IsForbidden()
    {
        var (chapterId, donor, _) = await SeedDonorWithSessionAsync();
        var recId = await SeedRecurringAsync(donor.Id, chapterId);
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/public/v1/recurring/{recId}/pause", new { DonorId = donor.Id, Token = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Pause_AnonymousWithSomeoneElsesToken_IsForbidden()
    {
        var (chapterId, donor, _) = await SeedDonorWithSessionAsync();
        var (_, otherDonor, otherToken) = await SeedDonorWithSessionAsync();
        var recId = await SeedRecurringAsync(donor.Id, chapterId);
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/public/v1/recurring/{recId}/pause", new { DonorId = donor.Id, Token = otherToken });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Pause_AnonymousWithOwnLiveToken_Succeeds()
    {
        var (chapterId, donor, token) = await SeedDonorWithSessionAsync();
        var recId = await SeedRecurringAsync(donor.Id, chapterId);
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/public/v1/recurring/{recId}/pause", new { DonorId = donor.Id, Token = token });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        Assert.Equal(RecurringStatus.Paused, (await db.RecurringDonations.FindAsync(recId))!.Status);
    }

    [Fact]
    public async Task Cancel_SignedInStaff_SucceedsWithoutAToken()
    {
        var (chapterId, donor, _) = await SeedDonorWithSessionAsync();
        var recId = await SeedRecurringAsync(donor.Id, chapterId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await AdminTokenAsync());

        var resp = await client.PostAsJsonAsync($"/api/public/v1/recurring/{recId}/cancel", new { DonorId = 0, Token = (string?)null });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Avatar_AnonymousWithoutToken_IsForbidden_ButWithLiveTokenSucceeds()
    {
        var (_, donor, token) = await SeedDonorWithSessionAsync();
        var client = _factory.CreateClient();

        var denied = await client.PutAsJsonAsync($"/api/public/v1/donors/{donor.Id}/avatar", new { AvatarUrl = "data:image/png;base64,xyz", Token = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await client.PutAsJsonAsync($"/api/public/v1/donors/{donor.Id}/avatar", new { AvatarUrl = "data:image/png;base64,xyz", Token = token });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task CreateRecurring_AnonymousWithoutToken_IsForbidden()
    {
        var (_, donor, _) = await SeedDonorWithSessionAsync();
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/public/v1/donors/{donor.Id}/recurring",
            new { Amount = 10m, Frequency = "Monthly", StartDate = (DateTime?)null, Campaign = (string?)null, Token = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
