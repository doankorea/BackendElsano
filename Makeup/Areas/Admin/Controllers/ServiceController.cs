using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Makeup.ViewModels;
using System.Linq;

namespace Makeup.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ServiceController : Controller
    {
        private readonly MakeupContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly int _pageSize = 10; // Số item trên mỗi trang

        public ServiceController(MakeupContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /Admin/Service/Index
        public async Task<IActionResult> Index(byte? status, int page = 1)
        {
            // Tạo query base
            var query = _context.Services.AsQueryable();

            // Lọc theo trạng thái nếu có
            if (status.HasValue)
            {
                query = query.Where(s => s.IsActive == status.Value);
            }

            // Lấy tổng số record để tính số trang
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)_pageSize);

            // Giới hạn page trong phạm vi hợp lệ
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // Lấy dữ liệu theo trang
            var services = await query
                .OrderByDescending(s => s.ServiceId)
                .Skip((page - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync();

            // Tạo view model với dữ liệu và thông tin phân trang
            var viewModel = new ServiceListVM
            {
                Services = services,
                CurrentPage = page,
                TotalPages = totalPages,
                Status = status,
                TotalItems = totalItems,
                PageSize = _pageSize
            };

            return View(viewModel);
        }

        // GET: /Admin/Service/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Admin/Service/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceCreateVM serviceVM)
        {
            if (ModelState.IsValid)
            {
                var service = new Service
                {
                    ServiceName = serviceVM.ServiceName,
                    Description = serviceVM.Description,
                    CreatedAt = DateTime.Now,
                    IsActive = serviceVM.IsActive
                };

                // Xử lý upload ảnh nếu có
                if (serviceVM.ImageFile != null && serviceVM.ImageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + serviceVM.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await serviceVM.ImageFile.CopyToAsync(fileStream);
                    }
                    
                    service.ImageUrl = "/images/services/" + uniqueFileName;
                }

                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Service", new { area = "Admin" });
            }
            return View(serviceVM);
        }

        // GET: /Admin/Service/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            var serviceVM = new ServiceEditVM
            {
                ServiceId = service.ServiceId,
                ServiceName = service.ServiceName,
                Description = service.Description,
                IsActive = service.IsActive,
                CurrentImageUrl = service.ImageUrl
            };

            return View(serviceVM);
        }

        // POST: /Admin/Service/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ServiceEditVM serviceVM)
        {
            if (ModelState.IsValid)
            {
                var service = await _context.Services.FindAsync(serviceVM.ServiceId);
                if (service == null)
                {
                    return NotFound();
                }

                service.ServiceName = serviceVM.ServiceName;
                service.Description = serviceVM.Description;
                service.IsActive = serviceVM.IsActive;

                // Xử lý upload ảnh mới nếu có
                if (serviceVM.ImageFile != null && serviceVM.ImageFile.Length > 0)
                {
                    // Xóa ảnh cũ nếu có
                    if (!string.IsNullOrEmpty(service.ImageUrl))
                    {
                        string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, service.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // Lưu ảnh mới
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + serviceVM.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await serviceVM.ImageFile.CopyToAsync(fileStream);
                    }
                    
                    service.ImageUrl = "/images/services/" + uniqueFileName;
                }

                _context.Update(service);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Service", new { area = "Admin" });
            }
            return View(serviceVM);
        }

        // GET: /Admin/Service/Deactivate/5
        public async Task<IActionResult> Deactivate(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            // Chỉ thay đổi trạng thái thành inactive thay vì xóa
            service.IsActive = 0;
            _context.Update(service);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"Dịch vụ '{service.ServiceName}' đã được chuyển sang trạng thái không hoạt động.";
            return RedirectToAction("Index", "Service", new { area = "Admin" });
        }

        // GET: /Admin/Service/Activate/5
        public async Task<IActionResult> Activate(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            // Kích hoạt lại dịch vụ
            service.IsActive = 1;
            _context.Update(service);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"Dịch vụ '{service.ServiceName}' đã được kích hoạt lại.";
            return RedirectToAction("Index", "Service", new { area = "Admin" });
        }

        // GET: api/admin/service/getall
        //[HttpGet("api/admin/service/getall")]
        //public async Task<IActionResult> GetAllServices()
        //{
        //    try
        //    {
        //        var services = await _context.Services
        //            .Select(s => new
        //            {
        //                id = s.ServiceId,
        //                name = s.ServiceName,
        //                description = s.Description,
        //                imageUrl = s.ImageUrl,
        //                isActive = s.IsActive == 1,
        //                createdAt = s.CreatedAt
        //            })
        //            .ToListAsync();
        //
        //        return Ok(services);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Error fetching services", error = ex.Message });
        //    }
        //}

        // GET: api/admin/service/{id}/artists
        [HttpGet("api/admin/service/{id}/artists")]
        public async Task<IActionResult> GetArtistsByService(int id)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null)
                {
                    return NotFound(new { message = "Service not found" });
                }

                // First get the artists who offer this service
                var artistIds = await _context.ServiceDetails
                    .Where(sd => sd.ServiceId == id)
                    .Where(sd => sd.Artist.IsActive == 1)
                    .Select(sd => sd.ArtistId)
                    .Distinct()
                    .ToListAsync();

                if (!artistIds.Any())
                {
                    return Ok(new
                    {
                        service = new
                        {
                            id = service.ServiceId,
                            name = service.ServiceName,
                            description = service.Description,
                            imageUrl = service.ImageUrl
                        },
                        artists = new List<object>()
                    });
                }

                // Then get all details for those artists
                var artists = await _context.MakeupArtists
                    .Where(a => artistIds.Contains(a.ArtistId))
                    .Include(a => a.User)
                    .Include(a => a.Location)
                    .Include(a => a.ServiceDetails)
                        .ThenInclude(sd => sd.Service)
                    .Select(a => new
                    {
                        artistId = a.ArtistId,
                        fullName = a.FullName,
                        avatar = a.User.Avatar,
                        rating = a.Rating,
                        reviewsCount = a.ReviewsCount,
                        location = a.Location != null ? new
                        {
                            address = a.Location.Address,
                            latitude = (double)a.Location.Latitude,
                            longitude = (double)a.Location.Longitude
                        } : null,
                        serviceDetails = a.ServiceDetails.Select(sd => new
                        {
                            serviceDetailId = sd.ServiceDetailId,
                            serviceId = sd.ServiceId,
                            serviceName = sd.Service.ServiceName,
                            price = (double)sd.Price,
                            duration = sd.Duration,
                            createdAt = sd.CreatedAt
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    service = new
                    {
                        id = service.ServiceId,
                        name = service.ServiceName,
                        description = service.Description,
                        imageUrl = service.ImageUrl
                    },
                    artists = artists
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching artists by service", error = ex.Message });
            }
        }
    }
}