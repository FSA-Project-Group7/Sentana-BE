using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Sentana.API.Models;
using Sentana.API.DTOs.Auth;
using Sentana.API.Enums;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using Sentana.API.Services.SEmail;

namespace Sentana.API.Services.SBuilding
{
    public class AuthService : IAuthService
    {
        private readonly SentanaContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;

        public AuthService(SentanaContext context, IConfiguration configuration, IMemoryCache cache, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _cache = cache;
            _emailService = emailService;
        }

        // login
        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var user = await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Info)
                .FirstOrDefaultAsync(a =>
                    (a.UserName == request.UserName || a.Email == request.UserName)
                    && a.Status == GeneralStatus.Active);

            // Dùng BCrypt.Verify để kiểm tra mật khẩu 
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                return null;
            }
            // tạo jwt token và refresh token
            var token = GenerateJwtToken(user);
            var plainRefreshToken = GenerateRefreshToken();

            // Băm Refresh Token trước khi lưu xuống Database
            user.RefreshToken = BCrypt.Net.BCrypt.HashPassword(plainRefreshToken);
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7); 

            await _context.SaveChangesAsync();

            // Trả về mã plainRefreshToken gốc cho Client
            return new LoginResponseDto
            {
                Token = token,
                RefreshToken = plainRefreshToken,
                Role = user.Role?.RoleName ?? "Resident",
                UserName = user.UserName,
                AccountId = user.AccountId
            };
        }

        // tạo jwt token cho login
        private string GenerateJwtToken(Account user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim("AccountId", user.AccountId.ToString()),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Resident"),
                new Claim(JwtRegisteredClaimNames.Name, user.Info?.FullName ?? user.UserName),
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(10),
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

			
			var activeContract = await _context.Contracts
				.Include(c => c.Apartment)          
					.ThenInclude(a => a.Building)   
				.FirstOrDefaultAsync(c => c.AccountId == accountId && c.Status == GeneralStatus.Active);

			
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
				CmndCccd = user.Info?.CmndCccd,

				
				ApartmentCode = activeContract?.Apartment?.ApartmentCode,
				BuildingName = activeContract?.Apartment?.Building?.BuildingName ?? "Tòa nhà SENTANA",
				ContractStart = activeContract?.StartDay, 
				ContractEnd = activeContract?.EndDay,     
				Status = activeContract != null ? "Đang cư trú" : "Chưa có hợp đồng"
			};
		}

		//update profile
		public async Task<(bool IsSuccess, string Message)> UpdateUserProfileAsync(int accountId, UpdateProfileRequestDto request)
        {
            if (await _context.Accounts.AnyAsync(a => a.Email == request.Email && a.AccountId != accountId))
            {
                return (false, "Email này đã được sử dụng bởi một tài khoản khác.");
            }

            if (await _context.Accounts.AnyAsync(a => a.Info != null && a.Info.PhoneNumber == request.PhoneNumber && a.AccountId != accountId))
            {
                return (false, "Số điện thoại này đã được sử dụng bởi một người khác.");
            }

            // không trùng thì update
            var user = await _context.Accounts
                .Include(a => a.Info)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (user == null) return (false, "Không tìm thấy tài khoản để cập nhật.");

            user.Email = request.Email;
            user.UpdatedAt = DateTime.Now;

            if (user.Info == null)
            {
                user.Info = new InFo
                {
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    BirthDay = request.BirthDay,
                    CreatedAt = DateTime.Now
                };
            }
            else
            {
                user.Info.FullName = request.FullName;
                user.Info.PhoneNumber = request.PhoneNumber;
                user.Info.BirthDay = request.BirthDay;
                user.Info.UpdatedAt = DateTime.Now;
            }

            bool isSaved = await _context.SaveChangesAsync() > 0;
            return isSaved ? (true, "Thành công") : (false, "Lỗi khi lưu dữ liệu.");
        }

        // Reset Password
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

            // xóa Refresh Token
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Xóa OTP khỏi RAM để tránh dùng lại
            _cache.Remove($"OTP_{request.Email}");

            return true;
        }

        // Change Password
        // tạo và gửi OTP cho người đang đăng nhập
        public async Task<bool> RequestChangePasswordOtpAsync(int accountId)
        {
            var user = await _context.Accounts.FindAsync(accountId);

            // Đảm bảo user tồn tại và có email
            if (user == null || string.IsNullOrEmpty(user.Email)) return false;

            var otpCode = new Random().Next(100000, 999999).ToString();

            // Lưu Cache với Key chứa ID để không bị nhầm lẫn giữa các user
            _cache.Set($"ChangePassOTP_{accountId}", otpCode, TimeSpan.FromMinutes(5));

            string emailBody = $"<h3>Mã xác nhận để đổi mật khẩu bảo mật của bạn là: <b style='color:green;'>{otpCode}</b></h3><p>Mã này có hiệu lực 5 phút. Nếu bạn không thực hiện yêu cầu này, hãy phớt lờ email này.</p>";
            await _emailService.SendEmailAsync(user.Email, "[SENTANA] Xác nhận Đổi Mật Khẩu", emailBody);

            return true;
        }

        // kiểm tra OTP và đổi mật khẩu
        public async Task<bool> ChangePasswordAsync(int accountId, ChangePasswordRequestDto request)
        {
            // Kiểm tra OTP trước
            if (!_cache.TryGetValue($"ChangePassOTP_{accountId}", out string? savedOtp))
                throw new Exception("Mã OTP đã hết hạn hoặc không tồn tại.");

            if (savedOtp != request.OtpCode)
                throw new Exception("Mã OTP không chính xác.");

            var user = await _context.Accounts.FindAsync(accountId);
            if (user == null) return false;

            // Kiểm tra mật khẩu cũ
            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password))
                throw new Exception("Mật khẩu cũ không chính xác.");

            // Cập nhật mật khẩu mới
            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Xóa OTP ngay sau khi dùng thành công
            _cache.Remove($"ChangePassOTP_{accountId}");

            return true;
        }

        // Refresh Token
        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        // đọc thông tin từ token đã hết hạn
        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
                ValidateLifetime = false // tắt kiểm tra hạn sử dụng để đọc được ruột Token
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            // Kiểm tra xem token này có đúng là chuẩn HmacSha256 không (chống token giả mạo)
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Token không hợp lệ");
            }

            return principal;
        }

        // cấp lại cặp Token mới 
        public async Task<TokenModelDto?> RenewTokenAsync(TokenModelDto request)
        {
            // mở Token cũ ra để lấy UserName
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null) return null;

            var userName = principal.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.NameIdentifier ||
            c.Type == JwtRegisteredClaimNames.Sub)?.Value;

            // tìm người dùng trong DB
            var user = await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Info)
                .FirstOrDefaultAsync(a => a.UserName == userName);

            // kiểm tra xem user có tồn tại và token còn hạn không
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.Now || string.IsNullOrEmpty(user.RefreshToken))
            {
                return null; // Từ chối cấp mới
            }

            // dùng BCrypt.Verify để so sánh mã Frontend gửi lên với bản Hash trong DB
            if (!BCrypt.Net.BCrypt.Verify(request.RefreshToken, user.RefreshToken))
            {
                return null; // Mã làm mới không khớp
            }

            var newAccessToken = GenerateJwtToken(user);
            var newPlainRefreshToken = GenerateRefreshToken();

            // ghi đè Refresh Token bản Hash mới vào DB
            user.RefreshToken = BCrypt.Net.BCrypt.HashPassword(newPlainRefreshToken);
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            await _context.SaveChangesAsync();

            // trả về FE
            return new TokenModelDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newPlainRefreshToken
            };
        }

        // logout 
        public async Task<bool> LogoutAsync(int accountId)
        {
            var user = await _context.Accounts.FindAsync(accountId);
            if (user == null) return false;

            // Xóa Refresh Token để chặn việc xin cấp lại Token mới
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            return await _context.SaveChangesAsync() > 0;
        }
    }
}