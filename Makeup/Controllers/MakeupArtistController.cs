using Makeup.Models;
using Makeup.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;

namespace Makeup.Controllers
{
    public class MakeupArtistController : Controller
    {

        private readonly MakeupContext _dataContext;
        private UserManager<User> _userManager;
        private readonly IWebHostEnvironment _hostingEnvironment;

        private readonly ILogger<MakeupArtistController> _logger;
        public MakeupArtistController(MakeupContext context, IWebHostEnvironment hostingEnvironment, UserManager<User> userManager, ILogger<MakeupArtistController> logger)
        {
            _dataContext = context;
            _userManager = userManager;
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }
        public IActionResult Index()
        {
            return View();
        }

        
    
        [HttpGet]
        public IActionResult Profile()
        {
            var userId = _userManager.GetUserId(User);
            var artist = _dataContext.MakeupArtists
                .Include(a => a.Location).Include(a => a.User)
                .FirstOrDefault(a => a.UserId.ToString() == userId)
                ;

            if (artist == null)
            {
                return View(new MakeupArtistVM());
            }

            var viewModel = new MakeupArtistVM
            {
                FullName = artist.FullName,
                Bio = artist.Bio,
                Experience = artist.Experience,
                Specialty = artist.Specialty,
                IsAvailableAtHome = artist.IsAvailableAtHome == 1,
                Address = artist.Location?.Address,
                Latitude = artist.Location?.Latitude,
                Longitude = artist.Location?.Longitude,
                Avatar = artist.User.Avatar ?? "~/assets/avatars/face-1.jpg"
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(MakeupArtistVM model, IFormFile avatarFile)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = _userManager.GetUserId(User);
            var artist = _dataContext.MakeupArtists
                .Include(a => a.Location)
                .FirstOrDefault(a => a.UserId.ToString() == userId);

            using (var transaction = _dataContext.Database.BeginTransaction())
            {
                try
                {
                    if (avatarFile != null && avatarFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads/avatars");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await avatarFile.CopyToAsync(fileStream);
                        }

                        var user = await _userManager.GetUserAsync(User);
                        user.Avatar = "/uploads/avatars/" + uniqueFileName;
                        await _userManager.UpdateAsync(user);
                    }
                    // Handle Location
                    Location location;
                    if (artist?.Location != null)
                    {
                        location = artist.Location;
                    }
                    else
                    {
                        location = new Location();
                        _dataContext.Locations.Add(location);
                    }

                    location.Address = model.Address;
                    location.Latitude = model.Latitude ?? 0;
                    location.Longitude = model.Longitude ?? 0;
                    location.Type = "artist";

                    // Handle MakeupArtist
                    if (artist == null)
                    {
                        artist = new MakeupArtist
                        {
                            UserId = int.Parse(userId),
                            IsActive = 1
                        };
                        _dataContext.MakeupArtists.Add(artist);
                    }

                    artist.FullName = model.FullName;
                    artist.Bio = model.Bio;
                    artist.Experience = model.Experience;
                    artist.Specialty = model.Specialty;
                    artist.IsAvailableAtHome = (byte)(model.IsAvailableAtHome ? 1 : 0);
                    artist.Location = location;

                    await _dataContext.SaveChangesAsync();
                    transaction.Commit();

                    TempData["Success"] = "Profile updated successfully";
                    return RedirectToAction("Profile");
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "An error occurred while saving your profile.");
                    return View(model);
                }
            }
        }
        [HttpGet("api/artist/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var artist = await _dataContext.MakeupArtists
                    .Include(a => a.Location)
                    .Include(a => a.User)
                    .Include(a => a.ServiceDetails)
                        .ThenInclude(sd => sd.Service)
                    .Include(a => a.Appointments)
                    .Where(a => a.ArtistId == id && a.IsActive == 1)
                    .Select(a => new
                    {
                        id = a.ArtistId,
                        fullName = a.FullName,
                        bio = a.Bio,
                        specialty = a.Specialty,
                        experience = a.Experience,
                        isAvailableAtHome = a.IsAvailableAtHome == 1,
                        avatar = a.User.Avatar ?? "~/assets/avatars/face-1.jpg",
                        address = a.Location != null ? a.Location.Address : null,
                        latitude = a.Location != null ? (double)a.Location.Latitude : 0.0,
                        longitude = a.Location != null ? (double)a.Location.Longitude : 0.0,
                        rating = a.Rating,
                        reviewsCount = a.ReviewsCount,
                        location = a.Location != null ? new
                        {
                            locationId = a.Location.LocationId,
                            address = a.Location.Address,
                            latitude = (double)a.Location.Latitude,
                            longitude = (double)a.Location.Longitude,
                            type = a.Location.Type
                        } : null,
                        appointments = a.Appointments.Select(ap => new
                        {
                            appointmentId = ap.AppointmentId,
                            appointmentDate = ap.AppointmentDate,
                            status = ap.Status,
                            serviceDetailId = ap.ServiceDetailId
                        }).ToList(),
                        services = a.ServiceDetails.Select(sd => new
                        {
                            serviceDetailId = sd.ServiceDetailId,
                            serviceName = sd.Service.ServiceName,
                            price = (double)sd.Price,
                            duration = sd.Duration
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (artist == null)
                {
                    return NotFound(new { message = "Artist not found or inactive" });
                }

                return Ok(artist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching artist details for ID {ArtistId}", id);
                return StatusCode(500, new { message = "Error fetching artist details", error = ex.Message });
            }
        }

        [HttpGet("api/artist/search")]
        public async Task<IActionResult> Search([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radius = 10.0)
        {
            try
            {
                _logger.LogInformation("Searching artists near ({Latitude}, {Longitude}) with radius {Radius} km", latitude, longitude, radius);

                var artists = await _dataContext.MakeupArtists
                    .Include(a => a.Location)
                    .Include(a => a.User)
                    .Where(a => a.IsActive == 1 && a.Location != null)
                    .Select(a => new
                    {
                        id = a.ArtistId,
                        fullName = a.FullName,
                        bio = a.Bio,
                        specialty = a.Specialty,
                        experience = a.Experience,
                        isAvailableAtHome = a.IsAvailableAtHome == 1,
                        avatar = a.User.Avatar,
                        address = a.Location.Address,
                        latitude = (double)a.Location.Latitude,
                        longitude = (double)a.Location.Longitude,
                        rating = a.Rating,
                        reviewsCount = a.ReviewsCount
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} active artists with location: {Artists}",
                    artists.Count,
                    string.Join("; ", artists.Select(a => $"ID={a.id}, Name={a.fullName}, Lat={a.latitude}, Lon={a.longitude}")));

                var filteredArtists = artists
                    .Select(a => new
                    {
                        a.id,
                        a.fullName,
                        a.bio,
                        a.specialty,
                        a.experience,
                        a.isAvailableAtHome,
                        a.avatar,
                        a.address,
                        a.latitude,
                        a.longitude,
                        distance = CalculateDistance(latitude, longitude, a.latitude, a.longitude),
                        a.rating,
                        a.reviewsCount
                    })
                    .Where(a => a.distance <= radius)
                    .OrderBy(a => a.distance)
                    .ToList();

                _logger.LogInformation("Filtered {Count} artists within {Radius} km: {Artists}",
                    filteredArtists.Count,
                    radius,
                    string.Join("; ", filteredArtists.Select(a => $"ID={a.id}, Name={a.fullName}, Distance={a.distance}")));

                return Ok(filteredArtists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching artists near ({Latitude}, {Longitude})", latitude, longitude);
                return StatusCode(500, new { message = "Error searching artists", error = ex.Message });
            }
        }

        [HttpGet("api/artist/all")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var artists = await _dataContext.MakeupArtists
                    .Include(a => a.Location)
                    .Include(a => a.User)
                    .Where(a => a.IsActive == 1)
                    .Select(a => new
                    {
                        id = a.ArtistId,
                        fullName = a.FullName,
                        bio = a.Bio,
                        specialty = a.Specialty,
                        experience = a.Experience,
                        isAvailableAtHome = a.IsAvailableAtHome == 1,
                        avatar = a.User.Avatar,
                        address = a.Location != null ? a.Location.Address : null,
                        latitude = a.Location != null ? (double)a.Location.Latitude : 0.0,
                        longitude = a.Location != null ? (double)a.Location.Longitude : 0.0,
                        rating = a.Rating,
                        reviewsCount = a.ReviewsCount
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} active artists: {Artists}",
                    artists.Count,
                    string.Join("; ", artists.Select(a => $"ID={a.id}, Name={a.fullName}")));

                return Ok(artists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all artists");
                return StatusCode(500, new { message = "Error fetching all artists", error = ex.Message });
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double deg) => deg * Math.PI / 180;
    }

}
