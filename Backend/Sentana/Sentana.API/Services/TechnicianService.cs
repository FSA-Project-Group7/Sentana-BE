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

        private async Task<Account?> GetTechinicianById(int accountId)
        {
            return await _context.Accounts.Include(a => a.Info).FirstOrDefaultAsync(a => a.AccountId == accountId && a.RoleId == 3);
        }

        public async Task<TechnicianResponseDto> CreateTechnician(CreateTechnicianRequestDto technicianRequest)
        {
            if (await CheckEmailExist(technicianRequest.Email))
            {
                throw new Exception("Email này đã được sử dụng trong hệ thống.");

            }
            if (await CheckUserNameExist(technicianRequest.UserName)) 
            {
                throw new Exception("Tên đăng nhập này đã tồn tại.");
            }
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(technicianRequest.Password);
            var newAccount = new Account
            {
                Email = technicianRequest.Email,
                UserName = technicianRequest.UserName,
                Password = hashedPassword,
                RoleId = 3, 
                Status = GeneralStatus.Active,
                TechAvailability = 1,

                Info = new InFo
                {
                    FullName = technicianRequest.FullName,
                    PhoneNumber = technicianRequest.PhoneNumber
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
            var technician =  await GetTechinicianById(technicianId);
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
            technician.Status = technicianRequest.Status;
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

    }
}
