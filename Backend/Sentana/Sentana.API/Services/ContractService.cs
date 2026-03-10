using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Contracts;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class ContractService : IContractService
    {
        private readonly SentanaContext _context;

        public ContractService(SentanaContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request)
        {
            var contract = await _context.Contracts
                .Include(c => c.Apartment)
                .FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == false);

            if (contract == null)
            {
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");
            }

            if (contract.Status != GeneralStatus.Active)
            {
                return ApiResponse<object>.Fail(400, "Hợp đồng không còn hiệu lực.");
            }

            if (contract.StartDay == null || contract.EndDay == null)
            {
                return ApiResponse<object>.Fail(400, "Ngày hợp đồng không hợp lệ.");
            }

            var startDate = contract.StartDay.Value;
            var endDate = contract.EndDay.Value;

            if (request.TerminationDate < startDate)
            {
                return ApiResponse<object>.Fail(400, "Ngày kết thúc không được nhỏ hơn ngày bắt đầu hợp đồng.");
            }

            if (request.TerminationDate > endDate)
            {
                return ApiResponse<object>.Fail(400, "Ngày kết thúc không được lớn hơn ngày kết thúc hợp đồng.");
            }

            decimal deposit = contract.Deposit ?? 0;

            if (deposit <= 0)
            {
                return ApiResponse<object>.Fail(400, "Giá trị đặt cọc không hợp lệ.");
            }

            if (request.AdditionalCost < 0)
            {
                return ApiResponse<object>.Fail(400, "Chi phí thêm không được âm.");
            }

            if (request.AdditionalCost > deposit)
            {
                return ApiResponse<object>.Fail(400, "Chi phí thêm không được vượt quá số tiền đặt cọc.");
            }

            if (contract.Apartment == null)
            {
                return ApiResponse<object>.Fail(400, "Không tìm thấy căn hộ.");
            }

            decimal refund = deposit - request.AdditionalCost;

            if (refund < 0)
            {
                refund = 0;
            }
            contract.AdditionalCost = request.AdditionalCost;
            contract.RefundAmount = refund;
            contract.Status = GeneralStatus.Inactive;
            contract.UpdatedAt = DateTime.Now;
            contract.Apartment.Status = ApartmentStatus.Vacant;

            await _context.SaveChangesAsync();

            return ApiResponse<object>.Success(new
            {
                contractId = contract.ContractId,
                deposit = deposit,
                additionalCost = request.AdditionalCost,
                refundAmount = refund
            }, "Hợp đồng đã được chấm dứt thành công.");
        }
        public async Task<ApiResponse<object>> ExtendContractAsync(int contractId, ExtendContractDto request)
        {
            if (contractId <= 0)
            {
                return ApiResponse<object>.Fail(400, "Contract ID không hợp lệ.");
            }

            var contract = await _context.Contracts
                .Include(c => c.Apartment)
                .FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == false);

            if (contract == null)
            {
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng.");
            }

            if (contract.Status != GeneralStatus.Active)
            {
                return ApiResponse<object>.Fail(400, "Hợp đồng không còn hiệu lực.");
            }

            if (contract.StartDay == null || contract.EndDay == null)
            {
                return ApiResponse<object>.Fail(400, "Ngày hợp đồng không hợp lệ.");
            }

            DateOnly startDate = contract.StartDay.Value;
            DateOnly currentEndDate = contract.EndDay.Value;

            if (request.NewEndDate <= currentEndDate)
            {
                return ApiResponse<object>.Fail(400, "Ngày kết thúc mới phải lớn hơn ngày kết thúc hiện tại.");
            }

            if (request.NewEndDate < startDate)
            {
                return ApiResponse<object>.Fail(400, "Ngày kết thúc mới không hợp lệ.");
            }
            contract.EndDay = request.NewEndDate;
            contract.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return ApiResponse<object>.Success(new
            {
                contractId = contract.ContractId,
                startDay = contract.StartDay,
                oldEndDay = currentEndDate,
                newEndDay = request.NewEndDate
            }, "Gia hạn hợp đồng thành công.");
        }
        public async Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request)
        {
            if (request == null)
            {
                return ApiResponse<object>.Fail(400, "Request body không được để trống.");
            }

            if (request.ApartmentId <= 0)
            {
                return ApiResponse<object>.Fail(400, "Apartment ID không hợp lệ.");
            }

            if (request.AccountId <= 0)
            {
                return ApiResponse<object>.Fail(400, "Account ID không hợp lệ.");
            }

            if (request.StartDay >= request.EndDay)
            {
                return ApiResponse<object>.Fail(400, "Ngày kết thúc phải lớn hơn ngày bắt đầu.");
            }

            if (request.MonthlyRent < 0)
            {
                return ApiResponse<object>.Fail(400, "Tiền thuê không hợp lệ.");
            }

            if (request.Deposit < 0)
            {
                return ApiResponse<object>.Fail(400, "Tiền cọc không hợp lệ.");
            }

            var apartment = await _context.Apartments
                .FirstOrDefaultAsync(a => a.ApartmentId == request.ApartmentId && a.IsDeleted == false);

            if (apartment == null)
            {
                return ApiResponse<object>.Fail(404, "Apartment không tồn tại.");
            }

            if (apartment.Status != ApartmentStatus.Vacant)
            {
                return ApiResponse<object>.Fail(400, "Phòng hiện không trống.");
            }

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == request.AccountId && a.IsDeleted == false);

            if (account == null)
            {
                return ApiResponse<object>.Fail(404, "Account không tồn tại.");
            }

            var overlapContract = await _context.Contracts
                .AnyAsync(c =>
                    c.ApartmentId == request.ApartmentId &&
                    c.Status == GeneralStatus.Active &&
                    c.IsDeleted == false);

            if (overlapContract)
            {
                return ApiResponse<object>.Fail(400, "Phòng đã có hợp đồng đang hoạt động.");
            }

            var contractCode = "CT-" + DateTime.Now.Ticks;

            var contract = new Contract
            {
                ContractCode = contractCode,
                ApartmentId = request.ApartmentId,
                AccountId = request.AccountId,
                StartDay = request.StartDay,
                EndDay = request.EndDay,
                MonthlyRent = request.MonthlyRent,
                Deposit = request.Deposit,
                AdditionalCost = 0,
                RefundAmount = 0,
                File = request.File,
                Status = GeneralStatus.Active,
                CreatedAt = DateTime.Now
            };

            await _context.Contracts.AddAsync(contract);

            apartment.Status = ApartmentStatus.Occupied;

            await _context.SaveChangesAsync();

            return ApiResponse<object>.Success(new
            {
                contractId = contract.ContractId,
                contractCode = contract.ContractCode,
                apartmentId = contract.ApartmentId,
                accountId = contract.AccountId,
                startDay = contract.StartDay,
                endDay = contract.EndDay
            }, "Tạo hợp đồng thành công.");
        }
    }
}