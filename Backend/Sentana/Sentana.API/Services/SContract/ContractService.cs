using Sentana.API.DTOs.Contracts;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;

namespace Sentana.API.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository;

        public ContractService(IContractRepository contractRepository)
        {
            _contractRepository = contractRepository;
        }

        public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request)
        {
            if (contractId <= 0)
                return ApiResponse<object>.Fail(400, "Contract ID không hợp lệ.");

            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Hợp đồng không còn hiệu lực.");

            if (contract.StartDay == null || contract.EndDay == null)
                return ApiResponse<object>.Fail(400, "Ngày hợp đồng không hợp lệ.");

            var startDate = contract.StartDay.Value;
            var endDate = contract.EndDay.Value;

            if (request.TerminationDate < startDate)
                return ApiResponse<object>.Fail(400, "Ngày kết thúc không được nhỏ hơn ngày bắt đầu hợp đồng.");

            if (request.TerminationDate > endDate)
                return ApiResponse<object>.Fail(400, "Ngày kết thúc không được lớn hơn ngày kết thúc hợp đồng.");

            decimal deposit = contract.Deposit ?? 0;

            if (request.AdditionalCost < 0)
                return ApiResponse<object>.Fail(400, "Chi phí thêm không được âm.");

            if (request.AdditionalCost > deposit)
                return ApiResponse<object>.Fail(400, "Chi phí thêm không được vượt quá tiền đặt cọc.");

            decimal refund = deposit - request.AdditionalCost;

            contract.AdditionalCost = request.AdditionalCost;
            contract.RefundAmount = refund;
            contract.Status = GeneralStatus.Inactive;
            contract.UpdatedAt = DateTime.Now;

            if (contract.Apartment != null)
                contract.Apartment.Status = ApartmentStatus.Vacant;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(new
            {
                contractId = contract.ContractId,
                refund
            }, "Hợp đồng đã được chấm dứt.");
        }

        public async Task<ApiResponse<object>> ExtendContractAsync(int contractId, ExtendContractDto request)
        {
            if (contractId <= 0)
                return ApiResponse<object>.Fail(400, "Contract ID không hợp lệ.");

            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Hợp đồng không còn hiệu lực.");

            if (contract.StartDay == null || contract.EndDay == null)
                return ApiResponse<object>.Fail(400, "Ngày hợp đồng không hợp lệ.");

            if (request.NewEndDate <= contract.EndDay)
                return ApiResponse<object>.Fail(400, "Ngày kết thúc mới phải lớn hơn ngày hiện tại.");

            contract.EndDay = request.NewEndDate;
            contract.UpdatedAt = DateTime.Now;

            await _contractRepository.SaveAsync();

            return ApiResponse<object>.Success(new
            {
                contractId = contract.ContractId,
                newEndDay = contract.EndDay
            }, "Gia hạn hợp đồng thành công.");
        }

        public async Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request)
        {
            if (request == null)
                return ApiResponse<object>.Fail(400, "Request body không hợp lệ.");

            if (request.StartDay >= request.EndDay)
                return ApiResponse<object>.Fail(400, "Ngày kết thúc phải lớn hơn ngày bắt đầu.");

            var apartment = await _contractRepository.GetApartmentAsync(request.ApartmentId);

            if (apartment == null)
                return ApiResponse<object>.Fail(404, "Apartment không tồn tại.");

            if (apartment.Status != ApartmentStatus.Vacant)
                return ApiResponse<object>.Fail(400, "Phòng không trống.");

            var account = await _contractRepository.GetAccountAsync(request.AccountId);

            if (account == null)
                return ApiResponse<object>.Fail(404, "Account không tồn tại.");

            var hasActive = await _contractRepository.HasActiveContractAsync(request.ApartmentId);

            if (hasActive)
                return ApiResponse<object>.Fail(400, "Phòng đã có hợp đồng đang hoạt động.");

            var contract = new Contract
            {
                ContractCode = "CT-" + DateTime.Now.Ticks,
                ApartmentId = request.ApartmentId,
                AccountId = request.AccountId,
                StartDay = request.StartDay,
                EndDay = request.EndDay,
                MonthlyRent = request.MonthlyRent,
                Deposit = request.Deposit,
                File = request.File,
                Status = GeneralStatus.Active,
                CreatedAt = DateTime.Now
            };

            await _contractRepository.AddContractAsync(contract);

			apartment.Status = ApartmentStatus.Occupied;
			await _contractRepository.SaveAsync();

			return ApiResponse<object>.Success(new
			{
				contractId = contract.ContractId,
				contractCode = contract.ContractCode
			}, "Tạo hợp đồng thành công.");
		}

        public async Task<ApiResponse<object>> UpdateContractAsync(int contractId, UpdateContractDto request)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            if (contract.Status != GeneralStatus.Active)
                return ApiResponse<object>.Fail(400, "Chỉ cập nhật hợp đồng đang hoạt động.");

            contract.StartDay = request.StartDay;
            contract.EndDay = request.EndDay;
            contract.MonthlyRent = request.MonthlyRent;
            contract.Deposit = request.Deposit;
            contract.File = request.File;
			contract.UpdatedAt = DateTime.Now;

			await _contractRepository.SaveAsync();
			return ApiResponse<object>.Success(new
			{
				contractId = contract.ContractId,
				contractCode = contract.ContractCode
			}, "Cập nhật hợp đồng thành công.");
		}

        public async Task<ApiResponse<object>> GetContractDetailAsync(int contractId)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");

            var dto = new ContractDetailDto
            {
                ContractId = contract.ContractId,
                ContractCode = contract.ContractCode,
                ApartmentId = contract.ApartmentId,
                ApartmentName = contract.Apartment?.ApartmentName,
                AccountId = contract.AccountId,
                TenantName = contract.Account?.Info?.FullName,
                StartDay = contract.StartDay,
                EndDay = contract.EndDay,
                MonthlyRent = contract.MonthlyRent,
                Deposit = contract.Deposit,
                Status = contract.Status
            };

            return ApiResponse<object>.Success(dto, "Lấy chi tiết hợp đồng thành công.");
        }

		public async Task<ApiResponse<object>> GetContractListAsync()
		{
			var contracts = await _contractRepository.GetContractListAsync();

			var contractDtos = contracts.Select(c => new ContractDetailDto
			{
				ContractId = c.ContractId,
				ContractCode = c.ContractCode,
				ApartmentId = c.ApartmentId,
				ApartmentName = c.Apartment?.ApartmentName,
				AccountId = c.AccountId,
				TenantName = c.Account?.Info?.FullName ?? c.Account?.UserName,

				StartDay = c.StartDay,
				EndDay = c.EndDay,

				MonthlyRent = c.MonthlyRent,
				Deposit = c.Deposit,
				Status = c.Status
			}).ToList();

			return ApiResponse<object>.Success(contractDtos, "Lấy danh sách hợp đồng thành công.");
		}
	}
}