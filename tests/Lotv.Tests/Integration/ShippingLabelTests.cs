using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lotv.Tests.Integration;

/// <summary>
/// Phase one of shipping-label generation: a ShippingLabel record is auto-created the first time a
/// package case's ProcessStage reaches Packing. No real carrier is called (see IShippingLabelGenerator);
/// TrackingNumber is a placeholder and Carrier/ServiceLevel/LabelFileUrl stay null until one is chosen.
/// </summary>
[Collection("Integration")]
public class ShippingLabelTests
{
    private const string Password = "TestPass1ShipLabel!";
    private readonly LotvApiFactory _factory;
    public ShippingLabelTests(LotvApiFactory factory) => _factory = factory;

    private async Task<HttpClient> HqAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"hq-ship-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password, FirstName = "H", LastName = "Q", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    private async Task<int> NewRequestAsync(bool wantsPackage)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"Ship {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow, MaxActiveCasesPerVolunteer = 6, AcceptanceWindowHours = 24, UrgentAcceptanceWindowHours = 4 };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = "Ily", ChapterId = chapter.Id };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var request = new PackageRequest { FamilyId = family.Id, ChapterId = chapter.Id, WantsPackage = wantsPackage, Status = CaseStatus.InProgress };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    private async Task<HttpStatusCode> MoveToStageAsync(HttpClient client, int requestId, ProcessStage stage) =>
        (await client.PutAsJsonAsync($"/api/v1/requests/{requestId}/process-stage", new { ProcessStage = stage })).StatusCode;

    [Fact]
    public async Task MovingAPackageCaseToPacking_GeneratesAShippingLabel()
    {
        var client = await HqAdminClientAsync();
        var requestId = await NewRequestAsync(wantsPackage: true);

        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Packing));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var label = await db.ShippingLabels.AsNoTracking().SingleOrDefaultAsync(l => l.PackageRequestId == requestId);
        Assert.NotNull(label);
        Assert.True(label!.IsPlaceholder);
        Assert.False(string.IsNullOrWhiteSpace(label.TrackingNumber));
        Assert.Null(label.Carrier);
        Assert.Null(label.ServiceLevel);
        Assert.Null(label.LabelFileUrl);
    }

    [Fact]
    public async Task MovingACaseToPackingTwice_NeverGeneratesASecondLabel()
    {
        var client = await HqAdminClientAsync();
        var requestId = await NewRequestAsync(wantsPackage: true);

        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Packing));
        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Notes));
        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Packing));   // moved back into Packing again

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var count = await db.ShippingLabels.AsNoTracking().CountAsync(l => l.PackageRequestId == requestId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task APrayerOnlyRequest_NeverGetsAShippingLabel_EvenIfMovedThroughPacking()
    {
        var client = await HqAdminClientAsync();
        var requestId = await NewRequestAsync(wantsPackage: false);

        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Assigned));
        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Confirmed));
        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Packing));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        Assert.False(await db.ShippingLabels.AsNoTracking().AnyAsync(l => l.PackageRequestId == requestId));
    }

    [Fact]
    public async Task ShippoExport_ListsPackingCasesOnly_WithShippingEssentialsAndNoSensitiveFields()
    {
        var client = await HqAdminClientAsync();
        var packing = await NewRequestAsync(wantsPackage: true);
        var notPacking = await NewRequestAsync(wantsPackage: true);
        var prayerOnly = await NewRequestAsync(wantsPackage: false);
        await MoveToStageAsync(client, packing, ProcessStage.Packing);
        await MoveToStageAsync(client, prayerOnly, ProcessStage.Packing);

        var resp = await client.GetAsync("/api/v1/requests/shippo-export");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var csv = await resp.Content.ReadAsStringAsync();

        Assert.StartsWith("Order Number,Order Date,Recipient Name", csv);
        Assert.Contains($"LOTV-{packing},", csv);
        Assert.DoesNotContain($"LOTV-{notPacking},", csv);
        Assert.DoesNotContain($"LOTV-{prayerOnly},", csv);
        Assert.DoesNotContain("Reason", csv);
    }

    private sealed class FakeShippo(bool configured, Func<ShippoOrderResult> result) : IShippoOrderClient
    {
        public Func<ShippoOrderResult> Result { get; set; } = result;
        public int Calls { get; private set; }
        public bool IsConfigured => configured;
        public Task<ShippoOrderResult> CreateOrderAsync(PackageRequest request, Family family, CancellationToken ct = default)
        { Calls++; return Task.FromResult(Result()); }
    }

    private async Task<HttpClient> ClientWithAsync(FakeShippo fake)
    {
        var client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IShippoOrderClient>();
            s.AddSingleton<IShippoOrderClient>(fake);
        })).CreateClient();
        var email = $"hq-shippo-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password, FirstName = "H", LastName = "Q", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    private async Task<ShippingLabel> LabelAsync(int requestId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LotvDbContext>().ShippingLabels.AsNoTracking().SingleAsync(l => l.PackageRequestId == requestId);
    }

    [Fact]
    public async Task Packing_WithShippoConfigured_PushesTheOrderAndStoresItsId()
    {
        var fake = new FakeShippo(true, () => new(true, "order_abc", null));
        var client = await ClientWithAsync(fake);
        var requestId = await NewRequestAsync(wantsPackage: true);

        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Packing));

        var label = await LabelAsync(requestId);
        Assert.Equal("order_abc", label.ShippoOrderId);
        Assert.NotNull(label.ShippoSyncedAt);
        Assert.Null(label.ShippoSyncError);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task ShippoFailure_NeverBlocksTheStageChange_AndCanBeRetried()
    {
        var fake = new FakeShippo(true, () => new(false, null, "Shippo returned HTTP 500."));
        var client = await ClientWithAsync(fake);
        var requestId = await NewRequestAsync(wantsPackage: true);

        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Packing));
        var failed = await LabelAsync(requestId);
        Assert.Null(failed.ShippoOrderId);
        Assert.Equal("Shippo returned HTTP 500.", failed.ShippoSyncError);

        fake.Result = () => new(true, "order_retry", null);
        var retry = await client.PostAsync($"/api/v1/requests/{requestId}/shippo/send", null);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var fixedLabel = await LabelAsync(requestId);
        Assert.Equal("order_retry", fixedLabel.ShippoOrderId);
        Assert.Null(fixedLabel.ShippoSyncError);

        // A second send must not create a duplicate order in Shippo.
        var calls = fake.Calls;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/requests/{requestId}/shippo/send", null)).StatusCode);
        Assert.Equal(calls, fake.Calls);
    }

    [Fact]
    public async Task ShippoNotConfigured_IsSilent_AndSendIsRejected()
    {
        var fake = new FakeShippo(false, () => throw new InvalidOperationException("must not be called"));
        var client = await ClientWithAsync(fake);
        var requestId = await NewRequestAsync(wantsPackage: true);

        Assert.Equal(HttpStatusCode.OK, await MoveToStageAsync(client, requestId, ProcessStage.Packing));
        var label = await LabelAsync(requestId);
        Assert.Null(label.ShippoOrderId);
        Assert.Null(label.ShippoSyncError);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/v1/requests/{requestId}/shippo/send", null)).StatusCode);
    }
}
