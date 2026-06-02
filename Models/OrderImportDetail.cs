using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

/// <summary>
/// 訂單匯入 - 單身 (Excel BC欄以後)
/// </summary>
public class OrderImportDetail
{
    public int Id { get; set; }

    /// <summary>關聯單頭 Id</summary>
    public int HeaderId { get; set; }

    /// <summary>單別 (Col BC)</summary>
    [Display(Name = "單別")]
    public string OrderType { get; set; } = string.Empty;

    /// <summary>序號 (Col BD)</summary>
    [Display(Name = "序號")]
    public string? SeqNo { get; set; }

    /// <summary>品號 (Col BE)</summary>
    [Display(Name = "品號")]
    public string? ProductCode { get; set; }

    /// <summary>品名 (Col BF)</summary>
    [Display(Name = "品名")]
    public string? ProductName { get; set; }

    /// <summary>庫別 (Col BG)</summary>
    [Display(Name = "庫別")]
    public string? Warehouse { get; set; }

    /// <summary>訂單數量 (Col BH)</summary>
    [Display(Name = "訂單數量")]
    public decimal OrderQty { get; set; }

    /// <summary>單位 (Col BI)</summary>
    [Display(Name = "單位")]
    public string? Unit { get; set; }

    /// <summary>單價 (Col BJ)</summary>
    [Display(Name = "單價")]
    public decimal UnitPrice { get; set; }

    /// <summary>金額 (Col BK)</summary>
    [Display(Name = "金額")]
    public decimal Amount { get; set; }

    /// <summary>交易幣別 (Col BL)</summary>
    [Display(Name = "交易幣別")]
    public string? Currency { get; set; }

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
