using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Maintenance;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services.SMaintenance
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly SentanaContext _context;

        public MaintenanceService(SentanaContext context)
        {
            _context = context;
        }

        // US22 & US23: Lấy danh sách Task
        public async Task<(bool IsSuccess, string Message, List<MaintenanceTaskDto>? Data)> GetMyAssignedTasksAsync(int currentTechId)
        {
            var tasks = await _context.MaintenanceRequests 
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Where(m => m.AssignedTo == currentTechId
                         && m.IsDeleted == false
                         && m.Status != MaintenanceRequestStatus.Closed
                         && m.Status != MaintenanceRequestStatus.Canceled)
                .OrderByDescending(m => m.Priority)
                .ThenBy(m => m.CreateDay)
                .Select(m => new MaintenanceTaskDto
                {
                    RequestId = m.RequestId,
                    Title = m.Title,
                    Description = m.Description,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",
                    ApartmentCode = m.Apartment != null ? m.Apartment.ApartmentCode : "N/A",

                    Priority = ((MaintenancePriority)(m.Priority ?? (byte)1)).ToString(),
                    Status = (m.Status ?? MaintenanceRequestStatus.Pending).ToString(),

                    CreateDay = m.CreateDay
                })
                .ToListAsync();

            return (true, "Lấy danh sách thành công", tasks);
        }

        // US24: Accept Task
        public async Task<(bool IsSuccess, string Message)> AcceptTaskAsync(int requestId, int currentTechId)
        {
            var task = await _context.MaintenanceRequests.FirstOrDefaultAsync(m => m.RequestId == requestId && m.IsDeleted == false);

            if (task == null) return (false, "Không tìm thấy yêu cầu bảo trì này.");

            if (task.Status != (MaintenanceRequestStatus)1)
                return (false, "Chỉ có thể nhận các yêu cầu đang ở trạng thái Chờ (Pending).");

            if (task.AssignedTo.HasValue && task.AssignedTo != currentTechId)
            {
                return (false, "Yêu cầu này đã được gán đích danh cho một kỹ thuật viên khác.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                task.Status = (MaintenanceRequestStatus)2; // ACCEPTED
                task.AssignedTo = currentTechId;
                task.UpdatedAt = DateTime.Now;
                task.UpdatedBy = currentTechId;

                var log = new History
                {
                    AccountId = currentTechId,
                    Action = 2, // Update
                    Screen = "Task Acceptance",
                    Description = $"Kỹ thuật viên đã tiếp nhận yêu cầu bảo trì #{task.RequestId}",
                    CreatedAt = DateTime.Now
                };
                _context.Histories.Add(log);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Đã tiếp nhận yêu cầu bảo trì thành công.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        // US25: Start Processing Task
        public async Task<(bool IsSuccess, string Message)> StartProcessingTaskAsync(int requestId, int currentTechId)
        {
            var task = await _context.MaintenanceRequests.FirstOrDefaultAsync(m => m.RequestId == requestId && m.IsDeleted == false);

            if (task == null) return (false, "Không tìm thấy yêu cầu bảo trì này.");
            if (task.AssignedTo != currentTechId) return (false, "Bạn không có quyền xử lý yêu cầu này.");

            if (task.Status != (MaintenanceRequestStatus)2)
                return (false, "Chỉ có thể bắt đầu làm những yêu cầu đã được Tiếp nhận (ACCEPTED).");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                task.Status = (MaintenanceRequestStatus)3; // PROCESSING
                task.UpdatedAt = DateTime.Now;
                task.UpdatedBy = currentTechId;

                // US27: Tự động thành BUSY
                var techAccount = await _context.Accounts.FindAsync(currentTechId);
                if (techAccount != null)
                {
                    techAccount.TechAvailability = (byte)TechAvailability.Busy;
                }

                var log = new History
                {
                    AccountId = currentTechId,
                    Action = 2, // Update
                    Screen = "Maintenance Dashboard",
                    Description = $"Bắt đầu xử lý (Processing) yêu cầu bảo trì #{task.RequestId}",
                    CreatedAt = DateTime.Now
                };
                _context.Histories.Add(log);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Đã cập nhật trạng thái thành Đang xử lý (PROCESSING).");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        // US26: Fixed Task
        public async Task<(bool IsSuccess, string Message)> FixTaskAsync(int requestId, FixTaskRequestDto request, int currentTechId)
        {
            var task = await _context.MaintenanceRequests.FirstOrDefaultAsync(m => m.RequestId == requestId && m.IsDeleted == false);

            if (task == null) return (false, "Không tìm thấy yêu cầu bảo trì này.");
            if (task.AssignedTo != currentTechId) return (false, "Bạn không có quyền cập nhật yêu cầu này.");

            if (task.Status != (MaintenanceRequestStatus)3)
                return (false, "Chỉ có thể báo cáo hoàn thành cho các yêu cầu Đang xử lý (PROCESSING).");

            if (string.IsNullOrWhiteSpace(request.ResolutionNote))
                return (false, "Ghi chú cách giải quyết (Resolution note) không được để trống.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                task.Status = (MaintenanceRequestStatus)4; // FIXED
                task.ResolutionNote = request.ResolutionNote;
                task.FixDay = DateTime.Now;
                task.UpdatedAt = DateTime.Now;
                task.UpdatedBy = currentTechId;

                // US27: chuyển về Available
                var hasOtherProcessingTasks = await _context.MaintenanceRequests
                    .AnyAsync(m => m.AssignedTo == currentTechId
                                && m.Status == (MaintenanceRequestStatus)3
                                && m.RequestId != requestId
                                && m.IsDeleted == false);

                if (!hasOtherProcessingTasks)
                {
                    var techAccount = await _context.Accounts.FindAsync(currentTechId);
                    if (techAccount != null)
                    {
                        techAccount.TechAvailability = (byte)TechAvailability.Free;
                    }
                }

                var log = new History
                {
                    AccountId = currentTechId,
                    Action = 3, // Complete/Fix
                    Screen = "Maintenance Dashboard",
                    Description = $"Đã sửa xong yêu cầu #{task.RequestId}. Ghi chú: {request.ResolutionNote}",
                    CreatedAt = DateTime.Now
                };
                _context.Histories.Add(log);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Đã báo cáo hoàn thành yêu cầu bảo trì thành công.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống: {ex.Message}");
            }
        }
    }
}