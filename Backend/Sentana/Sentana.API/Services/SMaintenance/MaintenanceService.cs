using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sentana.API.Constants;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Maintenance;
using Sentana.API.Enums;
using Sentana.API.Hubs;
using Sentana.API.Models;
using Sentana.API.Services.SRabbitMQ;
using Sentana.API.Services.SStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Sentana.API.Services.SMaintenance
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly SentanaContext _context;
        private readonly IMinioService _minioService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IRabbitMQProducer _rabbitMQProducer;

        public MaintenanceService(
            SentanaContext context,
            IMinioService minioService,
            IHubContext<NotificationHub> hubContext, IRabbitMQProducer rabbitMQProducer)
        {
            _context = context;
            _minioService = minioService;
            _hubContext = hubContext;
            _rabbitMQProducer = rabbitMQProducer; 
        }

        public async Task<(bool IsSuccess, string Message, object? Data)> GetIssueCategoriesAsync()
        {
            var categories = await _context.IssueCategories
                .Select(c => new { c.CategoryId, c.CategoryName })
                .ToListAsync();
            return (true, "Lấy danh mục thành công", categories);
        }

        public async Task<(bool IsSuccess, string Message, object? Data)> GetMyActiveApartmentsAsync(int residentId)
        {
            try
            {
                var myApartments = await _context.ApartmentResidents
                    .Include(ar => ar.Apartment)
                    .Where(ar => ar.AccountId == residentId && ar.Status == GeneralStatus.Active && ar.IsDeleted == false)
                    .Select(ar => new
                    {
                        ApartmentId = ar.ApartmentId,
                        ApartmentCode = ar.Apartment != null ? ar.Apartment.ApartmentCode : "N/A"
                    })
                    .Distinct()
                    .ToListAsync();

                return (true, "Lấy danh sách phòng thành công.", myApartments);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống: {ex.Message}", null);
            }
        }

        public async Task<(bool IsSuccess, string Message, object? Data)> CreateResidentRequestAsync(CreateMaintenanceDto request, int residentId)
        {
            // Kiểm tra xem Cư dân này có đang ở trong Căn hộ này và hợp đồng còn Active hay không
            var isAuthorized = await _context.ApartmentResidents
                .AnyAsync(ar => ar.AccountId == residentId
                             && ar.ApartmentId == request.ApartmentId
                             && ar.Status == GeneralStatus.Active
                             && ar.IsDeleted == false);

            if (!isAuthorized)
            {
                return (false, "Bạn không có quyền tạo báo cáo cho căn hộ này, hoặc hợp đồng của bạn đã hết hạn.", null);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string? uploadedImageUrl = null;

                if (request.Photo != null && request.Photo.Length > 0)
                {
                    uploadedImageUrl = await _minioService.UploadFileAsync(request.Photo, "maintenance-images");
                }

                var newRequest = new MaintenanceRequest
                {
                    AccountId = residentId,
                    ApartmentId = request.ApartmentId,
                    CategoryId = request.CategoryId,
                    Title = request.Title,
                    Description = request.Description,
                    ImageUrl = uploadedImageUrl,
                    Priority = (byte)MaintenancePriority.Low,
                    Status = MaintenanceRequestStatus.Pending,
                    CreateDay = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    CreatedBy = residentId,
                    IsDeleted = false
                };

                await _context.MaintenanceRequests.AddAsync(newRequest);
                await _context.SaveChangesAsync();

                // XỬ LÝ SIGNALR: BẮN THÔNG BÁO REALTIME CHO MANAGER
                var fullRequest = await GetSingleRequestPayloadAsync(newRequest.RequestId);
                var managerId = await _context.Apartments
                    .Where(a => a.ApartmentId == request.ApartmentId)
                    .Select(a => a.Building.ManagerId)
                    .FirstOrDefaultAsync();

                if (managerId.HasValue && fullRequest != null)
                {
                    await _hubContext.Clients.User(managerId.Value.ToString())
                        .SendAsync(SignalREvents.MAINTENANCE_REQUEST, fullRequest);
                }

                await transaction.CommitAsync();
                return (true, "Đã gửi yêu cầu bảo trì thành công.", newRequest);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, $"Từ chối lưu: {errorMessage}", null);
            }
        }

        public async Task<(bool IsSuccess, string Message, object? Data)> GetResidentRequestsAsync(int residentId)
        {
            var requests = await _context.MaintenanceRequests
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Where(m => m.AccountId == residentId && m.IsDeleted == false)
                .OrderByDescending(m => m.CreateDay)
                .Select(m => new
                {
                    RequestId = m.RequestId,
                    Title = m.Title,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",
                    Description = m.Description,
                    ImageUrl = m.ImageUrl,
                    ApartmentCode = m.Apartment != null ? m.Apartment.ApartmentCode : "N/A",
                    Status = (m.Status ?? MaintenanceRequestStatus.Pending).ToString(),
                    CreateDay = m.CreateDay,
                    FixDay = m.FixDay,
                    ResolutionNote = m.ResolutionNote
                })
                .ToListAsync();

            return (true, "Lấy danh sách thành công.", requests);
        }

        // FIX BUG 53 & 54
        public async Task<(bool IsSuccess, string Message, object? Data)> GetMyAssignedTasksAsync(int currentTechId, int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.MaintenanceRequests
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Where(m => m.AssignedTo == currentTechId && m.IsDeleted == false);

            var totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(m => m.Priority)
                .ThenBy(m => m.CreateDay)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MaintenanceTaskDto
                {
                    RequestId = m.RequestId,
                    Title = m.Title,
                    Description = m.Description,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",
                    ApartmentCode = m.Apartment != null ? m.Apartment.ApartmentCode : "N/A",
                    Priority = ((MaintenancePriority)(m.Priority ?? 1)).ToString(),
                    Status = (m.Status ?? MaintenanceRequestStatus.Pending).ToString(),
                    CreateDay = m.CreateDay
                })
                .ToListAsync();

            return (true, "Thành công", new { Items = items, TotalItems = totalItems });
        }

        // FIX BUG 55
        public async Task<(bool IsSuccess, string Message)> AcceptTaskAsync(int requestId, int currentTechId)
        {
            var task = await _context.MaintenanceRequests.FindAsync(requestId);
            if (task == null) return (false, "Không tìm thấy công việc.");

            if (task.AssignedTo != currentTechId) return (false, "Lỗi phân quyền: Bạn không thể nhận công việc của người khác.");
            if (task.Status != MaintenanceRequestStatus.Pending) return (false, "Trạng thái không hợp lệ. Công việc không ở trạng thái Chờ xử lý.");

            task.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return (true, "Đã xác nhận nhận việc.");
        }

        // FIX BUG 56
        public async Task<(bool IsSuccess, string Message)> StartProcessingTaskAsync(int requestId, int currentTechId)
        {
            var task = await _context.MaintenanceRequests
                .Include(m => m.Apartment)
                .ThenInclude(a => a.Building)
                .FirstOrDefaultAsync(m => m.RequestId == requestId);

            if (task == null) return (false, "Không tìm thấy công việc.");

            if (task.AssignedTo != currentTechId) return (false, "Lỗi phân quyền: Bạn không thể thao tác trên công việc của người khác.");
            if (task.Status != MaintenanceRequestStatus.Pending) return (false, "Trạng thái không hợp lệ. Chỉ có thể bắt đầu khi công việc đang ở trạng thái Chờ xử lý.");

            task.Status = MaintenanceRequestStatus.Processing;
            task.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var payload = await GetSingleRequestPayloadAsync(task.RequestId);
            var managerId = task.Apartment?.Building?.ManagerId;
            var notifyIds = new List<string> { task.AccountId.ToString()! };
            if (managerId.HasValue) notifyIds.Add(managerId.Value.ToString());

            await _hubContext.Clients.Users(notifyIds).SendAsync(SignalREvents.MAINTENANCE_TASKPROCESSING, payload);

            return (true, "Đã bắt đầu xử lý.");
        }

        // FIX BUG 57
        public async Task<(bool IsSuccess, string Message)> FixTaskAsync(int requestId, FixTaskRequestDto request, int currentTechId)
        {
            var task = await _context.MaintenanceRequests
                .Include(m => m.Apartment)
                .ThenInclude(a => a.Building)
                .FirstOrDefaultAsync(m => m.RequestId == requestId);

            if (task == null) return (false, "Không tìm thấy công việc.");

            if (task.AssignedTo != currentTechId) return (false, "Lỗi phân quyền: Bạn không thể thao tác trên công việc của người khác.");
            if (task.Status != MaintenanceRequestStatus.Processing) return (false, "Trạng thái không hợp lệ. Chỉ có thể báo cáo hoàn tất khi đang Đang xử lý.");

            string? uploadedImageUrl = null;
            if (request.Photo != null && request.Photo.Length > 0)
            {
                uploadedImageUrl = await _minioService.UploadImageAsync(request.Photo, "maintenance-fixed-images");
            }
            task.Status = MaintenanceRequestStatus.Fixed;
            task.ResolutionNote = request.ResolutionNote;
            task.FixDay = DateTime.Now;
            if (uploadedImageUrl != null)
            {
                task.FixedImageUrl = uploadedImageUrl;
            }

            var notif = new Notification
            {
                AccountId = task.AccountId ?? 0,
                Title = "Bảo trì hoàn tất",
                Message = $"Sự cố '{task.Title}' đã được kỹ thuật viên xử lý xong.",
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notif);

            await _context.SaveChangesAsync();

            var payload = await GetSingleRequestPayloadAsync(task.RequestId);
            var managerId = task.Apartment?.Building?.ManagerId;
            var notifyIds = new List<string> { task.AccountId.ToString()! };
            if (managerId.HasValue) notifyIds.Add(managerId.Value.ToString());

            await _hubContext.Clients.Users(notifyIds).SendAsync(SignalREvents.MAINTENANCE_TASKFIXED, payload);

            return (true, "Đã cập nhật hoàn tất công việc.");
        }

        public async Task<PagedResult<MaintenanceResponseDto>> GetRequestsForManagerAsync(int managerId, int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.MaintenanceRequests
                .Include(m => m.Apartment)
                    .ThenInclude(a => a.Building)
                .Include(m => m.Category)
                .Include(m => m.Account)
                    .ThenInclude(acc => acc.Info)
                .Include(m => m.AssignedToNavigation)
                    .ThenInclude(tech => tech.Info)
                .Where(m => m.IsDeleted == false &&
                            m.Apartment != null &&
                            m.Apartment.Building != null &&
                            m.Apartment.Building.ManagerId == managerId)
                .OrderByDescending(m => m.CreateDay)
                .AsQueryable();

            var totalRecords = await query.CountAsync();
            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MaintenanceResponseDto
                {
                    RequestId = m.RequestId,
                    Title = m.Title,
                    Description = m.Description,
                    Priority = (MaintenancePriority)(m.Priority ?? (byte)MaintenancePriority.Low),
                    Status = (MaintenanceRequestStatus)(m.Status ?? MaintenanceRequestStatus.Pending),

                    CreateDay = m.CreateDay,
                    FixDay = m.FixDay,
                    UpdatedAt = m.UpdatedAt,

                    ApartmentId = m.ApartmentId,
                    ApartmentName = m.Apartment.ApartmentName ?? m.Apartment.ApartmentCode,
                    CategoryId = m.CategoryId,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",

                    AccountId = m.AccountId,
                    ResidentName = m.Account != null && m.Account.Info != null ? m.Account.Info.FullName : "Cư dân ẩn danh",

                    AssignedTo = m.AssignedTo,
                    AssignedTechnicianName = m.AssignedToNavigation != null && m.AssignedToNavigation.Info != null ? m.AssignedToNavigation.Info.FullName : null,
                    ImageUrl = m.ImageUrl,

                    ResolutionNote = m.ResolutionNote,
                    FixedImageUrl = m.FixedImageUrl
                })
                .ToListAsync();

            return new PagedResult<MaintenanceResponseDto>
            {
                Items = items,
                TotalCount = totalRecords,
                PageNumber = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<bool> AssignTechnicianAsync(int requestId, int managerId, AssignMaintenanceRequestDto dto)
        {
            var request = await _context.MaintenanceRequests
                .FirstOrDefaultAsync(m => m.RequestId == requestId && m.IsDeleted == false);
            if (request == null) return false;

            var techAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == dto.TechnicianId && a.RoleId == 3 && a.IsDeleted == false);
            if (techAccount == null || techAccount.Status != GeneralStatus.Active)
                throw new Exception("Kỹ thuật viên không tồn tại hoặc tài khoản đã bị khóa.");
            if (techAccount.TechAvailability == (byte)TechAvailability.Busy)
                throw new Exception("Kỹ thuật viên này đang bận xử lý công việc khác.");

            request.AssignedTo = dto.TechnicianId;
            request.Priority = (byte)dto.Priority;
            request.Status = MaintenanceRequestStatus.Pending;
            request.UpdatedAt = DateTime.Now;
            request.UpdatedBy = managerId;

            techAccount.TechAvailability = (byte)TechAvailability.Busy;
            techAccount.UpdatedAt = DateTime.Now;
            techAccount.UpdatedBy = managerId;

            await _context.SaveChangesAsync();

            var payload = await GetSingleRequestPayloadAsync(request.RequestId);
            if (payload != null)
            {
                await _hubContext.Clients.User(dto.TechnicianId.ToString())
                    .SendAsync(SignalREvents.MAINTENANCE_ASSIGNEDTASK, payload);
            }

            return true;
        }

        // HÀM HELPER DÙNG CHUNG ĐỂ LẤY PAYLOAD GỬI SIGNALR
        private async Task<MaintenanceResponseDto?> GetSingleRequestPayloadAsync(int requestId)
        {
            return await _context.MaintenanceRequests
                .Include(m => m.Apartment)
                .Include(m => m.Category)
                .Include(m => m.Account).ThenInclude(acc => acc.Info)
                .Include(m => m.AssignedToNavigation).ThenInclude(tech => tech.Info)
                .Where(m => m.RequestId == requestId)
                .Select(m => new MaintenanceResponseDto
                {
                    RequestId = m.RequestId,
                    Title = m.Title,
                    Description = m.Description,
                    Priority = (MaintenancePriority)(m.Priority ?? (byte)MaintenancePriority.Low),
                    Status = (MaintenanceRequestStatus)(m.Status ?? MaintenanceRequestStatus.Pending),
                    CreateDay = m.CreateDay,
                    FixDay = m.FixDay,
                    UpdatedAt = m.UpdatedAt,
                    ApartmentId = m.ApartmentId,
                    ApartmentName = m.Apartment.ApartmentName ?? m.Apartment.ApartmentCode,
                    CategoryId = m.CategoryId,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",
                    AccountId = m.AccountId,
                    ResidentName = m.Account != null && m.Account.Info != null ? m.Account.Info.FullName : "Cư dân ẩn danh",
                    AssignedTo = m.AssignedTo,
                    AssignedTechnicianName = m.AssignedToNavigation != null && m.AssignedToNavigation.Info != null ? m.AssignedToNavigation.Info.FullName : null,
                    ImageUrl = m.ImageUrl,
                    ResolutionNote = m.ResolutionNote,
                    FixedImageUrl = m.FixedImageUrl
                })
                .FirstOrDefaultAsync();
        }

        // US18 - View Maintenance History (Resident)
        public async Task<List<MaintenanceHistoryDto>> GetMyMaintenanceHistoryAsync(int residentId)
        {
            return await _context.MaintenanceRequests
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Include(m => m.AssignedToNavigation)
                    .ThenInclude(tech => tech.Info)
                .Where(m => m.AccountId == residentId && m.IsDeleted == false)
                .OrderByDescending(m => m.CreateDay)
                .Select(m => new MaintenanceHistoryDto
                {
                    RequestId = m.RequestId,
                    ApartmentId = m.ApartmentId,
                    ApartmentCode = m.Apartment != null ? m.Apartment.ApartmentCode : null,
                    CategoryId = m.CategoryId,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",
                    Title = m.Title,
                    Description = m.Description,
                    ResolutionNote = m.ResolutionNote,
                    Priority = m.Priority,
                    PriorityName = m.Priority.HasValue ? ((MaintenancePriority)m.Priority.Value).ToString() : null,
                    CreateDay = m.CreateDay,
                    FixDay = m.FixDay,
                    AssignedToName = m.AssignedToNavigation != null && m.AssignedToNavigation.Info != null
                        ? m.AssignedToNavigation.Info.FullName
                        : null,
                    Status = m.Status.HasValue ? m.Status.Value.ToString() : null,
                    ImageUrl = m.ImageUrl
                })
                .ToListAsync();
        }

        // US19 - Track Maintenance Status (Resident)
        public async Task<MaintenanceStatusDto?> GetMaintenanceStatusAsync(int requestId, int residentId)
        {
            return await _context.MaintenanceRequests
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Include(m => m.AssignedToNavigation)
                    .ThenInclude(tech => tech.Info)
                .Where(m => m.RequestId == requestId
                         && m.AccountId == residentId    // Chỉ được xem request của chính mình
                         && m.IsDeleted == false)
                .Select(m => new MaintenanceStatusDto
                {
                    RequestId = m.RequestId,
                    Title = m.Title,
                    Description = m.Description,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",
                    ApartmentCode = m.Apartment != null ? m.Apartment.ApartmentCode : null,
                    PriorityName = m.Priority.HasValue ? ((MaintenancePriority)m.Priority.Value).ToString() : null,
                    Status = m.Status.HasValue ? m.Status.Value.ToString() : null,
                    AssignedTechnicianName = m.AssignedToNavigation != null && m.AssignedToNavigation.Info != null
                        ? m.AssignedToNavigation.Info.FullName
                        : null,
                    CreateDay = m.CreateDay,
                    FixDay = m.FixDay,
                    ResolutionNote = m.ResolutionNote,
                    ImageUrl = m.ImageUrl
                })
                .FirstOrDefaultAsync();
        }

        public async Task<(bool IsSuccess, string Message)> CloseTaskAsync(int requestId, int managerId)
        {
            var task = await _context.MaintenanceRequests
            .Include(m => m.Account).ThenInclude(a => a.Info)
            .Include(m => m.AssignedToNavigation).ThenInclude(t => t.Info)
            .FirstOrDefaultAsync(m => m.RequestId == requestId);
            if (task == null) return (false, "Không tìm thấy công việc.");
            if (task.Status != MaintenanceRequestStatus.Fixed)
                return (false, "Chỉ có thể đóng phiếu khi thợ đã báo cáo hoàn tất (Fixed).");
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                task.Status = MaintenanceRequestStatus.Closed;
                task.UpdatedAt = DateTime.Now;
                task.UpdatedBy = managerId;
                if (task.AssignedTo.HasValue)
                {
                    var techAccount = await _context.Accounts.FindAsync(task.AssignedTo.Value);
                    if (techAccount != null) techAccount.TechAvailability = (byte)TechAvailability.Free;
                }
                await _context.SaveChangesAsync();
                if (task.Account != null && !string.IsNullOrEmpty(task.Account.Email))
                {
                    var emailContent = $@"
                    <h3>Báo cáo hoàn tất yêu cầu bảo trì</h3>
                    <p>Chào bạn {task.Account.Info?.FullName},</p>
                    <p>Yêu cầu bảo trì <strong>'{task.Title}'</strong> của bạn đã được Ban quản lý nghiệm thu thành công.</p>
                    <ul>
                        <li>Kỹ thuật viên phụ trách: {task.AssignedToNavigation?.Info?.FullName}</li>
                        <li>Thời gian hoàn thành: {task.FixDay?.ToString("dd/MM/yyyy HH:mm")}</li>
                        <li>Ghi chú của thợ: {task.ResolutionNote}</li>
                    </ul>
                    <p>Cảm ơn bạn đã đồng hành cùng Sentana!</p>";

                    var emailDto = new Sentana.API.DTOs.Email.EmailMessageDto
                    {
                        To = task.Account.Email,
                        Subject = $"[Sentana] Nghiệm thu hoàn tất: {task.Title}",
                        Body = emailContent
                    };
                    await _rabbitMQProducer.SendEmailMessage(emailDto);
                }
                var payload = await GetSingleRequestPayloadAsync(task.RequestId);
                await _hubContext.Clients.User(task.AccountId.ToString()!).SendAsync(SignalREvents.MAINTENANCE_TASKCLOSED, payload);
                await transaction.CommitAsync();
                return (true, "Nghiệm thu ĐẠT: Đã đóng phiếu, giải phóng thợ và gửi Email thông báo.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống khi nghiệm thu: {ex.Message}");
            }
        }

        public async Task<(bool IsSuccess, string Message)> RejectTaskAsync(int requestId, RejectTaskRequestDto request, int managerId)
        {
            var task = await _context.MaintenanceRequests.FindAsync(requestId);
            if (task == null) return (false, "Không tìm thấy công việc.");
            if (task.Status != MaintenanceRequestStatus.Fixed)
                return (false, "Chỉ có thể từ chối nghiệm thu khi thợ đã báo cáo hoàn tất (Fixed).");
            task.Status = MaintenanceRequestStatus.Processing;
            task.UpdatedAt = DateTime.Now;
            task.UpdatedBy = managerId;
            task.ResolutionNote = $"[TỪ CHỐI NGHIỆM THU: {request.RejectReason}]\n--- Ghi chú cũ: {task.ResolutionNote}";
            var notif = new Notification
            {
                AccountId = task.AssignedTo ?? 0,
                Title = "Nghiệm thu không đạt",
                Message = $"Công việc '{task.Title}' không đạt yêu cầu. Quản lý yêu cầu: {request.RejectReason}",
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();
            var payload = await GetSingleRequestPayloadAsync(task.RequestId);
            if (task.AssignedTo.HasValue)
            {
                await _hubContext.Clients.User(task.AssignedTo.Value.ToString()).SendAsync(SignalREvents.MAINTENANCE_TASKREJECTED, payload);
            }
            return (true, "Nghiệm thu KHÔNG ĐẠT: Đã trả lại trạng thái Đang xử lý và thông báo yêu cầu thợ làm lại.");
        }
    }
}