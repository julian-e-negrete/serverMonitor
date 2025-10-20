namespace serverMonitor.Models;
public class MarketDataRecord
{
    public int Id { get; set; }
    public DateTime Time { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public decimal BidVolume { get; set; }
    public decimal BidPrice { get; set; }
    public decimal AskPrice { get; set; }
    public decimal AskVolume { get; set; }
    public decimal LastPrice { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal Low { get; set; }
    public decimal High { get; set; }
    public decimal PrevClose { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Calculated properties
    public decimal Spread => AskPrice - BidPrice;
    public decimal MidPrice => (BidPrice + AskPrice) / 2;
}
