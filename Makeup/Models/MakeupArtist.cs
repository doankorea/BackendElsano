using System;
using System.Collections.Generic;

namespace Makeup.Models;

public partial class MakeupArtist
{
    public int ArtistId { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Bio { get; set; }

    public int? Experience { get; set; }

    public string? Specialty { get; set; }

    public byte? IsAvailableAtHome { get; set; }

    public decimal? Rating { get; set; }

    public int? ReviewsCount { get; set; }

    public byte? IsActive { get; set; }

    public int? LocationId { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual Location? Location { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Service> Services { get; set; } = new List<Service>();
    public virtual ICollection<ServiceDetail> ServiceDetails { get; set; } = new List<ServiceDetail>();
    public virtual User User { get; set; } = null!;
}
