using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Makeup.Services
{
    public class AppointmentCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AppointmentCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Chạy kiểm tra mỗi 1 giờ

        public AppointmentCleanupService(
            IServiceProvider serviceProvider,
            ILogger<AppointmentCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Appointment Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CancelExpiredAppointments();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up expired appointments.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CancelExpiredAppointments()
        {
            _logger.LogInformation("Checking for expired appointments...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MakeupContext>();

                // Lấy tất cả lịch hẹn ở trạng thái "Pending" đã qua thời gian bắt đầu
                var now = DateTime.Now;
                var expiredAppointments = await dbContext.Appointments
                    .Where(a => a.Status == "Pending" && a.AppointmentDate < now)
                    .Include(a => a.Payments)
                    .ToListAsync();

                if (expiredAppointments.Any())
                {
                    _logger.LogInformation($"Found {expiredAppointments.Count} expired appointments to cancel.");

                    foreach (var appointment in expiredAppointments)
                    {
                        appointment.Status = "Cancelled";
                        appointment.UpdatedAt = now;

                        // Cập nhật trạng thái payment (nếu có)
                        foreach (var payment in appointment.Payments)
                        {
                            if (payment.PaymentStatus == "Pending")
                            {
                                payment.PaymentStatus = "Cancelled";
                            }
                        }

                        _logger.LogInformation($"Cancelled expired appointment ID: {appointment.AppointmentId}");
                    }

                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Successfully cancelled {expiredAppointments.Count} expired appointments.");
                }
                else
                {
                    _logger.LogInformation("No expired appointments found.");
                }
            }
        }
    }
} 