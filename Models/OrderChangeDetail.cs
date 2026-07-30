using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

/// <summary>
/// 訂單交期變更匯入 - 單身
/// 品號/品名/庫別/數量/單位/單價/金額/原交期 皆查詢 ERP COPTD 帶出，僅新交期由使用者輸入
/// </summary>
public class OrderChangeDetail
{
    public int Id { get; set; }

    /// <summary>關聯單頭 Id</summary>
    public int HeaderId { get; set; }

    /// <summary>原訂單明細序號 (COPTD.TD003)</summary>
    [Display(Name = "序號")]
    public string SeqNo { get; set; } = string.Empty;

    [Display(Name = "品號")]
    public string? ProductCode { get; set; }

    [Display(Name = "品名")]
    public string? ProductName { get; set; }

    [Display(Name = "庫別")]
    public string? Warehouse { get; set; }

    [Display(Name = "數量")]
    public decimal? Quantity { get; set; }

    [Display(Name = "單位")]
    public string? Unit { get; set; }

    [Display(Name = "單價")]
    public decimal? UnitPrice { get; set; }

    [Display(Name = "金額")]
    public decimal? Amount { get; set; }

    /// <summary>原交期 (查COPTD帶出)</summary>
    [Display(Name = "原交期")]
    public DateTime? OriginalDeliveryDate { get; set; }

    /// <summary>新交期 (使用者輸入)</summary>
    [Display(Name = "新交期")]
    public DateTime? NewDeliveryDate { get; set; }

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
