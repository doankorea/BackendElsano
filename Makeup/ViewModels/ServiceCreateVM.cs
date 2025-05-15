using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Makeup.ViewModels
{
    public class ServiceCreateVM
    {
        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ")]
        public string ServiceName { get; set; } = null!;

        public string? Description { get; set; }

        public byte? IsActive { get; set; } = 1;

        public IFormFile? ImageFile { get; set; }
    }
} 