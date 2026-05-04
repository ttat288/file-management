using BCrypt.Net;
using FileManagement.Api.Auth;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FileManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly TokenService _tokens;
        private readonly JwtSettings _jwt;

        public AuthController(
            IUserRepository users,
            IRefreshTokenRepository refreshTokens,
            TokenService tokens,
            IOptions<JwtSettings> jwt)
        {
            _users = users;
            _refreshTokens = refreshTokens;
            _tokens = tokens;
            _jwt = jwt.Value;
        }

        public record RegisterRequest(string Email, string Password, string? DisplayName);
        public record LoginRequest(string Email, string Password);
        public record RefreshRequest(string RefreshToken);

        public record AuthUserDto(Guid Id, string Email, string? DisplayName);
        public record AuthResponse(string AccessToken, string RefreshToken, AuthUserDto User);

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(ApiResponse<AuthResponse>.Error("Email and password are required"));

            var email = req.Email.Trim();
            if (req.Password.Length < 6)
                return BadRequest(ApiResponse<AuthResponse>.Error("Password must be at least 6 characters"));

            var existing = await _users.GetByEmailAsync(email);
            if (existing != null)
                return Conflict(ApiResponse<AuthResponse>.Error("Email already exists"));

            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            var user = await _users.CreateAsync(email, hash, req.DisplayName?.Trim());

            var access = _tokens.CreateAccessToken(user);
            var refresh = _tokens.CreateRefreshToken();
            var refreshHash = TokenService.HashToken(refresh);

            await _refreshTokens.StoreAsync(user.Id, refreshHash, DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays));

            var payload = new AuthResponse(access, refresh, new AuthUserDto(user.Id, user.Email, user.DisplayName));
            return Ok(ApiResponse<AuthResponse>.Ok(payload));
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(ApiResponse<AuthResponse>.Error("Email and password are required"));

            var email = req.Email.Trim();
            var found = await _users.GetWithPasswordHashByEmailAsync(email);
            if (found == null)
                return Unauthorized(ApiResponse<AuthResponse>.Error("Invalid email or password"));

            if (!BCrypt.Net.BCrypt.Verify(req.Password, found.Value.PasswordHash))
                return Unauthorized(ApiResponse<AuthResponse>.Error("Invalid email or password"));

            var access = _tokens.CreateAccessToken(found.Value.User);
            var refresh = _tokens.CreateRefreshToken();
            var refreshHash = TokenService.HashToken(refresh);

            await _refreshTokens.StoreAsync(found.Value.User.Id, refreshHash, DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays));

            var payload = new AuthResponse(access, refresh, new AuthUserDto(found.Value.User.Id, found.Value.User.Email, found.Value.User.DisplayName));
            return Ok(ApiResponse<AuthResponse>.Ok(payload));
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return BadRequest(ApiResponse<AuthResponse>.Error("Refresh token is required"));

            var hash = TokenService.HashToken(req.RefreshToken);
            var record = await _refreshTokens.GetByTokenHashAsync(hash);
            if (record == null)
                return Unauthorized(ApiResponse<AuthResponse>.Error("Invalid refresh token"));

            if (record.Value.RevokedAtUtc != null)
                return Unauthorized(ApiResponse<AuthResponse>.Error("Refresh token revoked"));

            if (record.Value.ExpiresAtUtc <= DateTime.UtcNow)
                return Unauthorized(ApiResponse<AuthResponse>.Error("Refresh token expired"));

            var user = await _users.GetByIdAsync(record.Value.UserId);
            if (user == null)
                return Unauthorized(ApiResponse<AuthResponse>.Error("User not found"));

            // Rotate refresh token for better security
            await _refreshTokens.RevokeAsync(hash);

            var newAccess = _tokens.CreateAccessToken(user);
            var newRefresh = _tokens.CreateRefreshToken();
            var newHash = TokenService.HashToken(newRefresh);
            await _refreshTokens.StoreAsync(user.Id, newHash, DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays));

            var payload = new AuthResponse(newAccess, newRefresh, new AuthUserDto(user.Id, user.Email, user.DisplayName));
            return Ok(ApiResponse<AuthResponse>.Ok(payload));
        }
    }
}

