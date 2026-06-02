using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

public class QuotationImport
{
    public int Id { get; set; }

    [Display(Name = "報價單號")]
    public string QuotationNo { get; set; } = string.Empty;

    [Display(Name = "客戶編號")]
    public string CustomerCode { get; set; } = string.Empty;

    [Display(Name = "客戶名稱")]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "品號")]
    public string ProductCode { get; set; } = string.Empty;

    [Display(Name = "品名")]
    public string ProductName { get; set; } = string.Empty;

    [Display(Name = "數量")]
    public decimal Quantity { get; set; }

    [Display(Name = "單價")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "幣別")]
    public string Currency { get; set; } = "TWD";

    [Display(Name = "匯入狀態")]
    public string ImportStatus { get; set; } = "待匯入";

    [Display(Name = "匯入時間")]
    public DateTime? ImportedAt { get; set; }

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
