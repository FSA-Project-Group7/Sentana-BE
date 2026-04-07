using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentana.API.Enums;
using Sentana.API.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sentana.API.Workers
{
    public class ContractExpirationWorker : BackgroundService
    {
        private readonly ILogger<ContractExpirationWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ContractExpirationWorker(ILogger<ContractExpirationWorker> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(" Contract Expiration Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredContractsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, " Lỗi xảy ra trong quá trình quét hợp đồng hết hạn.");
                }

                // Cấu hình thời gian chạy mỗi 12 tiếng
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }

        private async Task ProcessExpiredContractsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SentanaContext>();

            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
            var today = DateOnly.FromDateTime(vnTime);

            // Tìm các hợp đồng đang Active nhưng đã quá hạn (EndDay < hôm nay)
            var expiredContracts = await context.Contracts
                .Include(c => c.Apartment)
                .Where(c => c.Status == GeneralStatus.Active && c.IsDeleted == false && c.EndDay < today)
                .ToListAsync();

            if (!expiredContracts.Any())
            {
                _logger.LogInformation($" Không có hợp đồng nào hết hạn tính đến {today}.");
                return;
            }

            int count = 0;
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                foreach (var contract in expiredContracts)
                {
                    // 1. Đổi trạng thái hợp đồng
                    contract.Status = GeneralStatus.Inactive;
                    contract.UpdatedAt = vnTime;

                    // FIX: ĐÁNH DẤU CHỜ ADMIN ĐI KIỂM TRA PHÒNG VÀO SÁNG HÔM SAU

                    contract.SettlementStatus = SettlementStatus.PendingInspection;
                    contract.TerminationReason = "Hệ thống tự động chấm dứt do đến hạn. Cần kiểm tra phòng.";
                    contract.AdditionalCost = 0; // Tạm set = 0 cho đến khi Admin kiểm tra xong

                    if (contract.ApartmentId.HasValue)
                    {
                        int aptId = contract.ApartmentId.Value;

                        // 2. Trả phòng về trạng thái Trống (nếu phòng đang có người ở)
                        if (contract.Apartment != null && contract.Apartment.Status == ApartmentStatus.Occupied)
                        {
                            contract.Apartment.Status = ApartmentStatus.Vacant;
                        }

                        // 3. Kick cư dân phụ ra khỏi phòng (Xóa mềm)
                        var residents = await context.ApartmentResidents
                            .Where(r => r.ApartmentId == aptId && r.IsDeleted == false)
                            .ToListAsync();
                        foreach (var r in residents)
                        {
                            r.IsDeleted = true;
                        }

                        // 4. Ngắt các dịch vụ đính kèm
                        var services = await context.ApartmentServices
                            .Where(s => s.ApartmentId == aptId && s.IsDeleted == false)
                            .ToListAsync();
                        foreach (var s in services)
                        {
                            s.IsDeleted = true;
                            s.EndDay = today;
                        }
                    }
                    count++;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation($"🎉 Đã tự động dọn dẹp và đưa {count} hợp đồng hết hạn vào danh sách chờ kiểm tra.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}