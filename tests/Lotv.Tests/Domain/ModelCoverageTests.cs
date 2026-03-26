using Lotv.Core.Models;
using Lotv.Core.Reporting;

namespace Lotv.Tests.Domain;

/// <summary>
/// Exercises every model type and computed property in Lotv.Core that isn't
/// reached by integration or service tests. The goal is to push Lotv.Core
/// line coverage to ≥80%.
/// </summary>
public class ModelCoverageTests
{
    // ── ApplicationUser ───────────────────────────────────────────────────────

    [Fact]
    public void ApplicationUser_FullName_ConcatenatesFirstAndLast()
    {
        var u = new ApplicationUser { FirstName = "Alice", LastName = "Admin", Role = UserRole.HQAdmin };
        Assert.Equal("Alice Admin", u.FullName);
    }

    [Fact]
    public void ApplicationUser_ChapterId_NullForHqAdmin()
    {
        var u = new ApplicationUser { Role = UserRole.HQAdmin, ChapterId = null };
        Assert.Null(u.ChapterId);
        Assert.True(u.IsActive);
    }

    [Theory]
    [InlineData(UserRole.HQAdmin)]
    [InlineData(UserRole.ChapterAdmin)]
    [InlineData(UserRole.ChapterStaff)]
    [InlineData(UserRole.Volunteer)]
    [InlineData(UserRole.Donor)]
    [InlineData(UserRole.PublicUser)]
    public void ApplicationUser_AllRoles_CanBeAssigned(UserRole role)
    {
        var u = new ApplicationUser { Role = role };
        Assert.Equal(role, u.Role);
    }

    // ── Chapter ───────────────────────────────────────────────────────────────

    [Fact]
    public void Chapter_DefaultValues_AreCorrect()
    {
        var c = new Chapter { Name = "Chicago Chapter", City = "Chicago", State = "IL" };
        Assert.True(c.IsActive);
        Assert.Equal(6, c.MaxActiveCasesPerVolunteer);
        Assert.Equal(24, c.AcceptanceWindowHours);
        Assert.Equal(4, c.UrgentAcceptanceWindowHours);
        Assert.Equal(3, c.MaxReassignmentAttempts);
        Assert.Equal(25.0, c.DefaultServiceRadiusMiles);
    }

    [Fact]
    public void Chapter_PropertiesRoundTrip()
    {
        var c = new Chapter
        {
            Id            = 42,
            Name          = "Test Chapter",
            City          = "Springfield",
            State         = "IL",
            ContactName   = "Jane Doe",
            ContactEmail  = "jane@chapter.org",
            ContactPhone  = "555-1234",
            IsActive      = false,
            MaxActiveCasesPerVolunteer   = 8,
            AcceptanceWindowHours        = 48,
            UrgentAcceptanceWindowHours  = 2,
            MaxReassignmentAttempts      = 5,
            DefaultServiceRadiusMiles    = 50.0
        };
        Assert.Equal(42, c.Id);
        Assert.Equal("Test Chapter", c.Name);
        Assert.False(c.IsActive);
        Assert.Equal(8, c.MaxActiveCasesPerVolunteer);
        Assert.Equal(50.0, c.DefaultServiceRadiusMiles);
    }

    // ── RequestNote ───────────────────────────────────────────────────────────

    [Fact]
    public void RequestNote_PropertiesRoundTrip()
    {
        var note = new RequestNote
        {
            Id         = 1,
            RequestId  = 10,
            AuthorId   = "user-123",
            AuthorName = "Staff Member",
            Content    = "Called family, left voicemail.",
            IsInternal = true
        };
        Assert.Equal(1, note.Id);
        Assert.Equal("Called family, left voicemail.", note.Content);
        Assert.True(note.IsInternal);
    }

    [Fact]
    public void RequestNote_DefaultIsInternal_IsTrue()
    {
        var note = new RequestNote();
        Assert.True(note.IsInternal);
    }

    // ── RequestActivity ───────────────────────────────────────────────────────

    [Fact]
    public void RequestActivity_PropertiesRoundTrip()
    {
        var act = new RequestActivity
        {
            Id           = 5,
            RequestId    = 10,
            ActorId      = "staff-1",
            ActorName    = "Jane Staff",
            ActivityType = ActivityType.StatusChanged,
            OldValue     = "New",
            NewValue     = "InProgress",
            Details      = "Moved to in-progress"
        };
        Assert.Equal(ActivityType.StatusChanged, act.ActivityType);
        Assert.Equal("New", act.OldValue);
        Assert.Equal("InProgress", act.NewValue);
    }

    [Theory]
    [InlineData(ActivityType.StatusChanged)]
    [InlineData(ActivityType.Assigned)]
    [InlineData(ActivityType.Reassigned)]
    [InlineData(ActivityType.NoteAdded)]
    [InlineData(ActivityType.DueDateSet)]
    [InlineData(ActivityType.PriorityChanged)]
    [InlineData(ActivityType.Fulfilled)]
    [InlineData(ActivityType.Cancelled)]
    [InlineData(ActivityType.Escalated)]
    [InlineData(ActivityType.Created)]
    public void ActivityType_AllValues_CanBeAssigned(ActivityType type)
    {
        var act = new RequestActivity { ActivityType = type };
        Assert.Equal(type, act.ActivityType);
    }

    // ── RequestAssignment ─────────────────────────────────────────────────────

    [Fact]
    public void RequestAssignment_DefaultStatus_IsPending()
    {
        var a = new RequestAssignment();
        Assert.Equal(AssignmentStatus.Pending, a.Status);
        Assert.Equal(1, a.AttemptNumber);
    }

    [Fact]
    public void RequestAssignment_PropertiesRoundTrip()
    {
        var deadline = DateTime.UtcNow.AddHours(24);
        var a = new RequestAssignment
        {
            Id                  = 1,
            RequestId           = 5,
            AssignedToId        = 12,
            AssignedToName      = "Vol Name",
            AssignedById        = "staff-1",
            AssignedByName      = "Staff",
            Status              = AssignmentStatus.Accepted,
            AcceptedAt          = DateTime.UtcNow,
            AcceptanceDeadline  = deadline,
            AttemptNumber       = 2
        };
        Assert.Equal(AssignmentStatus.Accepted, a.Status);
        Assert.Equal(2, a.AttemptNumber);
        Assert.NotNull(a.AcceptedAt);
    }

    [Theory]
    [InlineData(AssignmentStatus.Pending)]
    [InlineData(AssignmentStatus.Accepted)]
    [InlineData(AssignmentStatus.Declined)]
    [InlineData(AssignmentStatus.Expired)]
    [InlineData(AssignmentStatus.Reassigned)]
    [InlineData(AssignmentStatus.Completed)]
    public void AssignmentStatus_AllValues_CanBeAssigned(AssignmentStatus status)
    {
        var a = new RequestAssignment { Status = status };
        Assert.Equal(status, a.Status);
    }

    [Fact]
    public void RequestAssignment_DeclineFields_CanBeSet()
    {
        var a = new RequestAssignment
        {
            Status        = AssignmentStatus.Declined,
            DeclinedAt    = DateTime.UtcNow,
            DeclineReason = "Schedule conflict"
        };
        Assert.Equal("Schedule conflict", a.DeclineReason);
        Assert.NotNull(a.DeclinedAt);
    }

    // ── EventAttendee ─────────────────────────────────────────────────────────

    [Fact]
    public void EventAttendee_DefaultTicketCount_IsOne()
    {
        var a = new EventAttendee();
        Assert.Equal(1, a.TicketCount);
        Assert.False(a.CheckedIn);
    }

    [Fact]
    public void EventAttendee_CheckIn_Fields()
    {
        var now = DateTime.UtcNow;
        var a = new EventAttendee
        {
            EventId        = 1,
            DonorId        = 5,
            TicketCount    = 2,
            AmountPaid     = 150m,
            PaymentChannel = DonationChannel.Online,
            CheckedIn      = true,
            CheckedInAt    = now,
            Notes          = "VIP table"
        };
        Assert.True(a.CheckedIn);
        Assert.Equal(now, a.CheckedInAt);
        Assert.Equal(DonationChannel.Online, a.PaymentChannel);
    }

    // ── SilentAuctionItem + AuctionBid ────────────────────────────────────────

    [Fact]
    public void SilentAuctionItem_DefaultStatus_IsAvailable()
    {
        var item = new SilentAuctionItem();
        Assert.Equal(AuctionItemStatus.Available, item.Status);
        Assert.Empty(item.Bids);
    }

    [Fact]
    public void SilentAuctionItem_PropertiesRoundTrip()
    {
        var item = new SilentAuctionItem
        {
            Id              = 1,
            EventId         = 10,
            Name            = "Wine Basket",
            Description     = "Assorted wines",
            FairMarketValue = 200m,
            StartingBid     = 50m,
            WinningBid      = 175m,
            WinnerId        = 3,
            Status          = AuctionItemStatus.Sold
        };
        Assert.Equal(AuctionItemStatus.Sold, item.Status);
        Assert.Equal(175m, item.WinningBid);
    }

    [Theory]
    [InlineData(AuctionItemStatus.Available)]
    [InlineData(AuctionItemStatus.Sold)]
    [InlineData(AuctionItemStatus.Unsold)]
    public void AuctionItemStatus_AllValues_CanBeAssigned(AuctionItemStatus status)
    {
        var item = new SilentAuctionItem { Status = status };
        Assert.Equal(status, item.Status);
    }

    [Fact]
    public void AuctionBid_PropertiesRoundTrip()
    {
        var bid = new AuctionBid
        {
            Id            = 1,
            AuctionItemId = 5,
            BidderId      = 10,
            BidAmount     = 125m
        };
        Assert.Equal(125m, bid.BidAmount);
        Assert.Equal(5, bid.AuctionItemId);
    }

    [Fact]
    public void SilentAuctionItem_CanAddBids()
    {
        var item = new SilentAuctionItem { Id = 1, Name = "Painting", StartingBid = 100m };
        item.Bids.Add(new AuctionBid { AuctionItemId = 1, BidderId = 1, BidAmount = 150m });
        item.Bids.Add(new AuctionBid { AuctionItemId = 1, BidderId = 2, BidAmount = 200m });
        Assert.Equal(2, item.Bids.Count);
        Assert.Equal(200m, item.Bids.Max(b => b.BidAmount));
    }

    // ── Expense ───────────────────────────────────────────────────────────────

    [Fact]
    public void Expense_PropertiesRoundTrip()
    {
        var paidAt = DateTime.UtcNow.AddDays(-1);
        var e = new Expense
        {
            Id             = 1,
            ChapterId      = 2,
            Description    = "Packaging supplies",
            Amount         = 84.50m,
            Category       = "Supplies",
            PaidAt         = paidAt,
            PaidBy         = "staff-1",
            ReceiptBlobUrl = "https://blob.example.com/receipt-1.pdf",
            Notes          = "Monthly box order"
        };
        Assert.Equal(84.50m, e.Amount);
        Assert.Equal("Supplies", e.Category);
        Assert.Equal(paidAt, e.PaidAt);
        Assert.NotNull(e.ReceiptBlobUrl);
    }

    // ── PackageRequest — remaining status branches ────────────────────────────

    [Fact]
    public void IsOverdue_AwaitingShipment_OlderThan7Days_IsTrue()
    {
        var r = new PackageRequest
        {
            Status    = CaseStatus.AwaitingShipment,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        Assert.True(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_OnHold_OlderThan7Days_IsFalse()
    {
        // OnHold is explicitly paused — should not trigger overdue alerts
        var r = new PackageRequest
        {
            Status    = CaseStatus.OnHold,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        Assert.False(r.IsOverdue);
    }

    [Theory]
    [InlineData(RequestPriority.Urgent)]
    [InlineData(RequestPriority.High)]
    [InlineData(RequestPriority.Normal)]
    [InlineData(RequestPriority.Low)]
    public void RequestPriority_AllValues_CanBeAssigned(RequestPriority p)
    {
        var r = new PackageRequest { Priority = p };
        Assert.Equal(p, r.Priority);
    }

    [Theory]
    [InlineData(RequestCategory.PackageDelivery)]
    [InlineData(RequestCategory.PrayerSupport)]
    [InlineData(RequestCategory.CounselingReferral)]
    [InlineData(RequestCategory.HospitalVisit)]
    [InlineData(RequestCategory.Memorial)]
    [InlineData(RequestCategory.ResourceProvision)]
    [InlineData(RequestCategory.Other)]
    public void RequestCategory_AllValues_CanBeAssigned(RequestCategory c)
    {
        var r = new PackageRequest { Category = c };
        Assert.Equal(c, r.Category);
    }

    // ── Family — remaining enum values ────────────────────────────────────────

    [Theory]
    [InlineData(FamilyStatus.Active)]
    [InlineData(FamilyStatus.Closed)]
    [InlineData(FamilyStatus.FollowUp)]
    [InlineData(FamilyStatus.Referred)]
    public void FamilyStatus_AllValues_CanBeAssigned(FamilyStatus status)
    {
        var f = new Family { Status = status };
        Assert.Equal(status, f.Status);
    }

    // ── Volunteer — remaining enum values ─────────────────────────────────────

    [Fact]
    public void VolunteerStatus_Onboarding_CanBeAssigned()
    {
        var v = new Volunteer { Status = VolunteerStatus.Onboarding };
        Assert.Equal(VolunteerStatus.Onboarding, v.Status);
    }

    [Fact]
    public void Volunteer_DefaultServiceRadius_Is25Miles()
    {
        var v = new Volunteer();
        Assert.Equal(25.0, v.ServiceRadiusMiles);
    }

    // ── Donor — DonorTier values ──────────────────────────────────────────────

    [Theory]
    [InlineData(DonorTier.Friend)]
    [InlineData(DonorTier.Supporter)]
    [InlineData(DonorTier.Champion)]
    [InlineData(DonorTier.Benefactor)]
    public void DonorTier_AllValues_CanBeAssigned(DonorTier tier)
    {
        var d = new Donor { Tier = tier };
        Assert.Equal(tier, d.Tier);
    }

    [Fact]
    public void Donor_AnonymousFlag_DefaultsFalse()
    {
        var d = new Donor();
        Assert.False(d.IsAnonymous);
    }

    // ── Reporting DTOs ────────────────────────────────────────────────────────

    [Fact]
    public void VolunteerScoreResult_PropertiesAccessible()
    {
        var r = new VolunteerScoreResult(1, "Jane Doe", 0.9, 0.8, 0.7, 0.85, true, 3.5);
        Assert.Equal(1, r.VolunteerId);
        Assert.Equal("Jane Doe", r.Name);
        Assert.True(r.Recommended);
        Assert.Equal(3.5, r.DistanceMiles);
    }

    [Fact]
    public void ChapterSummaryRow_PropertiesAccessible()
    {
        var r = new ChapterSummaryRow(1, "Chicago", 10, 2, 5, 5000m, 12);
        Assert.Equal(1, r.ChapterId);
        Assert.Equal(10, r.OpenRequests);
        Assert.Equal(5000m, r.TotalDonations);
    }

    [Fact]
    public void DailyDigestReport_PropertiesAccessible()
    {
        var date = DateTime.UtcNow.Date;
        var r = new DailyDigestReport(date, 1, "Chicago", 3, 2, 500m, 1, 0, 1);
        Assert.Equal(date, r.ReportDate);
        Assert.Equal(3, r.NewRequests);
        Assert.Equal(500m, r.DonationAmount);
    }

    [Fact]
    public void WeeklySummaryReport_PropertiesAccessible()
    {
        var week = DateTime.UtcNow.Date.AddDays(-7);
        var r = new WeeklySummaryReport(week, 1, "Chicago", 5, 3, 4, 2, 1000m, 800m, 15, 1);
        Assert.Equal(week, r.WeekStarting);
        Assert.Equal(5, r.NewRequestsThisWeek);
        Assert.Equal(1000m, r.DonationsThisWeek);
    }

    [Fact]
    public void DonationByPersonRow_PropertiesAccessible()
    {
        var first = new DateTime(2025, 1, 1);
        var last  = new DateTime(2025, 6, 1);
        var r = new DonationByPersonRow(1, "John Doe", "Chicago Diocese", "Chicago", "IL", 1500m, 3, 500m, first, last);
        Assert.Equal("John Doe", r.Name);
        Assert.Equal(1500m, r.TotalAmount);
        Assert.Equal(3, r.GiftCount);
    }

    [Fact]
    public void DonationByDioceseRow_PropertiesAccessible()
    {
        var r = new DonationByDioceseRow(1, "Chicago Diocese", "Chicago", "IL", 25, 10000m, 400m);
        Assert.Equal("Chicago Diocese", r.DioceseName);
        Assert.Equal(10000m, r.TotalAmount);
        Assert.Equal(25, r.TotalDonors);
    }

    [Fact]
    public void DonationByCityRow_PropertiesAccessible()
    {
        var r = new DonationByCityRow("Chicago", "IL", 10, 5000m);
        Assert.Equal("Chicago", r.City);
        Assert.Equal("IL", r.State);
        Assert.Equal(5000m, r.TotalAmount);
    }

    [Fact]
    public void DonationByChannelRow_PropertiesAccessible()
    {
        var r = new DonationByChannelRow("Online", 3000m, 15, 42.5);
        Assert.Equal("Online", r.Channel);
        Assert.Equal(3000m, r.TotalAmount);
        Assert.Equal(15, r.GiftCount);
        Assert.Equal(42.5, r.Percentage);
    }

    [Fact]
    public void DonationByAmountBand_PropertiesAccessible()
    {
        var r = new DonationByAmountBand("$100–$499", 8, 2400m, 30.0);
        Assert.Equal("$100–$499", r.Band);
        Assert.Equal(8, r.GiftCount);
        Assert.Equal(2400m, r.TotalAmount);
    }

    [Fact]
    public void ImpactSummary_NullChapterId_RepresentsHqRollup()
    {
        var start = new DateTime(2025, 1, 1);
        var end   = new DateTime(2025, 12, 31);
        var s = new ImpactSummary(null, 50000m, 45000m, 120, 98, 15, start, end);
        Assert.Null(s.ChapterId);
        Assert.Equal(50000m, s.TotalMoneyRaised);
        Assert.Equal(120, s.PeopleHelped);
    }

    [Fact]
    public void DonorImpactStatement_PropertiesAccessible()
    {
        var s = new DonorImpactStatement(1, "Jane Donor", 500m, 3, "Chicago Chapter", "Chicago");
        Assert.Equal("Jane Donor", s.DonorName);
        Assert.Equal(500m, s.TotalGiven);
        Assert.Equal(3, s.PeopleHelped);
    }
}
