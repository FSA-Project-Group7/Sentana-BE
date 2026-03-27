using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Maintenance;
using Sentana.API.Enums;
using Sentana.API.Models;
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

        public MaintenanceService(SentanaContext context)
        {
            _context = context;
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
                var newRequest = new MaintenanceRequest
                {
                    AccountId = residentId,
                    ApartmentId = request.ApartmentId,
                    CategoryId = request.CategoryId,
                    Title = request.Title,
                    Description = request.Description,
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
                // Trích xuất lỗi chính xác từ Database (Thường là lỗi Foreign Key)
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
    }
}