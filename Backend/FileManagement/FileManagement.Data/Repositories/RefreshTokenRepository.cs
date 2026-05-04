using Dapper;
using FileManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FileManagement.Data.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<RefreshTokenRepository> _logger;

        public RefreshTokenRepository(IConfiguration configuration, ILogger<RefreshTokenRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgreSQL")
                ?? throw new InvalidOperationException("PostgreSQL connection string not found");
            _logger = logger;
        }

        private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task StoreAsync(Guid userId, string tokenHash, DateTime expiresAtUtc)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                @"INSERT INTO refresh_tokens(user_id, token_hash, expires_at)
                  VALUES (@userId, @tokenHash, @expiresAtUtc)",
                new { userId, tokenHash, expiresAtUtc });
        }

        public async Task<(Guid UserId, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc)?> GetByTokenHashAsync(string tokenHash)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            var row = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT user_id, expires_at, revoked_at
                  FROM refresh_tokens
                  WHERE token_hash=@tokenHash",
                new { tokenHash });

            if (row == null) return null;

            return ((Guid)row.user_id, (DateTime)row.expires_at, (DateTime?)row.revoked_at);
        }

        public async Task TouchLastUsedAsync(string tokenHash)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "UPDATE refresh_tokens SET last_used_at=CURRENT_TIMESTAMP WHERE token_hash=@tokenHash",
                new { tokenHash });
        }

        public async Task RevokeAsync(string tokenHash)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "UPDATE refresh_tokens SET revoked_at=CURRENT_TIMESTAMP WHERE token_hash=@tokenHash AND revoked_at IS NULL",
                new { tokenHash });
        }
    }
}

