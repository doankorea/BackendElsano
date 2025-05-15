using System.ComponentModel.DataAnnotations;

namespace Makeup.ViewModels
{
    public class LoginVM
    {
    public int Id { get; set; }

    [Required(ErrorMessage = "Làm ơn nhập Username")]
    public string UserName { get; set; }

    [DataType(DataType.Password), Required(ErrorMessage = "Làm ơn nhập Password")]
    public string Password { get; set; }

    public string ?ReturnUrl { get; set; }
    }
}
