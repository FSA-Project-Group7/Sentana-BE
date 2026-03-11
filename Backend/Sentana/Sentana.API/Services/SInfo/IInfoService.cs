using Sentana.API.DTOs.Info;

namespace Sentana.API.Services.SInfo
{
    public interface IInfoService
    {
        Task<InfoCheckResponseDto?> GetInfoByCccd(string cccd);
    }
}
