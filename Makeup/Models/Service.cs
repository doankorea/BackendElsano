using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Makeup.Models;

public partial class Service
{
    [Key]
    public int ServiceId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ")]
    public string ServiceName { get; set; } = null!;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public byte? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }


    public virtual ICollection<ServiceDetail> ServiceDetails { get; set; } = new List<ServiceDetail>();
}
