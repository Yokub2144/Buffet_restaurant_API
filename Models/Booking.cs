using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Buffet_Restaurant_Managment_System_API.Models;

namespace Buffet_Restaurant_API.Models
{
    public class Booking
    {
        [Key]
        public int Booking_id { get; set; }
        public int Member_id { get; set; }
        public string Booking_Status { get; set; } = "Pending";
        public int Adult_Count { get; set; } = 0;
        public int Child_Count { get; set; } = 0;
        public DateTime Booking_DateTime { get; set; }
        public string? QR_Url { get; set; }
        public decimal Deposit_Amount { get; set; } = 0;


        public Member? Member { get; set; }
        public ICollection<GroupTable> GroupTables { get; set; } = new List<GroupTable>();
    }

    public class GroupTable
    {
        [Key]
        public int GroupTable_id { get; set; }
        public int? Booking_id { get; set; }
        public int? Table_id { get; set; }


        public Booking? Booking { get; set; }
        public Tables? Table { get; set; }
    }
}