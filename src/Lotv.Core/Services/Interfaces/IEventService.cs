using Lotv.Core.Common;
using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public interface IEventService
{
    Task<List<MinistryEvent>> GetEventsAsync(int chapterId, EventStatus? status = null);
    Task<MinistryEvent?> GetEventAsync(int id);
    Task<List<MinistryEvent>> GetUpcomingAsync();
    Task<Result<MinistryEvent>> CreateAsync(MinistryEvent evt);
    Task<Result> UpdateAsync(MinistryEvent evt);
    Task<Result> CancelAsync(int id, string cancelledBy);

    // Attendees
    Task<List<EventAttendee>> GetAttendeesAsync(int eventId);
    Task<Result<EventAttendee>> RegisterAttendeeAsync(EventAttendee attendee);
    Task<Result> CheckInAsync(int attendeeId);
    Task<decimal> GetEventRevenueAsync(int eventId);

    // Silent Auction
    Task<List<SilentAuctionItem>> GetAuctionItemsAsync(int eventId);
    Task<Result<SilentAuctionItem>> AddAuctionItemAsync(SilentAuctionItem item);
    Task<Result> UpdateAuctionItemAsync(SilentAuctionItem item);
    Task<Result<AuctionBid>> PlaceBidAsync(int itemId, int bidderId, decimal amount);
    Task<Result> CloseAuctionAsync(int eventId, string closedBy);
}
