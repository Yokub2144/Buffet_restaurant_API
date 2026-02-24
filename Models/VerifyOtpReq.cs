using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class VerifyOtpReq
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string OtpCode { get; set; }
    }
}