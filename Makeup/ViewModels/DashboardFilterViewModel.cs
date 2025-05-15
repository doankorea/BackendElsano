using System;
using System.ComponentModel.DataAnnotations;

namespace Makeup.ViewModels
{
    public class DashboardFilterViewModel
    {
        // Time period filter - "week", "month", "quarter", "year", "custom"
        public string TimePeriod { get; set; }

        // Artist filter
        public int? ArtistId { get; set; }

        // Service filter
        public int? ServiceId { get; set; }

        // Date range for "custom" time period
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime EndDate { get; set; }
    }
} 