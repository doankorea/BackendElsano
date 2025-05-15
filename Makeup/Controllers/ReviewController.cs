using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using Makeup.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Makeup.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly MakeupContext _context;

        public ReviewController(MakeupContext context)
        {
            _context = context;
        }

        // POST: api/Review/rate
        [HttpPost("rate")]
        public async Task<IActionResult> CreateReview([FromBody] ReviewCreateDto review)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var appointment = await _context.Appointments.FindAsync(review.AppointmentId);
            var user = await _context.Users.FindAsync(appointment.UserId);
            var artist = await _context.MakeupArtists.FindAsync(appointment.ArtistId);
            var newReview = new Review
            {
                AppointmentId = review.AppointmentId,
                UserId = user.Id,
                ArtistId = artist.ArtistId, 
                Rating = review.Rating,
                Content = review.Content,
                CreatedAt = DateTime.UtcNow
            };
            Console.WriteLine("newReview: " + newReview);
            _context.Reviews.Add(newReview);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Review created successfully", review = newReview });
        }
    }
}