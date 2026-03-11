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
            IsDeleted = account.IsDeleted
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
            throw new Exception("Người sở hữu CCCD này đã có tài khoản Cư dân có thể đã bị xóa. Vui lòng khôi phục thay vì tạo mới.");
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
            .Where(a => a.RoleId == 2)
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

    public async Task<bool> AssignResident(AssignResidentRequestDto request)
    {
        var resident = await _context.ApartmentResidents
            .FirstOrDefaultAsync(r => r.ResidentId == request.ResidentId && r.IsDeleted == false);

        if (resident == null)
            return false;

        resident.ApartmentId = request.ApartmentId;
        resident.Status = GeneralStatus.Active;

        await _context.SaveChangesAsync();

        return true;
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
}