using Makeup.Models;
using Makeup.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.IO;

namespace Makeup.Controllers
{
    public class ServiceController : Controller
    {
        private readonly MakeupContext _dataContext;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ServiceController(MakeupContext context, IWebHostEnvironment webHostEnvironment, UserManager<User> userManager)
        {
            _dataContext = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        [Route("Index")]
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var userId = _userManager.GetUserId(User);
            var artist = await _dataContext.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));
            if (artist == null)
            {
                return Unauthorized("Bạn phải là Makeup Artist để xem danh sách dịch vụ.");
            }

            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _dataContext.ServiceDetails
                .Where(sd => sd.ArtistId == artist.ArtistId)
                .Include(sd => sd.Service)
                .Include(sd => sd.Artist)
                .OrderByDescending(sd => sd.ServiceDetailId);

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
            var services = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Set up pagination info for the view
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            return View(services);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var services = await _dataContext.Services
                .Where(s => s.IsActive == 1)
                .Select(s => new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = s.ServiceName
                })
                .ToListAsync();

            ViewBag.Services = services;
            return View(new ServiceWithDetailVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceWithDetailVM model)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState không hợp lệ: " + string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                var services = await _dataContext.Services
                    .Where(s => s.IsActive == 1)
                    .Select(s => new SelectListItem
                    {
                        Value = s.ServiceId.ToString(),
                        Text = s.ServiceName
                    })
                    .ToListAsync();
                ViewBag.Services = services;
                return View(model);
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Bạn phải đăng nhập để đăng ký dịch vụ.");
            }

            var artist = await _dataContext.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));
            if (artist == null)
            {
                ModelState.AddModelError("", "Bạn phải được đăng ký làm Makeup Artist để đăng ký dịch vụ.");
                var services = await _dataContext.Services
                    .Where(s => s.IsActive == 1)
                    .Select(s => new SelectListItem
                    {
                        Value = s.ServiceId.ToString(),
                        Text = s.ServiceName
                    })
                    .ToListAsync();
                ViewBag.Services = services;
                return View(model);
            }

            var existingServiceDetail = await _dataContext.ServiceDetails
                .FirstOrDefaultAsync(sd => sd.ServiceId == model.ServiceId && sd.ArtistId == artist.ArtistId);
            if (existingServiceDetail != null)
            {
                ModelState.AddModelError("", "Bạn đã đăng ký dịch vụ này.");
                var services = await _dataContext.Services
                    .Where(s => s.IsActive == 1)
                    .Select(s => new SelectListItem
                    {
                        Value = s.ServiceId.ToString(),
                        Text = s.ServiceName
                    })
                    .ToListAsync();
                ViewBag.Services = services;
                return View(model);
            }

            // Kiểm tra ServiceId có tồn tại
            var selectedService = await _dataContext.Services
                .FirstOrDefaultAsync(s => s.ServiceId == model.ServiceId);
            if (selectedService == null)
            {
                ModelState.AddModelError("", "Dịch vụ không tồn tại.");
                var services = await _dataContext.Services
                    .Where(s => s.IsActive == 1)
                    .Select(s => new SelectListItem
                    {
                        Value = s.ServiceId.ToString(),
                        Text = s.ServiceName
                    })
                    .ToListAsync();
                ViewBag.Services = services;
                return View(model);
            }

            try
            {
                var serviceDetail = new ServiceDetail
                {
                    ServiceId = model.ServiceId,
                    ArtistId = artist.ArtistId,
                    UserId = int.Parse(userId),
                    Price = model.Price,
                    Duration = model.Duration,
                    CreatedAt = DateTime.Now
                };

                _dataContext.ServiceDetails.Add(serviceDetail);
                await _dataContext.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lưu: {ex.Message}");
                ModelState.AddModelError("", "Có lỗi xảy ra khi lưu dịch vụ. Vui lòng thử lại.");
                var services = await _dataContext.Services
                    .Where(s => s.IsActive == 1)
                    .Select(s => new SelectListItem
                    {
                        Value = s.ServiceId.ToString(),
                        Text = s.ServiceName
                    })
                    .ToListAsync();
                ViewBag.Services = services;
                return View(model);
            }
        }

        // GET: /Service/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var artist = await _dataContext.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));
            if (artist == null)
            {
                return Unauthorized("Bạn phải là Makeup Artist để chỉnh sửa dịch vụ.");
            }

            var serviceDetail = await _dataContext.ServiceDetails
                .Include(sd => sd.Service)
                .FirstOrDefaultAsync(sd => sd.ServiceDetailId == id && sd.ArtistId == artist.ArtistId);

            if (serviceDetail == null)
            {
                return NotFound();
            }

            var model = new ServiceDetailEditVM
            {
                ServiceDetailId = serviceDetail.ServiceDetailId,
                ServiceId = serviceDetail.ServiceId,
                ServiceName = serviceDetail.Service.ServiceName,
                Description = serviceDetail.Service.Description,
                ImageUrl = serviceDetail.Service.ImageUrl,
                Price = serviceDetail.Price,
                Duration = serviceDetail.Duration
            };

            return View(model);
        }

        // POST: /Service/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ServiceDetailEditVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = _userManager.GetUserId(User);
            var artist = await _dataContext.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));
            if (artist == null)
            {
                return Unauthorized("Bạn phải là Makeup Artist để chỉnh sửa dịch vụ.");
            }

            var serviceDetail = await _dataContext.ServiceDetails
                .FirstOrDefaultAsync(sd => sd.ServiceDetailId == model.ServiceDetailId && sd.ArtistId == artist.ArtistId);

            if (serviceDetail == null)
            {
                return NotFound();
            }

            // Chỉ cập nhật giá và thời gian
            serviceDetail.Price = model.Price;
            serviceDetail.Duration = model.Duration;

            try
            {
                _dataContext.Update(serviceDetail);
                await _dataContext.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi khi cập nhật dịch vụ: " + ex.Message);
                return View(model);
            }
        }

        // GET: api/service/getall
        [HttpGet("api/service/getall")]
        public async Task<IActionResult> GetAllServices()
        {
            try
            {
                var services = await _dataContext.Services
                    .Select(s => new
                    {
                        id = s.ServiceId,
                        name = s.ServiceName,
                        description = s.Description,
                        imageUrl = s.ImageUrl,
                        isActive = s.IsActive == 1,
                        createdAt = s.CreatedAt
                    })
                    .ToListAsync();

                return Ok(services);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching services", error = ex.Message });
            }
        }
        
        // GET: Service/ToggleStatus/5?activate=true
        public async Task<IActionResult> ToggleStatus(int id, bool activate)
        {
            var userId = _userManager.GetUserId(User);
            var artist = await _dataContext.MakeupArtists.FirstOrDefaultAsync(a => a.UserId == int.Parse(userId));
            if (artist == null)
            {
                return Unauthorized("Bạn phải là Makeup Artist để quản lý dịch vụ.");
            }

            var serviceDetail = await _dataContext.ServiceDetails
                .Include(sd => sd.Service)
                .FirstOrDefaultAsync(sd => sd.ServiceDetailId == id && sd.ArtistId == artist.ArtistId);

            if (serviceDetail == null)
            {
                return NotFound();
            }

            try
            {
                // Cập nhật trạng thái dịch vụ
                serviceDetail.Service.IsActive = activate ? (byte)1 : (byte)0;
                _dataContext.Update(serviceDetail.Service);
                await _dataContext.SaveChangesAsync();
                
                // Thông báo thành công
                TempData["SuccessMessage"] = activate 
                    ? "Dịch vụ đã được kích hoạt thành công" 
                    : "Dịch vụ đã được tạm dừng thành công";
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Xử lý lỗi
                TempData["ErrorMessage"] = "Lỗi khi cập nhật trạng thái dịch vụ: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}