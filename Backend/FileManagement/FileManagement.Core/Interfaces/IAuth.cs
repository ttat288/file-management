namespace FileManagement.Core.Interfaces
{
    public record AuthUser(Guid Id, string Email, string? DisplayName);

    public interface IUserRepository
    {
        Task<AuthUser?> GetByEmailAsync(string email);
        Task<AuthUser?> GetByIdAsync(Guid userId);
        Task<(AuthUser User, string PasswordHash)?> GetWithPasswordHashByEmailAsync(string email);
        Task<AuthUser> CreateAsync(string email, string passwordHash, string? displayName);
    }

    public interface IRefreshTokenRepository
    {
        Task StoreAsync(Guid userId, string tokenHash, DateTime expiresAtUtc);
        Task<(Guid UserId, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc)?> GetByTokenHashAsync(string tokenHash);
        Task TouchLastUsedAsync(string tokenHash);
        Task RevokeAsync(string tokenHash);
    }
}

