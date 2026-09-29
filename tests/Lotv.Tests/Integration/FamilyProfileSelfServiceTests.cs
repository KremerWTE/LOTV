using System.Net;
using System.Net.Http.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// PATCH /api/public/v1/families/{id}/profile has no login of its own — anyone can call it
/// with just a family's numeric id. ConfirmEmail (must match the email already on file) is
/// the only thing standing between that and a stranger rewriting a grieving family's contact
/// info. A signed-in staff caller (sends a Bearer token, even though the route allows
/// anonymous callers too) skips that check — the admin UI already authorized them.
/// </summary>
[Collection("Integration")]
public class FamilyProfileSelfServiceTests
{
    private readonly LotvApiFactory _factory;
    public FamilyProfileSelfServiceTests(LotvApiFactory factory) => _factory = factory;

    private async Task<(int ChapterId, int FamilyId)> SeedFamilyAsync(string email = "onfile@example.com")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"ProfileTest {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        var family = new Family
        {
            Parent1FirstName = "Original", Parent1LastName = "Name", Email = email,
            StreetAddress = "1 Old St", City = "Old Town", State = "IL", Zip = "60000",
            Reason = PackageReason.Miscarriage, ChapterId = chapter.Id, CreatedAt = DateTime.UtcNow,
        };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        return (chapter.Id, family.Id);
    }

    private async Task<string> AdminTokenAsync()
    {
        var client = _factory.CreateClient();
        var email = $"profiletest-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1ProfileQA!", FirstName = "Pro", LastName = "Admin", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1ProfileQA!" });
        return (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
    }

    [Fact]
    public async Task AnonymousCaller_WithoutConfirmEmail_IsRejected()
    {
        var (_, familyId) = await SeedFamilyAsync();
        var client = _factory.CreateClient();

        var resp = await client.PatchAsJsonAsync($"/api/public/v1/families/{familyId}/profile",
            new { FirstName = "Hijacked", LastName = "Name" });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.FindAsync(familyId);
        Assert.Equal("Original", family!.Parent1FirstName); // untouched
    }

    [Fact]
    public async Task AnonymousCaller_WithWrongConfirmEmail_IsRejected()
    {
        var (_, familyId) = await SeedFamilyAsync(email: "real@example.com");
        var client = _factory.CreateClient();

        var resp = await client.PatchAsJsonAsync($"/api/public/v1/families/{familyId}/profile",
            new { FirstName = "Hijacked", ConfirmEmail = "guess@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AnonymousCaller_WithMatchingConfirmEmail_CanUpdateTheirOwnProfile()
    {
        var (_, familyId) = await SeedFamilyAsync(email: "real@example.com");
        var client = _factory.CreateClient();

        var resp = await client.PatchAsJsonAsync($"/api/public/v1/families/{familyId}/profile",
            new { FirstName = "Updated", LastName = "Family", Phone = "5551234567", ConfirmEmail = "REAL@example.com" }); // case-insensitive

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.FindAsync(familyId);
        Assert.Equal("Updated", family!.Parent1FirstName);
        Assert.Equal("5551234567", family.Phone);
    }

    [Fact]
    public async Task SignedInStaff_CanUpdateWithoutConfirmEmail()
    {
        var (_, familyId) = await SeedFamilyAsync();
        var token = await AdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var resp = await client.PatchAsJsonAsync($"/api/public/v1/families/{familyId}/profile",
            new { FirstName = "StaffEdited" }); // no ConfirmEmail at all

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.FindAsync(familyId);
        Assert.Equal("StaffEdited", family!.Parent1FirstName);
    }
}
