using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

/// <summary>
/// 訂單交期變更匯入 - 單頭
/// </summary>
public class OrderChangeHeader
{
    public int Id { get; set; }

    [Display(Name = "匯入單號")]
    public string ImportBatchNo { get; set; } = string.Empty;

    [Display(Name = "ERP單號")]
    public string? ErpOrderNo { get; set; }

    /// <summary>原訂單單別</summary>
    [Display(Name = "原訂單單別")]
    public string SoErpPrefix { get; set; } = string.Empty;

    /// <summary>原訂單單號</summary>
    [Display(Name = "原訂單單號")]
    public string SoErpNo { get; set; } = string.Empty;

    /// <summary>原訂單完整號碼 (Prefix+No)</summary>
    [Display(Name = "原訂單號")]
    public string OriginalOrderNo { get; set; } = string.Empty;

    [Display(Name = "明細筆數")]
    public int DetailCount { get; set; }

    [Display(Name = "匯入狀態")]
    public string ImportStatus { get; set; } = "待匯入";

    [Display(Name = "匯入時間")]
    public DateTime? ImportedAt { get; set; }

    /// <summary>拋轉ERP狀態: 1=未拋轉 2=已拋轉 3=拋轉失敗</summary>
    [Display(Name = "拋轉ERP狀態")]
    public int TransferStatus { get; set; } = 1;

    /// <summary>拋轉失敗訊息</summary>
    [Display(Name = "拋轉訊息")]
    public string? TransferMessage { get; set; }

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
