using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Maintenance;
using Sentana.API.Enums;
using Sentana.API.Models;
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

        public MaintenanceService(SentanaContext context, IMinioService minioService)
        {
            _context = context;
            _minioService = minioService;
        }

        // HÀM MỚI: Lấy danh sách Category thay vì hardcode ở Frontend
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
                    .Where(ar => ar.AccountId == residentId && (int)ar.Status == 1 && ar.IsDeleted == false)
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
            try
            {
                string? uploadedImageUrl = null;

                // XỬ LÝ UPLOAD ẢNH: Gửi file vào folder "maintenance-images" trên MinIO
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
                    ImageUrl = uploadedImageUrl, // Lưu đường dẫn ảnh vào Database
                    Priority = 1,
                    Status = MaintenanceRequestStatus.Pending,
                    CreateDay = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    CreatedBy = residentId,
                    IsDeleted = false
                };

                await _context.MaintenanceRequests.AddAsync(newRequest);
                await _context.SaveChangesAsync();

                return (true, "Đã gửi yêu cầu bảo trì thành công.", newRequest);
            }
            catch (Exception ex)
            {
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
                    ImageUrl = m.ImageUrl, // Lấy đường dẫn ảnh trả về cho Frontend hiển thị
                    ApartmentCode = m.Apartment != null ? m.Apartment.ApartmentCode : "N/A",
                    Status = (m.Status ?? MaintenanceRequestStatus.Pending).ToString(),
                    CreateDay = m.CreateDay,
                    FixDay = m.FixDay,
                    ResolutionNote = m.ResolutionNote
                })
                .ToListAsync();

            return (true, "Lấy danh sách thành công.", requests);
        }

        public async Task<(bool IsSuccess, string Message, object? Data)> GetMyAssignedTasksAsync(int currentTechId, int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.MaintenanceRequests
                .Include(m => m.Category)
                .Include(m => m.Apartment)
                .Where(m => m.AssignedTo == currentTechId && m.IsDeleted == false);

            var totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(m => m.Priority)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MaintenanceTaskDto
                {
                    RequestId = m.RequestId,
                    Title = m.Title,
                    Description = m.Description,
                    CategoryName = m.Category != null ? m.Category.CategoryName : "Khác",
                    ApartmentCode = m.Apartment != null ? m.Apartment.ApartmentCode : "N/A",
                    Status = (m.Status ?? MaintenanceRequestStatus.Pending).ToString(),
                    CreateDay = m.CreateDay
                })
                .ToListAsync();

            return (true, "Thành công", new { Items = items, TotalItems = totalItems });
        }

        public async Task<(bool IsSuccess, string Message)> AcceptTaskAsync(int requestId, int currentTechId)
        {
            var task = await _context.MaintenanceRequests.FindAsync(requestId);
            if (task == null) return (false, "Không thấy task.");
            task.Status = (MaintenanceRequestStatus)2;
            task.AssignedTo = currentTechId;
            await _context.SaveChangesAsync();
            return (true, "Đã nhận task.");
        }

        public async Task<(bool IsSuccess, string Message)> StartProcessingTaskAsync(int requestId, int currentTechId)
        {
            var task = await _context.MaintenanceRequests.FindAsync(requestId);
            if (task == null) return (false, "Không thấy task.");
            task.Status = (MaintenanceRequestStatus)3;
            await _context.SaveChangesAsync();
            return (true, "Đang xử lý.");
        }

        public async Task<(bool IsSuccess, string Message)> FixTaskAsync(int requestId, FixTaskRequestDto request, int currentTechId)
        {
            var task = await _context.MaintenanceRequests.FindAsync(requestId);
            if (task == null) return (false, "Không thấy task.");
            task.Status = (MaintenanceRequestStatus)4;
            task.ResolutionNote = request.ResolutionNote;
            task.FixDay = DateTime.Now;

            var notif = new Notification
            {
                AccountId = task.AccountId ?? 0,
                Title = "Bảo trì hoàn tất",
                Message = $"Sự cố '{task.Title}' đã được xử lý xong.",
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notif);

            await _context.SaveChangesAsync();
            return (true, "Đã sửa xong.");
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

                    ResolutionNote = m.ResolutionNote
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

    }
}