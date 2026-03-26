# LOTV Data Model
**Version**: 1.0
**Status**: Approved — Phase 1
**Last Updated**: 2026-03-25

---

## Entity Relationship Overview

```
Diocese 1──* Parish
Diocese 1──* Donor (via DioceseName)
Chapter 1──* ApplicationUser
Chapter 1──* PackageRequest
Chapter 1──* Donation

Family 1──* PackageRequest
PackageRequest *──1 Volunteer (AssignedTo)
PackageRequest 1──* RequestNote
PackageRequest 1──* RequestActivity
PackageRequest 1──* RequestAssignment

Donor 1──* Donation
Donation 1──* FundAllocation
FundAllocation *──1 PackageRequest (or Expense)

FundraisingEvent 1──* EventAttendee
FundraisingEvent 1──* SilentAuctionItem
SilentAuctionItem 1──* AuctionBid
EventAttendee *──1 Donor
```

---

## Core Entities

### Family (PersonInNeed intake record)
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| ChapterId | int | FK → Chapter; scopes record to chapter |
| Parent1FirstName | string | Required |
| Parent1LastName | string | Required |
| Parent2FirstName | string? | Optional second parent |
| Parent2LastName | string? | |
| Email | string | Required; used for contact and deduplication |
| Phone | string? | |
| StreetAddress | string | |
| Apt | string? | |
| City | string | |
| State | string | 2-letter code |
| Zip | string | |
| Reason | PackageReason | Enum; reason for requesting support |
| FaithTradition | string? | Self-reported; not required |
| ChildrenInitials | string? | For bracelet personalization |
| Story | string? | Optional free-text; treated as sensitive PII |
| ParishName | string? | |
| DioceseName | string? | |
| HowHeard | string? | Referral source |
| CreatedAt | DateTime | UTC; set on intake |
| Status | FamilyStatus | Active / Closed / FollowUp / Referred |
| ContactNotes | string? | Staff-only notes; not visible to family |
| AnonymizedAt | DateTime? | Set when PII is scrubbed per retention policy |

**Computed**: `FullName` — "Parent1First & Parent2First LastName" or "Parent1First LastName"

---

### PackageRequest (ServiceRequest / Case)
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| ChapterId | int | FK → Chapter |
| FamilyId | int | FK → Family |
| Family | Family? | Navigation property |
| Reason | PackageReason | Copied from Family at creation |
| Status | CaseStatus | Full lifecycle (see below) |
| Priority | RequestPriority | Urgent / High / Normal / Low |
| AssignedToId | int? | FK → Volunteer |
| AssignedTo | string? | Denormalized FullName for display |
| AssignedAt | DateTime? | When volunteer was assigned |
| DueDate | DateTime? | Staff-set SLA date |
| TrackingNumber | string? | Carrier tracking number |
| ShippedDate | DateTime? | |
| InternalNotes | string? | Staff-only |
| CreatedAt | DateTime | UTC |
| UpdatedAt | DateTime | UTC; updated on every status change |

**Computed**: `IsOverdue` — Status not in {Fulfilled, Shipped, Cancelled} AND CreatedAt < UtcNow − 7 days (configurable)

---

### Volunteer
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| ChapterId | int | FK → Chapter |
| UserId | string? | FK → ApplicationUser (once auth is wired) |
| FirstName | string | |
| LastName | string | |
| Email | string | |
| Phone | string? | |
| Role | VolunteerRole | PackageAssembler / PrayerAmbassador / ParishLiaison / EventHelper / Driver / Admin |
| Status | VolunteerStatus | Active / Inactive / Onboarding |
| ParishName | string? | |
| DioceseName | string? | |
| City | string? | For proximity scoring |
| State | string? | |
| Latitude | double? | Geocoded; used for Haversine scoring |
| Longitude | double? | |
| ServiceRadiusMiles | int | Default 25; used in auto-assignment |
| ActiveCases | int | Denormalized count; updated on assignment/close |
| TotalCasesFulfilled | int | Incremented on Fulfilled status |
| JoinedDate | DateTime | |
| Notes | string? | Internal |
| LastActivityAt | DateTime? | For inactivity detection |

---

### Donor
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| ChapterId | int | FK → Chapter |
| UserId | string? | FK → ApplicationUser |
| FirstName | string | |
| LastName | string | |
| Email | string | Required; used for deduplication on Give form |
| Phone | string? | |
| City | string? | |
| State | string? | |
| ParishName | string? | |
| DioceseName | string? | |
| IsRecurring | bool | |
| RecurringAmount | decimal? | Monthly recurring pledge |
| Tier | DonorTier | Friend / Supporter / Champion / Benefactor |
| FirstGiftDate | DateTime | |
| LastGiftDate | DateTime | |
| TotalGiven | decimal | Running total; updated on each donation |
| GiftCount | int | |
| IsAnonymous | bool | Suppresses name in public impact reports |
| Notes | string? | Internal |

---

### Donation
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| ChapterId | int | FK → Chapter |
| DonorId | int | FK → Donor |
| Donor | Donor? | Navigation |
| Amount | decimal | |
| Date | DateTime | Date of receipt |
| Channel | DonationChannel | Online / Check / Cash / Gala / SilentAuction / CorporateMatch / PlannedGiving / Other |
| Campaign | string? | "Year-End 2025", "Gala 2026", etc. |
| CheckNumber | string? | If channel = Check |
| EventId | int? | FK → FundraisingEvent (if from event) |
| ProcessorTransactionId | string? | Stripe charge ID |
| IsRecurring | bool | |
| AllocationStatus | AllocationStatus | Unallocated / PendingReview / Allocated / Restricted |
| AllocatedTo | string? | Program, case ID, or expense ref |
| Notes | string? | |
| ReceivedAt | DateTime | UTC |

---

### FundAllocation
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| ChapterId | int | FK → Chapter |
| DonationId | int | FK → Donation |
| Donation | Donation? | Navigation |
| Amount | decimal | Portion of donation allocated |
| AllocatedTo | string? | Description or case/expense ref |
| Status | AllocationStatus | |
| ApprovedBy | string? | Staff username |
| ApprovedAt | DateTime? | |
| Notes | string? | |
| CreatedAt | DateTime | |

---

### FundraisingEvent
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| ChapterId | int | FK → Chapter |
| Title | string | |
| EventType | EventType | Gala / SilentAuction / Dinner / Concert / GolfTournament / Walkathon / Other |
| Description | string? | |
| Date | DateTime | Start date/time |
| EndDate | DateTime? | |
| Location | string? | |
| IsVirtual | bool | |
| Capacity | int? | |
| TicketPrice | decimal? | |
| GoalAmount | decimal? | Fundraising goal |
| Status | EventStatus | Draft / Published / Open / Closed / Completed / Cancelled |
| Registered | int | Denormalized attendee count |
| CreatedBy | string | Staff username |
| CreatedAt | DateTime | |

---

### EventAttendee
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| EventId | int | FK → FundraisingEvent |
| DonorId | int? | FK → Donor (null for walk-ins) |
| GuestName | string? | If no donor record |
| TicketCount | int | |
| AmountPaid | decimal | |
| Channel | DonationChannel | How payment was received |
| CheckedIn | bool | |
| CheckedInAt | DateTime? | |
| Notes | string? | |

---

### SilentAuctionItem
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| EventId | int | FK → FundraisingEvent |
| Name | string | |
| Description | string? | |
| FairMarketValue | decimal | |
| StartingBid | decimal | |
| BidIncrement | decimal | Default $10 |
| WinningBid | decimal? | |
| WinnerId | int? | FK → Donor |
| Status | AuctionItemStatus | Available / Sold / Unsold |
| ClosedAt | DateTime? | |

---

### AuctionBid
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| AuctionItemId | int | FK → SilentAuctionItem |
| BidderId | int | FK → Donor |
| BidAmount | decimal | |
| BidTime | DateTime | UTC |
| IsWinning | bool | Denormalized; true for current high bid |

---

### Parish
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | |
| DioceseId | int | FK → Diocese |
| City | string? | |
| State | string? | |
| LiaisonName | string? | LOTV parish liaison |
| LiaisonEmail | string? | |
| CertificationLevel | CertificationLevel | None / Basic / Full |
| ActiveCases | int | Denormalized |
| TotalCasesFulfilled | int | |

---

### Diocese
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | |
| City | string? | |
| State | string | |
| CoordinatorName | string? | |
| CoordinatorEmail | string? | |
| TotalParishes | int | Denormalized |
| ActiveParishes | int | Denormalized |
| TotalDonations | decimal | Denormalized |
| TotalCasesFulfilled | int | Denormalized |

---

### Chapter
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | e.g., "Chicago Chapter" |
| City | string | |
| State | string | |
| ContactName | string | Chapter lead |
| ContactEmail | string | |
| ContactPhone | string? | |
| IsActive | bool | |
| CreatedAt | DateTime | |

---

### ApplicationUser
| Field | Type | Notes |
|---|---|---|
| Id | string | ASP.NET Identity GUID |
| Email | string | Login identity |
| FirstName | string | |
| LastName | string | |
| Role | UserRole | See auth-design.md |
| ChapterId | int? | Null for HQAdmin; required for all chapter roles |
| IsActive | bool | |
| CreatedAt | DateTime | |
| LastLoginAt | DateTime? | |

---

### RequestNote
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| RequestId | int | FK → PackageRequest |
| AuthorId | string | FK → ApplicationUser |
| AuthorName | string | Denormalized |
| Content | string | |
| IsInternal | bool | False = visible to volunteer; True = staff-only |
| CreatedAt | DateTime | |

---

### RequestActivity (immutable audit trail per request)
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| RequestId | int | FK → PackageRequest |
| ActorId | string | FK → ApplicationUser |
| ActivityType | ActivityType | StatusChanged / Assigned / Reassigned / NoteAdded / DueDateSet / Fulfilled / Cancelled / Escalated |
| OldValue | string? | JSON or plain string |
| NewValue | string? | |
| Timestamp | DateTime | UTC; immutable |

---

### AuditEntry (system-wide append-only log)
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| UserName | string | Who acted |
| Action | string | Created / Updated / Approved / Exported / Flagged / Deleted |
| Entity | string | Entity type name |
| EntityId | string? | Entity PK as string |
| Details | string? | Human-readable summary |
| IpAddress | string? | For security audit |
| Timestamp | DateTime | UTC; never updated |

---

## Enumerations

### PackageReason
`Infertility | PrenatalDiagnosis | PrenatalLifeLimitingDiagnosis | Miscarriage | Stillbirth | InfantLoss | PastLoss | Other`

### CaseStatus (full lifecycle)
`New | InProgress | AwaitingShipment | Shipped | Fulfilled | OnHold | Cancelled`

### RequestPriority
`Urgent | High | Normal | Low`

### FamilyStatus
`Active | Closed | FollowUp | Referred`

### VolunteerRole
`PackageAssembler | PrayerAmbassador | ParishLiaison | EventHelper | Driver | Admin`

### VolunteerStatus
`Active | Inactive | Onboarding`

### UserRole
`HQAdmin | ChapterAdmin | ChapterStaff | Volunteer | Donor | PublicUser`

### DonorTier
`Friend | Supporter | Champion | Benefactor`
Thresholds: Friend < $250 | Supporter $250–$999 | Champion $1,000–$4,999 | Benefactor $5,000+

### DonationChannel
`Online | Check | Cash | Gala | SilentAuction | CorporateMatch | PlannedGiving | Other`

### AllocationStatus
`Unallocated | PendingReview | Allocated | Restricted`

### EventType
`Gala | SilentAuction | Dinner | Concert | GolfTournament | Walkathon | Other`

### EventStatus
`Draft | Published | Open | Closed | Completed | Cancelled`

### AuctionItemStatus
`Available | Sold | Unsold`

### ActivityType
`StatusChanged | Assigned | Reassigned | NoteAdded | DueDateSet | Fulfilled | Cancelled | Escalated`

### CertificationLevel
`None | Basic | Full`

---

## Lifecycle State Machines

### Service Request (Case) Lifecycle

```
[New] ──assign──> [InProgress]
[New] ──hold──> [OnHold]
[New] ──cancel──> [Cancelled]
[InProgress] ──ship-ready──> [AwaitingShipment]
[InProgress] ──hold──> [OnHold]
[InProgress] ──cancel──> [Cancelled]
[AwaitingShipment] ──shipped──> [Shipped]
[AwaitingShipment] ──hold──> [OnHold]
[Shipped] ──confirmed──> [Fulfilled]
[OnHold] ──resume──> [InProgress]
[OnHold] ──cancel──> [Cancelled]
```

Overdue trigger: any non-terminal status (not Fulfilled, Shipped, Cancelled) open > 7 days.

### Donation / Money Lifecycle

```
[Unallocated] ──flag for review──> [PendingReview]
[PendingReview] ──approve──> [Allocated]
[Allocated] ──restrict──> [Restricted]   (earmarked, cannot be reallocated)
[Unallocated] ──direct allocate──> [Allocated]
```

### Volunteer Assignment Lifecycle

```
[Unassigned] ──auto-score + assign──> [Pending Acceptance]
[Pending Acceptance] ──accept (within window)──> [Accepted / Active]
[Pending Acceptance] ──decline or timeout──> [Reassignment Queue]
[Reassignment Queue] ──next volunteer scored──> [Pending Acceptance]
[Accepted / Active] ──case fulfilled──> [Completed]
[Accepted / Active] ──manual reassign──> [Reassignment Queue]
```

### Event Lifecycle

```
[Draft] ──publish──> [Published]
[Published] ──open registration──> [Open]
[Open] ──close registration──> [Closed]
[Closed] ──event occurs──> [Completed]
[Published | Open | Closed] ──cancel──> [Cancelled]
```

### Silent Auction Item Lifecycle

```
[Available] ──bidding opens (event = Open)──>  bids accepted
[Available] ──event closes──> winner determined:
  - if bids exist: WinningBid set, status = [Sold]
  - if no bids: status = [Unsold]
```

---

## Data Scoping Rules

| Role | Scope |
|---|---|
| HQAdmin | All chapters — no filter applied |
| ChapterAdmin | Own ChapterId only — enforced at service layer |
| ChapterStaff | Own ChapterId only |
| Volunteer | Own assigned cases only |
| Donor | Own giving history only |
| PublicUser | No authenticated data access; read-only public pages |

All queries for chapter-scoped roles **must** include `WHERE ChapterId = @chapterId` injected from the JWT claim. HQAdmin bypass is an explicit opt-in, never the default.
