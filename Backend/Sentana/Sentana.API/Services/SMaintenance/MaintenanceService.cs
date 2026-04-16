using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sentana.API.Constants;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Maintenance;
using Sentana.API.Enums;
using Sentana.API.Hubs;
using Sentana.API.Models;
using Sentana.API.Services.SNotification;
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
        private readonly INotificationPublisher _notificationPublisher;

        public MaintenanceService(
            SentanaContext context,
            IMinioService minioService,
            IHubContext<NotificationHub> hubContext,
            IRabbitMQProducer rabbitMQProducer,
            INotificationPublisher notificationPublisher)
        {
            _context = context;
            _minioService = minioService;
            _hubContext = hubContext;
            _rabbitMQProducer = rabbitMQProducer; 
            _notificationPublisher = notificationPublisher;
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

				// XỬ LÝ SIGNALR VÀ LƯU DATABASE CHO MANAGER
				var fullRequest = await GetSingleRequestPayloadAsync(newRequest.RequestId);
				var managerId = await _context.Apartments
					.Where(a => a.ApartmentId == request.ApartmentId)
					.Select(a => a.Building.ManagerId)
					.FirstOrDefaultAsync();

				if (managerId.HasValue && fullRequest != null)
				{
					await _notificationPublisher.QueueNotificationAsync(
						managerId.Value,
						"Yêu cầu mới",
						$"P.{fullRequest.ApartmentName} vừa báo cáo sự cố: {request.Title}");
					
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

			// THÊM BLOCK NÀY ĐỂ LƯU DATABASE
			if (task.AccountId.HasValue)
				await _notificationPublisher.QueueNotificationAsync(task.AccountId.Value, "Đang xử lý", $"Thợ đã bắt đầu sửa chữa sự cố '{task.Title}'.");

			if (managerId.HasValue)
				await _notificationPublisher.QueueNotificationAsync(managerId.Value, "Đang xử lý", $"Thợ đã bắt đầu sửa chữa sự cố '{task.Title}' tại P.{payload?.ApartmentName}.");

			// SignalR cũ giữ nguyên
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

			// 1. CẬP NHẬT CHỐT CHẶN: Cho phép thợ submit khi đang xử lý (Processing) HOẶC bị bắt làm lại (Reopened)
			if (task.Status != MaintenanceRequestStatus.Processing && task.Status != MaintenanceRequestStatus.Reopened)
				return (false, "Trạng thái không hợp lệ. Chỉ có thể báo cáo hoàn tất khi Đang xử lý hoặc Yêu cầu làm lại.");

			string? uploadedImageUrl = null;
			if (request.Photo != null && request.Photo.Length > 0)
			{
				uploadedImageUrl = await _minioService.UploadImageAsync(request.Photo, "maintenance-fixed-images");
			}

			// Kiểm tra xem đây là làm lần đầu hay khắc phục lại
			bool isReDo = task.Status == MaintenanceRequestStatus.Reopened;

			// Chuyển trạng thái sang Chờ nghiệm thu
			task.Status = MaintenanceRequestStatus.Fixed;
			task.FixDay = DateTime.Now;

			// 2. GIỮ LẠI LỊCH SỬ: Nếu là báo cáo lại, nối thêm vào log cũ thay vì ghi đè
			if (isReDo)
			{
				task.ResolutionNote = $"[THỢ KHẮC PHỤC LẠI]: {request.ResolutionNote}\n{task.ResolutionNote}";
			}
			else
			{
				task.ResolutionNote = request.ResolutionNote;
			}

			if (uploadedImageUrl != null) task.FixedImageUrl = uploadedImageUrl;

			await _context.SaveChangesAsync();

			var payload = await GetSingleRequestPayloadAsync(task.RequestId);
			var managerId = task.Apartment?.Building?.ManagerId;
			var notifyIds = new List<string>();

			if (task.AccountId.HasValue && task.AccountId.Value > 0)
			{
				notifyIds.Add(task.AccountId.Value.ToString());
				string title = isReDo ? "Đã khắc phục lại sự cố" : "Bảo trì hoàn tất";
				string msg = isReDo ? $"Kỹ thuật viên đã xử lý lại sự cố '{task.Title}'. Vui lòng kiểm tra."
									: $"Sự cố '{task.Title}' đã được kỹ thuật viên xử lý xong.";
				await _notificationPublisher.QueueNotificationAsync(task.AccountId.Value, title, msg);
			}

			if (managerId.HasValue)
			{
				notifyIds.Add(managerId.Value.ToString());
				string titleAdmin = isReDo ? "Thợ đã khắc phục lại" : "Chờ nghiệm thu";
				string msgAdmin = $"Thợ vừa báo cáo hoàn tất sự cố '{task.Title}' tại P.{payload?.ApartmentName}.";
				await _notificationPublisher.QueueNotificationAsync(managerId.Value, titleAdmin, msgAdmin);
			}

			if (notifyIds.Any())
			{
				await _hubContext.Clients.Users(notifyIds).SendAsync("ReceiveFixedTask", payload);
			}

			return (true, "Đã gửi báo cáo hoàn tất công việc.");
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
            if (request.Status == MaintenanceRequestStatus.Closed ||
                request.Status == MaintenanceRequestStatus.Canceled ||
                request.Status == MaintenanceRequestStatus.Fixed)
            {
                throw new Exception("Không thể phân công thợ cho yêu cầu đã hoàn tất, đã đóng hoặc bị hủy.");
            }

            if (request.AssignedTo.HasValue)
            {
                throw new Exception("Yêu cầu bảo trì này đã được phân công cho một kỹ thuật viên khác và không thể phân công lại.");
            }
            var newTechAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == dto.TechnicianId && a.RoleId == 3 && a.IsDeleted == false);
            if (newTechAccount == null || newTechAccount.Status != GeneralStatus.Active)
                throw new Exception("Kỹ thuật viên không tồn tại hoặc tài khoản đã bị khóa.");
            if (newTechAccount.TechAvailability == (byte)TechAvailability.Busy)
                throw new Exception("Kỹ thuật viên này đang bận xử lý công việc khác.");

			request.AssignedTo = dto.TechnicianId;
			request.Priority = (byte)dto.Priority;
			request.Status = MaintenanceRequestStatus.Pending; // BE của bạn đang để 1 là Pending sau khi giao
			request.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();

			var payload = await GetSingleRequestPayloadAsync(request.RequestId);
			if (payload != null)
			{
				// 1. QUAN TRỌNG: Lưu thông báo vào Database để hiện ở Chuông
				await _notificationPublisher.QueueNotificationAsync(dto.TechnicianId,
					"Công việc mới",
					$"Bạn vừa được giao xử lý sự cố: {request.Title} tại P.{payload.ApartmentName}");

				if (request.AccountId.HasValue)
				{
					await _notificationPublisher.QueueNotificationAsync(request.AccountId.Value,
						"Đã phân công",
						$"Sự cố '{request.Title}' của bạn đã được giao cho kỹ thuật viên.");
				}

				// 2. Bắn SignalR (Đảm bảo tên Event là "ReceiveAssignedTask" cho khớp với FE của Tech)
				var notifyIds = new List<string> { dto.TechnicianId.ToString(), request.AccountId.ToString()! };
				await _hubContext.Clients.Users(notifyIds).SendAsync("ReceiveAssignedTask", payload);
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
            if (residentId <= 0) return new List<MaintenanceHistoryDto>();

            return await _context.MaintenanceRequests
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Include(m => m.AssignedToNavigation)
                    .ThenInclude(tech => tech.Info)
                .Where(m => m.AccountId == residentId && m.IsDeleted != true)
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
            if (requestId <= 0 || residentId <= 0) return null;

            return await _context.MaintenanceRequests
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Include(m => m.AssignedToNavigation)
                    .ThenInclude(tech => tech.Info)
                .Where(m => m.RequestId == requestId
                         && m.AccountId == residentId    // Chỉ được xem request của chính mình
                         && m.IsDeleted != true)
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
				.Include(m => m.Apartment).ThenInclude(a => a.Building)
				.Include(m => m.Account)
				.FirstOrDefaultAsync(m => m.RequestId == requestId);

			if (task == null) return (false, "Không tìm thấy công việc.");
			if (task.Status != MaintenanceRequestStatus.Fixed)
				return (false, "Chỉ có thể đóng phiếu khi thợ đã báo cáo hoàn tất.");

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				// 1. Cập nhật trạng thái và GHI CHÚ NGUỒN NGHIỆM THU
				task.Status = MaintenanceRequestStatus.Closed;
				task.UpdatedAt = DateTime.Now;
				task.UpdatedBy = managerId;

				// Thêm dòng ghi chú để phân biệt Admin hay Cư dân nghiệm thu
				task.ResolutionNote = $"{task.ResolutionNote}\n[HỆ THỐNG: Đã được nghiệm thu bởi Ban Quản Lý]";

				// 2. Giải phóng thợ
				if (task.AssignedTo.HasValue)
				{
					var techAccount = await _context.Accounts.FindAsync(task.AssignedTo.Value);
					if (techAccount != null) techAccount.TechAvailability = (byte)TechAvailability.Free;
				}

				await _context.SaveChangesAsync();

				// 3. LƯU THÔNG BÁO VÀO DATABASE CHO CƯ DÂN & THỢ
				var payload = await GetSingleRequestPayloadAsync(task.RequestId);

				if (task.AccountId.HasValue)
				{
					await _notificationPublisher.QueueNotificationAsync(task.AccountId.Value,
						"Nghiệm thu thành công", $"Ban quản lý đã thay mặt bạn nghiệm thu sự cố '{task.Title}'.");
				}
				if (task.AssignedTo.HasValue)
				{
					await _notificationPublisher.QueueNotificationAsync(task.AssignedTo.Value,
						"Công việc hoàn tất", $"Quản lý đã nghiệm thu sự cố '{task.Title}' bạn vừa xử lý.");
				}

				// 4. SIGNALR: PHÁT SÓNG ĐỂ TỰ ĐỘNG REFRESH MÀN HÌNH KHÔNG CẦN F5
				var notifyIds = new List<string>();
				if (task.AccountId.HasValue) notifyIds.Add(task.AccountId.Value.ToString());
				if (task.AssignedTo.HasValue) notifyIds.Add(task.AssignedTo.Value.ToString());

				if (notifyIds.Any())
				{
					// Sự kiện TaskClosed này sẽ kích hoạt hàm reloadTrigger ở các màn hình khác
					await _hubContext.Clients.Users(notifyIds).SendAsync("TaskClosed", payload);
				}

				await transaction.CommitAsync();
				return (true, "Đã nghiệm thu hộ và đồng bộ dữ liệu toàn hệ thống.");
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				return (false, $"Lỗi: {ex.Message}");
			}
		}

		public async Task<(bool IsSuccess, string Message)> RejectTaskAsync(int requestId, RejectTaskRequestDto request, int managerId)
        {
            var task = await _context.MaintenanceRequests.FindAsync(requestId);
            if (task == null) return (false, "Không tìm thấy công việc.");
            if (task.Status != MaintenanceRequestStatus.Fixed)
                return (false, "Chỉ có thể từ chối nghiệm thu khi thợ đã báo cáo hoàn tất (Fixed).");
            task.Status = MaintenanceRequestStatus.Reopened;
            task.UpdatedAt = DateTime.Now;
            task.UpdatedBy = managerId;
            task.ResolutionNote = $"[TỪ CHỐI NGHIỆM THU: {request.RejectReason}]\n--- Ghi chú cũ: {task.ResolutionNote}";
            await _context.SaveChangesAsync();

            if (task.AssignedTo.HasValue && task.AssignedTo.Value > 0)
            {
                await _notificationPublisher.QueueNotificationAsync(
                    task.AssignedTo.Value,
                    "Nghiệm thu không đạt",
                    $"Công việc '{task.Title}' không đạt yêu cầu. Quản lý yêu cầu: {request.RejectReason}");
            }
            var payload = await GetSingleRequestPayloadAsync(task.RequestId);
            if (task.AssignedTo.HasValue)
            {
                await _hubContext.Clients.User(task.AssignedTo.Value.ToString()).SendAsync(SignalREvents.MAINTENANCE_TASKREJECTED, payload);
            }
            return (true, "Nghiệm thu KHÔNG ĐẠT: Đã trả lại trạng thái Đang xử lý và thông báo yêu cầu thợ làm lại.");
        }

		public async Task<(bool IsSuccess, string Message)> ResidentAcceptTaskAsync(int requestId, int residentId)
		{
			// FIX LỖI 1: Bổ sung ThenInclude(Building) để lấy được ManagerId của Tòa nhà
			var task = await _context.MaintenanceRequests
				.Include(m => m.Apartment)
					.ThenInclude(a => a.Building) // <-- QUAN TRỌNG
				.FirstOrDefaultAsync(m => m.RequestId == requestId && m.AccountId == residentId);

			if (task == null) return (false, "Không tìm thấy công việc hoặc bạn không có quyền truy cập.");
			if (task.Status != MaintenanceRequestStatus.Fixed)
				return (false, "Sự cố chưa được thợ báo cáo hoàn tất, không thể nghiệm thu.");

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				task.Status = MaintenanceRequestStatus.Closed; // Đóng thẻ
				task.UpdatedAt = DateTime.Now;
				task.UpdatedBy = residentId;

				if (task.AssignedTo.HasValue)
				{
					var techAccount = await _context.Accounts.FindAsync(task.AssignedTo.Value);
					if (techAccount != null) techAccount.TechAvailability = (byte)TechAvailability.Free;
				}

				await _context.SaveChangesAsync();

				var payload = await GetSingleRequestPayloadAsync(task.RequestId);
				var notifyIds = new List<string>();
				var managerId = task.Apartment?.Building?.ManagerId;

				// FIX LỖI 2: LƯU DATABASE CHO QUẢN LÝ
				if (managerId.HasValue)
				{
					notifyIds.Add(managerId.Value.ToString());
					await _notificationPublisher.QueueNotificationAsync(managerId.Value, "Hoàn thành", $"Cư dân P.{payload?.ApartmentName} đã nghiệm thu sự cố '{task.Title}'.");
				}

				// FIX LỖI 2: LƯU DATABASE CHO KỸ THUẬT VIÊN
				if (task.AssignedTo.HasValue)
				{
					notifyIds.Add(task.AssignedTo.Value.ToString());
					await _notificationPublisher.QueueNotificationAsync(task.AssignedTo.Value, "Hoàn tất", $"Cư dân đã nghiệm thu sự cố '{task.Title}'. Bạn đã rảnh tay!");
				}

				if (notifyIds.Any())
				{
					// Ép cứng chuỗi "TaskClosed" để khớp tuyệt đối với useEffect của React
					await _hubContext.Clients.Users(notifyIds).SendAsync("TaskClosed", payload);
				}

				await transaction.CommitAsync();
				return (true, "Nghiệm thu thành công! Cảm ơn bạn đã phản hồi.");
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				return (false, $"Lỗi hệ thống khi nghiệm thu: {ex.Message}");
			}
		}

		public async Task<(bool IsSuccess, string Message)> ResidentRejectTaskAsync(int requestId, string reason, int residentId)
		{
			// FIX LỖI 1: Bổ sung ThenInclude
			var task = await _context.MaintenanceRequests
				.Include(m => m.Apartment)
					.ThenInclude(a => a.Building) // <-- QUAN TRỌNG
				.FirstOrDefaultAsync(m => m.RequestId == requestId && m.AccountId == residentId);

			if (task == null) return (false, "Không tìm thấy công việc hoặc bạn không có quyền.");
			if (task.Status != MaintenanceRequestStatus.Fixed)
				return (false, "Chỉ có thể từ chức nghiệm thu khi thợ đã báo cáo hoàn tất.");

			task.Status = MaintenanceRequestStatus.Reopened;
			task.UpdatedAt = DateTime.Now;
			task.UpdatedBy = residentId;
			task.ResolutionNote = $"[CƯ DÂN YÊU CẦU LÀM LẠI: {reason}]\n--- Báo cáo cũ: {task.ResolutionNote}";

			await _context.SaveChangesAsync();

			var payload = await GetSingleRequestPayloadAsync(task.RequestId);
			var notifyIds = new List<string>();
			var managerId = task.Apartment?.Building?.ManagerId;

			// FIX LỖI 2: LƯU DATABASE CHO QUẢN LÝ
			if (managerId.HasValue)
			{
				notifyIds.Add(managerId.Value.ToString());
				await _notificationPublisher.QueueNotificationAsync(managerId.Value, "Yêu cầu làm lại", $"Cư dân P.{payload?.ApartmentName} từ chối nghiệm thu sự cố '{task.Title}'.");
			}

			// FIX LỖI 2: LƯU DATABASE CHO KỸ THUẬT VIÊN
			if (task.AssignedTo.HasValue)
			{
				notifyIds.Add(task.AssignedTo.Value.ToString());
				await _notificationPublisher.QueueNotificationAsync(task.AssignedTo.Value, "Cư dân yêu cầu làm lại", $"Sự cố '{task.Title}' chưa đạt. Lời nhắn: {reason}");
			}

			if (notifyIds.Any())
			{
				// Ép cứng chuỗi "TaskRejectedByManager" để khớp với useEffect của màn Thợ
				await _hubContext.Clients.Users(notifyIds).SendAsync("TaskRejectedByManager", payload);
			}

			return (true, "Đã gửi yêu cầu xử lý lại cho Kỹ thuật viên.");
		}
	}
}