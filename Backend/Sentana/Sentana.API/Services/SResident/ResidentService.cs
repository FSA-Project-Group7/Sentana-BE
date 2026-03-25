using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Sentana.API.DTOs.Resident;
using Sentana.API.DTOs.Technician;
using Sentana.API.Enums;
using Sentana.API.Helpers;
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
        ExcelPackage.License.SetNonCommercialPersonal("Sentana");

        using var package = new ExcelPackage(fileStream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null || worksheet.Dimension == null)
        {
            return new ImportResidentsResultDto
            {
                IsRejected = true,
                Errors = new List<string> { "File Excel không chứa dữ liệu hoặc worksheet nào." }
            };
        }

        var result = new ImportResidentsResultDto();
        int startRow = 2; // row 1 là header

        // Intra-batch duplicate tracking
        var batchEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchCccds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var emailRegex = new System.Text.RegularExpressions.Regex(
            ValidationHelper.EmailRegex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var cccdRegex = new System.Text.RegularExpressions.Regex(ValidationHelper.CccdRegex);
        var phoneRegex = new System.Text.RegularExpressions.Regex(ValidationHelper.PhoneRegex);

        // Thêm Regex kiểm tra FullName (Chỉ chấp nhận chữ cái và khoảng trắng, hỗ trợ Tiếng Việt)
        var fullNameRegex = new System.Text.RegularExpressions.Regex(@"^[\p{L}\s]+$");

        var validDtos = new List<CreateResidentRequestDto>();

        // ════════════════════════════════════════════════════════════════════
        // PRE-PASS – Thu thập dữ liệu để kiểm tra DB hàng loạt (Giải quyết N+1)
        // ════════════════════════════════════════════════════════════════════
        var emailsToValidate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userNamesToValidate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cccdsToValidate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int row = startRow; row <= worksheet.Dimension.End.Row; row++)
        {
            var email = worksheet.Cells[row, 1].Text?.Trim();
            var userName = worksheet.Cells[row, 2].Text?.Trim();
            var identityCard = worksheet.Cells[row, 5].Text?.Trim();

            if (!string.IsNullOrWhiteSpace(email)) emailsToValidate.Add(email);
            if (!string.IsNullOrWhiteSpace(userName)) userNamesToValidate.Add(userName);
            if (!string.IsNullOrWhiteSpace(identityCard)) cccdsToValidate.Add(identityCard);
        }

        var existingEmailSet = new HashSet<string>(
            await _context.Accounts.Where(a => emailsToValidate.Contains(a.Email)).Select(a => a.Email).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        var existingUserNameSet = new HashSet<string>(
            await _context.Accounts.Where(a => userNamesToValidate.Contains(a.UserName)).Select(a => a.UserName).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        var existingCccdSet = new HashSet<string>(
            await _context.Accounts.Where(a => a.RoleId == 2 && a.Info != null && cccdsToValidate.Contains(a.Info.CmndCccd))
                                   .Select(a => a.Info.CmndCccd).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        // ════════════════════════════════════════════════════════════════════
        // PASS 1 – Validate dữ liệu
        // ════════════════════════════════════════════════════════════════════
        for (int row = startRow; row <= worksheet.Dimension.End.Row; row++)
        {
            var email = worksheet.Cells[row, 1].Text?.Trim();
            var userName = worksheet.Cells[row, 2].Text?.Trim();
            var fullName = worksheet.Cells[row, 3].Text?.Trim();
            var phoneNumber = worksheet.Cells[row, 4].Text?.Trim();
            var identityCard = worksheet.Cells[row, 5].Text?.Trim();
            var sexStr = worksheet.Cells[row, 7].Text?.Trim();
            var country = worksheet.Cells[row, 8].Text?.Trim();
            var city = worksheet.Cells[row, 9].Text?.Trim();
            var address = worksheet.Cells[row, 10].Text?.Trim();

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(userName) &&
                string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(identityCard))
            {
                continue;
            }

            result.TotalRows++;
            bool rowValid = true;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(identityCard))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Thiếu trường bắt buộc (Email, UserName, FullName, IdentityCard).");
                rowValid = false;
            }

            // Fix 1: Kiểm tra FullName không chứa số và ký tự đặc biệt
            if (rowValid && !fullNameRegex.IsMatch(fullName!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Họ tên '{fullName}' không hợp lệ (Không được chứa số hoặc ký tự đặc biệt).");
                rowValid = false;
            }

            if (rowValid && !emailRegex.IsMatch(email!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Email '{email}' không đúng định dạng.");
                rowValid = false;
            }

            if (rowValid && !cccdRegex.IsMatch(identityCard!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: CCCD '{identityCard}' không hợp lệ (phải có đúng 12 chữ số).");
                rowValid = false;
            }

            if (rowValid && !string.IsNullOrWhiteSpace(phoneNumber) && !phoneRegex.IsMatch(phoneNumber))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Số điện thoại '{phoneNumber}' không hợp lệ.");
                rowValid = false;
            }

            // Fix 2: Đọc trực tiếp giá trị DateTime bên dưới thay vì ép kiểu Text
            DateTime? birthDay = null;
            if (rowValid && worksheet.Cells[row, 6].Value != null)
            {
                try
                {
                    birthDay = worksheet.Cells[row, 6].GetValue<DateTime>();
                    if (birthDay >= DateTime.Today)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Dòng {row}: Ngày sinh phải là ngày trong quá khứ.");
                        rowValid = false;
                    }
                }
                catch
                {
                    result.FailedCount++;
                    result.Errors.Add($"Dòng {row}: Ngày sinh không đúng định dạng ngày tháng hợp lệ của Excel.");
                    rowValid = false;
                }
            }

            Gender? sex = null;
            if (rowValid && !string.IsNullOrWhiteSpace(sexStr))
            {
                if (!int.TryParse(sexStr, out int sexInt) || sexInt < 0 || sexInt > 2)
                {
                    result.FailedCount++;
                    result.Errors.Add($"Dòng {row}: Giới tính '{sexStr}' không hợp lệ (chỉ nhận 0=Nam, 1=Nữ, 2=Khác).");
                    rowValid = false;
                }
                else
                {
                    sex = (Gender)sexInt;
                }
            }

            if (rowValid && !batchEmails.Add(email!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Email '{email}' bị trùng lặp trong file Excel.");
                rowValid = false;
            }
            if (rowValid && !batchUserNames.Add(userName!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Tên đăng nhập '{userName}' bị trùng lặp trong file.");
                rowValid = false;
            }
            if (rowValid && !batchCccds.Add(identityCard!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: CCCD '{identityCard}' bị trùng lặp trong file.");
                rowValid = false;
            }

            if (rowValid && existingEmailSet.Contains(email!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Email '{email}' đã tồn tại trong hệ thống.");
                rowValid = false;
            }
            if (rowValid && existingUserNameSet.Contains(userName!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: Tên đăng nhập '{userName}' đã tồn tại.");
                rowValid = false;
            }
            if (rowValid && existingCccdSet.Contains(identityCard!))
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {row}: CCCD '{identityCard}' đã có tài khoản cư dân.");
                rowValid = false;
            }

            if (rowValid)
            {
                validDtos.Add(new CreateResidentRequestDto
                {
                    Email = email!,
                    UserName = userName!,
                    Password = "Temp@123",
                    FullName = fullName!,
                    PhoneNumber = phoneNumber,
                    IdentityCard = identityCard!,
                    BirthDay = birthDay,
                    Sex = sex,
                    Country = country,
                    City = city,
                    Address = address
                });
            }
        }

        if (result.FailedCount > 0)
        {
            result.IsRejected = true;
            result.SuccessCount = 0;
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // PASS 2 – LƯU HÀNG LOẠT VÀ CẬP NHẬT INFO (Giải quyết Race Condition & Data Update)
        // ════════════════════════════════════════════════════════════════════
        using var globalTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var validCccds = validDtos.Select(d => d.IdentityCard).ToList();

            // Lấy danh sách InFo hiện có để cập nhật
            var existingInfos = await _context.InFos
                .Where(i => validCccds.Contains(i.CmndCccd))
                .ToDictionaryAsync(i => i.CmndCccd, StringComparer.OrdinalIgnoreCase);

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

            DateTime currentTime = DateTime.Now;
            var newAccounts = new List<Account>();

            foreach (var dto in validDtos)
            {
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                string generatedCode = $"RES-{nextNumber:D3}";
                nextNumber++;

                var newAccount = new Account
                {
                    Code = generatedCode,
                    Email = dto.Email,
                    UserName = dto.UserName,
                    Password = hashedPassword,
                    RoleId = 2,
                    Status = GeneralStatus.Active,
                    CreatedAt = currentTime,
                    CreatedBy = managerId,
                    IsDeleted = false
                };

                if (existingInfos.TryGetValue(dto.IdentityCard, out var existingInfo))
                {
                    // Fix 3: Cập nhật thông tin mới từ Excel vào bảng InFo đang có sẵn
                    existingInfo.FullName = dto.FullName;
                    existingInfo.PhoneNumber = dto.PhoneNumber;
                    existingInfo.BirthDay = dto.BirthDay;
                    existingInfo.Sex = dto.Sex;
                    existingInfo.Country = dto.Country;
                    existingInfo.City = dto.City;
                    existingInfo.Address = dto.Address;
                    existingInfo.UpdatedAt = currentTime;

                    newAccount.InfoId = existingInfo.InfoId;
                }
                else
                {
                    newAccount.Info = new InFo
                    {
                        FullName = dto.FullName,
                        PhoneNumber = dto.PhoneNumber,
                        BirthDay = dto.BirthDay,
                        Sex = dto.Sex,
                        CmndCccd = dto.IdentityCard,
                        Country = dto.Country,
                        City = dto.City,
                        Address = dto.Address,
                        CreatedAt = currentTime
                    };
                }

                newAccounts.Add(newAccount);
                result.SuccessCount++;
            }

            _context.Accounts.AddRange(newAccounts);
            await _context.SaveChangesAsync();
            await globalTransaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await globalTransaction.RollbackAsync();
            result.SuccessCount = 0;
            result.FailedCount = result.TotalRows;
            result.IsRejected = true;
            result.Errors.Clear();
            result.Errors.Add($"Lỗi khi lưu dữ liệu: {ex.Message}. Toàn bộ file bị hủy.");
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

        // BUG45-[US45]: Chặn gán cư dân vào căn hộ khi chưa có hợp đồng active
        var hasActiveContract = await _context.Contracts
            .AnyAsync(c => c.ApartmentId == request.ApartmentId && c.Status == GeneralStatus.Active);

        if (!hasActiveContract)
        {
            return (false, "Không thể gán cư dân vào căn hộ này vì chưa có hợp đồng đang hiệu lực. Vui lòng tạo hợp đồng trước.");
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

        //// BUG43-[US46]: Bắt buộc phải có lý do khi gỡ cư dân
        //if (string.IsNullOrWhiteSpace(request.Reason))
        //{
        //    response.IsSuccess = false;
        //    response.Message = "Vui lòng nhập lý do gỡ cư dân.";
        //    return response;
        //}

        // 1. Tìm bản ghi cư dân trong phòng
        var aptResident = await _context.ApartmentResidents
            .FirstOrDefaultAsync(ar => ar.AccountId == request.AccountId
                                    && ar.ApartmentId == request.ApartmentId
                                    && ar.IsDeleted == false);

        if (aptResident == null)
        {
            response.IsSuccess = false;
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
                response.IsSuccess = false;
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

        await _context.SaveChangesAsync();

        response.IsSuccess = true;
        response.Message = "Gỡ cư dân khỏi phòng thành công.";
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

    // US06 - View Room Details
    public async Task<MyRoomResponseDto?> GetMyRoomAsync(int accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a =>
                a.AccountId == accountId &&
                a.RoleId == 2 &&
                a.IsDeleted == false &&
                a.Status == GeneralStatus.Active);

        if (account == null)
            throw new UnauthorizedAccessException("Tài khoản không hợp lệ hoặc không có quyền truy cập.");

        var myAssignment = await _context.ApartmentResidents
            .Include(ar => ar.Apartment)
            .FirstOrDefaultAsync(ar =>
                ar.AccountId == accountId &&
                ar.Status == GeneralStatus.Active &&
                ar.IsDeleted == false);

        if (myAssignment == null || myAssignment.Apartment == null)
            return null;

        var apartment = myAssignment.Apartment;

        if (apartment.IsDeleted == true)
            return null;

        var roommates = await _context.ApartmentResidents
            .Where(ar =>
                ar.ApartmentId == apartment.ApartmentId &&
                ar.AccountId != accountId &&
                ar.Status == GeneralStatus.Active &&
                ar.IsDeleted == false)
            .Include(ar => ar.Account)
                .ThenInclude(a => a.Info)
            .Include(ar => ar.Relationship)
            .Select(ar => new RoommateDto
            {
                AccountId = ar.Account!.AccountId,
                FullName = ar.Account.Info != null ? ar.Account.Info.FullName : null,
                PhoneNumber = ar.Account.Info != null ? ar.Account.Info.PhoneNumber : null,
                Email = ar.Account.Email,
                Relationship = ar.Relationship != null ? ar.Relationship.RelationshipName : null
            })
            .ToListAsync();

        return new MyRoomResponseDto
        {
            ApartmentId = apartment.ApartmentId,
            ApartmentCode = apartment.ApartmentCode ?? string.Empty,
            ApartmentName = apartment.ApartmentName,
            ApartmentNumber = apartment.ApartmentNumber,
            FloorNumber = apartment.FloorNumber,
            Area = apartment.Area,
            Status = apartment.Status.HasValue ? apartment.Status.Value.ToString() : null,
            Roommates = roommates
        };
    }
   
}