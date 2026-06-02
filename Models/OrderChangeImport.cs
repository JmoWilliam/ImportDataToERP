using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

public class OrderChangeImport
{
    public int Id { get; set; }

    [Display(Name = "原訂單號")]
    public string OriginalOrderNo { get; set; } = string.Empty;

    [Display(Name = "變更單號")]
    public string ChangeNo { get; set; } = string.Empty;

    [Display(Name = "客戶編號")]
    public string CustomerCode { get; set; } = string.Empty;

    [Display(Name = "客戶名稱")]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "品號")]
    public string ProductCode { get; set; } = string.Empty;

    [Display(Name = "品名")]
    public string ProductName { get; set; } = string.Empty;

    [Display(Name = "變更類型")]
    public string ChangeType { get; set; } = string.Empty; // 數量變更、單價變更、交期變更、取消

    [Display(Name = "原數量")]
    public decimal? OriginalQuantity { get; set; }

    [Display(Name = "新數量")]
    public decimal? NewQuantity { get; set; }

    [Display(Name = "原單價")]
    public decimal? OriginalUnitPrice { get; set; }

    [Display(Name = "新單價")]
    public decimal? NewUnitPrice { get; set; }

    [Display(Name = "原交期")]
    public DateTime? OriginalDeliveryDate { get; set; }

    [Display(Name = "新交期")]
    public DateTime? NewDeliveryDate { get; set; }

    [Display(Name = "變更原因")]
    public string? ChangeReason { get; set; }

    [Display(Name = "匯入狀態")]
    public string ImportStatus { get; set; } = "待匯入";

    [Display(Name = "匯入時間")]
    public DateTime? ImportedAt { get; set; }

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
