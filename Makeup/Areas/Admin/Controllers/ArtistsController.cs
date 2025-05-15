using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Makeup.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ArtistsController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly MakeupContext _context;

        public ArtistsController(
            UserManager<User> userManager,
            MakeupContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: Admin/Artists
        public async Task<IActionResult> Index(string searchTerm, string status, int page = 1)
        {
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            
            var pageSize = 10; // Số lượng item trên mỗi trang
            var skip = (page - 1) * pageSize; // Số lượng item bỏ qua
            
            var artistsQuery = _context.MakeupArtists
                .Include(a => a.User)
                .AsQueryable();

            // Filtering
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                artistsQuery = artistsQuery.Where(a => 
                    (a.FullName != null && a.FullName.ToLower().Contains(searchTerm)) ||
                    (a.User.Email != null && a.User.Email.ToLower().Contains(searchTerm)) ||
                    (a.User.PhoneNumber != null && a.User.PhoneNumber.Contains(searchTerm)) ||
                    (a.Specialty != null && a.Specialty.ToLower().Contains(searchTerm)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "active")
                {
                    artistsQuery = artistsQuery.Where(a => a.IsActive == 1);
                }
                else if (status == "inactive")
                {
                    artistsQuery = artistsQuery.Where(a => a.IsActive == 0 || a.IsActive == null);
                }
            }

            // Count total items for pagination
            var totalItems = await artistsQuery.CountAsync();
            
            // Tính toán số trang
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            // Đảm bảo trang hiện tại nằm trong phạm vi hợp lệ
            page = Math.Max(1, Math.Min(page, totalPages));

            // Apply pagination and ordering
            var artists = await artistsQuery
                .OrderByDescending(a => a.User.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            // Set ViewBag variables for pagination
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.Skip = skip;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;
            ViewBag.StartPage = Math.Max(1, page - 2);
            ViewBag.EndPage = Math.Min(totalPages, page + 2);
            ViewBag.ShowFirstPage = page > 3;
            ViewBag.ShowLastPage = page < totalPages - 2;
            
            return View(artists);
        }

        // POST: Admin/Artists/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, bool activate)
        {
            var artist = await _context.MakeupArtists
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.ArtistId == id);

            if (artist == null)
            {
                return NotFound();
            }

            // Set active status based on the activate parameter
            artist.IsActive = activate ? (byte)1 : (byte)0;

            // If artist is deactivated, deactivate the associated user account too
            if (!activate)
            {
                artist.User.IsActive = (byte)0;
                await _userManager.UpdateAsync(artist.User);
            }

            await _context.SaveChangesAsync();
            
            TempData["StatusMessage"] = $"Artist status updated. Artist is now {(artist.IsActive == 1 ? "active" : "inactive")}.";
            return RedirectToAction(nameof(Index));
        }
    }
} 