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

        public async Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request, int accountId)
        {
            if (request == null)
                return ApiResponse<object>.Fail(400, "Request không hợp lệ");

            if (request.StartDay >= request.EndDay)
                return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");

            var manager = await _contractRepository.GetAccountAsync(accountId);

            if (manager == null)
                return ApiResponse<object>.Fail(404, "Account không tồn tại");

            if (manager.Role?.RoleName != "Manager")
                return ApiResponse<object>.Fail(403, "Chỉ Manager được tạo contract");

            var resident = await _contractRepository.GetAccountAsync(request.ResidentAccountId);

            if (resident == null || resident.Role?.RoleName != "Resident")
                return ApiResponse<object>.Fail(400, "Resident không hợp lệ");

            var hasContract = await _contractRepository.GetContractByAccountIdAsync(request.ResidentAccountId);
            if (hasContract != null)
                return ApiResponse<object>.Fail(400, "Resident đã có contract");

            var apartment = await _contractRepository.GetApartmentAsync(request.ApartmentId);

            if (apartment == null || apartment.Status != ApartmentStatus.Vacant)
                return ApiResponse<object>.Fail(400, "Apartment không hợp lệ");

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
                CreatedBy = accountId
            };

            await _contractRepository.AddContractAsync(contract);
            await _contractRepository.SaveAsync();

            var fileUrl = await _minioService.UploadContractAsync(request.File, contract.ContractId, 1);

            contract.File = fileUrl;
            apartment.Status = ApartmentStatus.Occupied;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(contract, "Tạo thành công");
        }

        public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request)
        {
            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Contract không active");

            contract.Status = GeneralStatus.Inactive;

            if (contract.Apartment != null)
                contract.Apartment.Status = ApartmentStatus.Vacant;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(null, "Terminate thành công");
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

            contract.EndDay = request.NewEndDate;
            contract.UpdatedAt = DateTime.Now;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(null, "Extend thành công");
        }

        public async Task<ApiResponse<object>> UpdateContractAsync(int contractId, UpdateContractDto request)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Contract không active");

            if (request.StartDay.HasValue && request.EndDay.HasValue)
            {
                if (request.StartDay >= request.EndDay)
                    return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");
            }

            contract.StartDay = request.StartDay ?? contract.StartDay;
            contract.EndDay = request.EndDay ?? contract.EndDay;
            contract.MonthlyRent = request.MonthlyRent ?? contract.MonthlyRent;
            contract.Deposit = request.Deposit ?? contract.Deposit;
            contract.UpdatedAt = DateTime.Now;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(null, "Update thành công");
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