using System.ComponentModel.DataAnnotations;

namespace FINAPSA.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Admission Number")]
        public string AdmissionNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;
    }
}
