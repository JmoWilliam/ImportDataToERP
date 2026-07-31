using Dapper;
using Microsoft.Data.SqlClient;

namespace ImportDataToERP.Services;

/// <summary>
/// 依登入帳號查詢 ERP 登錄庫 (DSCSYS.dbo.ADMMF/ADMMG) 是否具備指定功能權限
/// </summary>
public class ErpPermissionService
{
    private readonly string _erpConnectionString;

    public ErpPermissionService(string erpConnectionString)
    {
        _erpConnectionString = erpConnectionString;
    }

    public async Task<bool> HasPermissionAsync(string account, string functionCode)
    {
        var sysConnStr = ErpConnectionAccessor.BuildConnectionStringForDatabase(_erpConnectionString, "DSCSYS");
        using var conn = new SqlConnection(sysConnStr);
        var count = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1)
              FROM ADMMF a
              INNER JOIN ADMMG b ON RTRIM(a.MF001) = RTRIM(b.MG001)
              WHERE RTRIM(b.MG002) = @FunctionCode AND RTRIM(b.MG001) = @Account
                AND MG004 = 'Y'
                AND SUBSTRING(MG006,2,1)='Y'",
            new { FunctionCode = functionCode, Account = account });
        return count > 0;
    }
}
