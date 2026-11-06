using System.Data;
using Microsoft.Data.SqlClient;

namespace ST10389916_CLDV7112_Practicum;

public sealed class UserAccountStore(IConfiguration configuration)
{
    public async Task<Guid> CreateUserAsync(string username, string password, string role, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Sql") ?? configuration["ConnectionStrings:Sql"] ?? configuration["SQL_CONNECTION_STRING"];
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var id = Guid.NewGuid();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO UserAccounts (Id, Username, [Password], [Role]) VALUES (@id, @username, @password, @role)";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });
        command.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 100) { Value = username });
        command.Parameters.Add(new SqlParameter("@password", SqlDbType.NVarChar, 255) { Value = password });
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.NVarChar, 30) { Value = role });
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task<UserAccount?> ValidateLoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Sql") ?? configuration["ConnectionStrings:Sql"] ?? configuration["SQL_CONNECTION_STRING"];
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP 1 Id, Username, [Role] FROM UserAccounts WHERE Username = @username AND [Password] = @password";
        command.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 100) { Value = username });
        command.Parameters.Add(new SqlParameter("@password", SqlDbType.NVarChar, 255) { Value = password });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UserAccount(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }
}

public sealed record UserAccount(Guid Id, string Username, string Role);
