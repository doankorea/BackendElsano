using System.Security.Claims;
using Makeup.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Makeup.Services
{
    public interface ICurrentUserService
    {
        int UserId { get; }
        Task<User> GetUser();
    }
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly MakeupContext _context;

        public CurrentUserService( IHttpContextAccessor httpContextAccessor, MakeupContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }
        public int UserId
        {
            get
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return userIdClaim != null && int.TryParse(userIdClaim, out int userId) ? userId : 0;
            }
        }

        public async Task<User> GetUser()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == UserId);
            return user;
        }
    }
}
