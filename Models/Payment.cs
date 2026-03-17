using System.ComponentModel.DataAnnotations;
using Buffet_Restaurant_API.Models;

namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class Payment
    {
        [Key]
        public int Payment_ID { get; set; }
        public int Booking_id { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "PromptPay";
        public string Payment_Type { get; set; } = "";
        public DateTime PaymentDateTime { get; set; } = DateTime.Now;
        public string? TransactionId { get; set; }

        public Booking? Booking { get; set; }
    }
}