using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Makeup.Models;

public partial class Location
{
    public int LocationId { get; set; }
    [JsonPropertyName("lat")]
    public decimal Latitude { get; set; }
    [JsonPropertyName("lng")]
    public decimal Longitude { get; set; }

    public string Address { get; set; } = null!;

    public string Type { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<MakeupArtist> MakeupArtists { get; set; } = new List<MakeupArtist>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
