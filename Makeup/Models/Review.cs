using System;
using System.Collections.Generic;

namespace Makeup.Models;

public partial class Review
{
    public int ReviewId { get; set; }

    public int AppointmentId { get; set; }

    public int UserId { get; set; }

    public int ArtistId { get; set; }

    public int Rating { get; set; }

    public string? Content { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual MakeupArtist Artist { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
