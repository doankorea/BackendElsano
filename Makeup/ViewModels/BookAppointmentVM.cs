namespace Makeup.ViewModels
{
    public class BookAppointmentVM
    {
        public int ArtistId { get; set; }
        public int ServiceId { get; set; } = 0;
        public int? ServiceDetailId { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string MeetingLocation { get; set; }
        public int UserId { get; set; }
        public string PaymentMethod { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string LocationType { get; set; }
    }
}
