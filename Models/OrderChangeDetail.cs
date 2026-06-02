using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

/// <summary>
/// 訂單變更匯入 - 單身
/// </summary>
public class OrderChangeDetail
{
    public int Id { get; set; }

    /// <summary>關聯單頭 Id</summary>
    public int HeaderId { get; set; }

    [Display(Name = "品號")]
    public string? ProductCode { get; set; }

    [Display(Name = "品名")]
    public string? ProductName { get; set; }

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

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
