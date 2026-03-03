using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Sentana.API.Models;
using Sentana.API.DTOs.Auth;
using Sentana.API.Enums;
using Sentana.API.Services;

namespace ApartmentBuildingManagement.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly SentanaContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(SentanaContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<string?> LoginAsync(LoginRequestDto request)
        {
            // tìm user trong db
            var user = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(a => a.UserName == request.UserName && a.Status == GeneralStatus.Active);

            // kiểm tra tài khoản mật khẩu
            // chưa hash nên check thường
            if (user == null || request.Password != user.Password)
            {
                return null;
            }

            // trả jwt token
            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(Account user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim("AccountId", user.AccountId.ToString()),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Resident")
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}