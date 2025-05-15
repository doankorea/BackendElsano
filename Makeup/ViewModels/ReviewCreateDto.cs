using System.ComponentModel.DataAnnotations;
namespace Makeup.ViewModels
{
    public class ReviewCreateDto
    {
        [Required]
        public int AppointmentId { get; set; }

        public int Rating { get; set; }

        public string Content { get; set; } = string.Empty;

    }
} 