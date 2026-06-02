using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

/// <summary>
/// 訂單變更匯入 - 單頭 (參考 SoModification.cshtml)
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
    public string? SoErpPrefix { get; set; }

    /// <summary>原訂單單號</summary>
    [Display(Name = "原訂單單號")]
    public string? SoErpNo { get; set; }

    /// <summary>原訂單完整號碼 (Prefix+No)</summary>
    [Display(Name = "原訂單號")]
    public string OriginalOrderNo { get; set; } = string.Empty;

    /// <summary>單據日期</summary>
    [Display(Name = "單據日期")]
    public DateTime? DocDate { get; set; }

    /// <summary>客戶採購單</summary>
    [Display(Name = "客戶採購單")]
    public string? CustomerPurchaseOrder { get; set; }

    [Display(Name = "客戶代號")]
    public string CustomerCode { get; set; } = string.Empty;

    [Display(Name = "客戶名稱")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>客戶聯絡地址(一)</summary>
    [Display(Name = "客戶地址(一)")]
    public string? CustomerAddressFirst { get; set; }

    /// <summary>客戶聯絡地址(二)</summary>
    [Display(Name = "客戶地址(二)")]
    public string? CustomerAddressSecond { get; set; }

    [Display(Name = "部門代號")]
    public string? DepartmentId { get; set; }

    [Display(Name = "業務人員")]
    public string? SalesRep { get; set; }

    [Display(Name = "備註")]
    public string? Remarks { get; set; }

    /// <summary>變更類型 (數量變更/單價變更/交期變更/取消)</summary>
    [Display(Name = "變更類型")]
    public string ChangeType { get; set; } = string.Empty;

    [Display(Name = "變更原因")]
    public string? ChangeReason { get; set; }

    // === 客戶基本資料 (collapsible) ===

    /// <summary>交易幣別</summary>
    [Display(Name = "交易幣別")]
    public string? Currency { get; set; }

    /// <summary>匯率</summary>
    [Display(Name = "匯率")]
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>交易條件 (1.FOB 2.CIF 等)</summary>
    [Display(Name = "交易條件")]
    public string? TradeTerm { get; set; }

    /// <summary>稅別碼</summary>
    [Display(Name = "稅別碼")]
    public string? TaxNo { get; set; }

    /// <summary>課稅別 (1.內含 2.外加 等)</summary>
    [Display(Name = "課稅別")]
    public string? Taxation { get; set; }

    /// <summary>營業稅率</summary>
    [Display(Name = "營業稅率")]
    public decimal BusinessTaxRate { get; set; }

    /// <summary>付款條件</summary>
    [Display(Name = "付款條件")]
    public string? PaymentTerm { get; set; }

    /// <summary>價格條件</summary>
    [Display(Name = "價格條件")]
    public string? PriceTerm { get; set; }

    /// <summary>訂金分批 (Y/N)</summary>
    [Display(Name = "訂金分批")]
    public string DepositPartial { get; set; } = "N";

    /// <summary>訂金比率</summary>
    [Display(Name = "訂金比率")]
    public decimal DepositRate { get; set; }

    /// <summary>單身多稅率 (Y/N)</summary>
    [Display(Name = "單身多稅率")]
    public string DetailMultiTax { get; set; } = "N";

    /// <summary>運輸方式 (1.空運 2.海運 等)</summary>
    [Display(Name = "運輸方式")]
    public string? ShipMethod { get; set; }

    /// <summary>整張結案 (Y/N)</summary>
    [Display(Name = "整張結案")]
    public string ClosureStatus { get; set; } = "N";

    /// <summary>確認碼 (Y/N/V)</summary>
    [Display(Name = "確認碼")]
    public string ConfirmStatus { get; set; } = "N";

    // === 狀態 ===

    [Display(Name = "明細筆數")]
    public int DetailCount { get; set; }

    [Display(Name = "匯入狀態")]
    public string ImportStatus { get; set; } = "待匯入";

    [Display(Name = "匯入時間")]
    public DateTime? ImportedAt { get; set; }

    [Display(Name = "拋轉ERP狀態")]
    public int TransferStatus { get; set; } = 1;

    [Display(Name = "拋轉訊息")]
    public string? TransferMessage { get; set; }

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
