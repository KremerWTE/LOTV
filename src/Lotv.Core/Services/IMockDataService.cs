using Lotv.Core.Models;

namespace Lotv.Core.Services;

public interface IMockDataService
{
    // Families
    List<Family> GetFamilies();
    Family? GetFamily(int id);
    void AddFamily(Family family);
    void UpdateFamily(Family family);

    // Package Requests / Cases
    List<PackageRequest> GetRequests();
    PackageRequest? GetRequest(int id);
    void AddRequest(PackageRequest request);
    void UpdateRequest(PackageRequest request);
    Volunteer? GetNextAvailableVolunteer();

    // Donors
    List<Donor> GetDonors();
    Donor? GetDonor(int id);
    void AddDonor(Donor donor);
    void UpdateDonor(Donor donor);

    // Donations
    List<Donation> GetDonations();
    void AddDonation(Donation donation);

    // Volunteers
    List<Volunteer> GetVolunteers();
    void AddVolunteer(Volunteer volunteer);
    void UpdateVolunteer(Volunteer volunteer);

    // Events
    List<MinistryEvent> GetEvents();
    void AddEvent(MinistryEvent e);

    // Parishes & Dioceses
    List<Parish> GetParishes();
    List<Diocese> GetDioceses();

    // Allocations
    List<FundAllocation> GetAllocations();
    void UpdateAllocation(FundAllocation allocation);

    // Audit
    List<AuditEntry> GetAuditLog();
    void LogAction(string userName, string action, string entity, string? entityId = null, string? details = null);

    // Dashboard KPIs
    DashboardStats GetDashboardStats();
}

public record DashboardStats(
    int OpenCases,
    int OverdueCases,
    decimal DonationsThisMonth,
    decimal DonationsLastMonth,
    int FamiliesServedYtd,
    int ActiveVolunteers,
    int UpcomingEvents,
    int CasesFulfilledLast30Days,
    decimal UnallocatedFunds,
    int PendingAllocations
);
