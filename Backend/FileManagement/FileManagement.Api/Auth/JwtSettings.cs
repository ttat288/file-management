namespace FileManagement.Api.Auth
{
    public class JwtSettings
    {
        public string Issuer { get; set; } = "file-management";
        public string Audience { get; set; } = "file-management";
        public string SigningKey { get; set; } = "";
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 14;
    }
}

