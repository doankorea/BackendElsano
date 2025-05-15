using System.ComponentModel.DataAnnotations;

namespace Makeup.ViewModels
{
    public class ServiceDetailEditVM
    {
        public int ServiceDetailId { get; set; }
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập thời gian (phút)")]
        [Range(1, int.MaxValue, ErrorMessage = "Thời gian phải lớn hơn 0")]
        public int Duration { get; set; }
    }
} 