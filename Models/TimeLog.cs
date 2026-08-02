using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buffet_Restaurant_Managment_System_API.Models
{
    [Table("TimeLog")]
    public class TimeLog
    {
        [Key]
        public int Timelog_id { get; set; }

        public int Emp_id { get; set; }

        public DateTime Date { get; set; }

        public DateTime Time_in { get; set; }

        public DateTime? Time_out { get; set; }


        // Relationship (Navigation Property)
        [ForeignKey("Emp_id")]
        public virtual Employee Employee { get; set; }
    }
}