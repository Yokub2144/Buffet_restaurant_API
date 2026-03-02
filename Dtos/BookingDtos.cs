namespace Buffet_Restaurant_API.Dtos
{
    public class CreateBookingDto
    {
        public int Member_id { get; set; }
        public DateTime Booking_Date { get; set; }
        public TimeSpan Booking_Time { get; set; }
        public int Adult_Count { get; set; }
        public int Child_Count { get; set; }
        public List<int> Table_ids { get; set; } = new();
    }

    public class UpdateBookingStatusDto
    {
        public string Booking_Status { get; set; } = string.Empty;
    }

    public class BookingResponseDto
    {
        public int Booking_id { get; set; }
        public string Member_Name { get; set; } = string.Empty;
        public string Member_Phone { get; set; } = string.Empty;
        public DateTime Booking_Date { get; set; }
        public TimeSpan? Booking_Time { get; set; }
        public string Booking_Status { get; set; } = string.Empty;
        public int Adult_Count { get; set; }
        public int Child_Count { get; set; }
        public List<string> Tables_Booked { get; set; } = new();
    }
    public class CheckinDto
    {
        public int BookingId { get; set; }
        public int TableId { get; set; }
    }
}