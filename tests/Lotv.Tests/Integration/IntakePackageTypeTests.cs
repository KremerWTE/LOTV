using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>The package the request form asked for is kept, and the form definition's default "Comfort" is recognised.</summary>
[Collection("Integration")]
public class IntakePackageTypeTests
{
    private readonly LotvApiFactory _factory;

    public IntakePackageTypeTests(LotvApiFactory factory) => _factory = factory;

    private async Task<PackageRequest> ApplyAsync(string? packageType)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Pat", Parent1LastName = $"Package{tag}", Email = $"pkg-{tag}@test.example.com", Phone = "",
                StreetAddress = "1 Test St", City = "Testville", State = "IL", Zip = "60601", Reason = "Miscarriage", ChapterId = 1,
            },
            ForSelf = true,
            PackageType = packageType,
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = body.GetProperty("requestId").GetInt32();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return await db.Requests.AsNoTracking().FirstAsync(r => r.Id == requestId);
    }

    [Theory]
    [InlineData("Comfort", RequestCategory.ResourceProvision)]
    [InlineData("comfort", RequestCategory.ResourceProvision)]
    [InlineData("Memory", RequestCategory.Memorial)]
    [InlineData("Something else", RequestCategory.Other)]
    public async Task PackageType_IsMatchedIgnoringCase_AndKeptInTheStaffNotes(string packageType, RequestCategory expected)
    {
        var req = await ApplyAsync(packageType);
        Assert.Equal(expected, req.Category);
        Assert.Contains($"Package requested: {packageType}", req.InternalNotes);
    }

    [Fact]
    public async Task NoPackageType_AddsNoNote()
    {
        var req = await ApplyAsync(null);
        Assert.Equal(RequestCategory.Other, req.Category);
        Assert.True(string.IsNullOrEmpty(req.InternalNotes));
    }
}
