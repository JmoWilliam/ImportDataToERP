namespace ImportDataToERP.Models;

/// <summary>
/// 訂單匯入 Excel 預覽 ViewModel
/// </summary>
public class OrderImportViewModel
{
    /// <summary>檔案名稱</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>解析後的單頭清單(含明細)</summary>
    public List<OrderImportHeaderGroup> HeaderGroups { get; set; } = new();

    /// <summary>總筆數(明細行數)</summary>
    public int TotalDetailRows { get; set; }

    /// <summary>總單頭數</summary>
    public int TotalHeaders { get; set; }

    /// <summary>檢核錯誤訊息</summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 單頭 + 所屬明細群組
/// </summary>
public class OrderImportHeaderGroup
{
    public OrderImportHeader Header { get; set; } = new();
    public List<OrderImportDetail> Details { get; set; } = new();
}
