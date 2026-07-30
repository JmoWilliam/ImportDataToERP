using Dapper;
using Microsoft.Data.SqlClient;

namespace ImportDataToERP.Services;

/// <summary>公司別清單 (登錄庫 DSCSYS.dbo.DSCMB)</summary>
public record ErpCompany(string Code, string Name, string Database);

/// <summary>查詢 DSCSYS.dbo.DSCMB 取得可切換的公司別/資料庫清單</summary>
public class ErpCompanyService
{
    private readonly string _erpConnectionString;

    public ErpCompanyService(string erpConnectionString)
    {
        _erpConnectionString = erpConnectionString;
    }

    public async Task<List<ErpCompany>> GetCompaniesAsync()
    {
        var sysConnStr = ErpConnectionAccessor.BuildConnectionStringForDatabase(_erpConnectionString, "DSCSYS");
        using var conn = new SqlConnection(sysConnStr);
        var rows = await conn.QueryAsync<ErpCompany>(
            @"SELECT RTRIM(MB001) AS Code, RTRIM(MB002) AS Name, RTRIM(MB003) AS [Database]
              FROM DSCMB
              WHERE MB003 IS NOT NULL AND RTRIM(MB003) <> ''
              ORDER BY MB002");
        return rows.ToList();
    }
}
