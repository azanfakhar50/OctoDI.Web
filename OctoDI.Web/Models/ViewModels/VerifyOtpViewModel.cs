using System.ComponentModel.DataAnnotations;

namespace OctoDI.Web.Models.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [Display(Name = "OTP Code")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits.")]
        public string Code { get; set; }
    }
}
