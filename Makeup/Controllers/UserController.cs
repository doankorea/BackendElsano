using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Makeup.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly MakeupContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UserController> _logger;
        private readonly IConfiguration _configuration;
        public UserController(UserManager<User> userManager, MakeupContext context, IWebHostEnvironment environment, ILogger<UserController> logger, IConfiguration configuration)
        {
            _userManager = userManager;
            _context = context;
            _environment = environment;
            _logger = logger;
            _configuration = configuration;

        }

        // GET: api/user/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                avatar = user.Avatar,
                isActive = user.IsActive,
                locationId = user.LocationId,
                location = user.Location != null ? new
                {
                    locationId = user.Location.LocationId,
                    latitude = user.Location.Latitude,
                    longitude = user.Location.Longitude,
                    address = user.Location.Address,
                    type = user.Location.Type
                } : null,
                createdAt = user.CreatedAt,
                updatedAt = user.UpdatedAt
            });
        }

        [HttpPut("{id}/avatar")]
        public async Task<IActionResult> UpdateAvatar(int id, IFormFile avatar)
        {
            _logger.LogInformation("Received avatar upload request for userId: {Id}", id);
            if (avatar == null || avatar.Length == 0)
            {
                _logger.LogWarning("No file uploaded for userId: {Id}", id);
                return BadRequest(new { message = "No file uploaded" });
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                _logger.LogWarning("User not found for id: {Id}", id);
                return NotFound(new { message = "User not found" });
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads/Avatars");
            if (!Directory.Exists(uploadsFolder))
            {
                _logger.LogInformation("Creating uploads directory: {Path}", uploadsFolder);
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(avatar.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            _logger.LogInformation("Saving avatar to: {FilePath}", filePath);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to save avatar file for userId: {Id}", id);
                return StatusCode(500, new { message = "Failed to save avatar file", error = ex.Message });
            }

            user.Avatar = $"/Uploads/Avatars/{fileName}";
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to update user avatar for userId: {Id}, errors: {Errors}", id, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(new { message = "Failed to update avatar", errors = result.Errors });
            }

            _logger.LogInformation("Avatar updated successfully for userId: {Id}, path: {AvatarPath}", id, user.Avatar);
            return Ok(new { status = "Success", message = "Avatar updated successfully", avatar = user.Avatar });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Update user fields
            user.UserName = request.UserName;
            user.Email = request.Email;
            user.PhoneNumber = request.PhoneNumber;
            user.IsActive = user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "Failed to update user", errors = result.Errors });
            }

            return Ok(new { status = "Success", message = "User updated successfully" });
        }

        [HttpPost("{id}/location")]
        public async Task<IActionResult> AddLocation(int id, [FromBody] AddLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var location = new Location
            {
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Address = request.Address,
                Type = request.Type
            };

            // Assuming Location is stored in a DbContext
            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            // Associate location with user
            user.LocationId = location.LocationId;
            user.Location = location;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "Failed to add location", errors = result.Errors });
            }

            return Ok(new { status = "Success", message = "Location added successfully" });
        }
        [HttpGet("search-address")]
        public async Task<IActionResult> SearchAddress([FromQuery] string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return BadRequest(new { message = "Query is required" });
            }

            var apiKey = _configuration["Goong:ApiKey"]; // Đọc từ appsettings.json
            var url = $"https://rsapi.goong.io/Place/AutoComplete?api_key={apiKey}&input={Uri.EscapeDataString(query)}&sessiontoken={Guid.NewGuid()}";

            using var client = new HttpClient();
            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json"); // Trả về JSON từ Goong API
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Goong Autocomplete API");
                return StatusCode(500, new { message = "Error fetching suggestions" });
            }
        }

        [HttpGet("geocode")]
        public async Task<IActionResult> GetCoordinates([FromQuery] string placeId)
        {
            if (string.IsNullOrEmpty(placeId))
            {
                return BadRequest(new { message = "Place ID is required" });
            }

            var apiKey = _configuration["Goong:ApiKey"];
            var url = $"https://rsapi.goong.io/Place/Detail?api_key={apiKey}&place_id={Uri.EscapeDataString(placeId)}";

            using var client = new HttpClient();
            try
            {
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Goong Geocode Response: {Response}", json); // Log response
                response.EnsureSuccessStatusCode();
                var result = JsonSerializer.Deserialize<GeocodeResponse>(json);
                if (result?.status == "OK" && result.result != null)
                {
                    var location = result.result.geometry.location;
                    return Ok(new { latitude = location.Latitude, longitude = location.Longitude });
                }

                _logger.LogWarning("Invalid Goong response: {Status}", result?.status);
                return BadRequest(new { message = "Invalid place ID" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Goong Geocode API");
                return StatusCode(500, new { message = "Error fetching coordinates", error = ex.Message });
            }
        }
    }
}
public class GeocodeResponse
{
    public string status { get; set; }
    public GeocodeResult result { get; set; }
}

public class GeocodeResult
{
    public Geometry geometry { get; set; }
}

public class Geometry
{
    public Location location { get; set; }
}
public class UpdateUserRequest
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int? IsActive { get; set; }
}

public class AddLocationRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string Address { get; set; }
    public string Type { get; set; }
}
