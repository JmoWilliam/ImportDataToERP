using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace ImportDataToERP.Services;

/// <summary>
/// 依使用者 Session 選擇的公司別，動態決定實際連線的 ERP 資料庫 (DSCSYS.dbo.DSCMB.MB003)。
/// 若尚未選擇，退回 appsettings.json 設定的預設 ErpConnection。
/// </summary>
public class ErpConnectionAccessor
{
    public const string SessionKey = "ErpDatabase";
    public const string SessionCompanyNameKey = "ErpCompanyName";

    private readonly string _defaultErpConnectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ErpConnectionAccessor(string defaultErpConnectionString, IHttpContextAccessor httpContextAccessor)
    {
        _defaultErpConnectionString = defaultErpConnectionString;
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetConnectionString()
    {
        var selectedDatabase = _httpContextAccessor.HttpContext?.Session.GetString(SessionKey);
        return string.IsNullOrWhiteSpace(selectedDatabase)
            ? _defaultErpConnectionString
            : BuildConnectionStringForDatabase(_defaultErpConnectionString, selectedDatabase);
    }

    public string? GetSelectedDatabase() => _httpContextAccessor.HttpContext?.Session.GetString(SessionKey);

    public string? GetSelectedCompanyName() => _httpContextAccessor.HttpContext?.Session.GetString(SessionCompanyNameKey);

    public static string BuildConnectionStringForDatabase(string baseConnectionString, string database)
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = database };
        return builder.ConnectionString;
    }
}
