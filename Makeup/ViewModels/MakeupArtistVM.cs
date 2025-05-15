namespace Makeup.ViewModels
{
    public class MakeupArtistVM
    {
        public string FullName { get; set; }
        public string? Bio { get; set; }
        public int? Experience { get; set; }
        public string? Specialty { get; set; }
        public bool IsAvailableAtHome { get; set; }
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Avatar { get; set; }

    }
}
