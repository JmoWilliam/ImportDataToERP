using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

/// <summary>
/// 訂單匯入 - 單頭 (Excel A~BC欄位)
/// </summary>
public class OrderImportHeader
{
    public int Id { get; set; }

    [Display(Name = "匯入單號")]
    public string ImportBatchNo { get; set; } = string.Empty;

    [Display(Name = "ERP單號")]
    public string? ErpOrderNo { get; set; }

    [Display(Name = "單別")]
    public string OrderType { get; set; } = string.Empty;

    [Display(Name = "單號")]
    public string? OrderNo { get; set; }

    [Display(Name = "客戶代號")]
    public string CustomerCode { get; set; } = string.Empty;

    [Display(Name = "部門代號")]
    public string? DeptCode { get; set; }

    [Display(Name = "備註")]
    public string? Remarks { get; set; }

    [Display(Name = "訂單日期")]
    public DateTime? OrderDate { get; set; }

    [Display(Name = "業務人員")]
    public string? SalesRep { get; set; }

    [Display(Name = "廠別代號")]
    public string? FactoryCode { get; set; }

    [Display(Name = "單據日期")]
    public DateTime? DocDate { get; set; }

    [Display(Name = "課稅別")]
    public string? TaxType { get; set; }

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
