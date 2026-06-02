namespace ImportDataToERP.Models;

/// <summary>
/// 訂單變更匯入 Excel 預覽 ViewModel
/// </summary>
public class OrderChangeViewModel
{
    /// <summary>檔案名稱</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>解析後的單頭清單(含明細)</summary>
    public List<OrderChangeHeaderGroup> HeaderGroups { get; set; } = new();

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
public class OrderChangeHeaderGroup
{
    public OrderChangeHeader Header { get; set; } = new();
    public List<OrderChangeDetail> Details { get; set; } = new();
}
