using Makeup.Models;
using Makeup.Models.Vnpay;
using Makeup.Services.Vnpay;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Makeup.Controllers
{
    public class PaymentController : Controller
    {
        private readonly MakeupContext _context;
        private readonly IVnPayService _vnPayService;
        public PaymentController(MakeupContext context, IVnPayService vnPayService)
        {
            _context = context;
            _vnPayService = vnPayService;
        }
        [HttpPost]
        public IActionResult CreatePaymentUrlVnpay([FromBody] PaymentInformationModel model)
        {

            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);

             return Ok(new { paymentUrl = url });
        }
        [HttpGet("Payment/PaymentCallbackVnpay")]
        public async Task<IActionResult> PaymentCallback()
        {
            try
            {
                var response = _vnPayService.PaymentExecute(Request.Query);
                if (response == null || !response.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Payment verification failed"
                    });
                }

                // Parse OrderId
                if (!int.TryParse(response.OrderId, out var appointmentId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid OrderId format"
                    });
                }

                // Find the payment record
                var payment = await _context.Payments
                    .Include(p => p.Appointment)
                    .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId && p.PaymentStatus == "Pending");

                if (payment == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Payment record not found"
                    });
                }

                // Update payment status
                payment.PaymentStatus = response.Success ? "Completed" : "Failed";

                await _context.SaveChangesAsync();

                // Return JSON response
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        appointmentId = payment.AppointmentId,
                        paymentStatus = payment.PaymentStatus,
                        message = response.Success ? "Payment completed successfully." : "Payment failed."
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }
    }
}
