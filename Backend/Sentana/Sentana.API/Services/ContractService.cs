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

            // 1️⃣ Contract tồn tại
            if (contract == null)
            {
                return ApiResponse<object>.Fail(404, "Contract not found.");
            }

            // 2️⃣ Contract đã terminate chưa
            if (contract.Status != GeneralStatus.Active)
            {
                return ApiResponse<object>.Fail(400, "Contract is not active.");
            }

            // 3️⃣ Validate contract date
            if (contract.StartDay == null || contract.EndDay == null)
            {
                return ApiResponse<object>.Fail(400, "Contract date is invalid.");
            }

            var startDate = contract.StartDay.Value;
            var endDate = contract.EndDay.Value;

            // 4️⃣ terminationDate < start
            if (request.TerminationDate < startDate)
            {
                return ApiResponse<object>.Fail(400, "Termination date cannot be before contract start date.");
            }

            // 5️⃣ terminationDate > end
            if (request.TerminationDate > endDate)
            {
                return ApiResponse<object>.Fail(400, "Termination date cannot be after contract end date.");
            }

            // 6️⃣ deposit validate
            decimal deposit = contract.Deposit ?? 0;

            if (deposit <= 0)
            {
                return ApiResponse<object>.Fail(400, "Deposit value is invalid.");
            }

            if (request.AdditionalCost < 0)
            {
                return ApiResponse<object>.Fail(400, "Additional cost cannot be negative.");
            }

            if (request.AdditionalCost > deposit)
            {
                return ApiResponse<object>.Fail(400, "Additional cost cannot exceed deposit.");
            }

            if (contract.Apartment == null)
            {
                return ApiResponse<object>.Fail(400, "Apartment not found.");
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
            }, "Contract terminated successfully.");
        }
    }
}