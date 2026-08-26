
namespace Buffet_Restaurant_API.Dtos
{
    public class OrderDto
    {
        public int CartId { get; set; }
        public int? BillId { get; set; }
        public int? BookingId { get; set; }
        public string OrderType { get; set; } = string.Empty;
    }
}
public class UpdateStatusDto
{
    public string Status { get; set; } = string.Empty;
}