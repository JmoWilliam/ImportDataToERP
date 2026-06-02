using Dapper;
using ImportDataToERP.Data;
using ImportDataToERP.Models;

namespace ImportDataToERP.Services;

public class QuotationImportService
{
    private readonly DbConnectionFactory _db;

    public QuotationImportService(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<IEnumerable<QuotationImport>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<QuotationImport>("SELECT * FROM QuotationImports ORDER BY Id DESC");
    }

    public async Task<QuotationImport?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<QuotationImport>(
            "SELECT * FROM QuotationImports WHERE Id = @Id", new { Id = id });
    }

    public async Task<int> CreateAsync(QuotationImport item)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var sql = @"INSERT INTO QuotationImports (QuotationNo, CustomerCode, CustomerName, ProductCode, ProductName, 
                        Quantity, UnitPrice, Currency, ImportStatus, ImportedAt, CreatedAt)
                        VALUES (@QuotationNo, @CustomerCode, @CustomerName, @ProductCode, @ProductName, 
                        @Quantity, @UnitPrice, @Currency, @ImportStatus, @ImportedAt, @CreatedAt);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";
            item.CreatedAt = DateTime.Now;
            var id = await conn.ExecuteScalarAsync<int>(sql, item, transaction);
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ImportToErpAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var sql = @"UPDATE QuotationImports SET ImportStatus='已匯入', ImportedAt=@ImportedAt WHERE Id=@Id";
            var rows = await conn.ExecuteAsync(sql, new { Id = id, ImportedAt = DateTime.Now }, transaction);
            await transaction.CommitAsync();
            return rows > 0;
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
            var rows = await conn.ExecuteAsync("DELETE FROM QuotationImports WHERE Id=@Id", new { Id = id }, transaction);
            await transaction.CommitAsync();
            return rows > 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> BatchImportAsync(List<QuotationImport> items)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var count = 0;
            foreach (var item in items)
            {
                item.CreatedAt = DateTime.Now;
                var sql = @"INSERT INTO QuotationImports (QuotationNo, CustomerCode, CustomerName, ProductCode, ProductName, 
                            Quantity, UnitPrice, Currency, ImportStatus, CreatedAt)
                            VALUES (@QuotationNo, @CustomerCode, @CustomerName, @ProductCode, @ProductName, 
                            @Quantity, @UnitPrice, @Currency, @ImportStatus, @CreatedAt)";
                await conn.ExecuteAsync(sql, item, transaction);
                count++;
            }
            await transaction.CommitAsync();
            return count;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
