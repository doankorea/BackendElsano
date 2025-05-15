using System.ComponentModel.DataAnnotations;

namespace Makeup.ViewModels
{
    public class ResendEmailConfirmationVM
    {
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        public string Email { get; set; }
    }
} 