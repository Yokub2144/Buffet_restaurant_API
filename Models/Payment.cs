using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Buffet_Restaurant_API.Models;

namespace Buffet_Restaurant_Managment_System_API.Models
{
[Table("Payment")]
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Payment_ID")]
        public int Payment_Id { get; set; }

        [Column("Booking_id")]
        public int? Booking_id { get; set; } // Nullable: เผื่อกรณีชำระหน้าร้านแบบไม่ได้จอง

        [Column("Bill_id")]
        public int? Bill_id { get; set; } // Nullable: เผื่อกรณีจ่ายค่ามัดจำจองล่วงหน้า

        [Required]
        [Column("Amount", TypeName = "decimal(10, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(10)]
        [Column("PaymentMethod")]
        public string PaymentMethod { get; set; } // เช่น "โอน", "เงินสด"

        [Required]
        [MaxLength(20)]
        [Column("Payment_Type")]
        public string Payment_Type { get; set; } // "หน้าร้าน" หรือ "ค่ามัดจำ"

        [Required]
        [Column("PaymentDateTime")]
        public DateTime PaymentDateTime { get; set; } = DateTime.Now;

        [MaxLength(255)]
        [Column("TransactionId")]
        public string TransactionId { get; set; }

        // ==========================================
        // 🔗 Relationships (Foreign Keys)
        // ==========================================

        [ForeignKey("Booking_id")]
        public virtual Booking Booking { get; set; }

        [ForeignKey("Bill_id")]
        public virtual Bill Bill { get; set; }
    }
}