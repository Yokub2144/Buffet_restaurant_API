namespace Buffet_Restaurant_API.Dtos
{
    // DTO สำหรับการ์ดสรุป 4 ช่องด้านบน
    public class DashboardOverviewDto
    {
        public decimal TodaySales { get; set; }
        public int TotalCustomersToday { get; set; }
        public string PeakTimeSlot { get; set; } = "-";
        public int PeakTimeCustomers { get; set; }
    }

    // DTO สำหรับไอเทมกราฟยอดขาย
    public class SalesChartItemDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    // DTO สำหรับ Response ของกราฟ
    public class SalesChartResponseDto
    {
        public string Type { get; set; } = "daily";
        public List<SalesChartItemDto> Data { get; set; } = new();
    }

    public class PeakTimeResult
    {
        public string PeakTimeSlot { get; set; } = "-";
        public object? PeakTimeCustomers { get; set; }
    }
    public class CashierDashboardStatsDto
    {
        public decimal NetRevenue { get; set; }
        public int TotalAdults { get; set; }
        public int TotalChildren { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalFines { get; set; }
        public decimal CashAmount { get; set; }
        public decimal TransferAmount { get; set; }
    }
}