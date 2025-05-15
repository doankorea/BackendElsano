using System.ComponentModel.DataAnnotations;

namespace Makeup.ViewModels
{
    public class RegisterVM
    {
        [Required(ErrorMessage = "Làm ơn nhập Username")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Làm ơn nhập Email"), EmailAddress]
        public string Email { get; set; }

        [DataType(DataType.Password), Required(ErrorMessage = "Làm ơn nhập Password")]
        public string Password { get; set; }
    }
}
