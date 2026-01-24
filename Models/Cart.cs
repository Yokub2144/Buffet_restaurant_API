using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Buffet_Restaurant_API.Models
{
    [Table("Cart")]
    public class Cart
    {
        [Key]
        [Column("Cart_id")]
        public int Cart_id { get; set; }

        [Column("Table_id")]
        public int Table_id { get; set; }

        [Column("Booking_id")]
        public int? Booking_id { get; set; }

        [Column("Created_at")]
        public DateTime Created_at { get; set; }
    }
}