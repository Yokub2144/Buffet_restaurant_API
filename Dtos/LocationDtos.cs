namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    // สำหรับพนักงานส่งพิกัดมาลงเวลา
    public class ClockInDto
    {
        public int EmployeeId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    // สำหรับเจ้าของร้านส่งพิกัดมาปักหมุด
    public class UpdateLocationDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}