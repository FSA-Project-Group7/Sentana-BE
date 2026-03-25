using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Contracts;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services.SStorage;
using System.Linq;

namespace Sentana.API.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IMinioService _minioService;
        private readonly SentanaContext _context;

        public ContractService(
            IContractRepository contractRepository,
            IMinioService minioService,
            SentanaContext context)
        {
            _contractRepository = contractRepository;
            _minioService = minioService;
            _context = context;
        }

        public async Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request, int accountId)
        {
            // 1. Basic validation
            if (request == null)
                return ApiResponse<object>.Fail(400, "Request không hợp lệ");

            if (request.StartDay >= request.EndDay)
                return ApiResponse<object>.Fail(400, "Ngày bắt đầu phải trước ngày kết thúc");

            if (request.File == null)
                return ApiResponse<object>.Fail(400, "File là bắt buộc");

            if (!request.File.ContentType.Contains("pdf"))
                return ApiResponse<object>.Fail(400, "Chỉ cho phép file PDF");

            if (request.File.Length > 5 * 1024 * 1024)
                return ApiResponse<object>.Fail(400, "File quá lớn (max 5MB)");

            // 2. Validate Manager (Người tạo hợp đồng)
            var manager = await _contractRepository.GetAccountAsync(accountId);
            if (manager == null || manager.Role?.RoleName != "Manager")
                return ApiResponse<object>.Fail(403, "Chỉ Manager được quyền tạo hợp đồng");

            // 3. Validate Account (Người thuê / Cư dân)
            var residentAccount = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(a => a.AccountId == request.ResidentAccountId && a.IsDeleted == false);

            if (residentAccount == null || residentAccount.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Tài khoản cư dân không tồn tại hoặc đang bị khóa");

            if (residentAccount.Role?.RoleName != "Resident")
                return ApiResponse<object>.Fail(400, "Tài khoản được gán không phải là Cư dân (Resident)");

            // 4. Validate Relationship "Chủ hộ" (Option 1A)
            var chuHoRel = await _context.Relationships
                .FirstOrDefaultAsync(r => r.IsDeleted == false &&
                    (r.RelationshipName.ToLower().Contains("chủ hộ") || r.RelationshipName.ToLower().Contains("chủ hợp đồng")));

            if (chuHoRel == null)
                return ApiResponse<object>.Fail(500, "Hệ thống chưa cấu hình mối quan hệ 'Chủ hộ' trong DB. Vui lòng liên hệ Admin.");

            // 5. Validate Apartment & Booking in Advance (Option 2B)
            var apartment = await _contractRepository.GetApartmentAsync(request.ApartmentId);
            if (apartment == null)
                return ApiResponse<object>.Fail(400, "Căn hộ không tồn tại");

            if (apartment.Status == ApartmentStatus.Maintenance)
                return ApiResponse<object>.Fail(400, "Căn hộ đang bảo trì, không thể tạo hợp đồng mới");

            // Thay vì chặn Status != Vacant, ta check Overlap thời gian với các hợp đồng đang Active khác
            var overlap = await _context.Contracts.AnyAsync(c =>
                c.ApartmentId == request.ApartmentId &&
                c.Status == GeneralStatus.Active &&
                c.IsDeleted == false &&
                (
                    request.StartDay < c.EndDay &&
                    request.EndDay > c.StartDay
                ));

            if (overlap)
                return ApiResponse<object>.Fail(400, "Thời gian hợp đồng bị trùng lặp với một khách thuê khác. Vui lòng chọn ngày StartDay sau ngày EndDay của khách cũ.");

            // Option 3A: Không chặn ResidentAccountId thuê nhiều phòng -> Bỏ qua validate "hasActive" cho AccountId

            // TRANSACTION BẮT ĐẦU
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Upload File Contract
                var fileUrl = await _minioService.UploadContractAsync(request.File, 0, 1);

                // Tạo Contract mới
                var contract = new Contract
                {
                    ContractCode = "CT-" + DateTime.Now.Ticks,
                    ApartmentId = request.ApartmentId,
                    AccountId = request.ResidentAccountId,
                    StartDay = request.StartDay,
                    EndDay = request.EndDay,
                    MonthlyRent = request.MonthlyRent,
                    Deposit = request.Deposit,
                    Status = GeneralStatus.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = accountId,
                    File = fileUrl,
                    IsDeleted = false
                };

                await _context.Contracts.AddAsync(contract);
                await _context.SaveChangesAsync();

                // Tạo Contract Version
                var version = new ContractVersion
                {
                    ContractId = contract.ContractId,
                    VersionNumber = 1,
                    File = fileUrl,
                    CreatedAt = DateTime.Now,
                    CreatedBy = accountId
                };

                await _context.ContractVersions.AddAsync(version);
                await _context.SaveChangesAsync();

                contract.CurrentVersionId = version.VersionId;

                // 6. Gán Resident vào Căn hộ cùng với Relationship (Chủ hộ)
                var apartmentResident = new ApartmentResident
                {
                    ApartmentId = request.ApartmentId,
                    AccountId = request.ResidentAccountId,
                    RelationshipId = chuHoRel.RelationshipId, 
                    Status = GeneralStatus.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = accountId,
                    IsDeleted = false
                };

                await _context.ApartmentResidents.AddAsync(apartmentResident);

                // 7. Đổi trạng thái phòng sang Đang Thuê (Occupied)
                apartment.Status = ApartmentStatus.Occupied;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.Success(contract, "Tạo hợp đồng thành công! Cư dân đã được gán làm Chủ hộ.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, "Lỗi server: " + ex.Message);
            }
        }

        public async Task<ApiResponse<object>> UpdateContractAsync(int contractId, UpdateContractDto request)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Chỉ update contract active");

            var newStart = request.StartDay ?? contract.StartDay;
            var newEnd = request.EndDay ?? contract.EndDay;

            if (newStart >= newEnd)
                return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");

            var overlap = await _context.Contracts.AnyAsync(c =>
                c.ContractId != contractId &&
                c.ApartmentId == contract.ApartmentId &&
                c.IsDeleted == false &&
                (
                    newStart < c.EndDay &&
                    newEnd > c.StartDay
                ));

            if (overlap)
                return ApiResponse<object>.Fail(400, "Contract bị overlap");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                contract.StartDay = newStart;
                contract.EndDay = newEnd;
                contract.MonthlyRent = request.MonthlyRent ?? contract.MonthlyRent;
                contract.Deposit = request.Deposit ?? contract.Deposit;
                contract.UpdatedAt = DateTime.Now;

                if (request.File != null)
                {
                    if (!request.File.ContentType.Contains("pdf"))
                        return ApiResponse<object>.Fail(400, "Chỉ PDF");

                    var fileUrl = await _minioService.UploadContractAsync(request.File, contractId, 2);

                    var lastVersion = await _contractRepository.GetLatestContractVersionAsync(contractId);
                    var newVersion = (lastVersion?.VersionNumber ?? 0) + 1;

                    var version = new ContractVersion
                    {
                        ContractId = contractId,
                        VersionNumber = newVersion,
                        File = fileUrl,
                        CreatedAt = DateTime.Now
                    };

                    await _context.ContractVersions.AddAsync(version);
                    await _context.SaveChangesAsync();

                    contract.File = fileUrl;
                    contract.CurrentVersionId = version.VersionId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.Success(null, "Update thành công");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, ex.Message);
            }
        }
        public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request)
        {
            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Contract không active");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                contract.Status = GeneralStatus.Inactive;
                contract.UpdatedAt = DateTime.Now;

                contract.EndDay = request.TerminationDate;

                if (contract.Apartment != null)
                    contract.Apartment.Status = ApartmentStatus.Vacant;

                if (contract.ApartmentId == null)
                    return ApiResponse<object>.Fail(400, "Apartment không hợp lệ");
                
                    int apartmentId = contract.ApartmentId.Value;

                var residents = await _context.ApartmentResidents
                    .Where(x => x.ApartmentId == apartmentId && x.IsDeleted == false)
                    .ToListAsync();

                foreach (var r in residents)
                {
                    r.IsDeleted = true;
                }

                var services = await _context.ApartmentServices
                    .Where(x => x.ApartmentId == apartmentId && x.IsDeleted == false)
                    .ToListAsync();

                foreach (var s in services)
                {
                    s.EndDay = request.TerminationDate;
                    s.IsDeleted = true;
                }

                var invoices = await _context.Invoices
                    .Where(x => x.ContractId == contractId)
                    .ToListAsync();

                var invoiceIds = invoices.Select(x => x.InvoiceId).ToList();

                var payments = await _context.PaymentTransactions
                    .Where(x => x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value))
                    .ToListAsync();

                decimal totalInvoice = invoices.Sum(x => x.TotalMoney ?? 0);
                decimal totalPaid = payments.Sum(x => x.AmountPaid ?? 0);

                decimal additionalCost = request.AdditionalCost;

                decimal refund = totalPaid - totalInvoice - additionalCost;

                bool isFullyPaid = totalPaid >= (totalInvoice + additionalCost);

                string paymentStatus = isFullyPaid
                        ? "Đã thanh toán đủ"
                        : "Chưa thanh toán đủ";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

               return ApiResponse<object>.Success(new
                    {
                        TotalInvoice = totalInvoice,
                        TotalPaid = totalPaid,
                        AdditionalCost = additionalCost,
                        RefundAmount = refund,
                        IsFullyPaid = isFullyPaid,
                        PaymentStatus = paymentStatus
                    }, "Terminate thành công");
                                }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, ex.Message);
            }
        }

        public async Task<ApiResponse<object>> ExtendContractAsync(int contractId, ExtendContractDto request)
        {
            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Contract không active");

            if (request.NewEndDate <= contract.EndDay)
                return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");

            var overlap = await _context.Contracts.AnyAsync(c =>
                c.ContractId != contractId &&
                c.ApartmentId == contract.ApartmentId &&
                c.IsDeleted == false &&
                request.NewEndDate > c.StartDay
            );

            if (overlap)
                return ApiResponse<object>.Fail(400, "Gia hạn bị overlap");

            contract.EndDay = request.NewEndDate;
            contract.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return ApiResponse<object>.Success(null, "Extend thành công");
        }

        public async Task<ApiResponse<object>> GetContractDetailAsync(int contractId)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy");

            return ApiResponse<object>.Success(contract, "OK");
        }

        public async Task<ApiResponse<object>> GetContractListAsync()
        {
            var list = await _contractRepository.GetContractListAsync();
            return ApiResponse<object>.Success(list, "OK");
        }

        public async Task<ApiResponse<object>> GetMyContractAsync(int accountId)
        {
            var contract = await _contractRepository.GetContractByAccountIdAsync(accountId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không có contract");

            return ApiResponse<object>.Success(contract, "OK");
        }
    }
}
