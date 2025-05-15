using Makeup.Models;
using Makeup.Models.Vnpay;
using Makeup.Services;
using Makeup.Services.Vnpay;
using Makeup.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Makeup.Controllers
{
    public class ApointmentController : Controller
    {
        private readonly MakeupContext _context;
        private UserManager<User> _userManager;
        private readonly IVnPayService _vnPayService;
        private readonly IEmailSender _emailSender;

        public ApointmentController(MakeupContext context, UserManager<User> userManager, IVnPayService vnPayService, IEmailSender emailSender)
        {
            _vnPayService = vnPayService;
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
        }
        [HttpPost("Apointment/Book")]
        public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentVM request)
        {
            try
            {
                // Debug log
                Console.WriteLine($"Received booking request: ArtistId={request.ArtistId}, " +
                    $"ServiceId={request.ServiceId}, ServiceDetailId={request.ServiceDetailId}, " +
                    $"StartTime={request.StartTime}, EndTime={request.EndTime}, " +
                    $"MeetingLocation={request.MeetingLocation}, UserId={request.UserId}, " +
                    $"PaymentMethod={request.PaymentMethod}");

                // Validate request
                if (request == null || request.ArtistId <= 0 || 
                    (request.ServiceId <= 0 && (!request.ServiceDetailId.HasValue || request.ServiceDetailId <= 0)) ||
                    string.IsNullOrEmpty(request.StartTime) || string.IsNullOrEmpty(request.EndTime) ||
                    string.IsNullOrEmpty(request.MeetingLocation) || request.UserId <= 0 ||
                    string.IsNullOrEmpty(request.PaymentMethod))
                {
                    Console.WriteLine("Invalid request data");
                    return BadRequest(new { success = false, message = "Invalid request data" });
                }

                // Parse dates
                if (!DateTime.TryParse(request.StartTime, out var startTime) ||
                    !DateTime.TryParse(request.EndTime, out var endTime))
                {
                    return BadRequest(new { success = false, message = "Invalid date format" });
                }

                if (startTime < DateTime.Now)
                {
                    return BadRequest(new { success = false, message = "Appointment time must be in the future" });
                }

                // Verify artist exists
                var artist = await _context.MakeupArtists
                    .FirstOrDefaultAsync(a => a.ArtistId == request.ArtistId);
                if (artist == null)
                {
                    return NotFound(new { success = false, message = "Artist not found" });
                }

                // Verify service exists, is active, and is associated with the artist
                ServiceDetail serviceDetail = null;
                
                if (request.ServiceDetailId.HasValue && request.ServiceDetailId.Value > 0)
                {
                    // Sử dụng ServiceDetailId nếu có
                    serviceDetail = await _context.ServiceDetails
                        .Include(sd => sd.Service)
                        .FirstOrDefaultAsync(sd => sd.ServiceDetailId == request.ServiceDetailId.Value && 
                                             sd.ArtistId == request.ArtistId);
                }
                
                if (serviceDetail == null)
                {
                    return NotFound(new { success = false, message = "Service not found or not offered by this artist" });
                }

                if (serviceDetail.Service.IsActive != 1)
                {
                    return BadRequest(new { success = false, message = "Service is not active" });
                }

                // Verify location exists or create new
                var location = await _context.Locations
                    .FirstOrDefaultAsync(l => l.Address == request.MeetingLocation);
                if (location == null)
                {
                    location = new Location
                    {
                        Address = request.MeetingLocation,
                        Latitude = request.Latitude ?? 0, 
                        Longitude = request.Longitude ?? 0,
                        Type = request.LocationType ?? "", 
                    };
                    _context.Locations.Add(location);
                    await _context.SaveChangesAsync();
                }

                // Check for conflicting appointments
                var conflictingAppointment = await _context.Appointments
                    .Where(a => a.ArtistId == request.ArtistId &&
                               ((startTime >= a.AppointmentDate && startTime < a.AppointmentDate.AddMinutes(a.ServiceDetail.Duration)) ||
                                (endTime > a.AppointmentDate && endTime <= a.AppointmentDate.AddMinutes(a.ServiceDetail.Duration)) ||
                                (startTime <= a.AppointmentDate && endTime >= a.AppointmentDate.AddMinutes(a.ServiceDetail.Duration))))
                    .FirstOrDefaultAsync();

                if (conflictingAppointment != null)
                {
                    return Conflict(new { success = false, message = "Time slot is already booked" });
                }

                // Create appointment
                var appointment = new Appointment
                {
                    UserId = request.UserId,
                    ArtistId = request.ArtistId,
                    ServiceDetailId = serviceDetail.ServiceDetailId,
                    AppointmentDate = startTime,
                    Status = "Pending",
                    LocationId = location.LocationId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                // Use the injected IEmailSender
                var artistEmail = artist.User.Email; // Ensure this property exists
                var subject = "New Appointment Booked";
                var body = $"You have a new appointment on {startTime} for {serviceDetail.Service.ServiceName}.";

                await _emailSender.SendEmailAsync(artistEmail, subject, body);

                // Không tạo payment ngay lúc này nữa, sẽ có chức năng thanh toán riêng sau
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        appointmentId = appointment.AppointmentId,
                        message = "Appointment created successfully. You can pay for this appointment from your appointments list."
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }
        public async Task<IActionResult> Index(string status = "", string sortOrder = "", int page = 1, int pageSize = 10)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var artist = await _context.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));

                if (artist == null)
                {
                    return RedirectToAction("Error", "Home");
                }

                // Ensure valid pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                var query = _context.Appointments
                    .Where(a => a.ArtistId == artist.ArtistId)
                    .Include(a => a.ServiceDetail)
                        .ThenInclude(sd => sd.Service)
                    .Include(a => a.Location)
                    .Include(a => a.User)
                    .AsQueryable();

                // Lọc theo trạng thái
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status.ToLower() == status.ToLower());
                }

                // Sắp xếp
                switch (sortOrder)
                {
                    case "date_desc":
                        query = query.OrderByDescending(a => a.AppointmentDate);
                        break;
                    case "date":
                        query = query.OrderBy(a => a.AppointmentDate);
                        break;
                    case "customer":
                        query = query.OrderBy(a => a.User.UserName);
                        break;
                    case "status":
                        query = query.OrderBy(a => a.Status);
                        break;
                    default:
                        query = query.OrderByDescending(a => a.AppointmentDate);
                        break;
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                
                // Calculate total pages
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                
                // Adjust current page if needed
                if (page > totalPages && totalPages > 0)
                {
                    page = totalPages;
                }

                // Get paginated results
                var appointments = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Truyền các tham số cho view
                ViewBag.DateSortParam = sortOrder == "date" ? "date_desc" : "date";
                ViewBag.CustomerSortParam = "customer";
                ViewBag.StatusSortParam = "status";
                ViewBag.CurrentSort = sortOrder;
                ViewBag.CurrentFilter = status;
                ViewBag.StatusList = new List<string> { "Pending", "Confirmed", "Completed", "Cancelled" };
                
                // Pagination info
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;

                return View(appointments);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Error", "Home", new { message = ex.Message });
            }
        }

        public async Task<IActionResult> GetAllAppointmentsByArtist()
        {
            var userId = _userManager.GetUserId(User);
            var artist = await _context.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));
            var artistId = artist.ArtistId;
            try
            {
                if (artistId == null)
                {
                    return BadRequest(new { success = false, message = "Artist ID is required" });
                }

                var appointments = await _context.Appointments
                    .Where(a => a.ArtistId == artistId).Include(a => a.ServiceDetail)
                        .ThenInclude(sd => sd.Service)
                    .Select(a => new
                    {
                        id = a.AppointmentId,
                        title = a.ServiceDetail.Service.ServiceName,  // Assuming ServiceDetail has a Service navigation property
                        start = a.AppointmentDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        end = a.AppointmentDate.AddMinutes(a.ServiceDetail.Duration).ToString("yyyy-MM-ddTHH:mm:ss"),  // Assuming Duration is in minutes
                        status = a.Status,
                        backgroundColor = a.Status.ToLower() == "pending" ? "#FF9800" :    // Cam 
                                         a.Status.ToLower() == "confirmed" ? "#FFEB3B" :   // Vàng
                                         a.Status.ToLower() == "completed" ? "#4CAF50" :   // Xanh lá
                                         a.Status.ToLower() == "cancelled" ? "#F44336" :   // Đỏ
                                         "#9E9E9E",                                         // Xám (cho các trạng thái khác)
                        borderColor = a.Status.ToLower() == "pending" ? "#F57C00" :        // Cam đậm
                                     a.Status.ToLower() == "confirmed" ? "#FBC02D" :       // Vàng đậm
                                     a.Status.ToLower() == "completed" ? "#388E3C" :       // Xanh lá đậm
                                     a.Status.ToLower() == "cancelled" ? "#D32F2F" :       // Đỏ đậm
                                     "#616161",                                             // Xám đậm
                        textColor = "#ffffff",
                        extendedProps = new
                        {
                            userId = a.UserId,
                            locationId = a.LocationId,
                            serviceDetailId = a.ServiceDetailId,
                            status = a.Status
                        }
                    })
                    .ToListAsync();

                return Json(new { success = true, data = appointments });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        

        [HttpGet]
        public async Task<IActionResult> GetAppointmentsByArtistId(int artistId)
        {
            try
            {
                if (artistId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid artist ID" });
                }

                var appointments = await _context.Appointments
                    .Where(a => a.ArtistId == artistId)
                    .Include(a => a.ServiceDetail)
                        .ThenInclude(sd => sd.Service)
                    .Include(a => a.Artist)
                    .Select(a => new
                    {
                        id = a.AppointmentId,
                        title = a.ServiceDetail.Service.ServiceName,
                        start = a.AppointmentDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        end = a.AppointmentDate.AddMinutes(a.ServiceDetail.Duration).ToString("yyyy-MM-ddTHH:mm:ss"),
                        status = a.Status,
                        backgroundColor = a.Status.ToLower() == "pending" ? "#FF9800" :    // Cam 
                                         a.Status.ToLower() == "confirmed" ? "#FFEB3B" :   // Vàng
                                         a.Status.ToLower() == "completed" ? "#4CAF50" :   // Xanh lá
                                         a.Status.ToLower() == "cancelled" ? "#F44336" :   // Đỏ
                                         "#9E9E9E",                                         // Xám (cho các trạng thái khác)
                        borderColor = a.Status.ToLower() == "pending" ? "#F57C00" :        // Cam đậm
                                     a.Status.ToLower() == "confirmed" ? "#FBC02D" :       // Vàng đậm
                                     a.Status.ToLower() == "completed" ? "#388E3C" :       // Xanh lá đậm
                                     a.Status.ToLower() == "cancelled" ? "#D32F2F" :       // Đỏ đậm
                                     "#616161",                                             // Xám đậm
                        textColor = "#ffffff",
                        extendedProps = new
                        {
                            userId = a.UserId,
                            artistId = a.ArtistId,
                            artistName = a.Artist.FullName,
                            locationId = a.LocationId,
                            serviceDetailId = a.ServiceDetailId,
                            status = a.Status
                        }
                    })
                    .ToListAsync();

                return Json(new { success = true, data = appointments });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet("api/user/{userId}/appointments")]
        public async Task<IActionResult> GetAppointmentsByUser(int userId)
        {
            try
            {
                // Kiểm tra nếu userId hợp lệ
                if (userId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid user ID" });
                }

                // Lấy tất cả appointments của user
                var appointments = await _context.Appointments
                    .Where(a => a.UserId == userId)
                    .Include(a => a.ServiceDetail)
                        .ThenInclude(sd => sd.Service)
                    .Include(a => a.Artist)
                    .Include(a => a.Location)
                    .Include(a => a.Payments)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Select(a => new
                    {
                        appointmentId = a.AppointmentId,
                        userId = a.UserId,
                        artistId = a.ArtistId,
                        artistName = a.Artist.FullName,
                        appointmentDate = a.AppointmentDate,
                        status = a.Status,
                        createdAt = a.CreatedAt,
                        updatedAt = a.UpdatedAt,
                        service = new
                        {
                            serviceDetailId = a.ServiceDetailId,
                            serviceName = a.ServiceDetail.Service.ServiceName,
                            price = (double)a.ServiceDetail.Price,
                            duration = a.ServiceDetail.Duration
                        },
                        location = new
                        {
                            locationId = a.LocationId,
                            address = a.Location.Address,
                            latitude = (double)a.Location.Latitude,
                            longitude = (double)a.Location.Longitude
                        },
                        payment = a.Payments.Select(p => new
                        {
                            paymentId = p.PaymentId,
                            paymentMethod = p.PaymentMethod,
                            amount = (double)p.Amount,
                            paymentStatus = p.PaymentStatus,
                            createdAt = p.CreatedAt
                        }).FirstOrDefault()
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = appointments });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet("api/appointments/{appointmentId}")]
        public async Task<IActionResult> GetAppointmentById(int appointmentId)
        {
            try
            {
                // Kiểm tra nếu appointmentId hợp lệ
                if (appointmentId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid appointment ID" });
                }

                // Lấy chi tiết appointment
                var appointment = await _context.Appointments
                    .Where(a => a.AppointmentId == appointmentId)
                    .Include(a => a.ServiceDetail)
                        .ThenInclude(sd => sd.Service)
                    .Include(a => a.Artist)
                    .Include(a => a.Location)
                    .Include(a => a.Payments)
                    .Include(a => a.User)
                    .Select(a => new
                    {
                        appointmentId = a.AppointmentId,
                        userId = a.UserId,
                        userName = a.User.UserName,
                        artistId = a.ArtistId,
                        artistName = a.Artist.FullName,
                        appointmentDate = a.AppointmentDate,
                        endTime = a.AppointmentDate.AddMinutes(a.ServiceDetail.Duration),
                        status = a.Status,
                        createdAt = a.CreatedAt,
                        updatedAt = a.UpdatedAt,
                        service = new
                        {
                            serviceDetailId = a.ServiceDetailId,
                            serviceName = a.ServiceDetail.Service.ServiceName,
                            price = (double)a.ServiceDetail.Price,
                            duration = a.ServiceDetail.Duration
                        },
                        location = new
                        {
                            locationId = a.LocationId,
                            address = a.Location.Address,
                            latitude = (double)a.Location.Latitude,
                            longitude = (double)a.Location.Longitude
                        },
                        payment = a.Payments.Select(p => new
                        {
                            paymentId = p.PaymentId,
                            paymentMethod = p.PaymentMethod,
                            amount = (double)p.Amount,
                            paymentStatus = p.PaymentStatus,
                            createdAt = p.CreatedAt
                        }).FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                if (appointment == null)
                {
                    return NotFound(new { success = false, message = "Appointment not found" });
                }

                return Ok(new { success = true, data = appointment });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPut("api/appointments/{appointmentId}/cancel")]
        public async Task<IActionResult> CancelAppointment(int appointmentId)
        {
            try
            {
                // Kiểm tra nếu appointmentId hợp lệ
                if (appointmentId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid appointment ID" });
                }

                // Tìm appointment cần hủy
                var appointment = await _context.Appointments
                    .Include(a => a.Payments)
                    .Include(a => a.Artist)
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

                if (appointment == null)
                {
                    return NotFound(new { success = false, message = "Appointment not found" });
                }

                // Chỉ cho phép hủy appointment có trạng thái Pending hoặc Confirmed
                if (appointment.Status != "Pending" && appointment.Status != "Confirmed")
                {
                    return BadRequest(new { success = false, message = $"Cannot cancel appointment with status '{appointment.Status}'" });
                }

                // Cập nhật trạng thái appointment thành Cancelled
                appointment.Status = "Cancelled";
                appointment.UpdatedAt = DateTime.Now;

                // Cập nhật trạng thái payment (nếu có)
                foreach (var payment in appointment.Payments)
                {
                    if (payment.PaymentStatus == "Pending")
                    {
                        payment.PaymentStatus = "Cancelled";
                    }
                }

                await _context.SaveChangesAsync();

                // Send email notification to the artist
                var artistEmail = appointment.Artist.User.Email; // Ensure this property exists
                var subject = "Appointment Canceled";
                var body = $"The appointment scheduled on {appointment.AppointmentDate} for {appointment.ServiceDetail.Service.ServiceName} has been canceled.";

                await _emailSender.SendEmailAsync(artistEmail, subject, body);

                return Ok(new 
                { 
                    success = true, 
                    message = "Appointment cancelled successfully",
                    data = new
                    {
                        appointmentId = appointment.AppointmentId,
                        status = appointment.Status,
                        updatedAt = appointment.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPut("api/appointments/{appointmentId}/confirm")]
        public async Task<IActionResult> ConfirmAppointment(int appointmentId)
        {
            try
            {
                // Kiểm tra nếu appointmentId hợp lệ
                if (appointmentId <= 0)
                {
                    return BadRequest(new { success = false, message = "Mã cuộc hẹn không hợp lệ" });
                }

                // Tìm appointment cần xác nhận
                var appointment = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

                if (appointment == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy cuộc hẹn" });
                }

                // Chỉ cho phép xác nhận appointment có trạng thái Pending
                if (appointment.Status != "Pending")
                {
                    return BadRequest(new { success = false, message = $"Không thể xác nhận cuộc hẹn với trạng thái '{appointment.Status}'" });
                }

                // Cập nhật trạng thái appointment thành Confirmed
                appointment.Status = "Confirmed";
                appointment.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new 
                { 
                    success = true, 
                    message = "Cuộc hẹn đã được xác nhận thành công",
                    data = new
                    {
                        appointmentId = appointment.AppointmentId,
                        status = appointment.Status,
                        updatedAt = appointment.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        [HttpPut("api/appointments/{appointmentId}/complete")]
        public async Task<IActionResult> CompleteAppointment(int appointmentId)
        {
            try
            {
                // Kiểm tra nếu appointmentId hợp lệ
                if (appointmentId <= 0)
                {
                    return BadRequest(new { success = false, message = "Mã cuộc hẹn không hợp lệ" });
                }

                // Tìm appointment cần hoàn thành
                var appointment = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

                if (appointment == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy cuộc hẹn" });
                }

                // Chỉ cho phép hoàn thành appointment có trạng thái Confirmed
                if (appointment.Status != "Confirmed")
                {
                    return BadRequest(new { success = false, message = $"Không thể hoàn thành cuộc hẹn với trạng thái '{appointment.Status}'" });
                }

                // Cập nhật trạng thái appointment thành Completed
                appointment.Status = "Completed";
                appointment.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new 
                { 
                    success = true, 
                    message = "Cuộc hẹn đã được đánh dấu là hoàn thành",
                    data = new
                    {
                        appointmentId = appointment.AppointmentId,
                        status = appointment.Status,
                        updatedAt = appointment.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Apointment/Details/5
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.ServiceDetail)
                    .ThenInclude(sd => sd.Service)
                .Include(a => a.Artist)
                .Include(a => a.Location)
                .Include(a => a.User)
                .Include(a => a.Payments)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            // Kiểm tra xem appointment có phải của artist đang đăng nhập không
            var userId = _userManager.GetUserId(User);
            var artist = await _context.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));

            if (artist == null || appointment.ArtistId != artist.ArtistId)
            {
                return Forbid(); // Trả về 403 nếu không phải appointment của artist
            }

            return View(appointment);
        }

        // GET: Apointment/Filter
        public async Task<IActionResult> Filter(string status = "", DateTime? fromDate = null, DateTime? toDate = null, string customerName = "", string sortOrder = "", int page = 1, int pageSize = 10)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var artist = await _context.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));

                if (artist == null)
                {
                    return RedirectToAction("Error", "Home");
                }

                // Ensure valid pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                var query = _context.Appointments
                    .Where(a => a.ArtistId == artist.ArtistId)
                    .Include(a => a.ServiceDetail)
                        .ThenInclude(sd => sd.Service)
                    .Include(a => a.Location)
                    .Include(a => a.User)
                    .AsQueryable();

                // Lọc theo trạng thái
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status.ToLower() == status.ToLower());
                }

                // Lọc theo khoảng thời gian
                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.AppointmentDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    // Cộng thêm 1 ngày để bao gồm cả ngày kết thúc
                    var nextDay = toDate.Value.AddDays(1);
                    query = query.Where(a => a.AppointmentDate < nextDay);
                }

                // Lọc theo tên khách hàng
                if (!string.IsNullOrEmpty(customerName))
                {
                    query = query.Where(a => a.User.UserName.Contains(customerName) || 
                                            a.User.Email.Contains(customerName));
                }

                // Sắp xếp
                switch (sortOrder)
                {
                    case "date_desc":
                        query = query.OrderByDescending(a => a.AppointmentDate);
                        break;
                    case "date":
                        query = query.OrderBy(a => a.AppointmentDate);
                        break;
                    case "customer":
                        query = query.OrderBy(a => a.User.UserName);
                        break;
                    case "status":
                        query = query.OrderBy(a => a.Status);
                        break;
                    default:
                        query = query.OrderByDescending(a => a.AppointmentDate);
                        break;
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                
                // Calculate total pages
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                
                // Adjust current page if needed
                if (page > totalPages && totalPages > 0)
                {
                    page = totalPages;
                }

                // Get paginated results
                var appointments = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Truyền các tham số cho view
                ViewBag.DateSortParam = sortOrder == "date" ? "date_desc" : "date";
                ViewBag.CustomerSortParam = "customer";
                ViewBag.StatusSortParam = "status";
                ViewBag.CurrentSort = sortOrder;
                ViewBag.CurrentFilter = status;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.CustomerName = customerName;
                ViewBag.StatusList = new List<string> { "Pending", "Confirmed", "Completed", "Cancelled" };
                
                // Pagination info
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;

                return View("Index", appointments);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Error", "Home", new { message = ex.Message });
            }
        }
    }
}
