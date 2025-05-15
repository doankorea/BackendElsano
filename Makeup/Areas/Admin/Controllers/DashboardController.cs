using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using Makeup.ViewModels;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Makeup.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly MakeupContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(MakeupContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        public async Task<IActionResult> Index(DashboardFilterViewModel filter = null)
        {
            if (filter == null)
            {
                filter = new DashboardFilterViewModel
                {
                    TimePeriod = "month",
                    ArtistId = null,
                    ServiceId = null,
                    StartDate = DateTime.Now.AddMonths(-1),
                    EndDate = DateTime.Now
                };
            }
            else
            {
                // Process the TimePeriod value to set appropriate date ranges
                switch (filter.TimePeriod?.ToLower())
                {
                    case "week":
                        filter.StartDate = DateTime.Now.AddDays(-7);
                        filter.EndDate = DateTime.Now;
                        break;
                    case "month":
                        filter.StartDate = DateTime.Now.AddMonths(-1);
                        filter.EndDate = DateTime.Now;
                        break;
                    case "quarter":
                        filter.StartDate = DateTime.Now.AddMonths(-3);
                        filter.EndDate = DateTime.Now;
                        break;
                    case "year":
                        filter.StartDate = DateTime.Now.AddYears(-1);
                        filter.EndDate = DateTime.Now;
                        break;
                    case "custom":
                        // Use the provided custom date range
                        if (filter.StartDate == default)
                            filter.StartDate = DateTime.Now.AddMonths(-1);
                        if (filter.EndDate == default)
                            filter.EndDate = DateTime.Now;
                        break;
                    default:
                        filter.TimePeriod = "month";
                        filter.StartDate = DateTime.Now.AddMonths(-1);
                        filter.EndDate = DateTime.Now;
                        break;
                }
            }

            // Store filter in ViewBag for the view
            ViewBag.Filter = filter;

            // Get all appointments with filtering
            var appointmentsQuery = _context.Appointments
                .Include(a => a.Payments)
                .Include(a => a.Artist)
                .ThenInclude(artist => artist.User)
                .Include(a => a.User)
                .Include(a => a.ServiceDetail)
                .ThenInclude(sd => sd.Service)
                .Where(a => a.AppointmentDate >= filter.StartDate && a.AppointmentDate <= filter.EndDate);

            // Apply additional filters if specified
            if (filter.ArtistId.HasValue)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.ArtistId == filter.ArtistId.Value);
            }

            if (filter.ServiceId.HasValue)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.ServiceDetailId == filter.ServiceId.Value);
            }

            var appointments = await appointmentsQuery.ToListAsync();

            // Calculate filtered revenue (for the chart and filtered view)
            var filteredPayments = appointments
                .SelectMany(a => a.Payments)
                .Where(p => p.PaymentStatus == "Completed")
                .GroupBy(p => p.PaymentId)
                .Select(g => g.First())
                .ToList();
                
            decimal filteredRevenue = filteredPayments.Sum(p => p.Amount);
            
            // Calculate TOTAL APP REVENUE (regardless of filters)
            var allCompletedPayments = await _context.Payments
                .Where(p => p.PaymentStatus == "Completed")
                .GroupBy(p => p.PaymentId)
                .Select(g => g.First())
                .ToListAsync();
                
            decimal artistTotalRevenue = allCompletedPayments.Sum(p => p.Amount);
            decimal platformFee = artistTotalRevenue * 0.1M; // 10% platform fee
            
            // The actual app revenue is the 10% platform fee
            decimal totalAppRevenue = platformFee;
            
            // Debugging: Log the number of payments and total amount
            _logger?.LogInformation($"Artist total revenue: {artistTotalRevenue} from {allCompletedPayments.Count} payments");
            _logger?.LogInformation($"Platform revenue (10%): {totalAppRevenue}");
            
            // Get the total appointments count from the database directly
            int totalAppointments = await _context.Appointments.CountAsync();
            
            // Get user counts - these aren't filtered by date range
            int totalUsers = await _context.Users.CountAsync();
            int totalArtists = await _context.MakeupArtists.CountAsync();
            int totalServices = await _context.Services.CountAsync();
            int activeServices = await _context.Services.CountAsync(s => s.IsActive == 1);

            // Get appointment counts by status
            int pendingCount = appointments.Count(a => a.Status == "Pending");
            int confirmedCount = appointments.Count(a => a.Status == "Confirmed");
            int completedCount = appointments.Count(a => a.Status == "Completed");
            int cancelledCount = appointments.Count(a => a.Status == "Cancelled");

            // Time series for chart - group by appropriate period based on filter
            var groupedPayments = appointments
                .SelectMany(a => a.Payments)
                .Where(p => p.PaymentStatus == "Completed")
                .GroupBy(p => GetTimeGroupKey(p.CreatedAt ?? DateTime.MinValue, filter.TimePeriod))
                .Select(g => new
                {
                    TimeGroup = g.Key,
                    TotalAmount = g.Sum(p => p.Amount),
                    PlatformFee = g.Sum(p => p.Amount) * 0.1M,  // 10% platform fee
                    ArtistRevenue = g.Sum(p => p.Amount) * 0.9M // 90% to artists
                })
                .OrderBy(x => x.TimeGroup)
                .ToList();

            // Create time labels and data points for chart
            var timeLabels = new List<string>();
            var revenueData = new List<decimal>();
            var platformFeeData = new List<decimal>();
            var artistRevenueData = new List<decimal>();

            // Generate all time period labels
            FillTimeLabels(timeLabels, filter.StartDate, filter.EndDate, filter.TimePeriod);

            // Fill data points, including zeros for periods with no data
            foreach (var label in timeLabels)
            {
                var data = groupedPayments.FirstOrDefault(p => p.TimeGroup == label);
                revenueData.Add(data?.TotalAmount ?? 0);
                platformFeeData.Add(data?.PlatformFee ?? 0);
                artistRevenueData.Add(data?.ArtistRevenue ?? 0);
            }

            // Top 6 services by appointment count
            var topServices = await _context.Appointments
                .Where(a => a.AppointmentDate >= filter.StartDate && a.AppointmentDate <= filter.EndDate)
                .GroupBy(a => a.ServiceDetailId)
                .Select(g => new
                {
                    ServiceDetailId = g.Key,
                    AppointmentCount = g.Count()
                })
                .OrderByDescending(x => x.AppointmentCount)
                .Take(6)
                .ToListAsync();

            var topServiceDetails = await _context.ServiceDetails
                .Where(sd => topServices.Select(ts => ts.ServiceDetailId).Contains(sd.ServiceDetailId))
                .Include(sd => sd.Service)
                .ToListAsync();

            var popularServices = topServices.Select(ts => new
            {
                ServiceName = topServiceDetails.FirstOrDefault(sd => sd.ServiceDetailId == ts.ServiceDetailId)?.Service?.ServiceName ?? "Unknown",
                AppointmentCount = ts.AppointmentCount
            }).ToList();

            // Top artists by appointment count
            var topArtists = await _context.Appointments
                .Where(a => a.AppointmentDate >= filter.StartDate && a.AppointmentDate <= filter.EndDate)
                .GroupBy(a => a.ArtistId)
                .Select(g => new
                {
                    ArtistId = g.Key,
                    AppointmentCount = g.Count()
                })
                .OrderByDescending(x => x.AppointmentCount)
                .Take(6)
                .ToListAsync();

            var topArtistDetails = await _context.MakeupArtists
                .Where(a => topArtists.Select(ta => ta.ArtistId).Contains(a.ArtistId))
                .Include(a => a.User)
                .ToListAsync();

            var popularArtists = topArtists.Select(ta => new
            {
                ArtistName = topArtistDetails.FirstOrDefault(a => a.ArtistId == ta.ArtistId)?.FullName ?? "Unknown",
                AppointmentCount = ta.AppointmentCount
            }).ToList();

            // Recent appointments (last 5)
            var recentAppointments = await _context.Appointments
                .Include(a => a.User)
                .Include(a => a.Artist)
                .Include(a => a.ServiceDetail)
                .ThenInclude(sd => sd.Service)
                .OrderByDescending(a => a.AppointmentDate)
                .Take(5)
                .ToListAsync();

            // Recent users (last 5) - without filtering
            var recentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Extract service names and booking counts directly
            string[] serviceNames = popularServices.Select(s => s.ServiceName).ToArray();
            int[] serviceBookings = popularServices.Select(s => s.AppointmentCount).ToArray();
            
            // Extract artist names and appointment counts directly
            string[] artistNames = popularArtists.Select(a => a.ArtistName).ToArray();
            int[] artistAppointments = popularArtists.Select(a => a.AppointmentCount).ToArray();

            // Get dropdown data for filters
            var allArtists = await _context.MakeupArtists
                .Select(a => new { a.ArtistId, a.FullName })
                .OrderBy(a => a.FullName)
                .ToListAsync();

            var allServices = await _context.Services
                .Select(s => new { s.ServiceId, s.ServiceName })
                .OrderBy(s => s.ServiceName)
                .ToListAsync();

            // Pass data to view
            ViewBag.TotalAppRevenue = totalAppRevenue; // This is the platform's 10% cut
            ViewBag.ArtistTotalRevenue = artistTotalRevenue; // This is the total revenue for all artists (100%)
            ViewBag.FilteredRevenue = filteredRevenue;
            ViewBag.PlatformFee = platformFee; // Same as TotalAppRevenue (10%)
            ViewBag.ArtistRevenue = artistTotalRevenue * 0.9M; // 90% of the total that goes to artists
            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalArtists = totalArtists;
            ViewBag.TotalServices = totalServices;
            ViewBag.ActiveServices = activeServices;
            ViewBag.PendingCount = pendingCount;
            ViewBag.ConfirmedCount = confirmedCount;
            ViewBag.CompletedCount = completedCount;
            ViewBag.CancelledCount = cancelledCount;
            ViewBag.TotalAppointments = totalAppointments;
            ViewBag.TimeLabels = timeLabels;
            ViewBag.RevenueData = revenueData;
            ViewBag.PlatformFeeData = platformFeeData;
            ViewBag.ArtistRevenueData = artistRevenueData;
            ViewBag.PopularServices = popularServices;
            ViewBag.PopularArtists = popularArtists;
            ViewBag.RecentAppointments = recentAppointments;
            ViewBag.RecentUsers = recentUsers;
            
            // Add pre-processed arrays for the charts
            ViewBag.ServiceNames = serviceNames;
            ViewBag.ServiceBookings = serviceBookings;
            ViewBag.ArtistNames = artistNames;
            ViewBag.ArtistAppointments = artistAppointments;
            ViewBag.StatusCounts = new int[] { completedCount, confirmedCount, pendingCount, cancelledCount };

            // Add filter dropdown data
            ViewBag.AllArtists = allArtists;
            ViewBag.AllServices = allServices;

            return View();
        }

        [HttpPost]
        public IActionResult Filter(DashboardFilterViewModel filter)
        {
            return RedirectToAction("Index", filter);
        }

        public async Task<IActionResult> ExportData(DashboardFilterViewModel filter)
        {
            // Implement export functionality for dashboard data
            // This could generate CSV, Excel, or PDF reports based on the filtered data
            return RedirectToAction("Index", filter);
        }

        #region Helper Methods
        private string GetTimeGroupKey(DateTime date, string timePeriod)
        {
            switch (timePeriod.ToLower())
            {
                case "week":
                    // Group by day of week
                    return date.ToString("ddd, MMM d");
                case "month":
                    // Group by day of month
                    return date.ToString("MMM d");
                case "quarter":
                    // Group by week
                    return $"Week {GetIso8601WeekOfYear(date)}";
                case "year":
                    // Group by month
                    return date.ToString("MMM yyyy");
                case "custom":
                    // Determine appropriate grouping based on date range
                    var range = (ViewBag.Filter.EndDate - ViewBag.Filter.StartDate).TotalDays;
                    if (range <= 31)
                        return date.ToString("MMM d");
                    else if (range <= 90)
                        return $"Week {GetIso8601WeekOfYear(date)}";
                    else
                        return date.ToString("MMM yyyy");
                default:
                    return date.ToString("MMM d");
            }
        }

        private void FillTimeLabels(List<string> labels, DateTime startDate, DateTime endDate, string timePeriod)
        {
            switch (timePeriod.ToLower())
            {
                case "week":
                    // Daily labels for a week
                    for (var day = startDate; day <= endDate; day = day.AddDays(1))
                    {
                        labels.Add(day.ToString("ddd, MMM d"));
                    }
                    break;
                case "month":
                    // Daily labels for a month
                    for (var day = startDate; day <= endDate; day = day.AddDays(1))
                    {
                        labels.Add(day.ToString("MMM d"));
                    }
                    break;
                case "quarter":
                    // Weekly labels for a quarter
                    for (var week = startDate; week <= endDate; week = week.AddDays(7))
                    {
                        labels.Add($"Week {GetIso8601WeekOfYear(week)}");
                    }
                    break;
                case "year":
                    // Monthly labels for a year
                    for (var month = new DateTime(startDate.Year, startDate.Month, 1);
                         month <= new DateTime(endDate.Year, endDate.Month, 1);
                         month = month.AddMonths(1))
                    {
                        labels.Add(month.ToString("MMM yyyy"));
                    }
                    break;
                case "custom":
                    // Determine appropriate intervals based on date range
                    var range = (endDate - startDate).TotalDays;
                    if (range <= 31)
                    {
                        // Daily for ranges up to a month
                        for (var day = startDate; day <= endDate; day = day.AddDays(1))
                        {
                            labels.Add(day.ToString("MMM d"));
                        }
                    }
                    else if (range <= 90)
                    {
                        // Weekly for ranges up to 3 months
                        for (var week = startDate; week <= endDate; week = week.AddDays(7))
                        {
                            labels.Add($"Week {GetIso8601WeekOfYear(week)}");
                        }
                    }
                    else
                    {
                        // Monthly for longer ranges
                        for (var month = new DateTime(startDate.Year, startDate.Month, 1);
                             month <= new DateTime(endDate.Year, endDate.Month, 1);
                             month = month.AddMonths(1))
                        {
                            labels.Add(month.ToString("MMM yyyy"));
                        }
                    }
                    break;
                default:
                    // Default to daily
                    for (var day = startDate; day <= endDate; day = day.AddDays(1))
                    {
                        labels.Add(day.ToString("MMM d"));
                    }
                    break;
            }
        }

        // Get ISO 8601 week number
        private static int GetIso8601WeekOfYear(DateTime date)
        {
            // Return the ISO 8601 week of year
            DayOfWeek day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                date = date.AddDays(3);
            }

            return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }
        #endregion
    }
}
