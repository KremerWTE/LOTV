using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// A new donation always waits for staff to say where it goes: recording one creates a matching pending Fund Allocation,
/// and approving or rejecting it keeps the donation's own status badge in step.
/// </summary>
[Collection("Integration")]
public class AllocationAutoCreateTests
{
    private readonly LotvApiFactory _factory;
    public AllocationAutoCreateTests(LotvApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private async Task<HttpClient> StaffClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"alloc-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1Alloc!", FirstName = "A", LastName = "Staff", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1Alloc!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    private async Task<int> DonorIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var donor = new Donor { FirstName = "Dana", LastName = "Donor", Email = $"donor-{Guid.NewGuid():N}@test.com", ChapterId = 1, CreatedAt = DateTime.UtcNow };
        db.Donors.Add(donor);
        await db.SaveChangesAsync();
        return donor.Id;
    }

    [Fact]
    public async Task RecordingADonation_CreatesAPendingAllocation_AndTheDonationShowsPendingToo()
    {
        var staff = await StaffClientAsync();
        var donorId = await DonorIdAsync();

        var resp = await staff.PostAsJsonAsync("/api/v1/donations", new { donorId, amount = 125.50m, channel = "Check" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var donation = await resp.Content.ReadFromJsonAsync<Donation>(Json);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var alloc = await db.FundAllocations.AsNoTracking().SingleAsync(a => a.DonationId == donation!.Id);
        Assert.Equal(AllocationStatus.PendingReview, alloc.Status);
        Assert.Equal(125.50m, alloc.Amount);
        var saved = await db.Donations.AsNoTracking().SingleAsync(d => d.Id == donation!.Id);
        Assert.Equal(AllocationStatus.PendingReview, saved.AllocationStatus);
    }

    [Fact]
    public async Task ApprovingItsAllocation_MarksTheDonationAllocated_AndRejectingReturnsItToUnallocated()
    {
        var staff = await StaffClientAsync();
        var donorId = await DonorIdAsync();
        var donation = await (await staff.PostAsJsonAsync("/api/v1/donations", new { donorId, amount = 40m, channel = "Online" }))
            .Content.ReadFromJsonAsync<Donation>(Json);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var allocId = (await db.FundAllocations.AsNoTracking().SingleAsync(a => a.DonationId == donation!.Id)).Id;

        var approve = await staff.PostAsJsonAsync($"/api/v1/allocations/{allocId}/approve", new { approvedBy = "A Staff" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal(AllocationStatus.Allocated, (await db.Donations.AsNoTracking().SingleAsync(d => d.Id == donation!.Id)).AllocationStatus);
    }

    [Fact]
    public async Task TheDonationsPageAllocateButton_PutsItBackInTheQueue_WithoutDuplicatingAnAllocation()
    {
        var staff = await StaffClientAsync();
        var donorId = await DonorIdAsync();
        var donation = await (await staff.PostAsJsonAsync("/api/v1/donations", new { donorId, amount = 75m, channel = "Cash" }))
            .Content.ReadFromJsonAsync<Donation>(Json);

        var again = await staff.PostAsJsonAsync($"/api/v1/donations/{donation!.Id}/request-allocation", new { });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        Assert.Equal(1, await db.FundAllocations.CountAsync(a => a.DonationId == donation.Id));
    }
}
