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

        public async Task<TechnicianResponseDto> CreateTechnician(CreateTechnicianRequestDto technicianRequest)
        {
            if (await CheckEmailExist(technicianRequest.Email))
            {
                throw new Exception("Email này đã tồn tại trong hệ thống.");
            }

            if (await CheckUserNameExist(technicianRequest.UserName))
            {
                throw new Exception("Tên đăng nhập này đã tồn tại.");
            }

            if(await CheckIdentityCardExist(technicianRequest.IdentityCard))
            {
                throw new Exception("Số CMND/CCCD này đã tồn tại trong hệ thống.");
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(technicianRequest.Password);

            string generatedCode = "TECH-" + DateTimeOffset.Now.ToUnixTimeSeconds().ToString().Substring(5);

            var newAccount = new Account
            {
                Code = generatedCode,
                Email = technicianRequest.Email,
                UserName = technicianRequest.UserName,
                Password = hashedPassword,
                RoleId = 3,
                Status = Sentana.API.Enums.GeneralStatus.Active,
                TechAvailability = (byte)Sentana.API.Enums.TechAvailability.Free,
                CreatedAt = DateTime.Now,
                Info = new InFo
                {
                    FullName = technicianRequest.FullName,
                    PhoneNumber = technicianRequest.PhoneNumber,
                    CmndCccd = technicianRequest.IdentityCard
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
                Status = newAccount.Status,
                TechAvailability = newAccount.TechAvailability
            };
        }


        public async Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician()
        {
            var technicians = await _context.Accounts.Include(a => a.Info).Where(a => a.RoleId == 3).ToListAsync();
            return technicians.Select(a => new TechnicianResponseDto
            {
                AccountId = a.AccountId,
                UserName = a.UserName,
                FullName = a.Info?.FullName,
                Email = a.Email,
                PhoneNumber = a.Info?.PhoneNumber,
                Status = a.Status,
                TechAvailability = a.TechAvailability
            });
        }

        public async Task<TechnicianResponseDto> UpdateTechnician(int technicianId, UpdateTechnicianRequestDto technicianRequest)
        {
            var technician =  await GetTechnicianById(technicianId);
            if (technician == null)
            {
                throw new Exception("Kỹ thuật viên không tồn tại.");
            };
            var emailExist = await _context.Accounts.AnyAsync(a => a.Email.ToLower() == technicianRequest.Email.ToLower() && a.AccountId != technicianId);
            if (emailExist)
            {
                throw new Exception("Email này đã được sử dụng trong hệ thống.");
            };
            technician.Email = technicianRequest.Email;
            if(technician.Info == null)
            {
                technician.Info = new InFo();
            }
            technician.Info.FullName = technicianRequest.FullName;
            technician.Info.PhoneNumber = technicianRequest.PhoneNumber;
            _context.Accounts.Update(technician);
            await _context.SaveChangesAsync();
            return new TechnicianResponseDto
            {
                AccountId = technician.AccountId,
                UserName = technician.UserName,
                Email = technician.Email,
                FullName = technician.Info?.FullName,
                PhoneNumber = technician.Info?.PhoneNumber,
                Status = technician.Status,
                TechAvailability = technician.TechAvailability
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
