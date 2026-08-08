using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Buffet_Restaurant_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly restaurantDbContext _context;

        public DashboardController(restaurantDbContext context)
        {
            _context = context;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<DashboardOverviewDto>> GetOverview()
        {
            try
            {
                using var connection = _context.Database.GetDbConnection();

                // 🟢 1. ตรวจสอบและเปิด Connection หากยังปิดอยู่
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                // 🟢 2. SQL ยอดขายวันนี้
                var sqlTodaySales = @"
            SELECT COALESCE(SUM(Amount), 0) 
            FROM Payment 
            WHERE DATE(PaymentDateTime) = CURDATE()";

                // 🟢 3. SQL ลูกค้าทั้งหมดวันนี้
                var sqlTotalCustomers = @"
            SELECT COALESCE(SUM(NumAdults + NumChildren), 0) 
            FROM bill 
            WHERE DATE(Created_at) = CURDATE()";

                // 🟢 4. SQL ช่วงเวลาคึกคักที่สุด (ปรับ GROUP BY ให้รองรับ sql_mode)
                var sqlPeakTime = @"
            SELECT 
                CONCAT(LPAD(HOUR(Created_at), 2, '0'), ':00-', LPAD(HOUR(Created_at) + 1, 2, '0'), ':00') AS PeakTimeSlot,
                COALESCE(SUM(NumAdults + NumChildren), 0) AS PeakTimeCustomers
            FROM bill
            WHERE DATE(Created_at) = CURDATE()
            GROUP BY HOUR(Created_at), CONCAT(LPAD(HOUR(Created_at), 2, '0'), ':00-', LPAD(HOUR(Created_at) + 1, 2, '0'), ':00')
            ORDER BY PeakTimeCustomers DESC
            LIMIT 1";

                // ดึงข้อมูลดิบแล้วใช้ Convert เพื่อแปลงชนิดข้อมูล MySQL อย่างปลอดภัย
                var rawSales = await connection.ExecuteScalarAsync(sqlTodaySales);
                var rawCustomers = await connection.ExecuteScalarAsync(sqlTotalCustomers);
                var peakTimeData = await connection.QueryFirstOrDefaultAsync<PeakTimeResult>(sqlPeakTime);

                var result = new DashboardOverviewDto
                {
                    TodaySales = rawSales != null ? Convert.ToDecimal(rawSales) : 0m,
                    TotalCustomersToday = rawCustomers != null ? Convert.ToInt32(rawCustomers) : 0,
                    PeakTimeSlot = peakTimeData?.PeakTimeSlot ?? "-",
                    PeakTimeCustomers = peakTimeData?.PeakTimeCustomers != null ? Convert.ToInt32(peakTimeData.PeakTimeCustomers) : 0
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                // พิมพ์ข้อผิดพลาดลงใน Console ของ C# สำหรับตรวจสอบ
                Console.WriteLine($"[Dashboard Overview Error]: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET: api/dashboard/sales-chart?type=daily
        [HttpGet("sales-chart")]
        public async Task<ActionResult<SalesChartResponseDto>> GetSalesChart([FromQuery] string type = "daily")
        {
            using var connection = _context.Database.GetDbConnection();
            string sqlQuery = string.Empty;

            switch (type.ToLower())
            {
                case "monthly":
                    sqlQuery = @"
                SELECT 
                    DATE_FORMAT(PaymentDateTime, '%m/%Y') AS Label,
                    SUM(Amount) AS Amount
                FROM Payment
                WHERE PaymentDateTime >= NOW() - INTERVAL 12 MONTH
                GROUP BY DATE_FORMAT(PaymentDateTime, '%m/%Y'), YEAR(PaymentDateTime), MONTH(PaymentDateTime)
                ORDER BY YEAR(PaymentDateTime) ASC, MONTH(PaymentDateTime) ASC";
                    break;

                case "yearly":
                    sqlQuery = @"
                SELECT 
                    DATE_FORMAT(PaymentDateTime, '%Y') AS Label,
                    SUM(Amount) AS Amount
                FROM Payment
                GROUP BY DATE_FORMAT(PaymentDateTime, '%Y'), YEAR(PaymentDateTime)
                ORDER BY YEAR(PaymentDateTime) ASC";
                    break;

                case "daily":
                default:
                    type = "daily";
                    sqlQuery = @"
                SELECT 
                    DATE_FORMAT(PaymentDateTime, '%d/%m/%Y') AS Label,
                    SUM(Amount) AS Amount
                FROM Payment
                WHERE PaymentDateTime >= CURDATE() - INTERVAL 6 DAY
                GROUP BY DATE_FORMAT(PaymentDateTime, '%d/%m/%Y'), DATE(PaymentDateTime)
                ORDER BY DATE(PaymentDateTime) ASC";
                    break;
            }

            var chartData = (await connection.QueryAsync<SalesChartItemDto>(sqlQuery)).ToList();

            var response = new SalesChartResponseDto
            {
                Type = type,
                Data = chartData
            };

            return Ok(response);
        }
    }
}