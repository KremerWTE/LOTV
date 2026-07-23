// =============================================================================
// MOCK / SEED DATA — Development and demo use only.
// DO NOT load in production. All names, emails, phone numbers, and addresses
// are entirely fictitious. Financial figures are illustrative only.
//
// This data is seeded automatically when ASPNETCORE_ENVIRONMENT=Development
// and the Chapters table is empty. See Program.cs for the seed hook.
// =============================================================================

using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Lotv.Api.Data;

public static class DevSeedData
{
    /// <summary>
    /// Idempotent seed: skips entirely if any Chapter rows already exist.
    /// Safe to call on every startup in Development.
    /// </summary>
    public static async Task SeedAsync(LotvDbContext db, UserManager<LotvIdentityUser> userMgr)
    {
        await db.Database.EnsureCreatedAsync();
        if (db.Chapters.Any()) return;

        // ── MOCK DATA: Chapters ───────────────────────────────────────────────
        var chapterChicago    = new Chapter { Id = 1, Name = "Chicago Metro",    City = "Chicago",      State = "IL", ContactName = "Sister Mary Agnes",   ContactEmail = "chicago@lotv-demo.org",    ContactPhone = "+13125550101", IsActive = true, CreatedAt = new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc) };
        var chapterMilwaukee  = new Chapter { Id = 2, Name = "Milwaukee",        City = "Milwaukee",    State = "WI", ContactName = "Deacon Paul Brennan", ContactEmail = "milwaukee@lotv-demo.org",  ContactPhone = "+14145550102", IsActive = true, CreatedAt = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc) };
        var chapterTwinCities = new Chapter { Id = 3, Name = "Twin Cities",      City = "Minneapolis",  State = "MN", ContactName = "Dr. Sarah Kowalski",  ContactEmail = "twincities@lotv-demo.org", ContactPhone = "+16125550103", IsActive = true, CreatedAt = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        db.Chapters.AddRange(chapterChicago, chapterMilwaukee, chapterTwinCities);

        // ── MOCK DATA: Dioceses ───────────────────────────────────────────────
        var dioceseChicago = new Diocese { Id = 1, Name = "Archdiocese of Chicago",    City = "Chicago",   State = "IL", ChapterId = 1, CoordinatorName = "Fr. Thomas Reed",   CoordinatorEmail = "treed@aoc-demo.org",   TotalParishes = 12, ActiveParishes = 9,  TotalCasesFulfilled = 84,  TotalDonations = 42500m };
        var dioceseMKE     = new Diocese { Id = 2, Name = "Diocese of Milwaukee",      City = "Milwaukee", State = "WI", ChapterId = 2, CoordinatorName = "Fr. James Olsen",   CoordinatorEmail = "jolsen@dmke-demo.org", TotalParishes = 6,  ActiveParishes = 4,  TotalCasesFulfilled = 31,  TotalDonations = 18200m };
        var dioceseSTP     = new Diocese { Id = 3, Name = "Archdiocese of St. Paul",   City = "St. Paul",  State = "MN", ChapterId = 3, CoordinatorName = "Sr. Anne Nguyen", CoordinatorEmail = "anguyen@astp-demo.org",TotalParishes = 8,  ActiveParishes = 6,  TotalCasesFulfilled = 22,  TotalDonations = 11800m };
        db.Dioceses.AddRange(dioceseChicago, dioceseMKE, dioceseSTP);

        // ── MOCK DATA: Parishes ───────────────────────────────────────────────
        db.Parishes.AddRange(
            new Parish { Id = 1, Name = "St. Michael the Archangel", DioceseName = "Archdiocese of Chicago",  ChapterId = 1, DioceseId = 1, Status = ParishStatus.Active,  CertificationLevel = CertificationLevel.Level2, LiaisonName = "Deacon Frank Nowak",  LiaisonEmail = "fnowak@stmichael-demo.org", ActiveCases = 4, TotalCasesFulfilled = 31, EnrolledDate = new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Parish { Id = 2, Name = "Holy Name Cathedral",        DioceseName = "Archdiocese of Chicago",  ChapterId = 1, DioceseId = 1, Status = ParishStatus.Active,  CertificationLevel = CertificationLevel.Level3, LiaisonName = "Sr. Catherine Moore",  LiaisonEmail = "cmoore@hnc-demo.org",       ActiveCases = 2, TotalCasesFulfilled = 28, EnrolledDate = new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Parish { Id = 3, Name = "Our Lady of Sorrows",        DioceseName = "Archdiocese of Chicago",  ChapterId = 1, DioceseId = 1, Status = ParishStatus.Active,  CertificationLevel = CertificationLevel.Level1, LiaisonName = "Mrs. Helen Garrett",   LiaisonEmail = "hgarrett@ols-demo.org",     ActiveCases = 1, TotalCasesFulfilled = 12, EnrolledDate = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Parish { Id = 4, Name = "St. Alphonsus",              DioceseName = "Diocese of Milwaukee",    ChapterId = 2, DioceseId = 2, Status = ParishStatus.Active,  CertificationLevel = CertificationLevel.Level2, LiaisonName = "Fr. Brendan Kowalski", LiaisonEmail = "bkowalski@stalphonsus-demo.org", ActiveCases = 2, TotalCasesFulfilled = 18, EnrolledDate = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new Parish { Id = 5, Name = "Basilica of St. Josaphat",   DioceseName = "Diocese of Milwaukee",    ChapterId = 2, DioceseId = 2, Status = ParishStatus.Active,  CertificationLevel = CertificationLevel.Level1, LiaisonName = "Deacon Paul Janicki",  LiaisonEmail = "pjanicki@josaphat-demo.org",ActiveCases = 1, TotalCasesFulfilled =  8, EnrolledDate = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Parish { Id = 6, Name = "St. Thomas the Apostle",     DioceseName = "Archdiocese of St. Paul", ChapterId = 3, DioceseId = 3, Status = ParishStatus.Pending, CertificationLevel = CertificationLevel.None,   LiaisonName = "Mrs. Karen Olson",     LiaisonEmail = "kolson@stthomasap-demo.org", ActiveCases = 2, TotalCasesFulfilled =  2, EnrolledDate = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // ── MOCK DATA: Families ───────────────────────────────────────────────
        // These are entirely fictitious families used for UI/demo purposes.
        var families = new List<Family>
        {
            new() { Id =  1, Parent1FirstName = "Elena",   Parent1LastName = "Moreno",    Parent2FirstName = "Carlos",  Email = "e.moreno@example.com",   Phone = "+13125550201", StreetAddress = "1842 N. Elm St",    City = "Chicago",      State = "IL", Zip = "60614", Reason = PackageReason.Stillbirth,                 ChapterId = 1, ParishName = "St. Michael the Archangel", DioceseName = "Archdiocese of Chicago", ChildrenInitials = "S.M.", FaithTradition = "Catholic", Status = FamilyStatus.Active,   CreatedAt = new DateTime(2025, 11,  5, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  2, Parent1FirstName = "Naomi",   Parent1LastName = "Okafor",                                  Email = "n.okafor@example.com",   Phone = "+13125550202", StreetAddress = "3301 W. Division St", City = "Chicago",    State = "IL", Zip = "60651", Reason = PackageReason.Miscarriage,                ChapterId = 1, ParishName = "Holy Name Cathedral",       DioceseName = "Archdiocese of Chicago", FaithTradition = "Catholic",           Status = FamilyStatus.Active,   CreatedAt = new DateTime(2025, 12,  2, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  3, Parent1FirstName = "David",   Parent1LastName = "Park",      Parent2FirstName = "Jenny",   Email = "d.park@example.com",     Phone = "+13125550203", StreetAddress = "755 W. Belden Ave", City = "Chicago",        State = "IL", Zip = "60614", Reason = PackageReason.PrenatalLifeLimitingDiagnosis, ChapterId = 1, ParishName = "Holy Name Cathedral", DioceseName = "Archdiocese of Chicago", ChildrenInitials = "L.K.", FaithTradition = "Catholic", Status = FamilyStatus.Active, CreatedAt = new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  4, Parent1FirstName = "Rachel",  Parent1LastName = "Swanson",   Parent2FirstName = "Eric",    Email = "r.swanson@example.com",  Phone = "+13125550204", StreetAddress = "920 Maple Ave",     City = "Oak Park",       State = "IL", Zip = "60302", Reason = PackageReason.PrenatalDiagnosis,           ChapterId = 1, ParishName = "Our Lady of Sorrows",       DioceseName = "Archdiocese of Chicago", ChildrenInitials = "H.E.", FaithTradition = "Catholic", Status = FamilyStatus.Active, CreatedAt = new DateTime(2026, 2,  3, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  5, Parent1FirstName = "Maria",   Parent1LastName = "Gonzalez",  Parent2FirstName = "Jorge",   Email = "m.gonzalez@example.com", Phone = "+13125550205", StreetAddress = "2200 S. Halsted St",City = "Chicago",        State = "IL", Zip = "60608", Reason = PackageReason.InfantLoss,                 ChapterId = 1, ParishName = "St. Michael the Archangel", DioceseName = "Archdiocese of Chicago", ChildrenInitials = "A.", FaithTradition = "Catholic",   Status = FamilyStatus.FollowUp, CreatedAt = new DateTime(2025,  9, 20, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  6, Parent1FirstName = "Brigid",  Parent1LastName = "Murphy",    Parent2FirstName = "Sean",    Email = "b.murphy@example.com",   Phone = "+14145550206", StreetAddress = "415 E. Brady St",   City = "Milwaukee",      State = "WI", Zip = "53202", Reason = PackageReason.Miscarriage,                ChapterId = 2, ParishName = "St. Alphonsus",             DioceseName = "Diocese of Milwaukee",   ChildrenInitials = "F.J.", FaithTradition = "Catholic", Status = FamilyStatus.Active, CreatedAt = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  7, Parent1FirstName = "Abby",    Parent1LastName = "Novak",                                   Email = "a.novak@example.com",    Phone = "+14145550207", StreetAddress = "1020 N. Farwell Ave",City = "Milwaukee",    State = "WI", Zip = "53202", Reason = PackageReason.Stillbirth,                 ChapterId = 2, ParishName = "Basilica of St. Josaphat",  DioceseName = "Diocese of Milwaukee",   ChildrenInitials = "C.G.", FaithTradition = "Catholic", Status = FamilyStatus.Active, CreatedAt = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  8, Parent1FirstName = "Mei",     Parent1LastName = "Chen",      Parent2FirstName = "Wei",     Email = "m.chen@example.com",     Phone = "+16125550208", StreetAddress = "3820 Hennepin Ave", City = "Minneapolis",    State = "MN", Zip = "55409", Reason = PackageReason.PrenatalLifeLimitingDiagnosis, ChapterId = 3, ParishName = "St. Thomas the Apostle", DioceseName = "Archdiocese of St. Paul", ChildrenInitials = "E.L.", FaithTradition = "Catholic", Status = FamilyStatus.Active, CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  9, Parent1FirstName = "Annika",  Parent1LastName = "Berg",      Parent2FirstName = "Lars",    Email = "a.berg@example.com",     Phone = "+16125550209", StreetAddress = "1501 Como Ave",     City = "St. Paul",       State = "MN", Zip = "55108", Reason = PackageReason.Infertility,                ChapterId = 3,                                                       DioceseName = "Archdiocese of St. Paul", FaithTradition = "Lutheran",          Status = FamilyStatus.Active,   CreatedAt = new DateTime(2026, 3,  1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = 10, Parent1FirstName = "Theresa", Parent1LastName = "Walsh",     Parent2FirstName = "Patrick", Email = "t.walsh@example.com",    Phone = "+13125550210", StreetAddress = "606 W. Barry Ave",  City = "Chicago",        State = "IL", Zip = "60657", Reason = PackageReason.PastLoss,                   ChapterId = 1, ParishName = "St. Michael the Archangel", DioceseName = "Archdiocese of Chicago", ChildrenInitials = "C.M.B.", FaithTradition = "Catholic", Status = FamilyStatus.Closed, CreatedAt = new DateTime(2025, 7, 10, 0, 0, 0, DateTimeKind.Utc) },
        };
        db.Families.AddRange(families);

        // ── MOCK DATA: Volunteers ─────────────────────────────────────────────
        var volunteers = new List<Volunteer>
        {
            new() { Id =  1, FirstName = "Claire",   LastName = "Hoffman",   Email = "c.hoffman@example.com",  Phone = "+13125550301", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active,     ChapterId = 1, ParishName = "St. Michael the Archangel", Latitude = 41.9028, Longitude = -87.6320, ServiceRadiusMiles = 20, ActiveCases = 3, TotalCasesFulfilled = 28, JoinedDate = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  2, FirstName = "Thomas",   LastName = "Quinn",     Email = "t.quinn@example.com",    Phone = "+13125550302", Role = VolunteerRole.Driver,           Status = VolunteerStatus.Active,     ChapterId = 1, ParishName = "Holy Name Cathedral",       Latitude = 41.8957, Longitude = -87.6298, ServiceRadiusMiles = 30, ActiveCases = 2, TotalCasesFulfilled = 14, JoinedDate = new DateTime(2023, 2, 15, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  3, FirstName = "Lucia",    LastName = "Esposito",  Email = "l.esposito@example.com", Phone = "+13125550303", Role = VolunteerRole.PrayerAmbassador, Status = VolunteerStatus.Active,     ChapterId = 1, ParishName = "Our Lady of Sorrows",       Latitude = 41.8851, Longitude = -87.7945, ServiceRadiusMiles = 15, ActiveCases = 1, TotalCasesFulfilled =  7, JoinedDate = new DateTime(2023, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  4, FirstName = "Marcus",   LastName = "Johnson",   Email = "m.johnson@example.com",  Phone = "+13125550304", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active,     ChapterId = 1, ParishName = "St. Michael the Archangel", Latitude = 41.9200, Longitude = -87.6400, ServiceRadiusMiles = 25, ActiveCases = 4, TotalCasesFulfilled = 19, JoinedDate = new DateTime(2023, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  5, FirstName = "Patricia", LastName = "Dolan",     Email = "p.dolan@example.com",    Phone = "+13125550305", Role = VolunteerRole.ParishLiaison,   Status = VolunteerStatus.Inactive,   ChapterId = 1, ParishName = "Holy Name Cathedral",       Latitude = 41.8960, Longitude = -87.6290, ServiceRadiusMiles = 20, ActiveCases = 0, TotalCasesFulfilled = 42, JoinedDate = new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  6, FirstName = "Saoirse",  LastName = "Byrne",     Email = "s.byrne@example.com",    Phone = "+14145550306", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active,     ChapterId = 2, ParishName = "St. Alphonsus",             Latitude = 43.0533, Longitude = -87.9050, ServiceRadiusMiles = 20, ActiveCases = 2, TotalCasesFulfilled = 11, JoinedDate = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  7, FirstName = "Dennis",   LastName = "Krueger",   Email = "d.krueger@example.com",  Phone = "+14145550307", Role = VolunteerRole.Driver,           Status = VolunteerStatus.Active,     ChapterId = 2, ParishName = "Basilica of St. Josaphat",  Latitude = 43.0410, Longitude = -87.9350, ServiceRadiusMiles = 35, ActiveCases = 1, TotalCasesFulfilled =  8, JoinedDate = new DateTime(2023, 7, 15, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  8, FirstName = "Fatima",   LastName = "Osei",      Email = "f.osei@example.com",     Phone = "+16125550308", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active,     ChapterId = 3, ParishName = "St. Thomas the Apostle",    Latitude = 44.9735, Longitude = -93.2750, ServiceRadiusMiles = 20, ActiveCases = 2, TotalCasesFulfilled =  6, JoinedDate = new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id =  9, FirstName = "Ingrid",   LastName = "Larsen",    Email = "i.larsen@example.com",   Phone = "+16125550309", Role = VolunteerRole.EventHelper,      Status = VolunteerStatus.Onboarding, ChapterId = 3,                                                    Latitude = 44.9440, Longitude = -93.1700, ServiceRadiusMiles = 15, ActiveCases = 0, TotalCasesFulfilled =  0, JoinedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = 10, FirstName = "Rebecca",  LastName = "Torres",    Email = "r.torres@example.com",   Phone = "+13125550310", Role = VolunteerRole.Admin,            Status = VolunteerStatus.Active,     ChapterId = 1,                                                    Latitude = 41.8827, Longitude = -87.6233, ServiceRadiusMiles = 10, ActiveCases = 0, TotalCasesFulfilled =  0, JoinedDate = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc) },
        };
        db.Volunteers.AddRange(volunteers);

        // ── MOCK DATA: Donors ─────────────────────────────────────────────────
        var donors = new List<Donor>
        {
            new() { Id = 1, FirstName = "William",  LastName = "Harrington", Email = "w.harrington@example.com", Phone = "+13125550401", StreetAddress = "1000 N. Lake Shore Dr", City = "Chicago",    State = "IL", Zip = "60611", ChapterId = 1, IsAnonymous = false, IsRecurring = true,  RecurringAmount = 250m,  FirstGiftDate = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc), LastGiftDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),  TotalGiven = 8250m,  GiftCount = 33, Tier = DonorTier.Benefactor, ParishName = "Holy Name Cathedral" },
            new() { Id = 2, FirstName = "Margaret",  LastName = "Flannery",  Email = "m.flannery@example.com",  Phone = "+13125550402", StreetAddress = "444 W. Oakdale Ave",    City = "Chicago",    State = "IL", Zip = "60657", ChapterId = 1, IsAnonymous = false, IsRecurring = true,  RecurringAmount = 100m,  FirstGiftDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), LastGiftDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),  TotalGiven = 3800m,  GiftCount = 38, Tier = DonorTier.Champion,   ParishName = "St. Michael the Archangel" },
            new() { Id = 3, FirstName = "Robert",    LastName = "Steinberg", Email = "r.steinberg@example.com", Phone = "+13125550403", StreetAddress = "520 N. Michigan Ave",   City = "Chicago",    State = "IL", Zip = "60611", ChapterId = 1, IsAnonymous = false, IsRecurring = false, RecurringAmount = null,  FirstGiftDate = new DateTime(2024, 11, 1, 0, 0, 0, DateTimeKind.Utc),LastGiftDate = new DateTime(2025, 11, 30, 0, 0, 0, DateTimeKind.Utc),TotalGiven = 5000m,  GiftCount = 1,  Tier = DonorTier.Benefactor, Notes = "Annual Gala lead sponsor" },
            new() { Id = 4, FirstName = "Susan",     LastName = "Klingman",  Email = "s.klingman@example.com",  Phone = "+13125550404",                                           City = "Evanston",   State = "IL", Zip = "60201", ChapterId = 1, IsAnonymous = false, IsRecurring = false, RecurringAmount = null,  FirstGiftDate = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc),LastGiftDate = new DateTime(2025, 12, 10, 0, 0, 0, DateTimeKind.Utc),TotalGiven = 750m,   GiftCount = 3,  Tier = DonorTier.Supporter,  ParishName = "Our Lady of Sorrows" },
            new() { Id = 5, FirstName = "Anonymous", LastName = "",          Email = "",                        ChapterId = 1, IsAnonymous = true,  IsRecurring = false,                                           FirstGiftDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), LastGiftDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), TotalGiven = 200m,   GiftCount = 2,  Tier = DonorTier.Friend },
            new() { Id = 6, FirstName = "Grace",     LastName = "Zimmerman", Email = "g.zimmerman@example.com", Phone = "+14145550406",                                           City = "Milwaukee",  State = "WI", Zip = "53202", ChapterId = 2, IsAnonymous = false, IsRecurring = true,  RecurringAmount = 75m,   FirstGiftDate = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc), LastGiftDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),  TotalGiven = 2025m,  GiftCount = 27, Tier = DonorTier.Champion,   ParishName = "St. Alphonsus" },
            new() { Id = 7, FirstName = "Theodore",  LastName = "Hanson",    Email = "t.hanson@example.com",    Phone = "+16125550407",                                           City = "Minneapolis",State = "MN", Zip = "55401", ChapterId = 3, IsAnonymous = false, IsRecurring = false, RecurringAmount = null,  FirstGiftDate = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),LastGiftDate = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),  TotalGiven = 1200m,  GiftCount = 4,  Tier = DonorTier.Champion },
            new() { Id = 8, FirstName = "Colleen",   LastName = "Burke",     Email = "c.burke@example.com",     Phone = "+13125550408",                                           City = "Chicago",    State = "IL", Zip = "60614", ChapterId = 1, IsAnonymous = false, IsRecurring = false, RecurringAmount = null,  FirstGiftDate = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc),LastGiftDate = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc), TotalGiven = 150m,   GiftCount = 1,  Tier = DonorTier.Friend,     ParishName = "Holy Name Cathedral" },
        };
        db.Donors.AddRange(donors);

        // ── MOCK DATA: Package Requests ───────────────────────────────────────
        var requests = new List<PackageRequest>
        {
            new() { Id =  1, FamilyId =  1, Reason = PackageReason.Stillbirth,                 Category = RequestCategory.PackageDelivery, Status = CaseStatus.Fulfilled,        Priority = RequestPriority.Normal, ChapterId = 1, AssignedToId = 1, AssignedTo = "Claire Hoffman",  CreatedAt = new DateTime(2025, 11,  5, 10, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2025, 11, 14, 0, 0, 0, DateTimeKind.Utc), ShippedDate = new DateTime(2025, 11, 12, 0, 0, 0, DateTimeKind.Utc), TrackingNumber = "9400111899223456789012", ChildrenInitials = "S.M.", InternalNotes = "Package included knitted blanket and memory box per family request." },
            new() { Id =  2, FamilyId =  2, Reason = PackageReason.Miscarriage,                Category = RequestCategory.PackageDelivery, Status = CaseStatus.Shipped,          Priority = RequestPriority.Normal, ChapterId = 1, AssignedToId = 4, AssignedTo = "Marcus Johnson",  CreatedAt = new DateTime(2025, 12,  2, 12, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2025, 12, 10, 0, 0, 0, DateTimeKind.Utc), ShippedDate = new DateTime(2025, 12, 10, 0, 0, 0, DateTimeKind.Utc), TrackingNumber = "9400111899223456789099" },
            new() { Id =  3, FamilyId =  3, Reason = PackageReason.PrenatalLifeLimitingDiagnosis, Category = RequestCategory.PackageDelivery, Status = CaseStatus.InProgress,    Priority = RequestPriority.Urgent, ChapterId = 1, AssignedToId = 1, AssignedTo = "Claire Hoffman",  CreatedAt = new DateTime(2026,  1, 14, 9, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026,  1, 20, 0, 0, 0, DateTimeKind.Utc),  ChildrenInitials = "L.K.",  IsForSelf = false, ReferrerName = "Fr. Thomas Reed", InternalNotes = "Family received prenatal Trisomy 18 diagnosis. Expedite." },
            new() { Id =  4, FamilyId =  4, Reason = PackageReason.PrenatalDiagnosis,          Category = RequestCategory.PrayerSupport,   Status = CaseStatus.InProgress,       Priority = RequestPriority.High,   ChapterId = 1, AssignedToId = 3, AssignedTo = "Lucia Esposito",  CreatedAt = new DateTime(2026,  2,  3, 14, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026,  2,  6, 0, 0, 0, DateTimeKind.Utc),  ChildrenInitials = "H.E." },
            new() { Id =  5, FamilyId =  5, Reason = PackageReason.InfantLoss,                 Category = RequestCategory.PackageDelivery, Status = CaseStatus.Fulfilled,        Priority = RequestPriority.Normal, ChapterId = 1, AssignedToId = 4, AssignedTo = "Marcus Johnson",  CreatedAt = new DateTime(2025,  9, 20, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2025, 10,  1, 0, 0, 0, DateTimeKind.Utc), ShippedDate = new DateTime(2025, 9, 28, 0, 0, 0, DateTimeKind.Utc),  TrackingNumber = "9400111899223456780011", ChildrenInitials = "A." },
            new() { Id =  6, FamilyId =  6, Reason = PackageReason.Miscarriage,                Category = RequestCategory.PackageDelivery, Status = CaseStatus.New,              Priority = RequestPriority.Normal, ChapterId = 2, CreatedAt = new DateTime(2026,  1, 28, 8, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026,  1, 28, 8, 0, 0, DateTimeKind.Utc),  ChildrenInitials = "F.J." },
            new() { Id =  7, FamilyId =  7, Reason = PackageReason.Stillbirth,                 Category = RequestCategory.PackageDelivery, Status = CaseStatus.InProgress,       Priority = RequestPriority.High,   ChapterId = 2, AssignedToId = 6, AssignedTo = "Saoirse Byrne",   CreatedAt = new DateTime(2026,  2, 10, 11, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026,  2, 13, 0, 0, 0, DateTimeKind.Utc),  ChildrenInitials = "C.G." },
            new() { Id =  8, FamilyId =  8, Reason = PackageReason.PrenatalLifeLimitingDiagnosis, Category = RequestCategory.PackageDelivery, Status = CaseStatus.New,           Priority = RequestPriority.Urgent, ChapterId = 3, CreatedAt = new DateTime(2026,  2, 20, 7, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026,  2, 20, 7, 0, 0, DateTimeKind.Utc),  ChildrenInitials = "E.L.", InternalNotes = "Referred by hospital social worker. Family requesting hospital visit as well." },
            new() { Id =  9, FamilyId =  9, Reason = PackageReason.Infertility,                Category = RequestCategory.ResourceProvision, Status = CaseStatus.AwaitingShipment, Priority = RequestPriority.Normal, ChapterId = 3, AssignedToId = 8, AssignedTo = "Fatima Osei", CreatedAt = new DateTime(2026,  3,  1, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026,  3, 10, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = 10, FamilyId =  3, Reason = PackageReason.PrenatalLifeLimitingDiagnosis, Category = RequestCategory.HospitalVisit,   Status = CaseStatus.InProgress,    Priority = RequestPriority.High,   ChapterId = 1, AssignedToId = 2, AssignedTo = "Thomas Quinn",    CreatedAt = new DateTime(2026,  1, 20, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026,  2,  1, 0, 0, 0, DateTimeKind.Utc),  ChildrenInitials = "L.K.", InternalNotes = "Second request for same family — hospital visit alongside package." },
            new() { Id = 11, FamilyId = 10, Reason = PackageReason.PastLoss,                   Category = RequestCategory.CounselingReferral, Status = CaseStatus.Fulfilled,     Priority = RequestPriority.Low,    ChapterId = 1, AssignedToId = 3, AssignedTo = "Lucia Esposito",  CreatedAt = new DateTime(2025,  7, 10, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2025,  8,  5, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = 12, FamilyId =  1, Reason = PackageReason.Stillbirth,                 Category = RequestCategory.Memorial,          Status = CaseStatus.OnHold,          Priority = RequestPriority.Normal, ChapterId = 1, CreatedAt = new DateTime(2025, 11, 20, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2025, 11, 22, 0, 0, 0, DateTimeKind.Utc), InternalNotes = "Family requested memorial planting kit. On hold pending supply." },
        };
        db.Requests.AddRange(requests);

        // ── MOCK DATA: Donations ──────────────────────────────────────────────
        var donations = new List<Donation>
        {
            new() { Id =  1, DonorId = 1, Amount = 250m,   Date = new DateTime(2026,  3,  1, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Online,       ChapterId = 1, IsRecurring = true,  Campaign = "Monthly Giving",          ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Allocated,      AllocatedTo = "Package Supplies" },
            new() { Id =  2, DonorId = 2, Amount = 100m,   Date = new DateTime(2026,  2,  1, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Online,       ChapterId = 1, IsRecurring = true,  Campaign = "Monthly Giving",          ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Allocated,      AllocatedTo = "Package Supplies" },
            new() { Id =  3, DonorId = 3, Amount = 5000m,  Date = new DateTime(2025, 11, 30, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Gala,         ChapterId = 1, IsRecurring = false, Campaign = "Annual Gala 2025",        ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Allocated,      AllocatedTo = "General Fund" },
            new() { Id =  4, DonorId = 4, Amount = 250m,   Date = new DateTime(2025, 12, 10, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Check,        ChapterId = 1, IsRecurring = false, CheckNumber = "1042",                ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.PendingReview },
            new() { Id =  5, DonorId = 5, Amount = 100m,   Date = new DateTime(2025, 10,  1, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Cash,         ChapterId = 1, IsRecurring = false,                                       ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Unallocated },
            new() { Id =  6, DonorId = 5, Amount = 100m,   Date = new DateTime(2025, 10, 15, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Cash,         ChapterId = 1, IsRecurring = false,                                       ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Unallocated },
            new() { Id =  7, DonorId = 6, Amount = 75m,    Date = new DateTime(2026,  3,  1, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Online,       ChapterId = 2, IsRecurring = true,  Campaign = "Monthly Giving",          ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Allocated,      AllocatedTo = "Package Supplies" },
            new() { Id =  8, DonorId = 7, Amount = 500m,   Date = new DateTime(2026,  1, 20, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Online,       ChapterId = 3, IsRecurring = false, Campaign = "Year-End 2025",           ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.PendingReview },
            new() { Id =  9, DonorId = 8, Amount = 150m,   Date = new DateTime(2026,  2, 14, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Online,       ChapterId = 1, IsRecurring = false, Campaign = "Valentine's Day Drive",   ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Unallocated },
            new() { Id = 10, DonorId = 1, Amount = 250m,   Date = new DateTime(2026,  2,  1, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Online,       ChapterId = 1, IsRecurring = true,  Campaign = "Monthly Giving",          ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Allocated,      AllocatedTo = "Shipping Costs" },
            new() { Id = 11, DonorId = 2, Amount = 100m,   Date = new DateTime(2026,  3,  1, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.Online,       ChapterId = 1, IsRecurring = true,  Campaign = "Monthly Giving",          ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Unallocated },
            new() { Id = 12, DonorId = 3, Amount = 5000m,  Date = new DateTime(2024, 11, 20, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.SilentAuction,ChapterId = 1, IsRecurring = false, Campaign = "Annual Gala 2024",        ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Allocated,      AllocatedTo = "General Fund" },
            new() { Id = 13, DonorId = 7, Amount = 700m,   Date = new DateTime(2025,  9, 15, 0, 0, 0, DateTimeKind.Utc), Channel = DonationChannel.CorporateMatch,ChapterId = 3, IsRecurring = false, Campaign = "Matching Gift Q3",        ContributionStatus = ContributionStatus.Processed,  AllocationStatus = AllocationStatus.Allocated,      AllocatedTo = "Package Supplies" },
        };
        db.Donations.AddRange(donations);

        // ── MOCK DATA: Fund Allocations ───────────────────────────────────────
        db.FundAllocations.AddRange(
            new FundAllocation { Id = 1, DonationId = 1,  Amount = 250m,  AllocatedTo = "Package Supplies — Chicago Q1 2026",  Status = AllocationStatus.Allocated,     ApprovedBy = "admin@lotv-demo.org", ApprovedAt = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),  CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new FundAllocation { Id = 2, DonationId = 3,  Amount = 3000m, AllocatedTo = "General Operating Fund 2025",        Status = AllocationStatus.Allocated,     ApprovedBy = "admin@lotv-demo.org", ApprovedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
            new FundAllocation { Id = 3, DonationId = 3,  Amount = 2000m, AllocatedTo = "Event Equipment Purchase",           Status = AllocationStatus.Allocated,     ApprovedBy = "admin@lotv-demo.org", ApprovedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
            new FundAllocation { Id = 4, DonationId = 4,  Amount = 250m,  AllocatedTo = "Package Supplies — Chicago Q1 2026",  Status = AllocationStatus.PendingReview, CreatedAt = new DateTime(2025, 12, 11, 0, 0, 0, DateTimeKind.Utc) },
            new FundAllocation { Id = 5, DonationId = 8,  Amount = 500m,  AllocatedTo = "Twin Cities Chapter Launch Fund",    Status = AllocationStatus.PendingReview, CreatedAt = new DateTime(2026, 1, 21, 0, 0, 0, DateTimeKind.Utc) },
            new FundAllocation { Id = 6, DonationId = 10, Amount = 250m,  AllocatedTo = "Shipping Costs — Chicago Q1 2026",   Status = AllocationStatus.Allocated,     ApprovedBy = "admin@lotv-demo.org", ApprovedAt = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc),  CreatedAt = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc) }
        );

        // ── MOCK DATA: Expenses ───────────────────────────────────────────────
        db.Expenses.AddRange(
            new Expense { Id =  1, ChapterId = 1, Description = "Shipping labels and postage — January",       Amount = 185.40m, Category = "Shipping",   PaidAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),  PaidBy = "admin@lotv-demo.org", CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Expense { Id =  2, ChapterId = 1, Description = "Memory boxes — order of 24",                  Amount = 312.00m, Category = "Supplies",   PaidAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), PaidBy = "admin@lotv-demo.org", CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new Expense { Id =  3, ChapterId = 1, Description = "Grief books bulk order — 15 titles",          Amount = 428.75m, Category = "Supplies",   PaidAt = new DateTime(2026, 1, 22, 0, 0, 0, DateTimeKind.Utc), PaidBy = "admin@lotv-demo.org", CreatedAt = new DateTime(2026, 1, 22, 0, 0, 0, DateTimeKind.Utc) },
            new Expense { Id =  4, ChapterId = 1, Description = "Gala venue deposit — Our Lady of Sorrows Hall",Amount = 750.00m, Category = "Events",    PaidAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc), PaidBy = "admin@lotv-demo.org" },
            new Expense { Id =  5, ChapterId = 1, Description = "Printed brochures and rack cards (500 ea.)",  Amount =  96.50m, Category = "Printing",   PaidAt = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), PaidBy = "admin@lotv-demo.org" },
            new Expense { Id =  6, ChapterId = 2, Description = "Shipping labels and postage — January",       Amount =  72.15m, Category = "Shipping",   PaidAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),  PaidBy = "milwaukee@lotv-demo.org" },
            new Expense { Id =  7, ChapterId = 2, Description = "Care package supplies bulk purchase",         Amount = 215.30m, Category = "Supplies",   PaidAt = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc), PaidBy = "milwaukee@lotv-demo.org" },
            new Expense { Id =  8, ChapterId = 3, Description = "Chapter launch event — room rental",          Amount = 200.00m, Category = "Events",     PaidAt = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),  PaidBy = "twincities@lotv-demo.org" },
            new Expense { Id =  9, ChapterId = 3, Description = "Initial inventory — memory boxes (12)",       Amount = 156.00m, Category = "Supplies",   PaidAt = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),  PaidBy = "twincities@lotv-demo.org" },
            new Expense { Id = 10, ChapterId = 1, Description = "Volunteer coordinator stipend — Q1 2026",     Amount = 500.00m, Category = "Staffing",   PaidAt = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), PaidBy = "admin@lotv-demo.org", Notes = "Part-time coordinator support through March." }
        );

        // ── MOCK DATA: Ministry Events ────────────────────────────────────────
        var events = new List<MinistryEvent>
        {
            new() { Id = 1, Title = "Annual Gala 2026",                  Type = EventType.Gala,              Status = EventStatus.Open,      ChapterId = 1, Date = new DateTime(2026, 5, 10, 18, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 5, 10, 22, 0, 0, DateTimeKind.Utc), Location = "Our Lady of Sorrows Parish Hall, Oak Park, IL", Capacity = 150, Registered = 67, TicketPrice = 125m, GoalAmount = 15000m, CreatedBy = "admin@lotv-demo.org", Description = "Our flagship annual fundraising gala featuring a silent auction, dinner, and testimonial speakers." },
            new() { Id = 2, Title = "Memorial Prayer Night — Lent 2026", Type = EventType.PrayerNight,       Status = EventStatus.Published, ChapterId = 1, Date = new DateTime(2026, 3, 27, 19, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 3, 27, 20, 30, 0, DateTimeKind.Utc), Location = "Holy Name Cathedral, Chicago, IL",            Capacity = 80,  Registered = 34, TicketPrice = null, GoalAmount = null, CreatedBy = "admin@lotv-demo.org", Description = "An evening of prayer, candle lighting, and reflection for families who have experienced pregnancy or infant loss." },
            new() { Id = 3, Title = "Milwaukee Chapter Launch Dinner",   Type = EventType.Dinner,            Status = EventStatus.Completed, ChapterId = 2, Date = new DateTime(2026, 2,  8, 18, 30, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 2, 8, 21, 0, 0, DateTimeKind.Utc),   Location = "St. Alphonsus Parish Hall, Milwaukee, WI",   Capacity = 60,  Registered = 58, TicketPrice = 50m,  GoalAmount = 2500m, CreatedBy = "milwaukee@lotv-demo.org", Notes = "Inaugural chapter event. Very successful; 3 new volunteers signed up." },
            new() { Id = 4, Title = "Knitting Circle — Volunteer Social", Type = EventType.InPersonGathering,Status = EventStatus.Open,      ChapterId = 1, Date = new DateTime(2026, 4,  5, 10, 0, 0, DateTimeKind.Utc),  EndDate = new DateTime(2026, 4, 5, 13, 0, 0, DateTimeKind.Utc),   Location = "St. Michael the Archangel, Chicago, IL",     Capacity = 20,  Registered = 11, TicketPrice = null, GoalAmount = null, CreatedBy = "admin@lotv-demo.org", Description = "Volunteers gather to knit memory blankets for upcoming package orders. All skill levels welcome." },
            new() { Id = 5, Title = "Twin Cities Walkathon 2026",        Type = EventType.Walkathon,         Status = EventStatus.Draft,     ChapterId = 3, Date = new DateTime(2026, 6, 21, 8, 0, 0, DateTimeKind.Utc),   EndDate = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc),  Location = "Lake Harriet Bandshell, Minneapolis, MN",    Capacity = 200, Registered = 0,  TicketPrice = null, GoalAmount = 10000m, CreatedBy = "twincities@lotv-demo.org", Description = "Annual walkathon in memory of babies lost. Participants collect pledges." },
        };
        db.Events.AddRange(events);

        // ── MOCK DATA: Resource Items (Inventory) ─────────────────────────────
        db.ResourceItems.AddRange(
            new ResourceItem { Id =  1, ChapterId = 1, Name = "Willow Memory Box",         Category = ResourceCategory.MemoryBox,       Description = "Wooden keepsake box, 10\"×8\"×4\", laser-engraved lily design", QuantityOnHand = 12, QuantityReserved = 3, Unit = "box",     CreatedAt = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  2, ChapterId = 1, Name = "Hand-knitted Comfort Blanket",Category = ResourceCategory.KnittedBlanket, Description = "Cream/white soft yarn, approx 18\"×24\"",                       QuantityOnHand = 18, QuantityReserved = 4, Unit = "blanket", CreatedAt = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 2,  1, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  3, ChapterId = 1, Name = "I'll Hold You In Heaven (book)",Category = ResourceCategory.GriefBook,    Description = "Jack Hayford — bereavement book for parents",                  QuantityOnHand = 22, QuantityReserved = 2, Unit = "copy",    CreatedAt = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 22, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  4, ChapterId = 1, Name = "Empty Cradle, Broken Heart (book)",Category = ResourceCategory.GriefBook, Description = "Deborah Davis — comprehensive grief resource",                QuantityOnHand = 15, QuantityReserved = 1, Unit = "copy",    CreatedAt = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 22, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  5, ChapterId = 1, Name = "Hospital Care Package — Newborn",Category = ResourceCategory.HospitalSupply,Description = "Soft cap, booties, receiving blanket set — fits micro-preemie through newborn", QuantityOnHand = 9, QuantityReserved = 2, Unit = "set", CreatedAt = new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  6, ChapterId = 1, Name = "Complete Care Package",       Category = ResourceCategory.CarePackage,    Description = "Bundled package: memory box + blanket + 1 grief book + journal", QuantityOnHand = 7,  QuantityReserved = 5, Unit = "package", CreatedAt = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  7, ChapterId = 2, Name = "Willow Memory Box",           Category = ResourceCategory.MemoryBox,      Description = "Wooden keepsake box, 10\"×8\"×4\", laser-engraved lily design", QuantityOnHand = 6,  QuantityReserved = 1, Unit = "box",     CreatedAt = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026, 2,  5, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  8, ChapterId = 2, Name = "Hand-knitted Comfort Blanket",Category = ResourceCategory.KnittedBlanket, Description = "Cream/white soft yarn, approx 18\"×24\"",                       QuantityOnHand = 8,  QuantityReserved = 2, Unit = "blanket", CreatedAt = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026, 2,  5, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id =  9, ChapterId = 3, Name = "Willow Memory Box",           Category = ResourceCategory.MemoryBox,      Description = "Wooden keepsake box, 10\"×8\"×4\", laser-engraved lily design", QuantityOnHand = 6,  QuantityReserved = 0, Unit = "box",     CreatedAt = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026, 2,  5, 0, 0, 0, DateTimeKind.Utc) },
            new ResourceItem { Id = 10, ChapterId = 3, Name = "Complete Care Package",       Category = ResourceCategory.CarePackage,    Description = "Bundled package: memory box + blanket + 1 grief book + journal", QuantityOnHand = 4,  QuantityReserved = 1, Unit = "package", CreatedAt = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),  UpdatedAt = new DateTime(2026, 2,  5, 0, 0, 0, DateTimeKind.Utc) }
        );

        // ── MOCK DATA: Wish List Items ─────────────────────────────────────────
        db.WishListItems.AddRange(
            new WishListItem { Id = 1, ChapterId = 1, FamilyId = 3, Title = "Hand-knitted comfort blanket", Category = WishListCategory.KnittedBlanket, QuantityRequested = 2, QuantityFulfilled = 1, Status = WishListStatus.PartiallyFulfilled, CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), Notes = "Cream or white preferred — baby boy" },
            new WishListItem { Id = 2, ChapterId = 1, FamilyId = 4, Title = "Grief support book",           Category = WishListCategory.GriefBook,      QuantityRequested = 1, QuantityFulfilled = 0, Status = WishListStatus.Open,               CreatedAt = new DateTime(2026, 2, 4, 0, 0, 0, DateTimeKind.Utc) },
            new WishListItem { Id = 3, ChapterId = 1, FamilyId = 8, Title = "Memory box",                  Category = WishListCategory.MemoryBox,      QuantityRequested = 1, QuantityFulfilled = 0, Status = WishListStatus.Open,               CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc), Notes = "Engraved if possible" },
            new WishListItem { Id = 4, ChapterId = 1, FamilyId = null, Title = "Baby clothing set (preemie)", Category = WishListCategory.BabyClothing, QuantityRequested = 5, QuantityFulfilled = 5, Status = WishListStatus.Fulfilled,          CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), FulfilledAt = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc) },
            new WishListItem { Id = 5, ChapterId = 2, FamilyId = 6, Title = "Knitted blanket",             Category = WishListCategory.KnittedBlanket, QuantityRequested = 1, QuantityFulfilled = 0, Status = WishListStatus.Open,               CreatedAt = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc) },
            new WishListItem { Id = 6, ChapterId = 2, FamilyId = 7, Title = "Memory box",                  Category = WishListCategory.MemoryBox,      QuantityRequested = 1, QuantityFulfilled = 0, Status = WishListStatus.Open,               CreatedAt = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc) },
            new WishListItem { Id = 7, ChapterId = 3, FamilyId = 8, Title = "Complete care package",       Category = WishListCategory.CarePackage,    QuantityRequested = 1, QuantityFulfilled = 0, Status = WishListStatus.Open,               CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc) },
            new WishListItem { Id = 8, ChapterId = 1, FamilyId = null, Title = "Gift card — grocery store", Category = WishListCategory.GiftCard,      QuantityRequested = 3, QuantityFulfilled = 1, Status = WishListStatus.PartiallyFulfilled, CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "$25–$50 value preferred; for families in active bereavement" }
        );

        // ── MOCK DATA: Recurring Donations ────────────────────────────────────
        db.RecurringDonations.AddRange(
            new RecurringDonation { Id = 1, DonorId = 1, ChapterId = 1, Amount = 250m,  Channel = DonationChannel.Online, Frequency = RecurringFrequency.Monthly,  NextChargeDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Status = RecurringStatus.Active,    Campaign = "Monthly Giving",  LastChargedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            new RecurringDonation { Id = 2, DonorId = 2, ChapterId = 1, Amount = 100m,  Channel = DonationChannel.Online, Frequency = RecurringFrequency.Monthly,  NextChargeDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Status = RecurringStatus.Active,    Campaign = "Monthly Giving",  LastChargedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new RecurringDonation { Id = 3, DonorId = 6, ChapterId = 2, Amount = 75m,   Channel = DonationChannel.Online, Frequency = RecurringFrequency.Monthly,  NextChargeDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Status = RecurringStatus.Active,    Campaign = "Monthly Giving",  LastChargedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            new RecurringDonation { Id = 4, DonorId = 4, ChapterId = 1, Amount = 500m,  Channel = DonationChannel.Online, Frequency = RecurringFrequency.Quarterly, NextChargeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Status = RecurringStatus.Paused,    Campaign = "Annual Sustainer", LastChargedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "Paused at donor request March 2026" },
            new RecurringDonation { Id = 5, DonorId = 7, ChapterId = 3, Amount = 200m,  Channel = DonationChannel.Online, Frequency = RecurringFrequency.Monthly,  NextChargeDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Status = RecurringStatus.Active,    Campaign = "Twin Cities Launch", LastChargedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // ── MOCK DATA: Donor Pledges ──────────────────────────────────────────
        db.DonorPledges.AddRange(
            new DonorPledge { Id = 1, DonorId = 1, ChapterId = 1, PledgedAmount = 3000m,  FulfilledAmount = 2750m, TargetDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), Status = PledgeStatus.Active,    Campaign = "Annual Gala 2026 Pledge",     CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), Notes = "Committed at launch dinner; paid 11 installments." },
            new DonorPledge { Id = 2, DonorId = 3, ChapterId = 1, PledgedAmount = 10000m, FulfilledAmount = 10000m,TargetDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc), Status = PledgeStatus.Fulfilled, Campaign = "Capital Campaign 2025",       CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new DonorPledge { Id = 3, DonorId = 6, ChapterId = 2, PledgedAmount = 1000m,  FulfilledAmount = 675m,  TargetDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), Status = PledgeStatus.Active,    Campaign = "Milwaukee Chapter Launch",    CreatedAt = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            new DonorPledge { Id = 4, DonorId = 7, ChapterId = 3, PledgedAmount = 2500m,  FulfilledAmount = 1200m, TargetDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), Status = PledgeStatus.Overdue,   Campaign = "Twin Cities Walkathon 2026",  CreatedAt = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc), Notes = "Missed Feb installment — follow up required." }
        );

        await db.SaveChangesAsync();

        // ── MOCK DATA: Login accounts ─────────────────────────────────────────
        // Credentials matched by tests/Lotv.E2E/Infrastructure/E2ESettings.cs —
        // keep these two in sync if either side changes. No real email on file;
        // a recovery email can be added later via Admin > User Management.
        await CreateUserIfMissingAsync(userMgr, "mary.roberts", null, "DevPassword1!",
            "Mary", "Roberts", UserRole.HQAdmin, chapterId: null);
        await CreateUserIfMissingAsync(userMgr, "claire.hoffman", null, "DevPassword1!",
            "Claire", "Hoffman", UserRole.ChapterStaff, chapterId: 1);

        // ── Real staff accounts (dev credentials only — rotate before any real
        // deployment; these are NOT meant to be used outside local dev). These
        // people don't have real email addresses on file, so they sign in with
        // a plain username (firstname.lastname) rather than an email — a
        // recovery email can be added later via Admin > User Management for
        // forgot-password to work.
        await CreateUserIfMissingAsync(userMgr, "whitney.whitmore", null, "DevPassword1!",
            "Whitney", "Whitmore", UserRole.HQAdmin, chapterId: null);
        await CreateUserIfMissingAsync(userMgr, "cynthia.destefano", null, "DevPassword1!",
            "Cynthia", "DeStefano", UserRole.HQAdmin, chapterId: null);
        await CreateUserIfMissingAsync(userMgr, "chris.kremer", null, "DevPassword1!",
            "Chris", "Kremer", UserRole.HQAdmin, chapterId: null);
        await CreateUserIfMissingAsync(userMgr, "admin", null, "DevPassword1!",
            "Admin", "Account", UserRole.HQAdmin, chapterId: null);
        await CreateUserIfMissingAsync(userMgr, "tech", null, "DevPassword1!",
            "Tech", "Account", UserRole.HQAdmin, chapterId: null);

        // Chapter-scoped staff — ChapterStaff is the least-privileged role that
        // still sees Cases/Kanban/Queue; it also currently includes the
        // Volunteers/Programs nav section (there's no narrower "kanban-only"
        // role yet). Defaulted to Chapter 1 (Chicago Metro) pending real
        // chapter assignments.
        await CreateUserIfMissingAsync(userMgr, "jamie-lee.lavelle", null, "DevPassword1!",
            "Jamie-Lee", "Lavelle", UserRole.ChapterStaff, chapterId: 1);
        await CreateUserIfMissingAsync(userMgr, "maegan.dobner", null, "DevPassword1!",
            "Maegan", "Dobner", UserRole.ChapterStaff, chapterId: 1);
        await CreateUserIfMissingAsync(userMgr, "stephanie.caccamo", null, "DevPassword1!",
            "Stephanie", "Caccamo", UserRole.ChapterStaff, chapterId: 1);
        await CreateUserIfMissingAsync(userMgr, "sammi.weaver", null, "DevPassword1!",
            "Sammi", "Weaver", UserRole.ChapterStaff, chapterId: 1);
        await CreateUserIfMissingAsync(userMgr, "stephanie.mercado-carrillo", null, "DevPassword1!",
            "Stephanie", "Mercado Carrillo", UserRole.ChapterStaff, chapterId: 1);
    }

    private static async Task CreateUserIfMissingAsync(UserManager<LotvIdentityUser> userMgr,
        string username, string? email, string password, string firstName, string lastName, UserRole role, int? chapterId)
    {
        if (await userMgr.FindByNameAsync(username) is not null) return;

        var user = new LotvIdentityUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = email is not null,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            ChapterId = chapterId
        };
        await userMgr.CreateAsync(user, password);
    }
}
