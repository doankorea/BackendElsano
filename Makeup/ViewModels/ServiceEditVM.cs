using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Makeup.ViewModels
{
    public class ServiceEditVM
    {
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ")]
        public string ServiceName { get; set; } = null!;

        public string? Description { get; set; }

        public byte? IsActive { get; set; }

        public string? CurrentImageUrl { get; set; }

        public IFormFile? ImageFile { get; set; }
    }
} 