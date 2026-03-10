using Sentana.API.DTOs.Contracts;
using Sentana.API.Helpers;

namespace Sentana.API.Services
{
    public interface IContractService
    {
        Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request);
        Task<ApiResponse<object>> ExtendContractAsync(int contractId, ExtendContractDto request);
        Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request);
        Task<ApiResponse<object>> UpdateContractAsync(int contractId, UpdateContractDto request);
    }
}