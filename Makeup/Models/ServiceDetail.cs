using System;
using System.Collections.Generic;

namespace Makeup.Models;

public partial class ServiceDetail
{
    public int ServiceDetailId { get; set; }

    public int ServiceId { get; set; }
    public int ArtistId { get; set; }

    public int UserId { get; set; }
    public decimal Price { get; set; }

    public int Duration { get; set; }

    public DateTime? CreatedAt { get; set; }
    public virtual MakeupArtist Artist { get; set; } = null!;

    public virtual Service Service { get; set; } = null!;
    public virtual User User { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
