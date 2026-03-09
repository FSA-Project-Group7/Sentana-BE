using Sentana.API.DTOs.Contracts;
using Sentana.API.Helpers;

namespace Sentana.API.Services
{
    public interface IContractService
    {
        Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto dto);
    }
}