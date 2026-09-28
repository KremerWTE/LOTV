using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// The Build Day planner: a recipe (what one box needs) times a target box count, checked against real stock —
/// "making 2 doesn't warrant the drive; making 40 does, but you need supplies for 40 when people show up."
/// </summary>
[Collection("Integration")]
public class BuildDayPlannerTests
{
    private readonly LotvApiFactory _factory;
    public BuildDayPlannerTests(LotvApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"buildday-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1BuildDay!", FirstName = "Bea", LastName = "Admin", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1BuildDay!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    private async Task<(int Chapter, int ItemAId, int ItemBId)> SeedInventoryAsync(int onHandA, int onHandB)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"BuildDay {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        var blanket = new ResourceItem { Name = "Blanket", Category = ResourceCategory.KnittedBlanket, QuantityOnHand = onHandA, ChapterId = chapter.Id };
        var book = new ResourceItem { Name = "Grief Book", Category = ResourceCategory.GriefBook, QuantityOnHand = onHandB, ChapterId = chapter.Id };
        db.ResourceItems.AddRange(blanket, book);
        await db.SaveChangesAsync();
        return (chapter.Id, blanket.Id, book.Id);
    }

    [Fact]
    public async Task TwoBoxes_IsCovered_ButFortyBoxes_ShowsExactlyWhatsShort()
    {
        var (chapter, blanketId, bookId) = await SeedInventoryAsync(onHandA: 50, onHandB: 10);
        var admin = await AdminClientAsync();
        await admin.PostAsJsonAsync("/api/v1/inventory/recipe", new { resourceItemId = blanketId, quantityPerBox = 1, chapterId = chapter });
        await admin.PostAsJsonAsync("/api/v1/inventory/recipe", new { resourceItemId = bookId, quantityPerBox = 1, chapterId = chapter });

        var small = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/inventory/build-day?boxes=2&chapterId={chapter}", Json);
        Assert.True(small.GetProperty("ready").GetBoolean());   // 2 boxes needs 2 of each; plenty on hand

        var big = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/inventory/build-day?boxes=40&chapterId={chapter}", Json);
        Assert.False(big.GetProperty("ready").GetBoolean());    // 40 boxes needs 40 books; only 10 on hand
        var bookRow = big.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("resourceItemId").GetInt32() == bookId);
        Assert.Equal(40, bookRow.GetProperty("needed").GetInt32());
        Assert.Equal(30, bookRow.GetProperty("shortBy").GetInt32());   // 40 needed - 10 on hand
        var blanketRow = big.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("resourceItemId").GetInt32() == blanketId);
        Assert.Equal(0, blanketRow.GetProperty("shortBy").GetInt32());
    }

    [Fact]
    public async Task SettingTheSameItemTwice_UpdatesTheQuantity_RatherThanDuplicating()
    {
        var (chapter, blanketId, _) = await SeedInventoryAsync(onHandA: 10, onHandB: 10);
        var admin = await AdminClientAsync();
        await admin.PostAsJsonAsync("/api/v1/inventory/recipe", new { resourceItemId = blanketId, quantityPerBox = 1, chapterId = chapter });
        await admin.PostAsJsonAsync("/api/v1/inventory/recipe", new { resourceItemId = blanketId, quantityPerBox = 3, chapterId = chapter });

        var recipe = await admin.GetFromJsonAsync<List<PackageRecipeItem>>($"/api/v1/inventory/recipe?chapterId={chapter}", Json);
        Assert.Single(recipe!);
        Assert.Equal(3, recipe![0].QuantityPerBox);
    }

    [Fact]
    public async Task RemovingARecipeItem_TakesItOutOfTheBuildDayCalculation()
    {
        var (chapter, blanketId, bookId) = await SeedInventoryAsync(onHandA: 5, onHandB: 5);
        var admin = await AdminClientAsync();
        var added = await admin.PostAsJsonAsync("/api/v1/inventory/recipe", new { resourceItemId = blanketId, quantityPerBox = 1, chapterId = chapter });
        var id = (await added.Content.ReadFromJsonAsync<PackageRecipeItem>(Json))!.Id;
        await admin.PostAsJsonAsync("/api/v1/inventory/recipe", new { resourceItemId = bookId, quantityPerBox = 1, chapterId = chapter });

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/inventory/recipe/{id}")).StatusCode);

        var plan = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/inventory/build-day?boxes=1&chapterId={chapter}", Json);
        Assert.Single(plan.GetProperty("items").EnumerateArray());
    }
}
