using Lotv.Core.Models;

namespace Lotv.Core.Services;

public class MockDataService : IMockDataService
{
    private readonly List<Family> _families;
    private readonly List<PackageRequest> _requests;
    private readonly List<Donor> _donors;
    private readonly List<Donation> _donations;
    private readonly List<Volunteer> _volunteers;
    private readonly List<MinistryEvent> _events;
    private readonly List<Parish> _parishes;
    private readonly List<Diocese> _dioceses;
    private readonly List<FundAllocation> _allocations;
    private readonly List<AuditEntry> _auditLog;

    public MockDataService()
    {
        _dioceses = SeedDioceses();
        _parishes = SeedParishes();
        _families = SeedFamilies();
        _requests = SeedRequests();
        _donors = SeedDonors();
        _donations = SeedDonations();
        _volunteers = SeedVolunteers();
        _events = SeedEvents();
        _allocations = SeedAllocations();
        _auditLog = SeedAuditLog();
    }

    // ── Families ──────────────────────────────────────────────────────────────
    public List<Family> GetFamilies() => _families;
    public Family? GetFamily(int id) => _families.FirstOrDefault(f => f.Id == id);
    public void AddFamily(Family f) { f.Id = _families.Max(x => x.Id) + 1; _families.Add(f); }
    public void UpdateFamily(Family f) { var i = _families.FindIndex(x => x.Id == f.Id); if (i >= 0) _families[i] = f; }

    // ── Requests ──────────────────────────────────────────────────────────────
    public List<PackageRequest> GetRequests() => _requests;
    public PackageRequest? GetRequest(int id) => _requests.FirstOrDefault(r => r.Id == id);
    public void AddRequest(PackageRequest r) { r.Id = _requests.Count > 0 ? _requests.Max(x => x.Id) + 1 : 1; _requests.Add(r); }
    public void UpdateRequest(PackageRequest r) { var i = _requests.FindIndex(x => x.Id == r.Id); if (i >= 0) _requests[i] = r; }

    public Volunteer? GetNextAvailableVolunteer()
    {
        var active = _volunteers.Where(v => v.Status == VolunteerStatus.Active).ToList();
        if (active.Count == 0) return null;
        var openByVol = _requests
            .Where(r => r.Status is CaseStatus.New or CaseStatus.InProgress or CaseStatus.AwaitingShipment)
            .GroupBy(r => r.AssignedTo ?? "")
            .ToDictionary(g => g.Key, g => g.Count());
        return active.OrderBy(v => openByVol.GetValueOrDefault(v.FullName, 0)).First();
    }

    // ── Donors ────────────────────────────────────────────────────────────────
    public List<Donor> GetDonors() => _donors;
    public Donor? GetDonor(int id) => _donors.FirstOrDefault(d => d.Id == id);
    public void AddDonor(Donor d) { d.Id = _donors.Max(x => x.Id) + 1; _donors.Add(d); }
    public void UpdateDonor(Donor d) { var i = _donors.FindIndex(x => x.Id == d.Id); if (i >= 0) _donors[i] = d; }

    // ── Donations ─────────────────────────────────────────────────────────────
    public List<Donation> GetDonations() => _donations;
    public void AddDonation(Donation d) { d.Id = _donations.Max(x => x.Id) + 1; _donations.Add(d); }

    // ── Volunteers ────────────────────────────────────────────────────────────
    public List<Volunteer> GetVolunteers() => _volunteers;
    public void AddVolunteer(Volunteer v) { v.Id = _volunteers.Max(x => x.Id) + 1; _volunteers.Add(v); }
    public void UpdateVolunteer(Volunteer v) { var i = _volunteers.FindIndex(x => x.Id == v.Id); if (i >= 0) _volunteers[i] = v; }

    // ── Events ────────────────────────────────────────────────────────────────
    public List<MinistryEvent> GetEvents() => _events;
    public void AddEvent(MinistryEvent e) { e.Id = _events.Count > 0 ? _events.Max(x => x.Id) + 1 : 1; _events.Add(e); }
    public void UpdateEvent(MinistryEvent e) { var i = _events.FindIndex(x => x.Id == e.Id); if (i >= 0) _events[i] = e; }

    // ── Parishes & Dioceses ───────────────────────────────────────────────────
    public List<Parish> GetParishes() => _parishes;
    public List<Diocese> GetDioceses() => _dioceses;

    // ── Allocations ───────────────────────────────────────────────────────────
    public List<FundAllocation> GetAllocations() => _allocations;
    public void UpdateAllocation(FundAllocation a) { var i = _allocations.FindIndex(x => x.Id == a.Id); if (i >= 0) _allocations[i] = a; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public List<AuditEntry> GetAuditLog() => _auditLog.OrderByDescending(a => a.Timestamp).ToList();
    public void LogAction(string userName, string action, string entity, string? entityId = null, string? details = null)
    {
        _auditLog.Add(new AuditEntry
        {
            Id = _auditLog.Count + 1,
            UserName = userName,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }

    // ── Dashboard Stats ───────────────────────────────────────────────────────
    public DashboardStats GetDashboardStats()
    {
        var now = DateTime.UtcNow;
        var openCases = _requests.Count(r => r.Status is CaseStatus.New or CaseStatus.InProgress or CaseStatus.AwaitingShipment);
        var overdue = _requests.Count(r => r.IsOverdue);
        var thisMonth = _donations.Where(d => d.Date.Year == now.Year && d.Date.Month == now.Month).Sum(d => d.Amount);
        var lastMonth = _donations.Where(d => d.Date.Year == now.AddMonths(-1).Year && d.Date.Month == now.AddMonths(-1).Month).Sum(d => d.Amount);
        var ytd = _requests.Count(r => r.Status == CaseStatus.Fulfilled && r.ShippedDate?.Year == now.Year);
        var activeVols = _volunteers.Count(v => v.Status == VolunteerStatus.Active);
        var upcomingEvents = _events.Count(e => e.Date >= now);
        var last30 = _requests.Count(r => r.Status == CaseStatus.Fulfilled && r.ShippedDate >= now.AddDays(-30));
        var unallocated = _donations.Where(d => d.AllocationStatus == AllocationStatus.Unallocated).Sum(d => d.Amount);
        var pendingAlloc = _allocations.Count(a => a.Status == AllocationStatus.PendingReview);

        return new DashboardStats(openCases, overdue, thisMonth, lastMonth, ytd, activeVols, upcomingEvents, last30, unallocated, pendingAlloc);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SEED DATA
    // ═══════════════════════════════════════════════════════════════════════════

    private static List<Diocese> SeedDioceses() =>
    [
        new() { Id = 1, Name = "Diocese of Raleigh", State = "NC", CoordinatorName = "Anne Bergman", CoordinatorEmail = "abergman@docraleigh.org", TotalParishes = 8, ActiveParishes = 5, TotalCasesFulfilled = 34, TotalDonations = 12400 },
        new() { Id = 2, Name = "Diocese of Charlotte", State = "NC", CoordinatorName = "Maria Santos", CoordinatorEmail = "msantos@charlottediocese.org", TotalParishes = 6, ActiveParishes = 4, TotalCasesFulfilled = 18, TotalDonations = 8600 },
        new() { Id = 3, Name = "Archdiocese of Baltimore", State = "MD", CoordinatorName = "Fr. Thomas Reilly", CoordinatorEmail = "treilly@archbalt.org", TotalParishes = 5, ActiveParishes = 3, TotalCasesFulfilled = 10, TotalDonations = 5200 },
        new() { Id = 4, Name = "Diocese of Richmond", State = "VA", CoordinatorName = "Lisa Chen", CoordinatorEmail = "lchen@richmonddiocese.org", TotalParishes = 4, ActiveParishes = 2, TotalCasesFulfilled = 8, TotalDonations = 3800 },
    ];

    private static List<Parish> SeedParishes() =>
    [
        new() { Id = 1, Name = "St. Michael the Archangel", DioceseId = 1, DioceseName = "Diocese of Raleigh", LiaisonName = "Sarah O'Brien", LiaisonEmail = "sobrien@stmichaelraleigh.org", CertificationLevel = CertificationLevel.Level2, ActiveCases = 4, TotalCasesFulfilled = 12, Status = ParishStatus.Active, EnrolledDate = new DateTime(2022, 9, 1) },
        new() { Id = 2, Name = "St. Raphael Catholic Church", DioceseId = 1, DioceseName = "Diocese of Raleigh", LiaisonName = "Deacon Paul Moretti", LiaisonEmail = "pmoretti@straphaelraleigh.org", CertificationLevel = CertificationLevel.Level3, ActiveCases = 6, TotalCasesFulfilled = 18, Status = ParishStatus.Active, EnrolledDate = new DateTime(2022, 7, 15) },
        new() { Id = 3, Name = "St. Catherine of Siena", DioceseId = 1, DioceseName = "Diocese of Raleigh", LiaisonName = "Jennifer Walsh", LiaisonEmail = "jwalsh@stcatherineknights.org", CertificationLevel = CertificationLevel.Level1, ActiveCases = 2, TotalCasesFulfilled = 4, Status = ParishStatus.Active, EnrolledDate = new DateTime(2023, 2, 1) },
        new() { Id = 4, Name = "Holy Name of Jesus", DioceseId = 2, DioceseName = "Diocese of Charlotte", LiaisonName = "Dr. Patrice Dunn", LiaisonEmail = "pdunn@holyname.org", CertificationLevel = CertificationLevel.Level2, ActiveCases = 3, TotalCasesFulfilled = 9, Status = ParishStatus.Active, EnrolledDate = new DateTime(2023, 1, 10) },
        new() { Id = 5, Name = "Our Lady of Lourdes", DioceseId = 2, DioceseName = "Diocese of Charlotte", LiaisonName = "Rosa Martinez", LiaisonEmail = "rmartinez@oll-charlotte.org", CertificationLevel = CertificationLevel.Level1, ActiveCases = 1, TotalCasesFulfilled = 5, Status = ParishStatus.Active, EnrolledDate = new DateTime(2023, 4, 20) },
        new() { Id = 6, Name = "St. Ann Parish", DioceseId = 3, DioceseName = "Archdiocese of Baltimore", LiaisonName = "Kevin Murphy", LiaisonEmail = "kmurphy@stannbalt.org", CertificationLevel = CertificationLevel.Level1, ActiveCases = 2, TotalCasesFulfilled = 6, Status = ParishStatus.Active, EnrolledDate = new DateTime(2023, 6, 5) },
        new() { Id = 7, Name = "St. Thomas More", DioceseId = 4, DioceseName = "Diocese of Richmond", LiaisonName = "Amanda Foster", LiaisonEmail = "afoster@stmrichmond.org", CertificationLevel = CertificationLevel.Level1, ActiveCases = 1, TotalCasesFulfilled = 3, Status = ParishStatus.Active, EnrolledDate = new DateTime(2023, 8, 12) },
        new() { Id = 8, Name = "Sacred Heart Parish", DioceseId = 1, DioceseName = "Diocese of Raleigh", LiaisonName = null, LiaisonEmail = null, CertificationLevel = CertificationLevel.None, ActiveCases = 0, TotalCasesFulfilled = 0, Status = ParishStatus.Pending, EnrolledDate = new DateTime(2024, 1, 3) },
    ];

    private static List<Family> SeedFamilies()
    {
        var base_ = new DateTime(2024, 1, 1);
        return
        [
            new() { Id = 1, Parent1FirstName = "Emily", Parent1LastName = "Hartmann", Parent2FirstName = "James", Email = "emily.hartmann@email.com", Phone = "919-555-0101", StreetAddress = "412 Elm St", City = "Raleigh", State = "NC", Zip = "27601", Reason = PackageReason.Miscarriage, ChildrenInitials = "O.J.H.", ParishName = "St. Raphael Catholic Church", DioceseName = "Diocese of Raleigh", CreatedAt = base_.AddDays(5), Status = FamilyStatus.Active },
            new() { Id = 2, Parent1FirstName = "Maria", Parent1LastName = "Santos", Parent2FirstName = "Carlos", Email = "maria.santos@email.com", Phone = "704-555-0202", StreetAddress = "88 Oak Ave", City = "Charlotte", State = "NC", Zip = "28201", Reason = PackageReason.PrenatalDiagnosis, ChildrenInitials = "L.M.S.", ParishName = "Holy Name of Jesus", DioceseName = "Diocese of Charlotte", CreatedAt = base_.AddDays(12), Status = FamilyStatus.Active },
            new() { Id = 3, Parent1FirstName = "Rachel", Parent1LastName = "Kim", Email = "rachel.kim@email.com", Phone = "919-555-0303", StreetAddress = "310 Maple Dr", City = "Cary", State = "NC", Zip = "27511", Reason = PackageReason.Infertility, ParishName = "St. Michael the Archangel", DioceseName = "Diocese of Raleigh", CreatedAt = base_.AddDays(18), Status = FamilyStatus.Active },
            new() { Id = 4, Parent1FirstName = "Grace", Parent1LastName = "Thompson", Parent2FirstName = "Daniel", Email = "grace.thompson@email.com", Phone = "410-555-0404", StreetAddress = "57 Birch Ln", City = "Baltimore", State = "MD", Zip = "21201", Reason = PackageReason.Stillbirth, ChildrenInitials = "A.E.T.", ParishName = "St. Ann Parish", DioceseName = "Archdiocese of Baltimore", CreatedAt = base_.AddDays(22), Status = FamilyStatus.Active },
            new() { Id = 5, Parent1FirstName = "Sofia", Parent1LastName = "Reyes", Email = "sofia.reyes@email.com", Phone = "704-555-0505", StreetAddress = "1204 Pine St", City = "Concord", State = "NC", Zip = "28025", Reason = PackageReason.InfantLoss, ChildrenInitials = "I.R.", ParishName = "Our Lady of Lourdes", DioceseName = "Diocese of Charlotte", CreatedAt = base_.AddDays(30), Status = FamilyStatus.Active },
            new() { Id = 6, Parent1FirstName = "Claire", Parent1LastName = "O'Sullivan", Parent2FirstName = "Patrick", Email = "claire.osullivan@email.com", Phone = "919-555-0606", StreetAddress = "900 Cedar Blvd", City = "Durham", State = "NC", Zip = "27701", Reason = PackageReason.PrenatalLifeLimitingDiagnosis, ChildrenInitials = "F.O.", ParishName = "St. Catherine of Siena", DioceseName = "Diocese of Raleigh", CreatedAt = base_.AddDays(40), Status = FamilyStatus.Active },
            new() { Id = 7, Parent1FirstName = "Hana", Parent1LastName = "Yoshida", Email = "hana.yoshida@email.com", Phone = "804-555-0707", StreetAddress = "73 Willow Way", City = "Richmond", State = "VA", Zip = "23218", Reason = PackageReason.Miscarriage, ParishName = "St. Thomas More", DioceseName = "Diocese of Richmond", CreatedAt = base_.AddDays(45), Status = FamilyStatus.Active },
            new() { Id = 8, Parent1FirstName = "Nadia", Parent1LastName = "Kowalski", Parent2FirstName = "Mark", Email = "nadia.kowalski@email.com", Phone = "919-555-0808", StreetAddress = "245 Aspen Ct", City = "Apex", State = "NC", Zip = "27502", Reason = PackageReason.PastLoss, ChildrenInitials = "B.K. T.K.", ParishName = "St. Michael the Archangel", DioceseName = "Diocese of Raleigh", CreatedAt = base_.AddDays(52), Status = FamilyStatus.Active },
            new() { Id = 9, Parent1FirstName = "Isabelle", Parent1LastName = "Fontaine", Email = "isabelle.fontaine@email.com", Phone = "704-555-0909", StreetAddress = "16 Magnolia Pl", City = "Huntersville", State = "NC", Zip = "28078", Reason = PackageReason.Infertility, ParishName = "Holy Name of Jesus", DioceseName = "Diocese of Charlotte", CreatedAt = base_.AddDays(60), Status = FamilyStatus.Active },
            new() { Id = 10, Parent1FirstName = "Abigail", Parent1LastName = "Monroe", Parent2FirstName = "Thomas", Email = "abigail.monroe@email.com", Phone = "919-555-1010", StreetAddress = "521 Rosewood Ave", City = "Raleigh", State = "NC", Zip = "27604", Reason = PackageReason.Stillbirth, ChildrenInitials = "C.J.M.", ParishName = "St. Raphael Catholic Church", DioceseName = "Diocese of Raleigh", CreatedAt = base_.AddDays(68), Status = FamilyStatus.Active },
        ];
    }

    private static List<PackageRequest> SeedRequests()
    {
        var now = DateTime.UtcNow;
        return
        [
            new() { Id = 1, FamilyId = 1, Reason = PackageReason.Miscarriage, Status = CaseStatus.Fulfilled, AssignedTo = "Mary Roberts", CreatedAt = now.AddDays(-45), ShippedDate = now.AddDays(-40), TrackingNumber = "USPS92345678901234" },
            new() { Id = 2, FamilyId = 2, Reason = PackageReason.PrenatalDiagnosis, Status = CaseStatus.Shipped, AssignedTo = "Mary Roberts", CreatedAt = now.AddDays(-10), ShippedDate = now.AddDays(-3), TrackingNumber = "USPS92345678902345" },
            new() { Id = 3, FamilyId = 3, Reason = PackageReason.Infertility, Status = CaseStatus.InProgress, AssignedTo = "John Davis", CreatedAt = now.AddDays(-14), InternalNotes = "Assembling package. Lily oil on order." },
            new() { Id = 4, FamilyId = 4, Reason = PackageReason.Stillbirth, Status = CaseStatus.New, AssignedTo = null, CreatedAt = now.AddDays(-2) },
            new() { Id = 5, FamilyId = 5, Reason = PackageReason.InfantLoss, Status = CaseStatus.AwaitingShipment, AssignedTo = "Mary Roberts", CreatedAt = now.AddDays(-6) },
            new() { Id = 6, FamilyId = 6, Reason = PackageReason.PrenatalLifeLimitingDiagnosis, Status = CaseStatus.InProgress, AssignedTo = "Sarah Lee", CreatedAt = now.AddDays(-9) },
            new() { Id = 7, FamilyId = 7, Reason = PackageReason.Miscarriage, Status = CaseStatus.New, AssignedTo = null, CreatedAt = now.AddDays(-1) },
            new() { Id = 8, FamilyId = 8, Reason = PackageReason.PastLoss, Status = CaseStatus.Fulfilled, AssignedTo = "John Davis", CreatedAt = now.AddDays(-30), ShippedDate = now.AddDays(-24) },
            new() { Id = 9, FamilyId = 9, Reason = PackageReason.Infertility, Status = CaseStatus.OnHold, AssignedTo = "Sarah Lee", CreatedAt = now.AddDays(-18), InternalNotes = "Address undeliverable — awaiting updated address." },
            new() { Id = 10, FamilyId = 10, Reason = PackageReason.Stillbirth, Status = CaseStatus.InProgress, AssignedTo = "Mary Roberts", CreatedAt = now.AddDays(-5) },
            // Additional open cases to reach ~24 total open
            new() { Id = 11, FamilyId = 1, Reason = PackageReason.PastLoss, Status = CaseStatus.New, CreatedAt = now.AddDays(-3) },
            new() { Id = 12, FamilyId = 3, Reason = PackageReason.Infertility, Status = CaseStatus.New, CreatedAt = now.AddDays(-8) },
            new() { Id = 13, FamilyId = 2, Reason = PackageReason.PrenatalDiagnosis, Status = CaseStatus.InProgress, AssignedTo = "John Davis", CreatedAt = now.AddDays(-11) },
            new() { Id = 14, FamilyId = 5, Reason = PackageReason.InfantLoss, Status = CaseStatus.New, CreatedAt = now.AddDays(-4) },
            new() { Id = 15, FamilyId = 7, Reason = PackageReason.Miscarriage, Status = CaseStatus.AwaitingShipment, AssignedTo = "Sarah Lee", CreatedAt = now.AddDays(-16) },
        ];
    }

    private static List<Donor> SeedDonors() =>
    [
        new() { Id = 1, FirstName = "Patricia", LastName = "Callahan", Email = "pcallahan@email.com", Phone = "919-555-2001", City = "Raleigh", State = "NC", ParishName = "St. Raphael Catholic Church", IsRecurring = true, RecurringAmount = 100, FirstGiftDate = new DateTime(2022, 8, 1), LastGiftDate = DateTime.UtcNow.AddDays(-5), TotalGiven = 1800, GiftCount = 18, Tier = DonorTier.Champion },
        new() { Id = 2, FirstName = "Robert", LastName = "Fitzgerald", Email = "rfitz@email.com", Phone = "704-555-2002", City = "Charlotte", State = "NC", IsRecurring = false, FirstGiftDate = new DateTime(2023, 3, 15), LastGiftDate = DateTime.UtcNow.AddDays(-30), TotalGiven = 750, GiftCount = 3, Tier = DonorTier.Supporter },
        new() { Id = 3, FirstName = "Margaret & William", LastName = "Donovan", Email = "mwdonovan@email.com", City = "Raleigh", State = "NC", ParishName = "St. Michael the Archangel", IsRecurring = true, RecurringAmount = 250, FirstGiftDate = new DateTime(2022, 9, 1), LastGiftDate = DateTime.UtcNow.AddDays(-2), TotalGiven = 6000, GiftCount = 24, Tier = DonorTier.Benefactor },
        new() { Id = 4, FirstName = "Teresa", LastName = "Nguyen", Email = "tnguyen@email.com", Phone = "919-555-2004", City = "Durham", State = "NC", IsRecurring = false, FirstGiftDate = new DateTime(2023, 12, 1), LastGiftDate = DateTime.UtcNow.AddDays(-60), TotalGiven = 200, GiftCount = 2, Tier = DonorTier.Friend },
        new() { Id = 5, FirstName = "Dr. Anthony", LastName = "Moreau", Email = "amoreau@email.com", Phone = "410-555-2005", City = "Baltimore", State = "MD", IsRecurring = true, RecurringAmount = 500, FirstGiftDate = new DateTime(2023, 1, 10), LastGiftDate = DateTime.UtcNow.AddDays(-10), TotalGiven = 7000, GiftCount = 14, Tier = DonorTier.Benefactor },
        new() { Id = 6, FirstName = "Linda", LastName = "Vasquez", Email = "lvasquez@email.com", City = "Apex", State = "NC", IsRecurring = false, FirstGiftDate = new DateTime(2024, 2, 14), LastGiftDate = DateTime.UtcNow.AddDays(-20), TotalGiven = 50, GiftCount = 1, Tier = DonorTier.Friend },
        new() { Id = 7, FirstName = "John & Karen", LastName = "O'Brien", Email = "jkobrien@email.com", Phone = "919-555-2007", City = "Cary", State = "NC", ParishName = "St. Catherine of Siena", IsRecurring = true, RecurringAmount = 75, FirstGiftDate = new DateTime(2023, 5, 1), LastGiftDate = DateTime.UtcNow.AddDays(-8), TotalGiven = 900, GiftCount = 12, Tier = DonorTier.Supporter },
        new() { Id = 8, FirstName = "Sister Mary", LastName = "Benedetta", Email = "smbenedetta@convent.org", City = "Richmond", State = "VA", IsRecurring = false, FirstGiftDate = new DateTime(2023, 4, 28), LastGiftDate = DateTime.UtcNow.AddDays(-90), TotalGiven = 300, GiftCount = 3, Tier = DonorTier.Supporter },
    ];

    private static List<Donation> SeedDonations()
    {
        var now = DateTime.UtcNow;
        return
        [
            new() { Id = 1, DonorId = 3, Amount = 250, Date = now.AddDays(-2), Channel = DonationChannel.Online, Campaign = "Monthly Giving", IsRecurring = true, AllocationStatus = AllocationStatus.Allocated, AllocatedTo = "General Fund" },
            new() { Id = 2, DonorId = 1, Amount = 100, Date = now.AddDays(-5), Channel = DonationChannel.Online, Campaign = "Monthly Giving", IsRecurring = true, AllocationStatus = AllocationStatus.Allocated, AllocatedTo = "Package Supplies" },
            new() { Id = 3, DonorId = 5, Amount = 500, Date = now.AddDays(-10), Channel = DonationChannel.Online, Campaign = "Monthly Giving", IsRecurring = true, AllocationStatus = AllocationStatus.Allocated, AllocatedTo = "General Fund" },
            new() { Id = 4, DonorId = 7, Amount = 75, Date = now.AddDays(-8), Channel = DonationChannel.Online, Campaign = "Monthly Giving", IsRecurring = true, AllocationStatus = AllocationStatus.Unallocated },
            new() { Id = 5, DonorId = 2, Amount = 250, Date = now.AddDays(-30), Channel = DonationChannel.Check, Campaign = "Year-End Appeal", AllocationStatus = AllocationStatus.Allocated, AllocatedTo = "Package Supplies" },
            new() { Id = 6, DonorId = 4, Amount = 100, Date = now.AddDays(-20), Channel = DonationChannel.Online, AllocationStatus = AllocationStatus.Unallocated },
            new() { Id = 7, DonorId = 8, Amount = 100, Date = now.AddDays(-90), Channel = DonationChannel.Check, Campaign = "Feast Day Appeal", AllocationStatus = AllocationStatus.Allocated, AllocatedTo = "General Fund" },
            new() { Id = 8, DonorId = 6, Amount = 50, Date = now.AddDays(-20), Channel = DonationChannel.Online, AllocationStatus = AllocationStatus.Unallocated },
            // Last month donations
            new() { Id = 9, DonorId = 3, Amount = 250, Date = now.AddMonths(-1).AddDays(-2), Channel = DonationChannel.Online, Campaign = "Monthly Giving", IsRecurring = true, AllocationStatus = AllocationStatus.Allocated },
            new() { Id = 10, DonorId = 1, Amount = 100, Date = now.AddMonths(-1).AddDays(-5), Channel = DonationChannel.Online, Campaign = "Monthly Giving", IsRecurring = true, AllocationStatus = AllocationStatus.Allocated },
            new() { Id = 11, DonorId = 5, Amount = 500, Date = now.AddMonths(-1).AddDays(-10), Channel = DonationChannel.Online, Campaign = "Monthly Giving", IsRecurring = true, AllocationStatus = AllocationStatus.Allocated },
            new() { Id = 12, DonorId = 7, Amount = 75, Date = now.AddMonths(-1).AddDays(-8), Channel = DonationChannel.Online, IsRecurring = true, AllocationStatus = AllocationStatus.Allocated },
            // Gala donations
            new() { Id = 13, DonorId = 3, Amount = 900, Date = now.AddMonths(-2), Channel = DonationChannel.Gala, Campaign = "Annual Gala 2025", AllocationStatus = AllocationStatus.Allocated, AllocatedTo = "Capital Campaign" },
            new() { Id = 14, DonorId = 5, Amount = 1000, Date = now.AddMonths(-2), Channel = DonationChannel.Gala, Campaign = "Annual Gala 2025", AllocationStatus = AllocationStatus.Allocated, AllocatedTo = "Capital Campaign" },
            new() { Id = 15, DonorId = 2, Amount = 225, Date = now.AddMonths(-2), Channel = DonationChannel.Gala, Campaign = "Annual Gala 2025", AllocationStatus = AllocationStatus.Allocated },
            new() { Id = 16, DonorId = 5, Amount = 3500, Date = now.AddDays(-15), Channel = DonationChannel.Online, Campaign = "Capital Campaign", AllocationStatus = AllocationStatus.PendingReview },
        ];
    }

    private static List<Volunteer> SeedVolunteers() =>
    [
        new() { Id = 1, FirstName = "Mary", LastName = "Roberts", Email = "mroberts@lotvministry.org", Phone = "919-555-3001", Role = VolunteerRole.Admin, Status = VolunteerStatus.Active, ParishName = "St. Raphael Catholic Church", DioceseName = "Diocese of Raleigh", ActiveCases = 5, TotalCasesFulfilled = 22, JoinedDate = new DateTime(2022, 7, 1) },
        new() { Id = 2, FirstName = "John", LastName = "Davis", Email = "jdavis@email.com", Phone = "919-555-3002", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ParishName = "St. Michael the Archangel", DioceseName = "Diocese of Raleigh", ActiveCases = 3, TotalCasesFulfilled = 15, JoinedDate = new DateTime(2022, 10, 1) },
        new() { Id = 3, FirstName = "Sarah", LastName = "Lee", Email = "slee@email.com", Phone = "704-555-3003", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ParishName = "Holy Name of Jesus", DioceseName = "Diocese of Charlotte", ActiveCases = 3, TotalCasesFulfilled = 10, JoinedDate = new DateTime(2023, 2, 14) },
        new() { Id = 4, FirstName = "Fr. Michael", LastName = "Quinn", Email = "frmquinn@straphael.org", Role = VolunteerRole.PrayerAmbassador, Status = VolunteerStatus.Active, ParishName = "St. Raphael Catholic Church", DioceseName = "Diocese of Raleigh", ActiveCases = 0, TotalCasesFulfilled = 0, JoinedDate = new DateTime(2022, 8, 1) },
        new() { Id = 5, FirstName = "Elena", LastName = "Petrov", Email = "epetrov@email.com", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ParishName = "St. Catherine of Siena", DioceseName = "Diocese of Raleigh", ActiveCases = 2, TotalCasesFulfilled = 7, JoinedDate = new DateTime(2023, 6, 1) },
        new() { Id = 6, FirstName = "Thomas", LastName = "Walsh", Email = "twalsh@email.com", Role = VolunteerRole.Driver, Status = VolunteerStatus.Active, ParishName = "St. Michael the Archangel", DioceseName = "Diocese of Raleigh", ActiveCases = 1, TotalCasesFulfilled = 8, JoinedDate = new DateTime(2023, 3, 20) },
        new() { Id = 7, FirstName = "Camille", LastName = "Tran", Email = "ctran@email.com", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Onboarding, ParishName = "Our Lady of Lourdes", DioceseName = "Diocese of Charlotte", ActiveCases = 0, TotalCasesFulfilled = 0, JoinedDate = new DateTime(2024, 3, 1) },
        new() { Id = 8, FirstName = "Derek", LastName = "Simmons", Email = "dsimmons@email.com", Role = VolunteerRole.EventHelper, Status = VolunteerStatus.Active, ActiveCases = 0, TotalCasesFulfilled = 0, JoinedDate = new DateTime(2023, 9, 5) },
    ];

    private static List<MinistryEvent> SeedEvents()
    {
        var now = DateTime.UtcNow;
        return
        [
            new() { Id = 1, Title = "Monthly Prayer Night — 28th", Type = EventType.PrayerNight, Date = new DateTime(now.Year, now.Month, 28, 20, 0, 0), Location = "Zoom (Virtual)", IsVirtual = true, MeetingLink = "https://zoom.us/lotv-prayer", Description = "Monthly community prayer night. All are welcome. We pray for every family on our prayer list.", Capacity = null, Registered = 47 },
            new() { Id = 2, Title = "Quarterly In-Person Gathering — Raleigh", Type = EventType.InPersonGathering, Date = now.AddDays(18), Location = "St. Raphael Catholic Church, Raleigh, NC", IsVirtual = false, Description = "In-person gathering for Prayer Ambassadors and supporters in the Raleigh area.", Capacity = 80, Registered = 34 },
            new() { Id = 3, Title = "Feast of St. Gianna Molla", Type = EventType.InPersonGathering, Date = new DateTime(now.Year, 4, 28, 18, 0, 0), Location = "St. Raphael Catholic Church, Raleigh, NC", IsVirtual = false, Description = "Annual celebration of the Feast Day of St. Gianna Beretta Molla, patroness of LOTV Ministry. Mass + reception. Relic veneration available.", Capacity = 150, Registered = 92 },
            new() { Id = 4, Title = "Annual Gala & Silent Auction", Type = EventType.Gala, Date = new DateTime(now.Year, 5, 10, 18, 0, 0), Location = "TBD — Raleigh, NC", IsVirtual = false, Description = "LOTV Ministry's annual fundraising gala. Dinner, silent auction, live auction, and testimonials.", Capacity = 200, Registered = 118 },
        ];
    }

    private static List<FundAllocation> SeedAllocations() =>
    [
        new() { Id = 1, DonationId = 16, AllocatedTo = "Capital Campaign — Building Fund", Amount = 3500, Status = AllocationStatus.PendingReview, CreatedAt = DateTime.UtcNow.AddDays(-15) },
        new() { Id = 2, DonationId = 4, AllocatedTo = "Package Supplies — Q2", Amount = 75, Status = AllocationStatus.PendingReview, CreatedAt = DateTime.UtcNow.AddDays(-8) },
        new() { Id = 3, DonationId = 6, AllocatedTo = "General Fund", Amount = 100, Status = AllocationStatus.PendingReview, CreatedAt = DateTime.UtcNow.AddDays(-20) },
        new() { Id = 4, DonationId = 1, AllocatedTo = "General Fund", Amount = 250, Status = AllocationStatus.Allocated, ApprovedBy = "Mary Roberts", ApprovedAt = DateTime.UtcNow.AddDays(-1) },
        new() { Id = 5, DonationId = 13, AllocatedTo = "Capital Campaign", Amount = 900, Status = AllocationStatus.Allocated, ApprovedBy = "Mary Roberts", ApprovedAt = DateTime.UtcNow.AddDays(-30) },
    ];

    private static List<AuditEntry> SeedAuditLog() =>
    [
        new() { Id = 1, UserName = "Mary Roberts", Action = "Created", Entity = "PackageRequest", EntityId = "15", Details = "New request for Family #7 — Miscarriage", Timestamp = DateTime.UtcNow.AddDays(-1).AddHours(-2) },
        new() { Id = 2, UserName = "John Davis", Action = "Updated", Entity = "PackageRequest", EntityId = "3", Details = "Status changed to InProgress", Timestamp = DateTime.UtcNow.AddDays(-1) },
        new() { Id = 3, UserName = "Mary Roberts", Action = "Approved", Entity = "FundAllocation", EntityId = "4", Details = "Allocated $250 to General Fund", Timestamp = DateTime.UtcNow.AddDays(-1).AddMinutes(-30) },
        new() { Id = 4, UserName = "Sarah Lee", Action = "Updated", Entity = "PackageRequest", EntityId = "9", Details = "Status set to OnHold — address undeliverable", Timestamp = DateTime.UtcNow.AddHours(-5) },
        new() { Id = 5, UserName = "Mary Roberts", Action = "Created", Entity = "Donor", EntityId = "8", Details = "New donor: Sister Mary Benedetta", Timestamp = DateTime.UtcNow.AddDays(-3) },
        new() { Id = 6, UserName = "John Davis", Action = "Exported", Entity = "DonationReport", EntityId = null, Details = "CSV export — March 2026 donations", Timestamp = DateTime.UtcNow.AddDays(-2) },
    ];
}
