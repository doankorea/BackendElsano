using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

namespace Makeup.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly MakeupContext _context;

        public UsersController(
            UserManager<User> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            MakeupContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: Admin/Users
        public async Task<IActionResult> Index(string searchTerm, string status, int page = 1)
        {
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            
            var pageSize = 10; // Số lượng item trên mỗi trang
            var skip = (page - 1) * pageSize; // Số lượng item bỏ qua
            
            var usersQuery = _userManager.Users.AsQueryable();

            // Filtering
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                usersQuery = usersQuery.Where(u => 
                    u.UserName.ToLower().Contains(searchTerm) || 
                    u.Email.ToLower().Contains(searchTerm) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(searchTerm)) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(searchTerm)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "active")
                {
                    usersQuery = usersQuery.Where(u => u.IsActive == 1);
                }
                else if (status == "inactive")
                {
                    usersQuery = usersQuery.Where(u => u.IsActive == 0 || u.IsActive == null);
                }
            }

            // Count total items for pagination
            var totalItems = await usersQuery.CountAsync();
            
            // Tính toán số trang
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            // Đảm bảo trang hiện tại nằm trong phạm vi hợp lệ
            page = Math.Max(1, Math.Min(page, totalPages));

            // Apply pagination and ordering
            var users = await usersQuery
                .OrderByDescending(u => u.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            // Get user roles
            var userRoles = new Dictionary<int, List<string>>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles[user.Id] = roles.ToList();
            }

            // Set ViewBag variables for pagination
            ViewBag.UserRoles = userRoles;
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
            
            return View(users);
        }

        // POST: Admin/Users/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, bool activate)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return NotFound();
            }

            // Set active status based on the activate parameter
            user.IsActive = activate ? (byte)1 : (byte)0;
            user.UpdatedAt = DateTime.Now;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["StatusMessage"] = $"User status updated. User is now {(user.IsActive == 1 ? "active" : "inactive")}.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update user status.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
} 