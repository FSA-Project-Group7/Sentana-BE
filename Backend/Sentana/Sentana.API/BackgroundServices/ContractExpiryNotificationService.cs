using Microsoft.EntityFrameworkCore;
using Sentana.API.Enums;
using Sentana.API.Models;
using Sentana.API.Services.SEmail;

namespace Sentana.API.BackgroundServices
{
    public class ContractExpiryNotificationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContractExpiryNotificationService> _logger;

        public ContractExpiryNotificationService(
            IServiceProvider serviceProvider,
            ILogger<ContractExpiryNotificationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Contract Expiry Notification Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndNotifyExpiringContractsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Contract Expiry Notification Service");
                }

                // Chạy mỗi ngày lúc 9:00 sáng
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task CheckAndNotifyExpiringContractsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SentanaContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var today = DateOnly.FromDateTime(DateTime.Now);
            var thirtyDaysLater = today.AddDays(30);
            var sevenDaysLater = today.AddDays(7);

            // Lấy các hợp đồng sắp hết hạn (7 ngày và 30 ngày)
            var expiringContracts = await context.Contracts
                .Include(c => c.Account)
                    .ThenInclude(a => a.Info)
                .Include(c => c.Apartment)
                .Where(c => c.Status == GeneralStatus.Active
                         && c.IsDeleted == false
                         && c.EndDay.HasValue
                         && (c.EndDay.Value == sevenDaysLater || c.EndDay.Value == thirtyDaysLater))
                .ToListAsync();

            foreach (var contract in expiringContracts)
            {
                try
                {
                    var daysRemaining = (contract.EndDay.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                    await SendExpiryNotificationEmailAsync(contract, daysRemaining, emailService);
                    
                    _logger.LogInformation($"Sent expiry notification for contract {contract.ContractCode} ({daysRemaining} days remaining)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send notification for contract {contract.ContractCode}");
                }
            }

            if (expiringContracts.Any())
            {
                _logger.LogInformation($"Processed {expiringContracts.Count} expiring contract notifications");
            }
        }

        private async Task SendExpiryNotificationEmailAsync(Contract contract, int daysRemaining, IEmailService emailService)
        {
            if (contract.Account?.Email == null) return;

            var residentName = contract.Account.Info?.FullName ?? "Quý khách";
            var apartmentName = contract.Apartment?.ApartmentName ?? contract.Apartment?.ApartmentCode ?? "N/A";

            string urgencyColor = daysRemaining <= 7 ? "#dc3545" : "#ffc107";
            string urgencyBg = daysRemaining <= 7 ? "#f8d7da" : "#fff3cd";
            string urgencyText = daysRemaining <= 7 ? "KHẨN CẤP" : "THÔNG BÁO";

            string emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0; text-align: center;'>
                        <h2 style='color: white; margin: 0;'>⏰ {urgencyText}: HỢP ĐỒNG SẮP HẾT HẠN</h2>
                    </div>
                    
                    <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                        <p style='font-size: 16px; color: #333;'>Kính gửi <strong>{residentName}</strong>,</p>
                        
                        <div style='background-color: {urgencyBg}; padding: 20px; border-left: 5px solid {urgencyColor}; border-radius: 8px; margin: 20px 0;'>
                            <h3 style='color: {urgencyColor}; margin: 0 0 10px 0; font-size: 24px;'>
                                Còn <strong>{daysRemaining} ngày</strong> hợp đồng hết hạn!
                            </h3>
                            <p style='margin: 0; color: #666; font-size: 14px;'>
                                Hợp đồng của bạn sẽ hết hạn vào ngày <strong>{contract.EndDay?.ToString("dd/MM/yyyy")}</strong>
                            </p>
                        </div>

                        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <h3 style='color: #667eea; margin-top: 0; border-bottom: 2px solid #667eea; padding-bottom: 10px;'>
                                📋 Thông Tin Hợp Đồng
                            </h3>
                            
                            <table style='width: 100%; border-collapse: collapse;'>
                                <tr style='border-bottom: 1px solid #dee2e6;'>
                                    <td style='padding: 12px 0; color: #666;'>Mã hợp đồng:</td>
                                    <td style='padding: 12px 0; text-align: right; font-weight: bold;'>{contract.ContractCode}</td>
                                </tr>
                                <tr style='border-bottom: 1px solid #dee2e6;'>
                                    <td style='padding: 12px 0; color: #666;'>Căn hộ:</td>
                                    <td style='padding: 12px 0; text-align: right; font-weight: bold;'>{apartmentName}</td>
                                </tr>
                                <tr style='border-bottom: 1px solid #dee2e6;'>
                                    <td style='padding: 12px 0; color: #666;'>Ngày bắt đầu:</td>
                                    <td style='padding: 12px 0; text-align: right;'>{contract.StartDay?.ToString("dd/MM/yyyy")}</td>
                                </tr>
                                <tr style='border-bottom: 1px solid #dee2e6;'>
                                    <td style='padding: 12px 0; color: #666;'>Ngày kết thúc:</td>
                                    <td style='padding: 12px 0; text-align: right; font-weight: bold; color: {urgencyColor};'>{contract.EndDay?.ToString("dd/MM/yyyy")}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px 0; color: #666;'>Tiền thuê/tháng:</td>
                                    <td style='padding: 12px 0; text-align: right; font-weight: bold; color: #28a745;'>{contract.MonthlyRent:N0} VNĐ</td>
                                </tr>
                            </table>
                        </div>

                        <div style='background-color: #e7f3ff; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <h4 style='color: #004085; margin-top: 0;'>🔔 Hành động cần thực hiện:</h4>
                            <ul style='color: #004085; line-height: 1.8; margin: 10px 0;'>
                                <li>Nếu muốn <strong>gia hạn hợp đồng</strong>, vui lòng liên hệ Ban Quản Lý sớm nhất có thể</li>
                                <li>Nếu muốn <strong>chấm dứt hợp đồng</strong>, vui lòng chuẩn bị bàn giao phòng và thanh toán các khoản còn nợ (nếu có)</li>
                                <li>Đảm bảo đã thanh toán đầy đủ các hóa đơn trước khi hợp đồng hết hạn</li>
                            </ul>
                        </div>

                        <div style='background-color: #fff3cd; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='margin: 0; color: #856404;'>
                                <strong>⚠️ Lưu ý:</strong> Sau khi hợp đồng hết hạn, nếu không gia hạn, căn hộ sẽ được thu hồi và tiền cọc sẽ được quyết toán theo quy định.
                            </p>
                        </div>

                        <div style='text-align: center; margin: 30px 0;'>
                            <p style='color: #666; margin-bottom: 15px;'>Liên hệ ngay với chúng tôi:</p>
                            <a href='tel:0343758273' style='display: inline-block; background-color: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 25px; font-weight: bold; margin: 5px;'>
                                📞 Gọi điện
                            </a>
                            <a href='mailto:thamthuongtromnho14@gmail.com' style='display: inline-block; background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 25px; font-weight: bold; margin: 5px;'>
                                ✉️ Gửi email
                            </a>
                        </div>

                        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #999;'>
                            <p style='margin: 5px 0;'>Trân trọng,</p>
                            <p style='margin: 5px 0; font-weight: bold; color: #667eea;'>Ban Quản Lý Sentana</p>
                            <p style='margin: 15px 0 5px 0; font-size: 12px; color: #999;'>
                                Email này được gửi tự động. Vui lòng không trả lời trực tiếp.
                            </p>
                        </div>
                    </div>
                </div>
            ";

            await emailService.SendEmailAsync(
                contract.Account.Email,
                $"[SENTANA] {urgencyText}: Hợp đồng {contract.ContractCode} còn {daysRemaining} ngày hết hạn",
                emailBody
            );
        }
    }
}
