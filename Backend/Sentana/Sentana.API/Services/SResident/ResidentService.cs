using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Sentana.API.DTOs.Resident;
using Sentana.API.DTOs.Technician;
using Sentana.API.Enums;
using Sentana.API.Models;
using Sentana.API.Services.SResident;

public class ResidentService : IResidentService
{
    private readonly SentanaContext _context;

    public ResidentService(SentanaContext context)
    {
        _context = context;
    }

    private async Task<string> GenerateResidentCode()
    {
        var lastRes = await _context.Accounts
            .Where(a => a.RoleId == 2 && a.Code != null && a.Code.StartsWith("RES-"))
            .OrderByDescending(a => a.AccountId)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastRes != null && lastRes.Code.Length > 4)
        {
            if (int.TryParse(lastRes.Code.Substring(4), out int lastNumber))
                nextNumber = lastNumber + 1;
        }
        return $"RES-{nextNumber:D3}";
    }

    private ResidentResponseDto MapToResponse(Account account, InFo? existingInfo = null)
    {
        var info = account.Info ?? existingInfo;
        return new ResidentResponseDto
        {
            AccountId = account.AccountId,
            Code = account.Code,
            UserName = account.UserName,
            Email = account.Email,
            FullName = info?.FullName,
            PhoneNumber = info?.PhoneNumber,
            IdentityCard = info?.CmndCccd,
            Country = info?.Country,
            City = info?.City,
            Address = info?.Address,
            Status = account.Status,
            IsDeleted = account.IsDeleted,
            Sex = info?.Sex,
            BirthDay = info?.BirthDay
        };
    }
    private async Task<bool> CheckEmailExist(string email)
    {
        return await _context.Accounts.AnyAsync(a => a.Email.ToLower() == email.ToLower());
    }
    private async Task<bool> CheckUserNameExist(string username)
    {
        return await _context.Accounts.AnyAsync(a => a.UserName.ToLower() == username.ToLower());
    }

    private async Task<Account?> GetResidentById(int accountId)
    {
        return await _context.Accounts
            .Include(a => a.Info)
            .FirstOrDefaultAsync(a => a.AccountId == accountId && a.RoleId == 2);
    }

    private async Task<bool> CheckDuplicateRoleByIdentityCard(string identityCard, int roleId)
    {
        return await _context.Accounts.AnyAsync(a =>
            a.RoleId == roleId &&
            a.Info != null &&
            a.Info.CmndCccd == identityCard);
    }

    public async Task<ResidentResponseDto> CreateResident(CreateResidentRequestDto request, int managerId)
    {
        if (await CheckEmailExist(request.Email)) throw new Exception("Email này đã tồn tại trong hệ thống.");
        if (await CheckUserNameExist(request.UserName)) throw new Exception("Tên đăng nhập này đã tồn tại.");
        if (await CheckDuplicateRoleByIdentityCard(request.IdentityCard, 2))
        {
            throw new Exception("Người sở hữu CCCD này đã có tài khoản Cư dân hoặc có thể đã bị xóa. Vui lòng khôi phục thay vì tạo mới.");
        }
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
        string generatedCode = await GenerateResidentCode();
        DateTime currentTime = DateTime.Now;
        var existingInfo = await _context.InFos.FirstOrDefaultAsync(i => i.CmndCccd == request.IdentityCard);
        var newAccount = new Account
        {
            Code = generatedCode,
            Email = request.Email,
            UserName = request.UserName,
            Password = hashedPassword,
            RoleId = 2,
            Status = GeneralStatus.Active,
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
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                BirthDay = request.BirthDay,
                Sex = request.Sex,
                CmndCccd = request.IdentityCard,
                Country = request.Country,
                City = request.City,
                Address = request.Address,
                CreatedAt = currentTime
            };
        }
        _context.Accounts.Add(newAccount);
        await _context.SaveChangesAsync();
        return MapToResponse(newAccount, existingInfo);
    }

	public async Task<IEnumerable<ResidentResponseDto>> GetAllResidents()
	{
		return await _context.Accounts
			.Where(a => a.RoleId == 2 && a.IsDeleted == false)
			.Select(a => new ResidentResponseDto
			{
				AccountId = a.AccountId,
				Code = a.Code,
				UserName = a.UserName,
				FullName = a.Info != null ? a.Info.FullName : null,
				Email = a.Email,
				PhoneNumber = a.Info != null ? a.Info.PhoneNumber : null,
				IdentityCard = a.Info != null ? a.Info.CmndCccd : null,
				Status = a.Status,
				Country = a.Info != null ? a.Info.Country : null,
				City = a.Info != null ? a.Info.City : null,
				Address = a.Info != null ? a.Info.Address : null,
				IsDeleted = a.IsDeleted,
				Sex = a.Info != null ? a.Info.Sex : null,
				BirthDay = a.Info != null ? a.Info.BirthDay : null,

				ApartmentId = a.ApartmentResidents
					.Where(ar => ar.Status == GeneralStatus.Active && ar.IsDeleted == false)
					.Select(ar => ar.ApartmentId)
					.FirstOrDefault(),

				ApartmentCode = a.ApartmentResidents
					.Where(ar => ar.Status == GeneralStatus.Active && ar.IsDeleted == false)
					.Select(ar => ar.Apartment.ApartmentCode)
					.FirstOrDefault()
			})
			.ToListAsync();
	}

	public async Task<ImportResidentsResultDto> ImportResidentsFromExcelAsync(Stream fileStream, int managerId)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage(fileStream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            return new ImportResidentsResultDto
            {
                Errors = new List<string> { "File Excel không chứa worksheet nào." }
            };
        }

        var result = new ImportResidentsResultDto();
        int startRow = 2; // row 1: header

        for (int row = startRow; row <= worksheet.Dimension.End.Row; row++)
        {
            result.TotalRows++;

            try
            {
                var email = worksheet.Cells[row, 1].Text?.Trim();
                var userName = worksheet.Cells[row, 2].Text?.Trim();
                var fullName = worksheet.Cells[row, 3].Text?.Trim();
                var phoneNumber = worksheet.Cells[row, 4].Text?.Trim();
                var identityCard = worksheet.Cells[row, 5].Text?.Trim();
                var country = worksheet.Cells[row, 6].Text?.Trim();
                var city = worksheet.Cells[row, 7].Text?.Trim();
                var address = worksheet.Cells[row, 8].Text?.Trim();
                var apartmentCode = worksheet.Cells[row, 9].Text?.Trim();

                // Required validations
                if (string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(userName) ||
                    string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(identityCard))
                {
                    result.FailedCount++;
                    result.Errors.Add($"Dòng {row}: Thiếu trường bắt buộc (Email, UserName, FullName, IdentityCard).");
                    continue;
                }

                if (await CheckEmailExist(email))
                {
                    result.FailedCount++;
                    result.Errors.Add($"Dòng {row}: Email '{email}' đã tồn tại.");
                    continue;
                }

                if (await CheckUserNameExist(userName))
                {
                    result.FailedCount++;
                    result.Errors.Add($"Dòng {row}: Tên đăng nhập '{userName}' đã tồn tại.");
                    continue;
                }

                if (await CheckDuplicateRoleByIdentityCard(identityCard, 2))
                {
                    result.FailedCount++;
                    result.Errors.Add($"Dòng {row}: CCCD '{identityCard}' đã có tài khoản cư dân.");
                    continue;
                }

                Apartment? apartment = null;
                if (!string.IsNullOrWhiteSpace(apartmentCode))
                {
                    apartment = await _context.Apartments
                        .FirstOrDefaultAsync(a => a.ApartmentCode == apartmentCode && a.IsDeleted == false);

                    if (apartment == null)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Dòng {row}: Không tìm thấy căn hộ với mã '{apartmentCode}'.");
                        continue;
                    }
                }

                var dto = new CreateResidentRequestDto
                {
                    Email = email,
                    UserName = userName,
                    Password = "Temp@123", // or generate random, sẽ reset sau
                    FullName = fullName,
                    PhoneNumber = phoneNumber,
                    IdentityCard = identityCard,
                    Country = country,
                    City = city,
                    Address = address
                };

                var resident = await CreateResident(dto, managerId);

                if (apartment != null)
                {
                    var aptResident = new ApartmentResident
                    {
                        ApartmentId = apartment.ApartmentId,
                        AccountId = resident.AccountId,
                        Status = GeneralStatus.Active,
                        CreatedAt = DateTime.Now,
                        CreatedBy = managerId,
                        IsDeleted = false
                    };
                    _context.ApartmentResidents.Add(aptResident);
                    await _context.SaveChangesAsync();
                }

                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: {ex.Message}");
            }
        }

        return result;
    }

    //duy anh
    public async Task<(bool IsSuccess, string Message)> AssignResidentToRoomAsync(AssignResidentRequestDto request, int managerId)
    {
        // 1. Kiểm tra Tài khoản Cư dân có tồn tại và đúng Role (RoleId = 2) không?
        var residentAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == request.AccountId && a.RoleId == 2 && a.IsDeleted == false);

        if (residentAccount == null)
        {
            return (false, "Không tìm thấy Cư dân này trong hệ thống.");
        }

        // 2. Kiểm tra Căn hộ có tồn tại không?
        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(a => a.ApartmentId == request.ApartmentId && a.IsDeleted == false);

        if (apartment == null)
        {
            return (false, "Không tìm thấy Căn hộ này.");
        }

        // US45 / BUG32: Chặn gán nhiều "Chủ hộ/Chủ hợp đồng" (RelationshipId = 1) cho cùng một căn hộ
        if (request.RelationshipId == 1)
        {
            var existingOwner = await _context.ApartmentResidents
                .AnyAsync(ar => ar.ApartmentId == request.ApartmentId
                             && ar.RelationshipId == 1
                             && ar.IsDeleted == false);

            if (existingOwner)
            {
                return (false, "Căn hộ này đã có chủ hộ/chủ hợp đồng. Vui lòng gỡ/chuyển chủ hộ trước khi gán mới.");
            }
        }

        // 3. Kiểm tra xem Cư dân này đã được gán vào chính Căn hộ này chưa? (Tránh duplicate)
        var isAlreadyAssigned = await _context.ApartmentResidents
            .AnyAsync(ar => ar.AccountId == request.AccountId
                         && ar.ApartmentId == request.ApartmentId
                         && ar.IsDeleted == false);

        if (isAlreadyAssigned)
        {
            return (false, "Cư dân này đã được gán vào căn hộ này từ trước.");
        }

        // 4. Thực hiện Map Cư dân vào Căn hộ (Insert mới)
        var newAssignment = new ApartmentResident
        {
            AccountId = request.AccountId,
            ApartmentId = request.ApartmentId,
            RelationshipId = request.RelationshipId, // Có thể Null tùy yêu cầu bài toán
            Status = GeneralStatus.Active,
            CreatedAt = DateTime.Now,
            CreatedBy = managerId,
            IsDeleted = false
        };

        _context.ApartmentResidents.Add(newAssignment);

        // Mở rộng: Nếu muốn tự động cập nhật trạng thái phòng thành "Đang ở" khi có người gán vào
        if (apartment.Status == ApartmentStatus.Vacant)
        {
            apartment.Status = ApartmentStatus.Occupied;
            apartment.UpdatedAt = DateTime.Now;
            apartment.UpdatedBy = managerId;
        }

        await _context.SaveChangesAsync();

        return (true, $"Đã thêm cư dân {residentAccount.UserName} vào căn hộ {apartment.ApartmentCode} thành công.");
    }

    public async Task<ResidentResponseDto> UpdateResident(int residentId, UpdateResidentRequestDto request, int managerId)
    {
        var resident = await GetResidentById(residentId);
        if (resident == null || resident.IsDeleted == true)
            throw new Exception("Cư dân không tồn tại trong hệ thống.");
        var emailExist = await _context.Accounts.AnyAsync(a =>
        a.Email.ToLower() == request.Email.ToLower() && a.AccountId != residentId);
        if (emailExist)
        {
            throw new Exception("Email này đã được sử dụng cho một tài khoản khác.");
        }
        var identityCardExist = await _context.InFos.AnyAsync(i =>
        i.CmndCccd == request.IdentityCard && i.InfoId != resident.InfoId);
        if (identityCardExist)
        {
            throw new Exception("CCCD này đã được sử dụng cho một tài khoản khác.");
        }
        DateTime currentTime = DateTime.Now;
        resident.Email = request.Email;
        resident.UpdatedAt = currentTime;
        resident.UpdatedBy = managerId;
        if (resident.Info != null)
        {
            resident.Info.FullName = request.FullName;
            resident.Info.PhoneNumber = request.PhoneNumber;
            resident.Info.BirthDay = request.BirthDay;
            resident.Info.Sex = request.Sex;
            resident.Info.CmndCccd = request.IdentityCard;
            resident.Info.Country = request.Country;
            resident.Info.City = request.City;
            resident.Info.Address = request.Address;
            resident.Info.UpdatedAt = currentTime;
        }
        _context.Accounts.Update(resident);
        await _context.SaveChangesAsync();
        return MapToResponse(resident);
    }



    // Remove resident from room (Manager only)
    public async Task<RemoveResidentResponseDto> RemoveResidentFromRoomAsync(RemoveResidentRequestDto request, int managerId)
    {
        var now = DateTime.UtcNow;
        var response = new RemoveResidentResponseDto
        {
            AccountId = request.AccountId,
            ApartmentId = request.ApartmentId,
            RemovedAt = now
        };

        // 1. Tìm bản ghi cư dân trong phòng
        var aptResident = await _context.ApartmentResidents
            .FirstOrDefaultAsync(ar => ar.AccountId == request.AccountId
                                    && ar.ApartmentId == request.ApartmentId
                                    && ar.IsDeleted == false);

        if (aptResident == null)
        {
            response.Message = "Cư dân không tồn tại trong căn hộ này.";
            return response;
        }

        // 2. Fix BUG 35: Chặn xóa nếu là Chủ hợp đồng (RelationshipId = 1) mà hợp đồng còn hiệu lực
        if (aptResident.RelationshipId == 1)
        {
            var hasActiveContract = await _context.Contracts
                .AnyAsync(c => c.ApartmentId == request.ApartmentId && c.Status == GeneralStatus.Active);

            if (hasActiveContract)
            {
                response.Message = "Không thể gỡ chủ hộ khi hợp đồng vẫn còn hiệu lực. Hãy tất toán hợp đồng trước.";
                return response;
            }
        }

        // 3. Thực hiện xóa mềm cư dân
        aptResident.IsDeleted = true;
        aptResident.Status = GeneralStatus.Inactive;

        // 4. Cập nhật trạng thái phòng (Nếu không còn ai ở thì chuyển về Vacant)
        var otherPeople = await _context.ApartmentResidents
            .AnyAsync(ar => ar.ApartmentId == request.ApartmentId && ar.AccountId != request.AccountId && ar.IsDeleted == false);

        if (!otherPeople)
        {
            var apartment = await _context.Apartments.FindAsync(request.ApartmentId);
            if (apartment != null) apartment.Status = ApartmentStatus.Vacant;
            response.ApartmentStatus = "Vacant";
        }

        // 5. Lưu lý do gỡ (Reason từ Request) vào bảng History/Info
        //_context.Histories.Add(new History
        //{
        //    AccountId = request.AccountId,
        //    Action = "Remove",
        //    Description = request.Reason ?? "Gỡ cư dân khỏi phòng",
        //    CreatedAt = now,
        //    CreatedBy = managerId
        //});

        await _context.SaveChangesAsync();

        response.IsSuccess = true;
        response.Message = "Gỡ cư dân thành công.";
        return response;
    }
    public async Task<string> ToggleResidentStatus(int residentId)
	{
		var account = await GetResidentById(residentId);
		if (account == null || account.IsDeleted == true)
		{
			throw new Exception("Không thể thay đổi trạng thái tài khoản không tồn tại hoặc đã bị xóa.");
		}

		string message = "";
		if (account.Status == GeneralStatus.Active)
		{
			var isAssignedToRoom = await _context.ApartmentResidents
				.AnyAsync(ar => ar.AccountId == residentId
							 && ar.Status == GeneralStatus.Active
							 && ar.IsDeleted == false);

			if (isAssignedToRoom)
			{
				throw new Exception("Không thể khóa tài khoản vì cư dân này đang được gán vào một căn hộ. Vui lòng gỡ cư dân khỏi phòng trước!");
			}

			account.Status = GeneralStatus.Inactive;
			message = "Khóa tài khoản Cư dân thành công!";
		}
		else
		{
			account.Status = GeneralStatus.Active;
			message = "Mở khóa tài khoản Cư dân thành công!";
		}

		_context.Accounts.Update(account);
		await _context.SaveChangesAsync();
		return message;
	}

    public async Task<bool> DeleteResident(int residentId, int managerId)
    {
        var account = await GetResidentById(residentId);
        if (account == null || account.IsDeleted == true)
            throw new Exception("Cư dân không tồn tại trong hệ thống.");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var hasActiveContract = await _context.ApartmentResidents
            .Include(ar => ar.Apartment)
            .ThenInclude(a => a.Contracts)
            .AnyAsync(ar => ar.AccountId == residentId
                         && ar.Apartment.Contracts.Any(c => c.Status == GeneralStatus.Active && c.EndDay >= today));
        if (hasActiveContract)
        {
            throw new Exception("Không thể xóa vì cư dân đang ở trong phòng còn hạn thuê.");
        }
        var isAssignedToRoom = await _context.ApartmentResidents
        .AnyAsync(ar => ar.AccountId == residentId
                     && ar.Status == GeneralStatus.Active
                     && ar.IsDeleted == false);

        if (isAssignedToRoom)
        {
            throw new Exception("Không thể xóa tài khoản vì cư dân vẫn đang ở trong một căn hộ. Vui lòng gỡ cư dân khỏi phòng trước.");
        }
        account.IsDeleted = true;
        account.Status = GeneralStatus.Inactive;
        account.UpdatedAt = DateTime.Now;
        account.UpdatedBy = managerId;
        _context.Accounts.Update(account);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<ResidentResponseDto>> GetDeletedResidents()
    {
        return await _context.Accounts
               .Where(a => a.RoleId == 2 && a.IsDeleted == true)
               .Select(a => new ResidentResponseDto
               {
                   AccountId = a.AccountId,
                   Code = a.Code,
                   UserName = a.UserName,
                   FullName = a.Info != null ? a.Info.FullName : null,
                   Email = a.Email,
                   PhoneNumber = a.Info != null ? a.Info.PhoneNumber : null,
                   IdentityCard = a.Info != null ? a.Info.CmndCccd : null,
                   Status = a.Status,
                   Country = a.Info != null ? a.Info.Country : null,
                   City = a.Info != null ? a.Info.City : null,
                   Address = a.Info != null ? a.Info.Address : null,
                   IsDeleted = a.IsDeleted
               })
               .ToListAsync();
    }


    public async Task<bool> RestoreResident(int residentId, int managerId)
    {
        var resident = await GetResidentById(residentId);
        if(resident == null)
        {
            throw new Exception("Cư dân không tồn tại trong hệ thống.");
        }
        if (resident.IsDeleted == false)
        {
            throw new Exception("Cư dân này đang hoạt động không thể khôi phục.");
        }
        var emailExist = await _context.Accounts.AnyAsync(a =>
        a.Email.ToLower() == resident.Email.ToLower()
        && a.AccountId != residentId
        && a.IsDeleted == false);
        if (emailExist)
        {
            throw new Exception("Email này đã được sử dụng cho một tài khoản khác.");
        }
        var userNameExist = await _context.Accounts.AnyAsync(a =>
        a.UserName.ToLower() == resident.UserName.ToLower()
        && a.AccountId != residentId
        && a.IsDeleted == false);

        if (userNameExist)
        {
            throw new Exception("Tên đăng nhập này đã được sử dụng cho một tài khoản khác đang hoạt động.");
        }
        resident.IsDeleted = false;
        resident.Status = GeneralStatus.Active;
        resident.UpdatedAt = DateTime.Now;
        resident.UpdatedBy = managerId;
        _context.Accounts.Update(resident);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> HardDeleteResident(int residentId)
    {
        var resident = await _context.Accounts
         .FirstOrDefaultAsync(a => a.AccountId == residentId && a.RoleId == 2 && a.IsDeleted == true);
        if (resident == null)
        {
            throw new Exception("Không tìm thấy cư dân này trong danh sách đã xóa.");
        }
        var hasApartmentHistory = await _context.ApartmentResidents
            .AnyAsync(ar => ar.AccountId == residentId);
        var hasContractHistory = await _context.Contracts
            .AnyAsync(c => c.AccountId == residentId);
        if (hasApartmentHistory || hasContractHistory)
        {
            throw new Exception("Không thể xóa vĩnh viễn! Cư dân này đã có lịch sử thuê phòng hoặc hợp đồng trong hệ thống.");
        }
        _context.Accounts.Remove(resident);
        return await _context.SaveChangesAsync() > 0;
    }
   
}