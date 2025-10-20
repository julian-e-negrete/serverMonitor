namespace ServerMonitor.Models
{
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
        
        public decimal Spread => AskPrice - BidPrice;
        public decimal MidPrice => (BidPrice + AskPrice) / 2;
    }

    public class MepCalculation
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public decimal Al30Price { get; set; }
        public decimal Al30DPrice { get; set; }
        public decimal MepRate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CauctionData
    {
        public string Description { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string Volume { get; set; } = string.Empty;
        public double RawVolume { get; set; }
        public string Variation { get; set; } = string.Empty;
        public decimal RawVariation { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class FinancialRecord
    {
        public int Id { get; set; }
        public string Instrument { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public decimal Rate { get; set; }
        public decimal Volume { get; set; }
        public decimal Variation { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime RecordDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class HistoricalDataRequest
    {
        public string Instrument { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int MaxRecords { get; set; } = 100;
    }

    public class MarketDataRequest
    {
        public string[] Instruments { get; set; } = Array.Empty<string>();
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int MaxRecords { get; set; } = 1000;
    }
}