using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Technician;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class TechnicianService : ITechnicianService
    {
        private readonly SentanaContext _context;

        public TechnicianService(SentanaContext context)
        {
            _context = context;
        }
        private async Task<bool> CheckEmailExist(string email)
        {
            return await _context.Accounts.AnyAsync(a => a.Email.ToLower() == email.ToLower());
        }

        private async Task<bool> CheckUserNameExist(string username)
        {
            return await _context.Accounts.AnyAsync(a => a.UserName.ToLower() == username.ToLower());
        }

        private async Task<Account?> GetTechnicianById(int accountId)
        {
            return await _context.Accounts.Include(a => a.Info).FirstOrDefaultAsync(a => a.AccountId == accountId && a.RoleId == 3);
        }

        private async Task<bool> CheckIdentityCardExist(string identityCard)
        {
            return await _context.InFos.AnyAsync(i => i.CmndCccd == identityCard);
        }

        public async Task<TechnicianResponseDto> CreateTechnician(CreateTechnicianRequestDto technicianRequest, int managerId)
        {
            if (await CheckEmailExist(technicianRequest.Email))
            {
                throw new Exception("Email này đã tồn tại trong hệ thống.");
            }
            if (await CheckUserNameExist(technicianRequest.UserName))
            {
                throw new Exception("Tên đăng nhập này đã tồn tại.");
            }
            if (await CheckIdentityCardExist(technicianRequest.IdentityCard))
            {
                throw new Exception("Căn cước công dân này đã tồn tại trong hệ thống.");
            }
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(technicianRequest.Password);
            var lastTech = await _context.Accounts
                .Where(a => a.RoleId == 3 && a.Code != null && a.Code.StartsWith("TECH-"))
                .OrderByDescending(a => a.AccountId)
                .FirstOrDefaultAsync();
            int nextNumber = 1;
            if (lastTech != null && lastTech.Code.Length > 5)
            {
                string lastNumberStr = lastTech.Code.Substring(5);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }
            string generatedCode = $"TECH-{nextNumber:D3}";
            DateTime currentTime = DateTime.Now;
            var newAccount = new Account
            {
                Code = generatedCode,
                Email = technicianRequest.Email,
                UserName = technicianRequest.UserName,
                Password = hashedPassword,
                RoleId = 3,
                Status = GeneralStatus.Active,
                TechAvailability = (byte)TechAvailability.Free,
                CreatedAt = currentTime,
                CreatedBy = managerId,
                IsDeleted = false,
                Info = new InFo
                {
                    FullName = technicianRequest.FullName,
                    PhoneNumber = technicianRequest.PhoneNumber,
                    CmndCccd = technicianRequest.IdentityCard,
                    Country = technicianRequest.Country,
                    City = technicianRequest.City,
                    Address = technicianRequest.Address,
                    CreatedAt = currentTime
                }
            };
            _context.Accounts.Add(newAccount);
            await _context.SaveChangesAsync();
            return new TechnicianResponseDto
            {
                AccountId = newAccount.AccountId,
                UserName = newAccount.UserName,
                Email = newAccount.Email,
                FullName = newAccount.Info?.FullName,
                PhoneNumber = newAccount.Info?.PhoneNumber,
                IdentityCard = newAccount.Info?.CmndCccd,
                Status = newAccount.Status,
                TechAvailability = newAccount.TechAvailability,
                Country = newAccount.Info?.Country,
                City = newAccount.Info?.City,
                Address = newAccount.Info?.Address,
                IsDeleted = newAccount.IsDeleted
            };
        }


        public async Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician()
        {
            var technicians = await _context.Accounts
                .Where(a => a.RoleId == 3)
                .Select(a => new TechnicianResponseDto
                {
                    AccountId = a.AccountId,
                    UserName = a.UserName,
                    FullName = a.Info != null ? a.Info.FullName : null,
                    Email = a.Email,
                    PhoneNumber = a.Info != null ? a.Info.PhoneNumber : null,
                    IdentityCard = a.Info != null ? a.Info.CmndCccd : null,
                    Status = a.Status,
                    TechAvailability = a.TechAvailability,
                    Country = a.Info != null ? a.Info.Country : null,
                    City = a.Info != null ? a.Info.City : null,
                    Address = a.Info != null ? a.Info.Address : null,
                    IsDeleted = a.IsDeleted
                })
                .ToListAsync();
            return technicians;
        }

        public async Task<TechnicianResponseDto> UpdateTechnician(int technicianId, UpdateTechnicianRequestDto technicianRequest, int managerId)
        {
            var technician = await GetTechnicianById(technicianId);
            if (technician == null)
            {
                throw new Exception("Kỹ thuật viên không tồn tại trong hệ thống.");
            }
            var emailExist = await _context.Accounts.AnyAsync(a =>
                a.Email.ToLower() == technicianRequest.Email.ToLower() &&
                a.AccountId != technicianId);
            if (emailExist)
            {
                throw new Exception("Email này đã được sử dụng cho một tài khoản khác.");
            }

            var identityCardExist = await _context.Accounts.AnyAsync(a => a.Info != null &&
            a.Info.CmndCccd == technicianRequest.IdentityCard &&
            a.AccountId != technicianId);
            if (identityCardExist)
            {
                throw new Exception("Số CCCD này đã tồn tại trên một tài khoản khác.");
            }
            DateTime currentTime = DateTime.Now;
            technician.Email = technicianRequest.Email;
            technician.IsDeleted = technicianRequest.IsDeleted;
            technician.UpdatedAt = currentTime;
            technician.UpdatedBy = managerId;
            if (technician.Info == null)
            {
                technician.Info = new InFo { CreatedAt = currentTime };
            }
            technician.Info.FullName = technicianRequest.FullName;
            technician.Info.PhoneNumber = technicianRequest.PhoneNumber;
            technician.Info.CmndCccd = technicianRequest.IdentityCard;
            technician.Info.Country = technicianRequest.Country;
            technician.Info.City = technicianRequest.City;
            technician.Info.Address = technicianRequest.Address;
            technician.Info.UpdatedAt = currentTime;
            _context.Accounts.Update(technician);
            await _context.SaveChangesAsync();
            return new TechnicianResponseDto
            {
                AccountId = technician.AccountId,
                UserName = technician.UserName,
                Email = technician.Email,
                FullName = technician.Info.FullName,
                PhoneNumber = technician.Info.PhoneNumber,
                IdentityCard = technician.Info.CmndCccd,
                Status = technician.Status,
                TechAvailability = technician.TechAvailability,
                Country = technician.Info.Country,
                City = technician.Info.City,
                Address = technician.Info.Address,
                IsDeleted = technician.IsDeleted
            };
        }

        public async Task<string> ToggleTechnicianStatus(int technicianId)
        {
            var technician = await GetTechnicianById(technicianId);
            if (technician == null)
            {
                throw new Exception("Kỹ thuật viên không tồn tại.");
            }
            string message = "";

            if (technician.Status == GeneralStatus.Active)
            {
                var hasProcessingTask = await _context.MaintenanceRequests
                    .AnyAsync(m => m.AssignedTo == technicianId && m.Status == MaintenanceRequestStatus.Processing);
                if (hasProcessingTask)
                {
                    throw new Exception("Không thể vô hiệu hóa kỹ thuật viên này vì họ đang có nhiệm vụ đang xử lý.");
                }
                technician.Status = GeneralStatus.Inactive;
                message = "Khóa tài khoản Kỹ thuật viên thành công!";
            }
            else
            {
                technician.Status = GeneralStatus.Active;
                message = "Mở khóa tài khoản Kỹ thuật viên thành công!";
            }
            _context.Accounts.Update(technician);
            await _context.SaveChangesAsync();
            return message;
        }
    }
}
