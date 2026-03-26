namespace Lotv.Core.Models;

public class SilentAuctionItem
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public MinistryEvent? Event { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal FairMarketValue { get; set; }
    public decimal StartingBid { get; set; }
    public decimal? WinningBid { get; set; }
    public int? WinnerId { get; set; }
    public Donor? Winner { get; set; }
    public AuctionItemStatus Status { get; set; } = AuctionItemStatus.Available;
    public List<AuctionBid> Bids { get; set; } = [];
}

public class AuctionBid
{
    public int Id { get; set; }
    public int AuctionItemId { get; set; }
    public SilentAuctionItem? AuctionItem { get; set; }
    public int BidderId { get; set; }
    public Donor? Bidder { get; set; }
    public decimal BidAmount { get; set; }
    public DateTime BidTime { get; set; } = DateTime.UtcNow;
}

public enum AuctionItemStatus
{
    Available,
    Sold,
    Unsold
}
