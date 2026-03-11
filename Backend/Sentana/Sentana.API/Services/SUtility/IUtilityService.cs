using System.Security.Claims;
using Sentana.API.DTOs.Utility;

namespace Sentana.API.Services
{
    public interface IUtilityService
    {
        // nhập chỉ số điện
        Task<(bool IsSuccess, string Message)> InputElectricityIndexAsync(InputElectricIndexDto request, int currentUserId);

        // nhập chỉ số nước
        Task<(bool IsSuccess, string Message)> InputWaterIndexAsync(InputWaterIndexDto request, int currentUserId);
        // Utility history
        Task<(bool IsSuccess, string Message, List<UtilityHistoryDto>? Data)> GetUtilityHistoryAsync(ClaimsPrincipal user, int? targetApartmentId, int? month, int? year);

    }
}