using System;
using Npgsql;

namespace FileManagement.Data.Utils
{
    internal static class PostgresErrorMapper
    {
        public static Exception MapOrSame(Exception ex, string requiredFunction, string setupScriptPath)
        {
            if (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.UndefinedFunction)
            {
                // Keep message actionable for FE logs.
                var message =
                    $"Database function not found: {requiredFunction}. " +
                    $"Ensure DB setup scripts were applied (at least `{setupScriptPath}`). " +
                    $"Original: {pg.MessageText}";

                return new InvalidOperationException(message, ex);
            }

            return ex;
        }
    }
}

