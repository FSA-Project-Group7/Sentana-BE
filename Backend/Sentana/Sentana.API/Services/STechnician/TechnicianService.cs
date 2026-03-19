using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Technician;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services.STechnician
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
            return await _context.Accounts
                .Include(a => a.Info)
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.RoleId == 3);
        }
        private async Task<bool> CheckDuplicateRoleByIdentityCard(string identityCard, int roleId)
        {
            return await _context.Accounts.AnyAsync(a =>
                a.RoleId == roleId &&
                a.Info != null &&
                a.Info.CmndCccd == identityCard);
        }
        private async Task<string> GenerateTechnicianCode()
        {
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
            return $"TECH-{nextNumber:D3}";
        }
        private TechnicianResponseDto MapToResponseDto(Account account, InFo? existingInfo = null)
        {
            var info = account.Info ?? existingInfo;
            return new TechnicianResponseDto
            {
                AccountId = account.AccountId,
				Code = account.Code,
				UserName = account.UserName,
                Email = account.Email,
                FullName = info?.FullName,
                PhoneNumber = info?.PhoneNumber,
                IdentityCard = info?.CmndCccd,
                Status = account.Status,
                TechAvailability = account.TechAvailability,
                Country = info?.Country,
                City = info?.City,
                Address = info?.Address,
                IsDeleted = account.IsDeleted,
                Sex = info?.Sex,
                BirthDay = info?.BirthDay
            };
        }
        public async Task<TechnicianResponseDto> CreateTechnician(CreateTechnicianRequestDto technicianRequest, int managerId)
        {
            if (await CheckEmailExist(technicianRequest.Email)) throw new Exception("Email này đã tồn tại trong hệ thống.");
            if (await CheckUserNameExist(technicianRequest.UserName)) throw new Exception("Tên đăng nhập này đã tồn tại.");
            if (await CheckDuplicateRoleByIdentityCard(technicianRequest.IdentityCard, 3))
            {
                throw new Exception("Người sở hữu CCCD này đã có tài khoản Kỹ thuật viên hoặc có thể đã bị xóa. Vui lòng khôi phục thay vì tạo mới.");
            }
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(technicianRequest.Password);
            string generatedCode = await GenerateTechnicianCode();
            DateTime currentTime = DateTime.Now;
            var existingInfo = await _context.InFos
                .FirstOrDefaultAsync(i => i.CmndCccd == technicianRequest.IdentityCard);
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
                IsDeleted = false
            };
            if (existingInfo != null)
            {
                newAccount.InfoId = existingInfo.InfoId;
            }
            else
            {
                newAccount.Info = new InFo
                {
                    FullName = technicianRequest.FullName,
                    PhoneNumber = technicianRequest.PhoneNumber,
                    BirthDay = technicianRequest.BirthDay,
                    Sex = technicianRequest.Sex,
                    CmndCccd = technicianRequest.IdentityCard,
                    Country = technicianRequest.Country,
                    City = technicianRequest.City,
                    Address = technicianRequest.Address,
                    CreatedAt = currentTime
                };
            }

            _context.Accounts.Add(newAccount);
            await _context.SaveChangesAsync();
            return MapToResponseDto(newAccount, existingInfo);
        }

        public async Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician()
        {
            return await _context.Accounts
                .Where(a => a.RoleId == 3 && a.IsDeleted == false)
                .Select(a => new TechnicianResponseDto
                {
                    AccountId = a.AccountId,
					Code = a.Code,
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
                    IsDeleted = a.IsDeleted,
                    Sex = a.Info != null ? a.Info.Sex : null,
                    BirthDay = a.Info != null ? a.Info.BirthDay : null
                })
                .ToListAsync();
        }

        public async Task<TechnicianResponseDto> UpdateTechnician(int technicianId, UpdateTechnicianRequestDto technicianRequest, int managerId)
        {
            var technician = await GetTechnicianById(technicianId);
            if (technician == null || technician.IsDeleted == true) throw new Exception("Kỹ thuật viên không tồn tại trong hệ thống.");
            var emailExist = await _context.Accounts.AnyAsync(a =>
                a.Email.ToLower() == technicianRequest.Email.ToLower() && a.AccountId != technicianId);
            if (emailExist) throw new Exception("Email này đã được sử dụng cho một tài khoản khác.");
            var identityCardExist = await _context.InFos.AnyAsync(i =>
                i.CmndCccd == technicianRequest.IdentityCard && i.InfoId != technician.InfoId);
            if (identityCardExist) throw new Exception("Số CCCD này đã tồn tại ở một hồ sơ khác.");
            DateTime currentTime = DateTime.Now;
            technician.Email = technicianRequest.Email;
            technician.UpdatedAt = currentTime;
            technician.UpdatedBy = managerId;
            if (technician.Info != null)
            {
                technician.Info.FullName = technicianRequest.FullName;
                technician.Info.PhoneNumber = technicianRequest.PhoneNumber;
                technician.Info.BirthDay = technicianRequest.BirthDay;
                technician.Info.Sex = technicianRequest.Sex;
                technician.Info.CmndCccd = technicianRequest.IdentityCard;
                technician.Info.Country = technicianRequest.Country;
                technician.Info.City = technicianRequest.City;
                technician.Info.Address = technicianRequest.Address;
                technician.Info.UpdatedAt = currentTime;
            }
            _context.Accounts.Update(technician);
            await _context.SaveChangesAsync();
            return MapToResponseDto(technician);
        }

        public async Task<string> ToggleTechnicianStatus(int technicianId)
        {
            var technician = await GetTechnicianById(technicianId);
            if (technician == null || technician.IsDeleted == true) throw new Exception("Không thể thay đổi trạng thái tài khoản đã bị xóa.");
            string message = "";
            if (technician.Status == GeneralStatus.Active)
            {
                var hasProcessingTask = await _context.MaintenanceRequests
                    .AnyAsync(m => m.AssignedTo == technicianId && m.Status == MaintenanceRequestStatus.Processing);
                if (hasProcessingTask) throw new Exception("Không thể vô hiệu hóa kỹ thuật viên đang xử lý nhiệm vụ.");
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

        public async Task<bool> DeleteTechnician(int technicianId, int managerId)
        {
            var technician = await GetTechnicianById(technicianId);
            if (technician == null || technician.IsDeleted == true)
                throw new Exception("Kỹ thuật viên không tồn tại hoặc đã bị xóa trước đó.");
            var hasProcessingTask = await _context.MaintenanceRequests
                .AnyAsync(m => m.AssignedTo == technicianId && m.Status == MaintenanceRequestStatus.Processing);
            if (hasProcessingTask)
                throw new Exception("Không thể xóa kỹ thuật viên đang trong quá trình xử lý nhiệm vụ.");
            technician.IsDeleted = true;
            technician.Status = GeneralStatus.Inactive;
            technician.UpdatedAt = DateTime.Now;
            technician.UpdatedBy = managerId;
            _context.Accounts.Update(technician);
            return await _context.SaveChangesAsync() > 0;
        }

		public async Task<string> ToggleTechAvailability(int technicianId)
		{
			var technician = await GetTechnicianById(technicianId);
			if (technician == null) throw new Exception("Kỹ thuật viên không tồn tại.");

			// Sử dụng trực tiếp Enum TechAvailability và ép kiểu về byte
			if (technician.TechAvailability == (byte)TechAvailability.Free)
			{
				technician.TechAvailability = (byte)TechAvailability.Busy; // Chuyển sang 0
			}
			else
			{
				technician.TechAvailability = (byte)TechAvailability.Free; // Chuyển về 1
			}

			_context.Accounts.Update(technician);
			await _context.SaveChangesAsync();

			return technician.TechAvailability == (byte)TechAvailability.Free
				? "Đã chuyển tình trạng: Rảnh rỗi"
				: "Đã chuyển tình trạng: Đang bận";
		}

        public async Task<IEnumerable<TechnicianResponseDto>> GetDeletedTechnicians()
        {
            return await _context.Accounts
                .Where(a => a.RoleId == 3 && a.IsDeleted == true)
                .Select(a => new TechnicianResponseDto
                {
                    AccountId = a.AccountId,
                    Code = a.Code,
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
        }

        public async Task<bool> RestoreTechnician(int technicianId, int managerId)
        {
            var technician = await GetTechnicianById(technicianId);
            if (technician == null)
                throw new Exception("Kỹ thuật viên không tồn tại hoặc đã bị xóa trước đó.");
            if (technician.IsDeleted == false)
                throw new Exception("Kỹ thuật viên này đang hoạt động, không thể khôi phục.");
            var emailExist = await _context.Accounts.AnyAsync(a =>
            a.Email.ToLower() == technician.Email.ToLower() && a.AccountId != technicianId && a.IsDeleted == false);
            if (emailExist) throw new Exception("Email của tài khoản này đã được sử dụng bởi một tài khoản đang hoạt động khác.");
            var userNameExist = await _context.Accounts.AnyAsync(a =>
            a.UserName.ToLower() == technician.UserName.ToLower()
            && a.AccountId != technicianId
            && a.IsDeleted == false);
            if (userNameExist)
            {
                throw new Exception("Tên đăng nhập này đã được sử dụng cho một tài khoản khác đang hoạt động.");
            }
            technician.IsDeleted = false;
            technician.Status = GeneralStatus.Active;
            technician.UpdatedAt = DateTime.Now;
            technician.UpdatedBy = managerId;
            _context.Accounts.Update(technician);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> HardDeleteTechnician(int technicianId)
        {
            var technician = await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == technicianId && a.RoleId == 3 && a.IsDeleted == true);
            if (technician == null)
            {
                throw new Exception("Không tìm thấy kỹ thuật viên này trong danh sách đã xóa.");
            }
            var hasRelatedTasks = await _context.MaintenanceRequests
            .AnyAsync(m => m.AssignedTo == technicianId);
            if (hasRelatedTasks)
            {
                throw new Exception("Không thể xóa vĩnh viễn! Kỹ thuật viên này đã có lịch sử công việc trong hệ thống.");
            }
            _context.Accounts.Remove(technician);
            return await _context.SaveChangesAsync() > 0;
        }


    }
}