using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Sentana.API.Models;
using Sentana.API.DTOs.Auth;
using Sentana.API.Enums;
using Sentana.API.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Sentana.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly SentanaContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache; // Thêm Cache
        private readonly IEmailService _emailService; // Thêm EmailService

        public AuthService(SentanaContext context, IConfiguration configuration, IMemoryCache cache, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _cache = cache;
            _emailService = emailService;
        }

        //login
        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
        {
            // tìm user trong db
            var user = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(a => a.UserName == request.UserName && a.Status == GeneralStatus.Active);

            // Dùng BCrypt.Verify để kiểm tra mật khẩu 
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                return null;
            }

            var token = GenerateJwtToken(user);

            // Trả về DTO hoàn chỉnh 
            return new LoginResponseDto
            {
                Token = token,
                Role = user.Role?.RoleName ?? "Resident",
                UserName = user.UserName
            };
        }

        //tạo jwt token cho login
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

        //get user profile
        public async Task<UserProfileResponseDto?> GetUserProfileAsync(int accountId)
        {
            var user = await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Info)
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.Status == GeneralStatus.Active);

            if (user == null)
            {
                return null;
            }

            // gán dữ liệu sang cho UserProfileResponseDto
            return new UserProfileResponseDto
            {
                AccountId = user.AccountId,
                UserName = user.UserName,
                Email = user.Email, 
                Role = user.Role?.RoleName ?? "Resident",

                FullName = user.Info?.FullName,
                PhoneNumber = user.Info?.PhoneNumber,
                BirthDay = user.Info?.BirthDay,
                Address = user.Info?.Address,
                CmndCccd = user.Info?.CmndCccd
            };
        }

        //update profile
        public async Task<bool> UpdateUserProfileAsync(int accountId, UpdateProfileRequestDto request)
        {
            var user = await _context.Accounts
                .Include(a => a.Info)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (user == null) return false;

            // 1. Cập nhật bảng Account
            user.Email = request.Email ?? user.Email;
            user.UpdatedAt = DateTime.Now;

            // 2. Cập nhật bảng InFo
            if (user.Info == null)
            {
                // Nếu chưa có thông tin thì tạo mới bản ghi Info
                user.Info = new InFo
                {
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    CmndCccd = request.CmndCccd,
                    Address = request.Address,
                    BirthDay = request.BirthDay,
                    CreatedAt = DateTime.Now
                };
            }
            else
            {
                // Nếu đã có thì cập nhật 
                user.Info.FullName = request.FullName ?? user.Info.FullName;
                user.Info.PhoneNumber = request.PhoneNumber ?? user.Info.PhoneNumber;
                user.Info.CmndCccd = request.CmndCccd ?? user.Info.CmndCccd;
                user.Info.Address = request.Address ?? user.Info.Address;
                user.Info.BirthDay = request.BirthDay ?? user.Info.BirthDay;
                user.Info.UpdatedAt = DateTime.Now;
            }

            return await _context.SaveChangesAsync() > 0;
        }

        // tạo và gửi OTP
        public async Task<bool> SendOtpAsync(SendOtpRequestDto request)
        {
            // Kiểm tra xem email có tồn tại trong hệ thống không
            var user = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
            if (user == null) return false;

            // Sinh mã OTP 6 số ngẫu nhiên
            var otpCode = new Random().Next(100000, 999999).ToString();

            // Lưu OTP vào Cache với Key là Email, thời hạn ĐÚNG 5 PHÚT
            _cache.Set($"OTP_{request.Email}", otpCode, TimeSpan.FromMinutes(5));

            // Gửi thư
            string emailBody = $"<h3>Mã xác nhận của bạn là: <b>{otpCode}</b></h3><p>Mã này sẽ hết hạn trong vòng 5 phút.</p>";
            await _emailService.SendEmailAsync(request.Email, "Mã OTP Đổi Mật Khẩu SENTANA", emailBody);

            return true;
        }

        // xác thực OTP và đổi mật khẩu
        public async Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            // Lấy OTP từ RAM ra kiểm tra
            if (!_cache.TryGetValue($"OTP_{request.Email}", out string? savedOtp))
                throw new Exception("OTP đã hết hạn hoặc không tồn tại.");

            if (savedOtp != request.OtpCode)
                throw new Exception("Mã OTP không chính xác.");

            // Nếu OTP đúng, tiến hành tìm user và đổi pass
            var user = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
            if (user == null) return false;

            // Băm mật khẩu mới bằng BCrypt
            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Xóa OTP khỏi RAM để tránh dùng lại
            _cache.Remove($"OTP_{request.Email}");

            return true;
        }
    }
}