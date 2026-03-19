using Sentana.API.DTOs.Contracts;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services.SStorage;

namespace Sentana.API.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IMinioService _minioService;

        public ContractService(
            IContractRepository contractRepository,
            IMinioService minioService)
        {
            _contractRepository = contractRepository;
            _minioService = minioService;
        }

        // ================= CREATE CONTRACT =================
        public async Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request, int accountId)
        {
            if (request == null)
                return ApiResponse<object>.Fail(400, "Request body không hợp lệ.");

            if (request.StartDay >= request.EndDay)
                return ApiResponse<object>.Fail(400, "Ngày kết thúc phải lớn hơn ngày bắt đầu.");

            if (request.File == null || request.File.Length == 0)
                return ApiResponse<object>.Fail(400, "File hợp đồng là bắt buộc.");

            var apartment = await _contractRepository.GetApartmentAsync(request.ApartmentId);

            if (apartment == null)
                return ApiResponse<object>.Fail(404, "Apartment không tồn tại.");

            if (apartment.Status != ApartmentStatus.Vacant)
                return ApiResponse<object>.Fail(400, "Phòng không trống.");

            var managerAccount = await _contractRepository.GetAccountAsync(accountId);

            if (managerAccount == null)
                return ApiResponse<object>.Fail(404, "Account không tồn tại.");

            var resident = await _contractRepository.GetAccountAsync(request.ResidentAccountId);

            if (resident == null)
                return ApiResponse<object>.Fail(404, "Resident không tồn tại.");

            if (resident.Role?.RoleName != "Resident")
                return ApiResponse<object>.Fail(400, "Account này không phải Resident.");

            var hasActive = await _contractRepository.HasActiveContractAsync(request.ApartmentId);

            if (hasActive)
                return ApiResponse<object>.Fail(400, "Phòng đã có hợp đồng đang hoạt động.");

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
                CreatedAt = DateTime.Now
            };

            await _contractRepository.AddContractAsync(contract);
            await _contractRepository.SaveAsync();

            decimal versionNumber = 1.0m;

            var fileUrl = await _minioService.UploadContractAsync(
                request.File,
                contract.ContractId,
                versionNumber
            );

            var version = new ContractVersion
            {
                ContractId = contract.ContractId,
                VersionNumber = versionNumber,
                File = fileUrl,
                CreatedAt = DateTime.Now,
                CreatedBy = accountId
            };

            await _contractRepository.AddContractVersionAsync(version);
            await _contractRepository.SaveAsync();

            contract.File = fileUrl;
            contract.CurrentVersionId = version.VersionId;

            apartment.Status = ApartmentStatus.Occupied;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(new
            {
                contractId = contract.ContractId,
                contractCode = contract.ContractCode
            }, "Tạo hợp đồng thành công.");
        }

        // ================= TERMINATE =================
        public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request)
        {
            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            contract.Status = GeneralStatus.Inactive;

            if (contract.Apartment != null)
                contract.Apartment.Status = ApartmentStatus.Vacant;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(null, "Chấm dứt hợp đồng thành công.");
        }

        // ================= EXTEND =================
        public async Task<ApiResponse<object>> ExtendContractAsync(int contractId, ExtendContractDto request)
        {
            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            contract.EndDay = request.NewEndDate;
            contract.UpdatedAt = DateTime.Now;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(null, "Gia hạn hợp đồng thành công.");
        }

        // ================= UPDATE =================
        public async Task<ApiResponse<object>> UpdateContractAsync(int contractId, UpdateContractDto request)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            if (request.StartDay.HasValue)
                contract.StartDay = request.StartDay;

            if (request.EndDay.HasValue)
                contract.EndDay = request.EndDay;

            if (request.MonthlyRent.HasValue)
                contract.MonthlyRent = request.MonthlyRent;

            if (request.Deposit.HasValue)
                contract.Deposit = request.Deposit;

            contract.UpdatedAt = DateTime.Now;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(null, "Cập nhật hợp đồng thành công.");
        }

        // ================= DETAIL =================
        public async Task<ApiResponse<object>> GetContractDetailAsync(int contractId)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            return ApiResponse<object>.Success(contract, "Lấy chi tiết hợp đồng thành công.");
        }

        // ================= LIST =================
        public async Task<ApiResponse<object>> GetContractListAsync()
        {
            var contracts = await _contractRepository.GetContractListAsync();

            return ApiResponse<object>.Success(contracts, "Lấy danh sách hợp đồng thành công.");
        }

        // FIX BUG34
        public async Task<ApiResponse<object>> GetMyContractAsync(int accountId)
        {
            var contract = await _contractRepository.GetContractByAccountIdAsync(accountId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Resident chưa có hợp đồng.");

            return ApiResponse<object>.Success(contract, "Lấy hợp đồng thành công.");
        }
    }
}