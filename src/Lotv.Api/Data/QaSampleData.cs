using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lotv.Api.Data;

/// <summary>
/// A small, clearly marked set of sample families, requests, volunteers and follow-ups so someone can QA the
/// portal on a live database. Everything it adds can be found by its address (the reserved <c>.invalid</c> domain,
/// which can never receive mail) and removed again, and it is guarded so it can never reach a real person:
/// no email is ever sent to those addresses, the team is not emailed about sample requests, the CRM export leaves
/// them out, and their mailing-list entries are flagged "do not mail". It only ever adds rows; loading twice does nothing.
/// </summary>
public static class QaSampleData
{
    public const string Domain = "sample.lotv.invalid";
    public const string Marker = "QA sample record: safe to remove (Admin > QA Sample Data).";

    public static bool IsSampleEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Trim().EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);

    public static bool IsSample(Family? f) => f is not null && IsSampleEmail(f.Email);

    public record Status(bool Loaded, int Families, int Requests, int Volunteers);

    public record LoadResult(bool Loaded, string Message, Status Status);

    public static async Task<Status> GetStatusAsync(LotvDbContext db)
    {
        var familyIds = await db.Families.Where(f => f.Email.EndsWith(".invalid")).Select(f => f.Id).ToListAsync();
        return new Status(familyIds.Count > 0, familyIds.Count,
            await db.Requests.CountAsync(r => familyIds.Contains(r.FamilyId)),
            await db.Volunteers.CountAsync(v => v.Email.EndsWith(".invalid")));
    }

    /// <summary>Loads the sample data only if none is loaded. True when it loaded something (used by the optional startup switch).</summary>
    public static async Task<bool> EnsureLoadedAsync(LotvDbContext db)
    {
        if ((await GetStatusAsync(db)).Loaded) return false;
        return (await LoadAsync(db)).Loaded;
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    public static async Task<LoadResult> LoadAsync(LotvDbContext db, DateTime? now = null)
    {
        var existing = await GetStatusAsync(db);
        if (existing.Loaded) return new LoadResult(false, "Sample data is already loaded. Remove it first to load a fresh copy.", existing);

        var chapter = await db.Chapters.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (chapter is null) return new LoadResult(false, "There is no chapter to attach sample data to.", existing);

        var t = now ?? DateTime.UtcNow;
        var volunteers = new List<Volunteer>
        {
            NewVolunteer("Priya", "Nair", chapter, t.AddMonths(-14)),
            NewVolunteer("Marcus", "Webb", chapter, t.AddMonths(-9)),
            NewVolunteer("Helen", "Ostrowski", chapter, t.AddMonths(-4)),
            NewVolunteer("Tomasz", "Kruk", chapter, t.AddMonths(-7)),
            NewVolunteer("Grace", "Adeyemi", chapter, t.AddMonths(-2)),
        };
        db.Volunteers.AddRange(volunteers);
        await db.SaveChangesAsync();
        var (priya, marcus, helen, tomasz, grace) = (volunteers[0], volunteers[1], volunteers[2], volunteers[3], volunteers[4]);
        // Susan Harper (the QA tester) is a volunteer too, when her record exists: give her a few cases to work.
        var susan = await db.Volunteers.FirstOrDefaultAsync(v => v.Email == "susan@wte.net");
        Volunteer? ForSusan(Volunteer? original) => susan ?? original;

        // Each entry is one situation worth checking in the portal.
        var families = new List<(Family Family, PackageRequest Request, Volunteer? Volunteer, ProcessStage Stage, CaseStatus Status, int DaysAgo, string Note)>();

        (Family, PackageRequest, Volunteer?, ProcessStage, CaseStatus, int, string) Scenario(
            string dad, string mom, string last, PackageReason reason, CaseStatus status, ProcessStage stage, Volunteer? vol, int daysAgo,
            string city, string state, string zip, string note, bool? grief = null, int? lossDaysAgo = null, bool forSelf = true, string? referrer = null)
        {
            var family = NewFamily(chapter, dad, mom, last, reason, city, state, zip, t.AddDays(-daysAgo), grief, lossDaysAgo is null ? null : t.AddDays(-lossDaysAgo.Value));
            var request = new PackageRequest
            {
                Family = family, Reason = reason, ChapterId = chapter.Id, Status = status, ProcessStage = stage,
                IsForSelf = forSelf, ReferrerName = referrer, ReferrerEmail = referrer is null ? null : $"referrer.{last.ToLowerInvariant()}@{Domain}",
                Priority = RequestPriority.Normal, CreatedAt = t.AddDays(-daysAgo), UpdatedAt = t.AddDays(-Math.Max(0, daysAgo - 1)),
                ChildrenInitials = "B.M.", Category = RequestCategory.PackageDelivery,
                AssignedToId = vol?.Id, AssignedTo = vol is null ? null : $"{vol.FirstName} {vol.LastName}",
                InternalNotes = "QA sample request.",
            };
            if (status is CaseStatus.Shipped or CaseStatus.Fulfilled)
            {
                request.TrackingNumber = "9400 1112 0000 0000 0000 " + (10 + families.Count);
                request.ShippedDate = t.AddDays(-Math.Max(1, daysAgo - 3));
            }
            return (family, request, vol, stage, status, daysAgo, note);
        }

        families.Add(Scenario("Daniel", "Elena", "Whitaker", PackageReason.Infertility, CaseStatus.New, ProcessStage.Unassigned, null, 1, "Naperville", "IL", "60540", "New request waiting in the Unassigned Queue"));
        families.Add(Scenario("Samuel", "Rebecca", "Lindgren", PackageReason.PrenatalDiagnosis, CaseStatus.InProgress, ProcessStage.Assigned, ForSusan(priya), 6, "Evanston", "IL", "60201", "Assigned, waiting for the volunteer to accept"));
        families.Add(Scenario("Anthony", "Maria", "Castellanos", PackageReason.Miscarriage, CaseStatus.InProgress, ProcessStage.Confirmed, ForSusan(marcus), 9, "Chicago", "IL", "60614", "Volunteer accepted (Confirmed)", lossDaysAgo: 40));
        families.Add(Scenario("Peter", "Hannah", "Ostrander", PackageReason.Stillbirth, CaseStatus.InProgress, ProcessStage.Packing, ForSusan(helen), 14, "Oak Park", "IL", "60302", "Being packed; grief support: yes, bereavement follow-up running", grief: true, lossDaysAgo: 30));
        families.Add(Scenario("Thomas", "Grace", "Fairweather", PackageReason.InfantLoss, CaseStatus.AwaitingShipment, ProcessStage.Shipping, priya, 20, "Skokie", "IL", "60076", "Ready to ship; grief support: no", grief: false, lossDaysAgo: 60));
        families.Add(Scenario("Joseph", "Katherine", "Brennan", PackageReason.PrenatalLifeLimitingDiagnosis, CaseStatus.Shipped, ProcessStage.Shipping, marcus, 26, "Wheaton", "IL", "60187", "Shipped with a tracking number"));
        families.Add(Scenario("Nathan", "Olivia", "Prescott", PackageReason.Stillbirth, CaseStatus.Fulfilled, ProcessStage.Delivered, helen, 70, "Joliet", "IL", "60435", "Delivered; bereavement touchpoints partly sent", grief: true, lossDaysAgo: 100));
        families.Add(Scenario("Adrian", "Sophia", "Kowalski", PackageReason.PostnatalMedical, CaseStatus.OnHold, ProcessStage.Assigned, null, 12, "Aurora", "IL", "60506", "On hold"));
        families.Add(Scenario("Michael", "Rachel", "Donovan", PackageReason.Miscarriage, CaseStatus.New, ProcessStage.Unassigned, null, 2, "Elmhurst", "IL", "606", "Bad zip code: shows the 'information needs a check' alert and Email the family", lossDaysAgo: 10));
        families.Add(Scenario("Kevin", "Megan", "Alderman", PackageReason.PrenatalDiagnosis, CaseStatus.New, ProcessStage.Unassigned, null, 3, "Lombard", "IL", "60148", "Requested by a friend for someone else", forSelf: false, referrer: "Jane Friend"));

        // A single-parent family (no Father's Day card) and a possible duplicate of the first family.
        var single = Scenario("", "", "Abernathy", PackageReason.Infertility, CaseStatus.InProgress, ProcessStage.Assigned, priya, 8, "Glenview", "IL", "60025", "Single parent: on the Mother's Day list only");
        single.Item1.Parent1FirstName = "Claire"; single.Item1.Parent2FirstName = null; single.Item1.Parent2LastName = null;
        single.Item1.Email = $"claire.abernathy@{Domain}";
        families.Add(single);

        var dupOf = families[0].Family;
        var dup = Scenario("Daniel", "Elena", "Whitaker", PackageReason.Infertility, CaseStatus.New, ProcessStage.Unassigned, null, 0, "Naperville", "IL", "60540", "Possible duplicate of the first family: held in Possible Duplicates");
        dup.Item1.Email = dupOf.Email;   // same email, so the duplicate check would have flagged it
        dup.Item2.NeedsDuplicateReview = true;
        dup.Item2.DuplicateMatchReason = $"Same email address as existing family ({dupOf.FullName})";
        families.Add(dup);

        // ── One of each situation, for every kind of request ─────────────────────────────────────────
        // For each reason: a new request waiting in the queue, one being worked at some stage of the process, and one
        // that is finished (shipped or delivered). That is enough of each to watch how the board, the queues, the
        // volunteer workloads, the bereavement follow-ups, the grief support list and the card lists behave.
        string[] surnames =
        [
            "Hartwell", "Ellison", "Marchetti", "Thorne", "Vandermeer", "Okonkwo", "Sinclair", "Reyes", "Halloran", "Baptiste",
            "Nakagawa", "Whitlock", "Delgado", "Fitzsimmons", "Pruitt", "Lombardi", "Achterberg", "Kensington", "Bellweather",
            "Ashcroft", "Quintero", "Rasmussen", "Tennyson", "Vasilenko", "Galloway", "Harcourt", "Montoya",
        ];
        string[] dads = ["Robert", "Luis", "Ethan", "Marcus", "Oliver", "Victor", "Simon", "Andre", "Gregory"];
        string[] moms = ["Julia", "Amara", "Nicole", "Danielle", "Isabel", "Renee", "Tamara", "Lucia", "Vivian"];
        (string City, string State, string Zip)[] places =
        [
            ("Chicago", "IL", "60657"), ("Naperville", "IL", "60564"), ("Evanston", "IL", "60202"), ("Milwaukee", "WI", "53202"),
            ("Madison", "WI", "53703"), ("Indianapolis", "IN", "46204"), ("Gary", "IN", "46402"), ("Rockford", "IL", "61101"),
            ("Peoria", "IL", "61602"),
        ];
        Volunteer[] crew = [priya, marcus, helen, tomasz, grace];
        ProcessStage[] workStages = [ProcessStage.Assigned, ProcessStage.Confirmed, ProcessStage.Packing, ProcessStage.Notes, ProcessStage.Shipping];

        var reasons = Enum.GetValues<PackageReason>();
        var n = 0;
        for (var r = 0; r < reasons.Length; r++)
        {
            var reason = reasons[r];
            var isLoss = reason is PackageReason.Miscarriage or PackageReason.Stillbirth or PackageReason.InfantLoss or PackageReason.PastLoss;
            var asksGrief = reason is PackageReason.Stillbirth or PackageReason.InfantLoss;
            for (var variant = 0; variant < 3; variant++, n++)
            {
                var (city, state, zip) = places[(n + variant) % places.Length];
                var last = surnames[n % surnames.Length];
                var dad = dads[(r + variant) % dads.Length];
                var mom = moms[(r * 2 + variant) % moms.Length];
                var name = reason.ToDisplayName();
                var lossAgo = isLoss ? (reason == PackageReason.PastLoss ? 540 : 20 + r * 6 + variant * 25) : (int?)null;
                bool? grief = asksGrief ? variant != 1 : null;
                var forSelf = variant == 0 || r % 2 == 0;
                var referrer = forSelf ? null : "A friend of the family";

                var scenario = variant switch
                {
                    0 => Scenario(dad, mom, last, reason, CaseStatus.New, ProcessStage.Unassigned, null, 1 + r % 4, city, state, zip,
                            $"{name}: new request waiting in the Unassigned Queue", grief, lossAgo, forSelf, referrer),
                    1 => Scenario(dad, mom, last, reason,
                            workStages[r % workStages.Length] == ProcessStage.Shipping ? CaseStatus.AwaitingShipment : CaseStatus.InProgress,
                            workStages[r % workStages.Length], crew[r % crew.Length], 6 + r * 2, city, state, zip,
                            $"{name}: being worked, at the {workStages[r % workStages.Length]} stage", grief, lossAgo, forSelf, referrer),
                    _ => Scenario(dad, mom, last, reason,
                            r % 2 == 0 ? CaseStatus.Shipped : CaseStatus.Fulfilled,
                            r % 2 == 0 ? ProcessStage.Shipping : ProcessStage.Delivered, crew[(r + 2) % crew.Length], 24 + r * 4, city, state, zip,
                            $"{name}: {(r % 2 == 0 ? "shipped, on its way" : "delivered and finished")}", grief, lossAgo, forSelf, referrer),
                };
                // The most urgent kind of request is flagged urgent, and some others are high priority.
                if (reason == PackageReason.PrenatalLifeLimitingDiagnosis) scenario.Item2.Priority = RequestPriority.Urgent;
                else if (variant == 0 && r % 3 == 0) scenario.Item2.Priority = RequestPriority.High;
                families.Add(scenario);
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var (family, _, _, _, _, _, _) in families) db.Families.Add(family);
        await db.SaveChangesAsync();
        // The possible duplicate must know which family it may duplicate.
        dup.Item2.PossibleDuplicateFamilyId = dupOf.Id;

        foreach (var (family, request, vol, stage, status, daysAgo, note) in families)
        {
            request.FamilyId = family.Id;
            request.Family = null;
            db.Requests.Add(request);
        }
        await db.SaveChangesAsync();

        foreach (var (family, request, vol, stage, status, daysAgo, note) in families)
        {
            var created = request.CreatedAt;
            db.RequestActivities.Add(new RequestActivity
            {
                RequestId = request.Id, ActorId = "qa-sample", ActorName = "QA sample data", ActivityType = ActivityType.Created, Timestamp = created,
                Details = note,
            });
            db.RequestNotes.Add(new RequestNote
            {
                RequestId = request.Id, AuthorId = "qa-sample", AuthorName = "QA sample data", IsInternal = true, CreatedAt = created.AddHours(1),
                Content = "Scenario: " + note,
            });
            if (vol is not null)
            {
                db.RequestAssignments.Add(new RequestAssignment
                {
                    RequestId = request.Id, AssignedToId = vol.Id, AssignedToName = $"{vol.FirstName} {vol.LastName}",
                    AssignedById = "qa-sample", AssignedByName = "QA sample data", AssignedAt = created.AddHours(2), AttemptNumber = 1,
                    AcceptanceDeadline = created.AddHours(26),
                    Status = stage is ProcessStage.Assigned ? AssignmentStatus.Pending : AssignmentStatus.Accepted,
                    AcceptedAt = stage is ProcessStage.Assigned ? null : created.AddHours(5),
                });
            }

            // Bereavement follow-up for the families that lost a child, with the usual four touchpoints.
            if (family.DateOfLoss is { } loss)
                db.FollowUpTrackers.Add(NewTracker(family, loss, sentThrough: status == CaseStatus.Fulfilled ? 1 : 0));

            var entry = await MothersDayMailing.EnsureEntryAsync(db, family, possibleDuplicate: request.NeedsDuplicateReview, t);
            // Never mail a sample: the entries stay flagged.
            foreach (var e in await db.MailingListEntries.Where(m => m.FamilyId == family.Id).ToListAsync())
            {
                e.FlaggedForReview = true;
                e.ReviewNote = "QA sample: do not mail. " + (e.ReviewNote ?? "");
            }
            _ = entry;
        }
        await db.SaveChangesAsync();
        var workers = volunteers.Select(v => v.Id).ToList();
        if (susan is not null) workers.Add(susan.Id);
        await Lotv.Api.Services.VolunteerWorkload.RecomputeAsync(db, workers);
        await tx.CommitAsync();

        var status2 = await GetStatusAsync(db);
        return new LoadResult(true, $"Loaded {status2.Families} sample families, {status2.Requests} requests and {status2.Volunteers} volunteers.", status2);
    }

    private static Volunteer NewVolunteer(string first, string last, Chapter chapter, DateTime joined) => new()
    {
        FirstName = first, LastName = last + " (sample)", Email = $"{first}.{last}@{Domain}".ToLowerInvariant(), Phone = "+13125550100",
        Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ChapterId = chapter.Id, JoinedDate = joined,
        ServiceRadiusMiles = 40, Notes = Marker,
    };

    private static Family NewFamily(Chapter chapter, string dad, string mom, string last, PackageReason reason, string city, string state, string zip,
        DateTime created, bool? grief, DateTime? lossDate) => new()
    {
        Parent1FirstName = dad, Parent1LastName = last, Parent2FirstName = mom, Parent2LastName = last,
        Email = $"{dad}.{last}@{Domain}".ToLowerInvariant(), Phone = "+13125550123",
        StreetAddress = $"{100 + Math.Abs((last + dad).GetHashCode() % 800)} Sample Street", City = city, State = state, Zip = zip,
        Reason = reason, FaithTradition = "Catholic", ChildrenInitials = "B.M.", ParishName = "St. Sample Parish", DioceseName = "Archdiocese of Chicago",
        HowHeard = "Friend", ChapterId = chapter.Id, CreatedAt = created, Status = FamilyStatus.Active, ContactNotes = Marker,
        PrivacyPreference = PrivacyPreference.Anonymous, DateOfLoss = lossDate, GriefSupportRequested = grief,
    };

    private static FollowUpTracker NewTracker(Family f, DateTime loss, int sentThrough)
    {
        var types = new[] { FollowUpMilestoneType.ThreeWeeks, FollowUpMilestoneType.ThreeMonths, FollowUpMilestoneType.SixMonths, FollowUpMilestoneType.ElevenMonths };
        var dues = new[] { loss.AddDays(21), loss.AddMonths(3), loss.AddMonths(6), loss.AddMonths(11) };
        return new FollowUpTracker
        {
            FamilyId = f.Id, Parent1Name = $"{f.Parent1FirstName} {f.Parent1LastName}".Trim(),
            Parent2Name = string.IsNullOrWhiteSpace(f.Parent2FirstName) ? null : $"{f.Parent2FirstName} {f.Parent2LastName}".Trim(),
            Email = f.Email, Phone = f.Phone, StreetAddress = f.StreetAddress, City = f.City, State = f.State, Zip = f.Zip,
            Reason = f.Reason.ToString(), ChildName = f.ChildrenInitials, DateOfLoss = loss,
            Milestones = types.Select((type, i) => new FollowUpMilestone { Type = type, DueDate = dues[i], BookSent = i < sentThrough }).ToList(),
        };
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    public static async Task<Status> RemoveAsync(LotvDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var familyIds = await db.Families.Where(f => f.Email.EndsWith(".invalid")).Select(f => f.Id).ToListAsync();
        var volunteerIds = await db.Volunteers.Where(v => v.Email.EndsWith(".invalid")).Select(v => v.Id).ToListAsync();

        // A real request someone assigned to a sample volunteer goes back to the queue, not away.
        if (volunteerIds.Count > 0)
        {
            var realOnes = await db.Requests.Where(r => r.AssignedToId != null && volunteerIds.Contains(r.AssignedToId.Value) && !familyIds.Contains(r.FamilyId)).ToListAsync();
            foreach (var r in realOnes) { r.AssignedToId = null; r.AssignedTo = null; r.Status = CaseStatus.New; r.ProcessStage = ProcessStage.Unassigned; }
            await db.SaveChangesAsync();
            var assignmentIds = await db.RequestAssignments.Where(a => volunteerIds.Contains(a.AssignedToId) && !familyIds.Contains(a.Request!.FamilyId)).Select(a => a.Id).ToListAsync();
            if (assignmentIds.Count > 0) db.RequestAssignments.RemoveRange(await db.RequestAssignments.Where(a => assignmentIds.Contains(a.Id)).ToListAsync());
            await db.SaveChangesAsync();
        }

        // A real request that was held as a possible duplicate of a sample family is released, never deleted with it.
        if (familyIds.Count > 0)
        {
            var released = await db.Requests.Where(r => r.PossibleDuplicateFamilyId != null && familyIds.Contains(r.PossibleDuplicateFamilyId.Value) && !familyIds.Contains(r.FamilyId)).ToListAsync();
            foreach (var r in released)
            {
                r.PossibleDuplicateFamilyId = null;
                r.NeedsDuplicateReview = false;
                r.DuplicateMatchReason = "It matched a QA sample record, which has since been removed.";
            }
            await db.SaveChangesAsync();
        }

        // Volunteers who are not sample records (Susan) may have been given sample cases: their counts need fixing afterward.
        var affectedVolunteers = await db.Requests.Where(r => familyIds.Contains(r.FamilyId) && r.AssignedToId != null)
            .Select(r => r.AssignedToId!.Value).Distinct().ToListAsync();

        var seen = new HashSet<(string, int)>();
        await DeleteAsync(db, db.Model.FindEntityType(typeof(Family))!, familyIds, seen);
        await DeleteAsync(db, db.Model.FindEntityType(typeof(Volunteer))!, volunteerIds, seen);
        db.ChangeTracker.Clear();
        await Lotv.Api.Services.VolunteerWorkload.RecomputeAsync(db, affectedVolunteers.Where(id => !volunteerIds.Contains(id)).ToList());
        await tx.CommitAsync();
        return await GetStatusAsync(db);
    }

    // The raw SQL below is built only from EF's own model metadata (table and column names) and integer ids read from
    // the database, never from user input, and identifiers cannot be passed as parameters. EF1002 is suppressed for that reason.
#pragma warning disable EF1002
    /// <summary>Deletes the rows and everything that points at them, following the model's relationships.</summary>
    private static async Task DeleteAsync(LotvDbContext db, IEntityType type, List<int> ids, HashSet<(string, int)> seen)
    {
        var table = type.GetTableName();
        var pk = type.FindPrimaryKey();
        if (table is null || pk is null || pk.Properties.Count != 1 || ids.Count == 0) return;
        ids = ids.Where(i => seen.Add((table, i))).ToList();
        if (ids.Count == 0) return;
        var list = string.Join(",", ids);

        foreach (var fk in db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).Where(k => k.PrincipalEntityType == type && k.Properties.Count == 1).ToList())
        {
            var child = fk.DeclaringEntityType;
            var childTable = child.GetTableName();
            if (childTable is null || child == type) continue;
            var fkColumn = fk.Properties[0].GetColumnName(StoreObjectIdentifier.Table(childTable, child.GetSchema()));
            var childPk = child.FindPrimaryKey();
            if (fkColumn is null) continue;

            // Something that only optionally points here is not owned by it: detach it rather than delete it.
            if (!fk.IsRequired && !OwnedWhenOptional.Contains(childTable))
            {
                await db.Database.ExecuteSqlRawAsync($"UPDATE {Q(db, childTable)} SET {Q(db, fkColumn)} = NULL WHERE {Q(db, fkColumn)} IN ({list})");
                continue;
            }

            if (childPk is { Properties.Count: 1 } && childPk.Properties[0].ClrType == typeof(int))
            {
                var pkColumn = childPk.Properties[0].GetColumnName(StoreObjectIdentifier.Table(childTable, child.GetSchema()))!;
                var childIds = await db.Database.SqlQueryRaw<int>($"SELECT {Q(db, pkColumn)} AS Value FROM {Q(db, childTable)} WHERE {Q(db, fkColumn)} IN ({list})").ToListAsync();
                await DeleteAsync(db, child, childIds, seen);
            }
            else
            {
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM {Q(db, childTable)} WHERE {Q(db, fkColumn)} IN ({list})");
            }
        }

        var pkName = pk.Properties[0].GetColumnName(StoreObjectIdentifier.Table(table, type.GetSchema()))!;
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM {Q(db, table)} WHERE {Q(db, pkName)} IN ({list})");
    }

#pragma warning restore EF1002

    /// <summary>Tables whose optional link to a family means "this row belongs to that family".</summary>
    private static readonly HashSet<string> OwnedWhenOptional = ["MailingListEntries", "FollowUpTrackers"];

    private static string Q(LotvDbContext db, string name) => db.Database.IsSqlServer() ? $"[{name}]" : $"\"{name}\"";
}
