using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class ResetPasswordReq
    {
    [Required]
    public string Email { get; set; }

    [Required]
    public string NewPassword { get; set; }
    } 
}