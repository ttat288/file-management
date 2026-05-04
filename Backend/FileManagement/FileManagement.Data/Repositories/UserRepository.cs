using Dapper;
using FileManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FileManagement.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgreSQL")
                ?? throw new InvalidOperationException("PostgreSQL connection string not found");
            _logger = logger;
        }

        private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<AuthUser?> GetByEmailAsync(string email)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            return await connection.QueryFirstOrDefaultAsync<AuthUser>(
                "SELECT id, email, display_name AS displayName FROM users WHERE LOWER(email)=LOWER(@email)",
                new { email });
        }

        public async Task<AuthUser?> GetByIdAsync(Guid userId)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            return await connection.QueryFirstOrDefaultAsync<AuthUser>(
                "SELECT id, email, display_name AS displayName FROM users WHERE id=@userId",
                new { userId });
        }

        public async Task<(AuthUser User, string PasswordHash)?> GetWithPasswordHashByEmailAsync(string email)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            var row = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT id, email, display_name, password_hash FROM users WHERE LOWER(email)=LOWER(@email)",
                new { email });

            if (row == null) return null;

            var user = new AuthUser((Guid)row.id, (string)row.email, (string?)row.display_name);
            return (user, (string)row.password_hash);
        }

        public async Task<AuthUser> CreateAsync(string email, string passwordHash, string? displayName)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            var row = await connection.QueryFirstAsync<dynamic>(
                @"INSERT INTO users(email, password_hash, display_name)
                  VALUES (@email, @passwordHash, @displayName)
                  RETURNING id, email, display_name",
                new { email, passwordHash, displayName });

            return new AuthUser((Guid)row.id, (string)row.email, (string?)row.display_name);
        }
    }
}

