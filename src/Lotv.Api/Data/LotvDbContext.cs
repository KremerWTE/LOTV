using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

public class LotvDbContext : IdentityDbContext<LotvIdentityUser>
{
    public LotvDbContext(DbContextOptions<LotvDbContext> options) : base(options) { }

    // ─── Org structure ───────────────────────────────────────────────────────
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Diocese> Dioceses => Set<Diocese>();
    public DbSet<Parish> Parishes => Set<Parish>();

    // ─── Families & requests ─────────────────────────────────────────────────
    public DbSet<Family> Families => Set<Family>();
    public DbSet<PackageRequest> Requests => Set<PackageRequest>();
    public DbSet<RequestNote> RequestNotes => Set<RequestNote>();
    public DbSet<RequestActivity> RequestActivities => Set<RequestActivity>();
    public DbSet<RequestAssignment> RequestAssignments => Set<RequestAssignment>();

    // ─── People ──────────────────────────────────────────────────────────────
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<Donor> Donors => Set<Donor>();

    // ─── Donations & money ───────────────────────────────────────────────────
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<FundAllocation> FundAllocations => Set<FundAllocation>();
    public DbSet<Expense> Expenses => Set<Expense>();

    // ─── Events & auction ────────────────────────────────────────────────────
    public DbSet<MinistryEvent> Events => Set<MinistryEvent>();
    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
    public DbSet<SilentAuctionItem> AuctionItems => Set<SilentAuctionItem>();
    public DbSet<AuctionBid> AuctionBids => Set<AuctionBid>();

    // ─── Inventory ───────────────────────────────────────────────────────────
    public DbSet<ResourceItem> ResourceItems => Set<ResourceItem>();

    // ─── Recurring giving & pledges ──────────────────────────────────────────
    public DbSet<RecurringDonation> RecurringDonations => Set<RecurringDonation>();
    public DbSet<DonorPledge> DonorPledges => Set<DonorPledge>();

    // ─── Wish list ────────────────────────────────────────────────────────────
    public DbSet<WishListItem> WishListItems => Set<WishListItem>();

    // ─── SMS log ──────────────────────────────────────────────────────────────
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();

    // ─── Sponsors ────────────────────────────────────────────────────────────
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();

    // ─── Partner API keys ────────────────────────────────────────────────────
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    // ─── Auth & audit ────────────────────────────────────────────────────────
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // ─── Settings ────────────────────────────────────────────────────────────
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // ─── Retreats ─────────────────────────────────────────────────────────────
    public DbSet<Retreat> Retreats => Set<Retreat>();
    public DbSet<RetreatRegistration> RetreatRegistrations => Set<RetreatRegistration>();
    public DbSet<RetreatExpense> RetreatExpenses => Set<RetreatExpense>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Chapter ──────────────────────────────────────────────────────────
        builder.Entity<Chapter>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
        });

        // ── Diocese ──────────────────────────────────────────────────────────
        builder.Entity<Diocese>(e =>
        {
            e.HasIndex(d => d.ChapterId);
        });

        // ── Parish ───────────────────────────────────────────────────────────
        builder.Entity<Parish>(e =>
        {
            e.HasIndex(p => p.ChapterId);
            e.HasIndex(p => p.DioceseId);
        });

        // ── Family ───────────────────────────────────────────────────────────
        builder.Entity<Family>(e =>
        {
            e.HasIndex(f => f.ChapterId);
            e.Ignore(f => f.Chapter);   // navigation — not FK enforced at DB level for seed simplicity
        });

        // ── PackageRequest ───────────────────────────────────────────────────
        builder.Entity<PackageRequest>(e =>
        {
            e.HasIndex(r => r.ChapterId);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.AssignedToId);
            e.HasIndex(r => r.CreatedAt);                                        // overdue detection (global)
            e.HasIndex(r => new { r.ChapterId, r.Status });                      // most common dashboard query
            e.HasIndex(r => new { r.ChapterId, r.AssignedToId });                // workload view per chapter
            e.HasIndex(r => new { r.ChapterId, r.Status, r.CreatedAt });         // overdue-by-chapter (status filter + age sort)
            e.HasOne(r => r.Family).WithMany().HasForeignKey(r => r.FamilyId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(r => r.Chapter);
        });

        // ── RequestNote ──────────────────────────────────────────────────────
        builder.Entity<RequestNote>(e =>
        {
            e.HasIndex(n => n.RequestId);
            e.HasOne(n => n.Request).WithMany().HasForeignKey(n => n.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── RequestActivity ──────────────────────────────────────────────────
        builder.Entity<RequestActivity>(e =>
        {
            e.HasIndex(a => a.RequestId);
            e.HasOne(a => a.Request).WithMany().HasForeignKey(a => a.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── RequestAssignment ────────────────────────────────────────────────
        builder.Entity<RequestAssignment>(e =>
        {
            e.HasIndex(a => a.RequestId);
            e.HasOne(a => a.Request).WithMany().HasForeignKey(a => a.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Volunteer ────────────────────────────────────────────────────────
        builder.Entity<Volunteer>(e =>
        {
            e.HasIndex(v => v.ChapterId);
            e.HasIndex(v => new { v.ChapterId, v.Status });  // active volunteer workload queries
            e.Ignore(v => v.Chapter);
        });

        // ── Donor ────────────────────────────────────────────────────────────
        builder.Entity<Donor>(e =>
        {
            e.HasIndex(d => d.ChapterId);
            e.HasIndex(d => d.Email);                               // de-dupe on public signup
            e.HasIndex(d => new { d.ChapterId, d.TotalGiven });     // top-donor leaderboard / tier sort
            e.Property(d => d.TotalGiven).HasPrecision(12, 2);
            e.Property(d => d.RecurringAmount).HasPrecision(12, 2);
            e.Ignore(d => d.Chapter);
        });

        // ── Donation ─────────────────────────────────────────────────────────
        builder.Entity<Donation>(e =>
        {
            e.HasIndex(d => d.ChapterId);
            e.HasIndex(d => d.DonorId);
            e.HasIndex(d => d.AllocationStatus);
            e.HasIndex(d => d.Channel);                             // by-channel report
            e.HasIndex(d => new { d.ChapterId, d.Date });           // date-range dashboard aggregates
            e.HasIndex(d => new { d.ChapterId, d.Channel, d.Date }); // channel breakdown over time
            e.Property(d => d.Amount).HasPrecision(12, 2);
            e.HasOne(d => d.Donor).WithMany().HasForeignKey(d => d.DonorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── FundAllocation ───────────────────────────────────────────────────
        builder.Entity<FundAllocation>(e =>
        {
            e.HasIndex(a => a.DonationId);
            e.HasIndex(a => a.Status);                      // PendingReview filter for approval queue
            e.Property(a => a.Amount).HasPrecision(12, 2);
            e.HasOne(a => a.Donation).WithMany().HasForeignKey(a => a.DonationId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Expense ──────────────────────────────────────────────────────────
        builder.Entity<Expense>(e =>
        {
            e.HasIndex(ex => ex.ChapterId);
            e.HasIndex(ex => new { ex.ChapterId, ex.PaidAt });   // monthly expense aggregates
            e.Property(ex => ex.Amount).HasPrecision(12, 2);
            e.Ignore(ex => ex.Chapter);
        });

        // ── MinistryEvent ────────────────────────────────────────────────────
        builder.Entity<MinistryEvent>(e =>
        {
            e.HasIndex(ev => ev.ChapterId);
            e.HasIndex(ev => ev.Date);                          // upcoming events sort
            e.HasIndex(ev => new { ev.Status, ev.Date });       // published upcoming filter
            e.Ignore(ev => ev.Chapter);
        });

        // ── EventAttendee ────────────────────────────────────────────────────
        builder.Entity<EventAttendee>(e =>
        {
            e.HasIndex(a => a.EventId);
            e.HasIndex(a => a.DonorId);                         // donor event history lookup
            e.HasIndex(a => a.TicketCode).IsUnique();           // QR scan lookup
            e.Property(a => a.TicketCode).HasMaxLength(32);
            e.Property(a => a.AmountPaid).HasPrecision(12, 2);
            e.HasOne(a => a.Event).WithMany().HasForeignKey(a => a.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Donor).WithMany().HasForeignKey(a => a.DonorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── SilentAuctionItem ────────────────────────────────────────────────
        builder.Entity<SilentAuctionItem>(e =>
        {
            e.HasIndex(i => i.EventId);
            e.HasOne(i => i.Event).WithMany().HasForeignKey(i => i.EventId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(i => i.Winner);
        });

        // ── AuctionBid ───────────────────────────────────────────────────────
        builder.Entity<AuctionBid>(e =>
        {
            e.HasIndex(b => b.AuctionItemId);
            e.HasOne(b => b.AuctionItem).WithMany(i => i.Bids).HasForeignKey(b => b.AuctionItemId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(b => b.Bidder);
        });

        // ── RefreshToken ─────────────────────────────────────────────────────
        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.Token).IsUnique();
            e.HasIndex(t => t.UserId);
        });

        // ── ResourceItem ─────────────────────────────────────────────────────
        builder.Entity<ResourceItem>(e =>
        {
            e.HasIndex(r => r.ChapterId);
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Unit).HasMaxLength(30);
        });

        // ── ApiKey ───────────────────────────────────────────────────────────
        builder.Entity<ApiKey>(e =>
        {
            e.HasIndex(k => k.KeyHash).IsUnique();
            e.HasIndex(k => k.IsActive);
            e.Property(k => k.PartnerName).HasMaxLength(100);
            e.Property(k => k.KeyHash).HasMaxLength(64);
        });

        // ── SmsLog ───────────────────────────────────────────────────────────
        builder.Entity<SmsLog>(e =>
        {
            e.HasIndex(s => s.SentAt);
            e.HasIndex(s => s.CaseId);
            e.HasIndex(s => s.MessageType);
            e.Property(s => s.ToPhoneNumber).HasMaxLength(20);
            e.Property(s => s.ProviderMessageId).HasMaxLength(50);
        });

        // ── WishListItem ──────────────────────────────────────────────────────
        builder.Entity<WishListItem>(e =>
        {
            e.HasIndex(w => w.ChapterId);
            e.HasIndex(w => w.FamilyId);
            e.HasIndex(w => w.Status);
            e.HasIndex(w => new { w.ChapterId, w.Status });   // chapter open wish-list browse
            e.Property(w => w.Title).HasMaxLength(150);
            e.HasOne(w => w.Family).WithMany().HasForeignKey(w => w.FamilyId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        // ── RecurringDonation ─────────────────────────────────────────────────
        builder.Entity<RecurringDonation>(e =>
        {
            e.HasIndex(r => r.DonorId);
            e.HasIndex(r => r.ChapterId);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.NextChargeDate);                    // scheduler needs upcoming charges
            e.HasIndex(r => new { r.ChapterId, r.Status });       // chapter active schedules
            e.Property(r => r.Amount).HasPrecision(12, 2);
            e.Property(r => r.Campaign).HasMaxLength(100);
            e.HasOne(r => r.Donor).WithMany().HasForeignKey(r => r.DonorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── DonorPledge ──────────────────────────────────────────────────────
        builder.Entity<DonorPledge>(e =>
        {
            e.HasIndex(p => p.DonorId);
            e.HasIndex(p => p.ChapterId);
            e.HasIndex(p => p.Status);
            e.HasIndex(p => p.TargetDate);                        // overdue detection
            e.HasIndex(p => new { p.ChapterId, p.Status });       // chapter active pledges
            e.Property(p => p.PledgedAmount).HasPrecision(12, 2);
            e.Property(p => p.FulfilledAmount).HasPrecision(12, 2);
            e.Property(p => p.Campaign).HasMaxLength(100);
            e.HasOne(p => p.Donor).WithMany().HasForeignKey(p => p.DonorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── AuditEntry ───────────────────────────────────────────────────────
        builder.Entity<AuditEntry>(e =>
        {
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.Entity);                          // filter by entity type (FundAllocation export)
            e.HasIndex(a => new { a.Entity, a.Timestamp });     // entity-scoped time-ordered audit trail
        });

        // ── AppSetting ───────────────────────────────────────────────────────
        builder.Entity<AppSetting>(e =>
        {
            e.HasIndex(s => new { s.ChapterId, s.Key }).IsUnique();
            e.Property(s => s.Key).HasMaxLength(100);
            e.Property(s => s.Value).HasMaxLength(1000);
        });

        // ── Retreat ───────────────────────────────────────────────────────────
        builder.Entity<Retreat>(e =>
        {
            e.HasIndex(r => r.ChapterId);
            e.HasIndex(r => r.Date);
            e.HasIndex(r => new { r.ChapterId, r.Status });
            e.Property(r => r.TicketPrice).HasPrecision(12, 2);
            e.Property(r => r.GoalAmount).HasPrecision(12, 2);
            e.Ignore(r => r.Chapter);
        });

        // ── RetreatRegistration ───────────────────────────────────────────────
        builder.Entity<RetreatRegistration>(e =>
        {
            e.HasIndex(r => r.RetreatId);
            e.HasIndex(r => r.ChapterId);
            e.HasIndex(r => new { r.RetreatId, r.ChapterId });
            e.HasIndex(r => r.GiveButterTransactionId).IsUnique().HasFilter("[GiveButterTransactionId] IS NOT NULL");
            e.HasIndex(r => r.DudaSubmissionId).IsUnique().HasFilter("[DudaSubmissionId] IS NOT NULL");
            e.HasIndex(r => r.PaymentStatus);
            e.HasIndex(r => r.RegistrationSource);
            e.Property(r => r.AmountPaid).HasPrecision(12, 2);
            e.HasOne(r => r.Retreat).WithMany(rt => rt.Registrations).HasForeignKey(r => r.RetreatId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── RetreatExpense ─────────────────────────────────────────────────────
        builder.Entity<RetreatExpense>(e =>
        {
            e.HasIndex(ex => ex.RetreatId);
            e.HasIndex(ex => ex.ChapterId);
            e.HasIndex(ex => new { ex.RetreatId, ex.Category });
            e.Property(ex => ex.Amount).HasPrecision(12, 2);
            e.HasOne(ex => ex.Retreat).WithMany(r => r.Expenses).HasForeignKey(ex => ex.RetreatId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Sponsor ──────────────────────────────────────────────────────────
        builder.Entity<Sponsor>(e =>
        {
            e.HasIndex(s => s.ChapterId);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.Email);
            e.Property(s => s.CompanyName).HasMaxLength(150);
            e.Property(s => s.ContactName).HasMaxLength(100);
            e.Property(s => s.Email).HasMaxLength(200);
            e.Property(s => s.CommittedAmount).HasPrecision(12, 2);
            e.Property(s => s.PaidToDate).HasPrecision(12, 2);
            e.HasOne(s => s.Chapter).WithMany().HasForeignKey(s => s.ChapterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
