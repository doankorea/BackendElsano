using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Makeup.Models;

public partial class Appointment
{
    [Key]

    public int AppointmentId { get; set; }

    public int UserId { get; set; }

    public int? ArtistId { get; set; }

    public int ServiceDetailId { get; set; }

    public DateTime AppointmentDate { get; set; }

    public string Status { get; set; } = null!;

    public int? LocationId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual MakeupArtist? Artist { get; set; }

    public virtual Location? Location { get; set; }


    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Review? Review { get; set; }

    public virtual ServiceDetail ServiceDetail { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
