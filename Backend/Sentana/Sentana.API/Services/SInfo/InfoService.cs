using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Info;
using Sentana.API.Models;

namespace Sentana.API.Services.SInfo
{
    public class InfoService : IInfoService
    {
        private readonly SentanaContext _context;

        public InfoService(SentanaContext context)
        {
            _context = context;
        }
        public async Task<InfoCheckResponseDto?> GetInfoByCccd(string cccd)
        {
            var info = await _context.InFos
                .FirstOrDefaultAsync(i => i.CmndCccd == cccd);
            if (info == null) return null;
            return new InfoCheckResponseDto
            {
                InfoId = info.InfoId,
                FullName = info.FullName,
                PhoneNumber = info.PhoneNumber,
                Country = info.Country,
                City = info.City,
                Address = info.Address,
                Sex = info.Sex,
                Birthday = info.BirthDay
            };
        }
    }
}
