using Microsoft.EntityFrameworkCore;
using Makeup.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Makeup.Services
{
    public class AppointmentCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AppointmentCleanupJob> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Kiểm tra mỗi 15 phút

        public AppointmentCleanupJob(
            IServiceProvider serviceProvider,
            ILogger<AppointmentCleanupJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AppointmentCleanupJob started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CancelExpiredAppointments();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cancelling expired appointments");
                }

                // Đợi một khoảng thời gian trước khi kiểm tra lại
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CancelExpiredAppointments()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MakeupContext>();

            // Lấy tất cả lịch hẹn ở trạng thái "Pending" đã qua thời gian bắt đầu
            var now = DateTime.Now;
            var expiredAppointments = await dbContext.Appointments
                .Where(a => a.Status == "Pending" && a.AppointmentDate < now)
                .Include(a => a.Payments)
                .ToListAsync();

            if (!expiredAppointments.Any())
            {
                _logger.LogInformation("No expired appointments found to cancel.");
                return;
            }

            int count = 0;
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
                
                _logger.LogInformation("Cancelled appointment ID: {appointmentId}, scheduled for: {appointmentDate}", 
                    appointment.AppointmentId, appointment.AppointmentDate);
                
                count++;
            }

            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully cancelled {count} expired appointments.", count);
        }
    }
} 