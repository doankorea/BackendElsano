using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Makeup.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Makeup.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MakeupContext _context;
    private readonly UserManager<User> _userManager;

    public HomeController(ILogger<HomeController> logger, MakeupContext context, UserManager<User> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var artist = await _context.MakeupArtists
            .FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));

        if (artist == null)
        {
            return View("NotArtist");
        }

        // Get artist appointments
        var appointments = await _context.Appointments
            .Where(a => a.ArtistId == artist.ArtistId)
            .Include(a => a.Payments)
            .ToListAsync();

        // Calculate total and actual revenue (after 10% fee)
        decimal totalRevenue = appointments
            .SelectMany(a => a.Payments)
            .Where(p => p.PaymentStatus == "Completed")
            .Sum(p => p.Amount);
        
        decimal actualRevenue = totalRevenue * 0.9M; // 90% of total after 10% fee

        // Get appointment counts by status
        int pendingCount = appointments.Count(a => a.Status == "Pending");
        int confirmedCount = appointments.Count(a => a.Status == "Confirmed");
        int completedCount = appointments.Count(a => a.Status == "Completed");
        int cancelledCount = appointments.Count(a => a.Status == "Cancelled");
        int totalAppointments = appointments.Count;

        // Monthly revenue for the last 6 months
        var currentDate = DateTime.Now;
        var sixMonthsAgo = currentDate.AddMonths(-5);
        
        var monthlyRevenue = appointments
            .Where(a => a.AppointmentDate >= sixMonthsAgo)
            .SelectMany(a => a.Payments)
            .Where(p => p.PaymentStatus == "Completed")
            .GroupBy(p => new { Month = p.CreatedAt?.Month ?? 0, Year = p.CreatedAt?.Year ?? 0 })
            .Select(g => new
            {
                Month = g.Key.Month,
                Year = g.Key.Year,
                Total = g.Sum(p => p.Amount),
                Actual = g.Sum(p => p.Amount) * 0.9M
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();

        // For chart data
        List<string> monthLabels = new List<string>();
        List<decimal> revenueData = new List<decimal>();
        List<decimal> actualRevenueData = new List<decimal>();

        // Fill in all months (including those with zero revenue)
        for (int i = 0; i < 6; i++)
        {
            DateTime month = currentDate.AddMonths(-5 + i);
            string monthName = month.ToString("MMM yyyy");
            monthLabels.Add(monthName);
            
            var data = monthlyRevenue.FirstOrDefault(r => r.Month == month.Month && r.Year == month.Year);
            revenueData.Add(data?.Total ?? 0);
            actualRevenueData.Add(data?.Actual ?? 0);
        }

        // Pass data to view
        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.ActualRevenue = actualRevenue;
        ViewBag.PendingCount = pendingCount;
        ViewBag.ConfirmedCount = confirmedCount;
        ViewBag.CompletedCount = completedCount;
        ViewBag.CancelledCount = cancelledCount;
        ViewBag.TotalAppointments = totalAppointments;
        ViewBag.MonthLabels = monthLabels;
        ViewBag.RevenueData = revenueData;
        ViewBag.ActualRevenueData = actualRevenueData;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
