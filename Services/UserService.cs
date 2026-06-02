using System.Security.Cryptography;
using System.Text;
using Dapper;
using ImportDataToERP.Data;
using ImportDataToERP.Models;

namespace ImportDataToERP.Services;

public class UserService
{
    private readonly DbConnectionFactory _db;

    public UserService(DbConnectionFactory db)
    {
        _db = db;
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public async Task<User?> AuthenticateAsync(string account, string password)
    {
        using var conn = _db.CreateConnection();
        var passwordHash = HashPassword(password);
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Account=@Account AND PasswordHash=@PasswordHash AND IsActive=1",
            new { Account = account, PasswordHash = passwordHash });
    }

    public async Task<User?> GetByAccountAsync(string account)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Account=@Account", new { Account = account });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<User>("SELECT Id, Account, Name, Email, IsActive, CreatedAt, UpdatedAt FROM Users ORDER BY Id DESC");
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT Id, Account, Name, Email, IsActive, CreatedAt, UpdatedAt FROM Users WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<int> CreateAsync(User user, string password)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var sql = @"INSERT INTO Users (Account, Name, Email, PasswordHash, IsActive, CreatedAt, UpdatedAt)
                        VALUES (@Account, @Name, @Email, @PasswordHash, @IsActive, @CreatedAt, @UpdatedAt);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";
            user.PasswordHash = HashPassword(password);
            user.CreatedAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;
            var id = await conn.ExecuteScalarAsync<int>(sql, user, transaction);
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateAsync(User user, string? newPassword = null)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            user.UpdatedAt = DateTime.Now;
            if (!string.IsNullOrEmpty(newPassword))
            {
                user.PasswordHash = HashPassword(newPassword);
                var sql = @"UPDATE Users SET Account=@Account, Name=@Name, Email=@Email, PasswordHash=@PasswordHash,
                            IsActive=@IsActive, UpdatedAt=@UpdatedAt WHERE Id=@Id";
                var rows = await conn.ExecuteAsync(sql, user, transaction);
                await transaction.CommitAsync();
                return rows > 0;
            }
            else
            {
                var sql = @"UPDATE Users SET Account=@Account, Name=@Name, Email=@Email, 
                            IsActive=@IsActive, UpdatedAt=@UpdatedAt WHERE Id=@Id";
                var rows = await conn.ExecuteAsync(sql, user, transaction);
                await transaction.CommitAsync();
                return rows > 0;
            }
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var rows = await conn.ExecuteAsync("DELETE FROM Users WHERE Id=@Id", new { Id = id }, transaction);
            await transaction.CommitAsync();
            return rows > 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
