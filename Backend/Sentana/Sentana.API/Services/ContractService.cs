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

        public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto dto)
        {
            var contract = await _context.Contracts
                .Include(c => c.Apartment)
                .FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == false);

            if (contract == null)
            {
                return ApiResponse<object>.Fail(404, "Contract not found");
            }

            if (contract.Status != GeneralStatus.Active)
            {
                return ApiResponse<object>.Fail(400, "Only active contracts can be terminated");
            }

            if (contract.StartDay == null || contract.EndDay == null)
            {
                return ApiResponse<object>.Fail(400, "Contract date information is invalid");
            }

            DateOnly startDate = contract.StartDay.Value;
            DateOnly endDate = contract.EndDay.Value;

            if (dto.TerminationDate < startDate)
            {
                return ApiResponse<object>.Fail(400, "Termination date cannot be before contract start date");
            }

            if (dto.TerminationDate > endDate)
            {
                return ApiResponse<object>.Fail(400, "Termination date cannot be after contract end date");
            }
            if (contract.Apartment == null)
            {
                return ApiResponse<object>.Fail(400, "Apartment not found for this contract");
            }
            int totalDays = endDate.DayNumber - startDate.DayNumber;
            int usedDays = dto.TerminationDate.DayNumber - startDate.DayNumber;
            int remainingDays = totalDays - usedDays;

            decimal refundAmount = 0;

            if (totalDays > 0 && contract.Deposit.HasValue)
            {
                refundAmount = contract.Deposit.Value * remainingDays / totalDays;
            }

            if (refundAmount < 0)
            {
                refundAmount = 0;
            }
            contract.Status = GeneralStatus.Inactive;
            contract.RefundAmount = refundAmount;
            contract.UpdatedAt = DateTime.Now;

            contract.Apartment.Status = ApartmentStatus.Vacant;

            await _context.SaveChangesAsync();

            return ApiResponse<object>.Success(new
            {
                ContractId = contract.ContractId,
                RefundAmount = refundAmount
            }, "Contract terminated successfully");
        }
    }
}