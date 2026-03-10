using Sentana.API.DTOs.Info;

namespace Sentana.API.Services
{
    public interface IInfoService
    {
        Task<InfoCheckResponseDto?> GetInfoByCccd(string cccd);
    }
}
