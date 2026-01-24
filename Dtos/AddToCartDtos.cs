using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_API.Dtos
{
    public class AddToCartDtos
    {
        public int TableId { get; set; }
        public int MenuId { get; set; }
        public int Quantity { get; set; }
        public int? Booking_id { get; set; }
    }
}