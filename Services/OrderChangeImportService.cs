using System.Data;
using System.Transactions;
using ClosedXML.Excel;
using Dapper;
using ImportDataToERP.Data;
using ImportDataToERP.Models;
using Microsoft.Data.SqlClient;

namespace ImportDataToERP.Services;

public class OrderChangeImportService
{
    private readonly DbConnectionFactory _db;
    private readonly string _erpConnectionString;

    public OrderChangeImportService(DbConnectionFactory db, string erpConnectionString)
    {
        _db = db;
        _erpConnectionString = erpConnectionString;
    }

    // ========== 查詢 ==========

    public async Task<IEnumerable<OrderChangeHeader>> GetAllHeadersAsync()
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            SELECT h.*, ISNULL(d.DetailCount, 0) AS DetailCount
            FROM OrderChangeHeaders h
            LEFT JOIN (
                SELECT HeaderId, COUNT(*) AS DetailCount
                FROM OrderChangeDetails
                GROUP BY HeaderId
            ) d ON d.HeaderId = h.Id
            ORDER BY h.Id DESC";
        return await conn.QueryAsync<OrderChangeHeader>(sql);
    }

    public async Task<OrderChangeHeader?> GetHeaderByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<OrderChangeHeader>(
            "SELECT * FROM OrderChangeHeaders WHERE Id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<OrderChangeDetail>> GetDetailsByHeaderIdAsync(int headerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<OrderChangeDetail>(
            "SELECT * FROM OrderChangeDetails WHERE HeaderId = @HeaderId ORDER BY Id",
            new { HeaderId = headerId });
    }

    // ========== 單頭 CRUD ==========

    public async Task<int> CreateHeaderAsync(OrderChangeHeader header)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            header.ImportBatchNo = await GenerateBatchNoAsync(conn, transaction, today);
            header.ImportStatus = "已匯入";
            header.ImportedAt = DateTime.Now;
            header.CreatedAt = DateTime.Now;

            var sql = @"
                INSERT INTO OrderChangeHeaders 
                    (ImportBatchNo, ErpOrderNo, ChangeType,
                     SoErpPrefix, SoErpNo, OriginalOrderNo,
                     DocDate, CustomerPurchaseOrder, CustomerCode, CustomerName,
                     CustomerAddressFirst, CustomerAddressSecond,
                     DepartmentId, SalesRep,
                     Remarks, ChangeReason,
                     Currency, ExchangeRate, TradeTerm,
                     TaxNo, Taxation, BusinessTaxRate,
                     PaymentTerm, PriceTerm,
                     DepositPartial, DepositRate, DetailMultiTax, ShipMethod,
                     ClosureStatus, ConfirmStatus,
                     ImportStatus, ImportedAt, TransferStatus, CreatedAt)
                VALUES 
                    (@ImportBatchNo, @ErpOrderNo, @ChangeType,
                     @SoErpPrefix, @SoErpNo, @OriginalOrderNo,
                     @DocDate, @CustomerPurchaseOrder, @CustomerCode, @CustomerName,
                     @CustomerAddressFirst, @CustomerAddressSecond,
                     @DepartmentId, @SalesRep,
                     @Remarks, @ChangeReason,
                     @Currency, @ExchangeRate, @TradeTerm,
                     @TaxNo, @Taxation, @BusinessTaxRate,
                     @PaymentTerm, @PriceTerm,
                     @DepositPartial, @DepositRate, @DetailMultiTax, @ShipMethod,
                     @ClosureStatus, @ConfirmStatus,
                     @ImportStatus, @ImportedAt, @TransferStatus, @CreatedAt);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await conn.ExecuteScalarAsync<int>(sql, header, transaction);
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateHeaderAsync(OrderChangeHeader header)
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            UPDATE OrderChangeHeaders SET
                ChangeType             = @ChangeType,
                SoErpPrefix            = @SoErpPrefix,
                SoErpNo                = @SoErpNo,
                OriginalOrderNo        = @OriginalOrderNo,
                DocDate                = @DocDate,
                CustomerPurchaseOrder  = @CustomerPurchaseOrder,
                CustomerCode           = @CustomerCode,
                CustomerName           = @CustomerName,
                CustomerAddressFirst   = @CustomerAddressFirst,
                CustomerAddressSecond  = @CustomerAddressSecond,
                DepartmentId           = @DepartmentId,
                SalesRep               = @SalesRep,
                Remarks                = @Remarks,
                ChangeReason           = @ChangeReason,
                Currency               = @Currency,
                ExchangeRate           = @ExchangeRate,
                TradeTerm              = @TradeTerm,
                TaxNo                  = @TaxNo,
                Taxation               = @Taxation,
                BusinessTaxRate        = @BusinessTaxRate,
                PaymentTerm            = @PaymentTerm,
                PriceTerm              = @PriceTerm,
                DepositPartial         = @DepositPartial,
                DepositRate            = @DepositRate,
                DetailMultiTax         = @DetailMultiTax,
                ShipMethod             = @ShipMethod,
                ClosureStatus          = @ClosureStatus,
                ConfirmStatus          = @ConfirmStatus
            WHERE Id = @Id";
        return await conn.ExecuteAsync(sql, header) > 0;
    }

    public async Task<bool> DeleteHeaderAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync("DELETE FROM OrderChangeDetails WHERE HeaderId = @Id", new { Id = id }, transaction);
            var rows = await conn.ExecuteAsync("DELETE FROM OrderChangeHeaders WHERE Id = @Id", new { Id = id }, transaction);
            await transaction.CommitAsync();
            return rows > 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ========== 單身 CRUD ==========

    public async Task<int> CreateDetailAsync(OrderChangeDetail detail)
    {
        using var conn = _db.CreateConnection();
        detail.CreatedAt = DateTime.Now;
        var sql = @"
            INSERT INTO OrderChangeDetails
                (HeaderId, ProductCode, ProductName,
                 OriginalQuantity, NewQuantity,
                 OriginalUnitPrice, NewUnitPrice,
                 OriginalDeliveryDate, NewDeliveryDate, CreatedAt)
            VALUES
                (@HeaderId, @ProductCode, @ProductName,
                 @OriginalQuantity, @NewQuantity,
                 @OriginalUnitPrice, @NewUnitPrice,
                 @OriginalDeliveryDate, @NewDeliveryDate, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await conn.ExecuteScalarAsync<int>(sql, detail);
    }

    public async Task<bool> UpdateDetailAsync(OrderChangeDetail detail)
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            UPDATE OrderChangeDetails SET
                ProductCode         = @ProductCode,
                ProductName         = @ProductName,
                OriginalQuantity    = @OriginalQuantity,
                NewQuantity         = @NewQuantity,
                OriginalUnitPrice   = @OriginalUnitPrice,
                NewUnitPrice        = @NewUnitPrice,
                OriginalDeliveryDate = @OriginalDeliveryDate,
                NewDeliveryDate     = @NewDeliveryDate
            WHERE Id = @Id";
        return await conn.ExecuteAsync(sql, detail) > 0;
    }

    public async Task<bool> DeleteDetailAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM OrderChangeDetails WHERE Id = @Id", new { Id = id }) > 0;
    }

    // ========== Excel 解析 / 匯入 / 範本 ==========

    public async Task<OrderChangeViewModel> ParseExcelAsync(Stream fileStream, string fileName)
    {
        var result = new OrderChangeViewModel { FileName = fileName };
        var rawRows = new List<(OrderChangeHeader hdr, OrderChangeDetail dtl)>();

        using var wb = new XLWorkbook(fileStream);
        var ws = wb.Worksheet(1);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 2;
        if (lastRow < 3)
        {
            result.Errors.Add("Excel 無資料列 (資料應從第3列開始)");
            return result;
        }

        for (int row = 3; row <= lastRow; row++)
        {
            var header = new OrderChangeHeader
            {
                SoErpPrefix           = ws.Cell(row, 2).GetString().Trim(),
                SoErpNo               = ws.Cell(row, 3).GetString().NullIfEmpty(),
                OriginalOrderNo       = (ws.Cell(row, 2).GetString().Trim() + ws.Cell(row, 3).GetString().Trim()),
                DocDate               = ParseDateTime(ws.Cell(row, 4)),
                CustomerPurchaseOrder  = ws.Cell(row, 5).GetString().NullIfEmpty(),
                CustomerCode           = ws.Cell(row, 6).GetString().Trim(),
                CustomerName           = ws.Cell(row, 7).GetString().Trim(),
                CustomerAddressFirst   = ws.Cell(row, 8).GetString().NullIfEmpty(),
                CustomerAddressSecond  = ws.Cell(row, 9).GetString().NullIfEmpty(),
                DepartmentId           = ws.Cell(row, 10).GetString().NullIfEmpty(),
                SalesRep               = ws.Cell(row, 11).GetString().NullIfEmpty(),
                Remarks                = ws.Cell(row, 12).GetString().NullIfEmpty(),
                ChangeReason           = ws.Cell(row, 13).GetString().NullIfEmpty(),
                Currency               = ws.Cell(row, 14).GetString().NullIfEmpty(),
                ExchangeRate           = ws.Cell(row, 15).TryGetValue(out double er) ? (decimal)er : 1m,
                TradeTerm              = ws.Cell(row, 16).GetString().NullIfEmpty(),
                TaxNo                  = ws.Cell(row, 17).GetString().NullIfEmpty(),
                Taxation               = ws.Cell(row, 18).GetString().NullIfEmpty(),
                BusinessTaxRate        = ws.Cell(row, 19).TryGetValue(out double btr) ? (decimal)btr : 0m,
                PaymentTerm            = ws.Cell(row, 20).GetString().NullIfEmpty(),
                PriceTerm              = ws.Cell(row, 21).GetString().NullIfEmpty(),
                DepositPartial         = ws.Cell(row, 22).GetString().NullIfEmpty() ?? "N",
                DepositRate            = ws.Cell(row, 23).TryGetValue(out double dr) ? (decimal)dr : 0m,
                DetailMultiTax         = ws.Cell(row, 24).GetString().NullIfEmpty() ?? "N",
                ShipMethod             = ws.Cell(row, 25).GetString().NullIfEmpty(),
                ClosureStatus          = ws.Cell(row, 26).GetString().NullIfEmpty() ?? "N",
                ConfirmStatus          = ws.Cell(row, 27).GetString().NullIfEmpty() ?? "N",
            };

            var detail = new OrderChangeDetail
            {
                ProductCode          = ws.Cell(row, 55).GetString().NullIfEmpty(),
                ProductName          = ws.Cell(row, 56).GetString().NullIfEmpty(),
                OriginalQuantity     = ws.Cell(row, 57).TryGetValue(out double oq) ? (decimal)oq : null,
                NewQuantity          = ws.Cell(row, 58).TryGetValue(out double nq) ? (decimal)nq : null,
                OriginalUnitPrice    = ws.Cell(row, 59).TryGetValue(out double oup) ? (decimal)oup : null,
                NewUnitPrice         = ws.Cell(row, 60).TryGetValue(out double nup) ? (decimal)nup : null,
                OriginalDeliveryDate  = ParseDateTime(ws.Cell(row, 61)),
                NewDeliveryDate       = ParseDateTime(ws.Cell(row, 62)),
            };

            if (string.IsNullOrWhiteSpace(header.CustomerCode) && string.IsNullOrWhiteSpace(header.SoErpPrefix))
                continue;

            rawRows.Add((header, detail));
        }

        if (rawRows.Count == 0)
        {
            result.Errors.Add("Excel 無有效資料列");
            return result;
        }

        await FillMissingProductNamesAsync(rawRows.Select(r => r.dtl));

        var groups = rawRows
            .GroupBy(r => new
            {
                r.hdr.CustomerCode,
                OriginalOrderNo = r.hdr.OriginalOrderNo,
                DocDate = r.hdr.DocDate?.Date
            })
            .ToList();

        foreach (var g in groups)
        {
            var firstHdr = g.First().hdr;
            firstHdr.Remarks = g.Select(r => r.hdr.Remarks)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
            firstHdr.ChangeReason = g.Select(r => r.hdr.ChangeReason)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
            firstHdr.DetailCount = g.Count();

            result.HeaderGroups.Add(new OrderChangeHeaderGroup
            {
                Header = firstHdr,
                Details = g.Select(r => r.dtl).ToList()
            });
        }

        result.TotalHeaders = result.HeaderGroups.Count;
        result.TotalDetailRows = rawRows.Count;

        return result;
    }

    public async Task<(int SuccessHeaders, int SuccessDetails, List<string> Errors)>
        ConfirmImportAsync(List<OrderChangeHeaderGroup> groups)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        int headersDone = 0, detailsDone = 0;
        var errors = new List<string>();

        try
        {
            var today = DateTime.Now.ToString("yyyyMMdd");

            foreach (var (group, idx) in groups.Select((g, i) => (g, i)))
            {
                var hdr = group.Header;

                var dupCheckSql = @"
                    SELECT COUNT(1) FROM OrderChangeHeaders
                    WHERE CustomerCode = @CustomerCode
                      AND OriginalOrderNo = @OriginalOrderNo
                      AND DocDate = @DocDate
                      AND ImportStatus = N'已匯入'";
                var dupCount = await conn.ExecuteScalarAsync<int>(dupCheckSql, new
                {
                    hdr.CustomerCode,
                    hdr.OriginalOrderNo,
                    hdr.DocDate
                }, transaction);

                if (dupCount > 0)
                {
                    errors.Add($"第{idx + 1}組：客戶[{hdr.CustomerCode}] 原單號[{hdr.OriginalOrderNo}] 日期[{hdr.DocDate:yyyy-MM-dd}] 已重複匯入，略過");
                    continue;
                }

                var batchNo = await GenerateBatchNoAsync(conn, transaction, today);

                var insertHeaderSql = @"
                    INSERT INTO OrderChangeHeaders 
                        (ImportBatchNo, ErpOrderNo, ChangeType,
                         SoErpPrefix, SoErpNo, OriginalOrderNo,
                         DocDate, CustomerPurchaseOrder, CustomerCode, CustomerName,
                         CustomerAddressFirst, CustomerAddressSecond,
                         DepartmentId, SalesRep,
                         Remarks, ChangeReason,
                         Currency, ExchangeRate, TradeTerm,
                         TaxNo, Taxation, BusinessTaxRate,
                         PaymentTerm, PriceTerm,
                         DepositPartial, DepositRate, DetailMultiTax, ShipMethod,
                         ClosureStatus, ConfirmStatus,
                         ImportStatus, ImportedAt, TransferStatus, CreatedAt)
                    VALUES 
                        (@ImportBatchNo, @ErpOrderNo, @ChangeType,
                         @SoErpPrefix, @SoErpNo, @OriginalOrderNo,
                         @DocDate, @CustomerPurchaseOrder, @CustomerCode, @CustomerName,
                         @CustomerAddressFirst, @CustomerAddressSecond,
                         @DepartmentId, @SalesRep,
                         @Remarks, @ChangeReason,
                         @Currency, @ExchangeRate, @TradeTerm,
                         @TaxNo, @Taxation, @BusinessTaxRate,
                         @PaymentTerm, @PriceTerm,
                         @DepositPartial, @DepositRate, @DetailMultiTax, @ShipMethod,
                         @ClosureStatus, @ConfirmStatus,
                         @ImportStatus, @ImportedAt, @TransferStatus, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                hdr.ImportBatchNo = batchNo;
                hdr.ImportStatus = "已匯入";
                hdr.ImportedAt = DateTime.Now;
                hdr.CreatedAt = DateTime.Now;
                hdr.TransferStatus = 1;

                var headerId = await conn.ExecuteScalarAsync<int>(insertHeaderSql, hdr, transaction);
                headersDone++;

                foreach (var dtl in group.Details)
                {
                    dtl.HeaderId = headerId;
                    dtl.CreatedAt = DateTime.Now;

                    var insertDetailSql = @"
                        INSERT INTO OrderChangeDetails
                            (HeaderId, ProductCode, ProductName,
                             OriginalQuantity, NewQuantity,
                             OriginalUnitPrice, NewUnitPrice,
                             OriginalDeliveryDate, NewDeliveryDate, CreatedAt)
                        VALUES
                            (@HeaderId, @ProductCode, @ProductName,
                             @OriginalQuantity, @NewQuantity,
                             @OriginalUnitPrice, @NewUnitPrice,
                             @OriginalDeliveryDate, @NewDeliveryDate, @CreatedAt)";

                    await conn.ExecuteAsync(insertDetailSql, dtl, transaction);
                    detailsDone++;
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return (headersDone, detailsDone, errors);
    }

    public byte[] GenerateTemplateExcel()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("批次匯入變更表");

        ws.Cell(1, 1).Value = "說明欄";
        ws.Cell(1, 8).Value = "地址字數建議200以內";
        ws.Cell(1, 15).Value = "ex:31.5";
        ws.Cell(1, 18).Value = "1.內含 2.外加 3.零稅率 4.免稅 9.不計稅";
        ws.Cell(1, 22).Value = "Y/N";
        ws.Cell(1, 24).Value = "Y/N";
        ws.Cell(1, 26).Value = "Y/N";
        ws.Cell(1, 27).Value = "Y/N/V";

        ws.Cell(2, 2).Value = "原單別";
        ws.Cell(2, 3).Value = "原單號";
        ws.Cell(2, 4).Value = "單據日期";
        ws.Cell(2, 5).Value = "客戶採購單";
        ws.Cell(2, 6).Value = "客戶代號";
        ws.Cell(2, 7).Value = "客戶名稱";
        ws.Cell(2, 8).Value = "地址(一)";
        ws.Cell(2, 9).Value = "地址(二)";
        ws.Cell(2, 10).Value = "部門代號";
        ws.Cell(2, 11).Value = "業務人員";
        ws.Cell(2, 12).Value = "備註";
        ws.Cell(2, 13).Value = "變更原因";
        ws.Cell(2, 14).Value = "幣別";
        ws.Cell(2, 15).Value = "匯率";
        ws.Cell(2, 16).Value = "交易條件";
        ws.Cell(2, 17).Value = "稅別碼";
        ws.Cell(2, 18).Value = "課稅別";
        ws.Cell(2, 19).Value = "營業稅率";
        ws.Cell(2, 20).Value = "付款條件";
        ws.Cell(2, 21).Value = "價格條件";
        ws.Cell(2, 22).Value = "訂金分批";
        ws.Cell(2, 23).Value = "訂金比率";
        ws.Cell(2, 24).Value = "單身多稅率";
        ws.Cell(2, 25).Value = "運輸方式";
        ws.Cell(2, 26).Value = "整張結案";
        ws.Cell(2, 27).Value = "確認碼";

        ws.Cell(2, 55).Value = "品號";
        ws.Cell(2, 56).Value = "品名";
        ws.Cell(2, 57).Value = "原數量";
        ws.Cell(2, 58).Value = "新數量";
        ws.Cell(2, 59).Value = "原單價";
        ws.Cell(2, 60).Value = "新單價";
        ws.Cell(2, 61).Value = "原交期";
        ws.Cell(2, 62).Value = "新交期";

        var headerRange = ws.Range(2, 2, 2, 27);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var detailRange = ws.Range(2, 55, 2, 62);
        detailRange.Style.Font.Bold = true;
        detailRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        ws.Columns(2, 27).Width = 14;
        ws.Columns(55, 62).Width = 14;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ========== 拋轉 ERP (訂單變更單 → COPTE / COPTF) ==========

    public async Task<string> TransferToErpAsync(int headerId)
    {
        var header = await GetHeaderByIdAsync(headerId);
        if (header == null) return "單據不存在";

        if (header.TransferStatus == 2)
            return "此單據已拋轉過ERP";

        var details = (await GetDetailsByHeaderIdAsync(headerId)).ToList();
        if (details.Count == 0) return "無明細資料，無法拋轉";

        if (details.Any(d => string.IsNullOrWhiteSpace(d.ProductCode)))
            return "部分明細無品號，無法拋轉";

        var dateNow = DateTime.Now.ToString("yyyyMMdd");
        var timeNow = DateTime.Now.ToString("HH:mm:ss");
        var erpPrefix = header.SoErpPrefix ?? "CHG";
        var erpNo = "";
        string companyNo = "";

        // 前置：字串截斷 helper
        string Trunc(string? s) =>
            s == null ? "" : s.Length > 255 ? s[..255] : s;

        // Defaults
        var docDateStr = header.DocDate?.ToString("yyyyMMdd") ?? "";
        var changeReason = Trunc(header.ChangeReason);
        var remarks = Trunc(header.Remarks);

        try
        {
            // === Phase 1: ERP 端操作 (TransactionScope 包裹) ===
            {
                using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                using var erpConn = new SqlConnection(_erpConnectionString);
                await erpConn.OpenAsync();

                // 取得廠別 (CMSMB)
                var factoryResult = await erpConn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT TOP 1 RTRIM(LTRIM(MB001)) MB001 FROM CMSMB WHERE COMPANY = (SELECT TOP 1 COMPANY FROM CMSMB)");
                if (factoryResult != null)
                {
                    companyNo = factoryResult.MB001 ?? "";
                }

                // 檢查單據設定 CMSMQ（用 COPTE 的單別查詢編碼規則）
                var docSetting = await erpConn.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT MQ004, MQ005, MQ006 FROM CMSMQ WHERE MQ001 = @ErpPrefix",
                    new { ErpPrefix = erpPrefix });

                string encode = "3";
                int yearLength = 0, lineLength = 4;
                if (docSetting != null)
                {
                    encode = docSetting.MQ004?.ToString() ?? "3";
                    yearLength = Convert.ToInt32(docSetting.MQ005 ?? 0);
                    lineLength = Convert.ToInt32(docSetting.MQ006 ?? 4);
                }

                // 產生 ERP 單號 TE002（查 COPTE 表，非 COPTC）
                var refDate = header.DocDate ?? DateTime.Now;
                string datePart = encode switch
                {
                    "1" => refDate.ToString(new string('y', Math.Max(yearLength, 1)) + "MMdd"),
                    "2" => refDate.ToString(new string('y', Math.Max(yearLength, 1)) + "MM"),
                    "4" => header.SoErpNo ?? "",
                    _ => ""
                };

                var numResult = await erpConn.QueryFirstOrDefaultAsync<dynamic>(
                    $@"SELECT CONVERT(INT, RIGHT(ISNULL(MAX(RTRIM(LTRIM(TE002))), '{new string('0', lineLength)}'), {lineLength})) + 1 CurrentNum
                       FROM COPTE WHERE TE001 = @ErpPrefix" +
                       (string.IsNullOrEmpty(datePart) ? "" : " AND RTRIM(LTRIM(TE002)) LIKE @DatePart + '" + new string('_', lineLength) + "'"),
                    new { ErpPrefix = erpPrefix, DatePart = datePart });

                int currentNum = numResult != null ? Convert.ToInt32(numResult.CurrentNum) : 1;
                erpNo = datePart + currentNum.ToString(new string('0', lineLength));

                // ========== 寫入 COPTE (訂單變更單頭) ==========
                // TE001-TE082: 新值, TE103-TE182: 原值 (無 COPTC 查詢故映射相同)
                var coptEParams = new
                {
                    COMPANY = companyNo, CREATOR = "IMPORT", USR_GROUP = "",
                    CREATE_DATE = dateNow, CREATE_TIME = timeNow, CREATE_AP = "IMPORT", CREATE_PRID = "BM",
                    MODIFIER = "", MODI_DATE = "", MODI_TIME = "", MODI_AP = "", MODI_PRID = "", FLAG = "1",

                    // ---- 新值 TE001-TE082 ----
                    TE001 = erpPrefix,
                    TE002 = erpNo,
                    TE003 = "0001",
                    TE004 = docDateStr,
                    TE005 = header.ClosureStatus ?? "N",
                    TE006 = changeReason,
                    TE007 = header.CustomerCode ?? "",
                    TE008 = header.DepartmentId ?? "",
                    TE009 = header.SalesRep ?? "",
                    TE010 = "",
                    TE011 = header.Currency ?? "",
                    TE012 = header.ExchangeRate,
                    TE013 = header.CustomerAddressFirst ?? "",
                    TE014 = header.CustomerAddressSecond ?? "",
                    TE015 = header.CustomerPurchaseOrder ?? "",
                    TE016 = header.PriceTerm ?? "",
                    TE017 = header.PaymentTerm ?? "",
                    TE018 = header.Taxation ?? "1",
                    TE019 = "", TE020 = "", TE021 = "", TE022 = "", TE023 = "",
                    TE024 = "", TE025 = "", TE026 = "", TE027 = "", TE028 = "",
                    TE029 = header.ConfirmStatus ?? "N",
                    TE030 = 0,
                    TE031 = "", TE032 = "", TE033 = "", TE034 = "",
                    TE035 = "", TE036 = "", TE037 = "",
                    TE038 = docDateStr,
                    TE039 = "",
                    TE040 = header.BusinessTaxRate,
                    TE041 = "",
                    TE042 = header.DepositRate,
                    TE043 = "",
                    TE044 = "N",
                    TE045 = "N",
                    TE046 = 0,
                    TE047 = "", TE048 = "", TE049 = "",
                    TE050 = remarks,
                    TE051 = "", TE052 = 0, TE053 = "", TE054 = "N",
                    TE055 = header.CustomerName ?? "",
                    TE056 = "", TE057 = "", TE058 = "", TE059 = "", TE060 = "",
                    TE061 = header.TradeTerm ?? "",
                    TE062 = "",
                    TE063 = 0, TE064 = "", TE065 = 0, TE066 = "", TE067 = "",
                    TE068 = "N",
                    TE069 = header.TaxNo ?? "",
                    TE070 = "", TE071 = "", TE072 = "", TE073 = "", TE074 = "",
                    TE075 = "", TE076 = "", TE077 = "", TE078 = "",
                    TE079 = header.DepositPartial ?? "N",
                    TE080 = header.DetailMultiTax ?? "N",
                    TE081 = "",
                    TE082 = "",

                    // ---- 原值 TE103-TE182（映射同新值，無 COPTC 可查）----
                    TE103 = "0001",
                    TE104 = docDateStr,
                    TE105 = header.ClosureStatus ?? "N",
                    TE106 = changeReason,
                    TE107 = header.CustomerCode ?? "",
                    TE108 = header.DepartmentId ?? "",
                    TE109 = header.SalesRep ?? "",
                    TE110 = "",
                    TE111 = header.Currency ?? "",
                    TE112 = header.ExchangeRate,
                    TE113 = header.CustomerAddressFirst ?? "",
                    TE114 = header.CustomerAddressSecond ?? "",
                    TE115 = header.CustomerPurchaseOrder ?? "",
                    TE116 = header.PriceTerm ?? "",
                    TE117 = header.PaymentTerm ?? "",
                    TE118 = header.Taxation ?? "1",
                    TE119 = "", TE120 = "", TE121 = "", TE122 = "", TE123 = "",
                    TE124 = "", TE125 = "", TE126 = "", TE127 = "", TE128 = "",
                    TE129 = header.ConfirmStatus ?? "N",
                    TE130 = 0,
                    TE131 = "", TE132 = "", TE133 = "", TE134 = "",
                    TE135 = "", TE136 = "", TE137 = "",
                    TE138 = docDateStr,
                    TE139 = "",
                    TE140 = header.BusinessTaxRate,
                    TE141 = "",
                    TE142 = header.DepositRate,
                    TE143 = "",
                    TE144 = "N",
                    TE145 = "N",
                    TE146 = 0,
                    TE147 = "", TE148 = "", TE149 = "",
                    TE150 = remarks,
                    TE151 = "", TE152 = 0, TE153 = "", TE154 = "N",
                    TE155 = header.CustomerName ?? "",
                    TE156 = "", TE157 = "", TE158 = "", TE159 = "", TE160 = "",
                    TE161 = header.TradeTerm ?? "",
                    TE162 = "",
                    TE163 = 0, TE164 = "", TE165 = 0, TE166 = "", TE167 = "",
                    TE168 = "N",
                    TE169 = header.TaxNo ?? "",
                    TE170 = "", TE171 = "", TE172 = "", TE173 = "", TE174 = "",
                    TE175 = "", TE176 = "", TE177 = "", TE178 = "",
                    TE179 = header.DepositPartial ?? "N",
                    TE180 = header.DetailMultiTax ?? "N",
                    TE181 = "",
                    TE182 = "",
                };

                await erpConn.ExecuteAsync(@"
                    INSERT INTO COPTE (COMPANY, CREATOR, USR_GROUP, CREATE_DATE, CREATE_TIME, CREATE_AP, CREATE_PRID
                        , MODIFIER, MODI_DATE, MODI_TIME, MODI_AP, MODI_PRID, FLAG
                        , TE001, TE002, TE003, TE004, TE005, TE006, TE007, TE008, TE009, TE010
                        , TE011, TE012, TE013, TE014, TE015, TE016, TE017, TE018, TE019, TE020
                        , TE021, TE022, TE023, TE024, TE025, TE026, TE027, TE028, TE029, TE030
                        , TE031, TE032, TE033, TE034, TE035, TE036, TE037, TE038, TE039, TE040
                        , TE041, TE042, TE043, TE044, TE045, TE046, TE047, TE048, TE049, TE050
                        , TE051, TE052, TE053, TE054, TE055, TE056, TE057, TE058, TE059, TE060
                        , TE061, TE062, TE063, TE064, TE065, TE066, TE067, TE068, TE069, TE070
                        , TE071, TE072, TE073, TE074, TE075, TE076, TE077, TE078, TE079, TE080
                        , TE081, TE082
                        , TE103, TE104, TE105, TE106, TE107, TE108, TE109, TE110
                        , TE111, TE112, TE113, TE114, TE115, TE116, TE117, TE118, TE119, TE120
                        , TE121, TE122, TE123, TE124, TE125, TE126, TE127, TE128, TE129, TE130
                        , TE131, TE132, TE133, TE134, TE135, TE136, TE137, TE138, TE139, TE140
                        , TE141, TE142, TE143, TE144, TE145, TE146, TE147, TE148, TE149, TE150
                        , TE151, TE152, TE153, TE154, TE155, TE156, TE157, TE158, TE159, TE160
                        , TE161, TE162, TE163, TE164, TE165, TE166, TE167, TE168, TE169, TE170
                        , TE171, TE172, TE173, TE174, TE175, TE176, TE177, TE178, TE179, TE180
                        , TE181, TE182)
                    VALUES (@COMPANY, @CREATOR, @USR_GROUP, @CREATE_DATE, @CREATE_TIME, @CREATE_AP, @CREATE_PRID
                        , @MODIFIER, @MODI_DATE, @MODI_TIME, @MODI_AP, @MODI_PRID, @FLAG
                        , @TE001, @TE002, @TE003, @TE004, @TE005, @TE006, @TE007, @TE008, @TE009, @TE010
                        , @TE011, @TE012, @TE013, @TE014, @TE015, @TE016, @TE017, @TE018, @TE019, @TE020
                        , @TE021, @TE022, @TE023, @TE024, @TE025, @TE026, @TE027, @TE028, @TE029, @TE030
                        , @TE031, @TE032, @TE033, @TE034, @TE035, @TE036, @TE037, @TE038, @TE039, @TE040
                        , @TE041, @TE042, @TE043, @TE044, @TE045, @TE046, @TE047, @TE048, @TE049, @TE050
                        , @TE051, @TE052, @TE053, @TE054, @TE055, @TE056, @TE057, @TE058, @TE059, @TE060
                        , @TE061, @TE062, @TE063, @TE064, @TE065, @TE066, @TE067, @TE068, @TE069, @TE070
                        , @TE071, @TE072, @TE073, @TE074, @TE075, @TE076, @TE077, @TE078, @TE079, @TE080
                        , @TE081, @TE082
                        , @TE103, @TE104, @TE105, @TE106, @TE107, @TE108, @TE109, @TE110
                        , @TE111, @TE112, @TE113, @TE114, @TE115, @TE116, @TE117, @TE118, @TE119, @TE120
                        , @TE121, @TE122, @TE123, @TE124, @TE125, @TE126, @TE127, @TE128, @TE129, @TE130
                        , @TE131, @TE132, @TE133, @TE134, @TE135, @TE136, @TE137, @TE138, @TE139, @TE140
                        , @TE141, @TE142, @TE143, @TE144, @TE145, @TE146, @TE147, @TE148, @TE149, @TE150
                        , @TE151, @TE152, @TE153, @TE154, @TE155, @TE156, @TE157, @TE158, @TE159, @TE160
                        , @TE161, @TE162, @TE163, @TE164, @TE165, @TE166, @TE167, @TE168, @TE169, @TE170
                        , @TE171, @TE172, @TE173, @TE174, @TE175, @TE176, @TE177, @TE178, @TE179, @TE180
                        , @TE181, @TE182)", coptEParams);

                // ========== 寫入 COPTF (訂單變更單身) ==========
                foreach (var d in details)
                {
                    var seqNo = (details.IndexOf(d) + 1).ToString("D4");
                    var coptFParams = new
                    {
                        COMPANY = companyNo, CREATOR = "IMPORT", USR_GROUP = "",
                        CREATE_DATE = dateNow, CREATE_TIME = timeNow, CREATE_AP = "IMPORT", CREATE_PRID = "BM",
                        MODIFIER = "", MODI_DATE = "", MODI_TIME = "", MODI_AP = "", MODI_PRID = "", FLAG = "1",

                        TF001 = erpPrefix,
                        TF002 = erpNo,
                        TF003 = "0001",
                        TF004 = seqNo,
                        TF005 = d.ProductCode ?? "",
                        TF006 = d.ProductName ?? "",
                        TF007 = "",
                        TF008 = "",
                        TF009 = d.NewQuantity ?? 0,
                        TF010 = "",
                        TF011 = 0, TF012 = "",
                        TF013 = d.NewUnitPrice ?? 0,
                        TF014 = (d.NewUnitPrice ?? 0) * (d.NewQuantity ?? 0),
                        TF015 = d.NewDeliveryDate?.ToString("yyyyMMdd") ?? "",
                        TF016 = "",
                        TF017 = "N",
                        TF018 = changeReason,
                        TF019 = "N",
                        TF020 = "", TF021 = "", TF022 = "", TF023 = "", TF024 = "",
                        TF025 = "", TF026 = "", TF027 = "", TF028 = "", TF029 = "", TF030 = "",
                        TF031 = "", TF032 = "", TF033 = "", TF034 = "", TF035 = "", TF036 = "",
                        TF037 = "", TF038 = "", TF039 = "", TF040 = "", TF041 = "", TF042 = "",
                        TF043 = "", TF044 = "", TF045 = "", TF046 = "", TF047 = "", TF048 = "",
                        TF049 = "", TF050 = "", TF051 = "", TF052 = "", TF053 = "", TF054 = "",
                        TF055 = "", TF056 = "", TF057 = "", TF058 = "", TF059 = "", TF060 = "",
                        TF061 = "", TF062 = "", TF063 = "", TF064 = "", TF065 = "", TF066 = "",
                        TF067 = "", TF068 = "", TF069 = "", TF070 = "", TF071 = "", TF072 = "",
                        TF073 = "", TF074 = "", TF075 = "", TF076 = "", TF077 = "", TF078 = "",
                        TF079 = "", TF080 = "", TF081 = "", TF082 = "", TF083 = "", TF084 = "",
                        TF085 = "", TF086 = "", TF087 = "", TF088 = "", TF089 = "", TF090 = "",
                        TF091 = "", TF092 = "", TF093 = "", TF094 = "", TF095 = "", TF096 = "",
                        TF097 = "", TF098 = "", TF099 = "", TF100 = "",
                        TF101 = "", TF102 = "", TF103 = "",

                        // ---- 原值 TF104-TF177 ----
                        TF104 = seqNo,
                        TF105 = d.ProductCode ?? "",
                        TF106 = d.ProductName ?? "",
                        TF107 = "", TF108 = "",
                        TF109 = d.OriginalQuantity ?? 0,
                        TF110 = "", TF111 = 0, TF112 = "",
                        TF113 = d.OriginalUnitPrice ?? 0,
                        TF114 = (d.OriginalUnitPrice ?? 0) * (d.OriginalQuantity ?? 0),
                        TF115 = d.OriginalDeliveryDate?.ToString("yyyyMMdd") ?? "",
                        TF116 = "", TF117 = "N",
                        TF118 = changeReason,
                        TF119 = "N",
                        TF120 = "", TF121 = "", TF122 = "", TF123 = "", TF124 = "", TF125 = "",
                        TF126 = "", TF127 = "", TF128 = "", TF129 = "", TF130 = "", TF131 = "",
                        TF132 = "", TF133 = "", TF134 = "", TF135 = "", TF136 = "", TF137 = "",
                        TF138 = "", TF139 = "", TF140 = "", TF141 = "", TF142 = "", TF143 = "",
                        TF144 = "", TF145 = "", TF146 = "", TF147 = "", TF148 = "", TF149 = "",
                        TF150 = "", TF151 = "", TF152 = "", TF153 = "", TF154 = "", TF155 = "",
                        TF156 = "", TF157 = "", TF158 = "", TF159 = "", TF160 = "", TF161 = "",
                        TF162 = "", TF163 = "", TF164 = "", TF165 = "", TF166 = "", TF167 = "",
                        TF168 = "", TF169 = "", TF170 = "", TF171 = "", TF172 = "", TF173 = "",
                        TF174 = "", TF175 = "", TF176 = "", TF177 = "",

                        // ---- TF500-TF509 ----
                        TF500 = "", TF501 = 0, TF502 = "", TF503 = "", TF504 = "N",
                        TF505 = "", TF506 = 0, TF507 = "", TF508 = "", TF509 = "N",
                    };

                    await erpConn.ExecuteAsync(@"
                        INSERT INTO COPTF (COMPANY, CREATOR, USR_GROUP, CREATE_DATE, CREATE_TIME, CREATE_AP, CREATE_PRID
                            , MODIFIER, MODI_DATE, MODI_TIME, MODI_AP, MODI_PRID, FLAG
                            , TF001, TF002, TF003, TF004, TF005, TF006, TF007, TF008, TF009, TF010
                            , TF011, TF012, TF013, TF014, TF015, TF016, TF017, TF018, TF019, TF020
                            , TF021, TF022, TF023, TF024, TF025, TF026, TF027, TF028, TF029, TF030
                            , TF031, TF032, TF033, TF034, TF035, TF036, TF037, TF038, TF039, TF040
                            , TF041, TF042, TF043, TF044, TF045, TF046, TF047, TF048, TF049, TF050
                            , TF051, TF052, TF053, TF054, TF055, TF056, TF057, TF058, TF059, TF060
                            , TF061, TF062, TF063, TF064, TF065, TF066, TF067, TF068, TF069, TF070
                            , TF071, TF072, TF073, TF074, TF075, TF076, TF077, TF078, TF079, TF080
                            , TF081, TF082, TF083, TF084, TF085, TF086, TF087, TF088, TF089, TF090
                            , TF091, TF092, TF093, TF094, TF095, TF096, TF097, TF098, TF099, TF100
                            , TF101, TF102, TF103
                            , TF104, TF105, TF106, TF107, TF108, TF109, TF110
                            , TF111, TF112, TF113, TF114, TF115, TF116, TF117, TF118, TF119, TF120
                            , TF121, TF122, TF123, TF124, TF125, TF126, TF127, TF128, TF129, TF130
                            , TF131, TF132, TF133, TF134, TF135, TF136, TF137, TF138, TF139, TF140
                            , TF141, TF142, TF143, TF144, TF145, TF146, TF147, TF148, TF149, TF150
                            , TF151, TF152, TF153, TF154, TF155, TF156, TF157, TF158, TF159, TF160
                            , TF161, TF162, TF163, TF164, TF165, TF166, TF167, TF168, TF169, TF170
                            , TF171, TF172, TF173, TF174, TF175, TF176, TF177
                            , TF500, TF501, TF502, TF503, TF504
                            , TF505, TF506, TF507, TF508, TF509)
                        VALUES (@COMPANY, @CREATOR, @USR_GROUP, @CREATE_DATE, @CREATE_TIME, @CREATE_AP, @CREATE_PRID
                            , @MODIFIER, @MODI_DATE, @MODI_TIME, @MODI_AP, @MODI_PRID, @FLAG
                            , @TF001, @TF002, @TF003, @TF004, @TF005, @TF006, @TF007, @TF008, @TF009, @TF010
                            , @TF011, @TF012, @TF013, @TF014, @TF015, @TF016, @TF017, @TF018, @TF019, @TF020
                            , @TF021, @TF022, @TF023, @TF024, @TF025, @TF026, @TF027, @TF028, @TF029, @TF030
                            , @TF031, @TF032, @TF033, @TF034, @TF035, @TF036, @TF037, @TF038, @TF039, @TF040
                            , @TF041, @TF042, @TF043, @TF044, @TF045, @TF046, @TF047, @TF048, @TF049, @TF050
                            , @TF051, @TF052, @TF053, @TF054, @TF055, @TF056, @TF057, @TF058, @TF059, @TF060
                            , @TF061, @TF062, @TF063, @TF064, @TF065, @TF066, @TF067, @TF068, @TF069, @TF070
                            , @TF071, @TF072, @TF073, @TF074, @TF075, @TF076, @TF077, @TF078, @TF079, @TF080
                            , @TF081, @TF082, @TF083, @TF084, @TF085, @TF086, @TF087, @TF088, @TF089, @TF090
                            , @TF091, @TF092, @TF093, @TF094, @TF095, @TF096, @TF097, @TF098, @TF099, @TF100
                            , @TF101, @TF102, @TF103
                            , @TF104, @TF105, @TF106, @TF107, @TF108, @TF109, @TF110
                            , @TF111, @TF112, @TF113, @TF114, @TF115, @TF116, @TF117, @TF118, @TF119, @TF120
                            , @TF121, @TF122, @TF123, @TF124, @TF125, @TF126, @TF127, @TF128, @TF129, @TF130
                            , @TF131, @TF132, @TF133, @TF134, @TF135, @TF136, @TF137, @TF138, @TF139, @TF140
                            , @TF141, @TF142, @TF143, @TF144, @TF145, @TF146, @TF147, @TF148, @TF149, @TF150
                            , @TF151, @TF152, @TF153, @TF154, @TF155, @TF156, @TF157, @TF158, @TF159, @TF160
                            , @TF161, @TF162, @TF163, @TF164, @TF165, @TF166, @TF167, @TF168, @TF169, @TF170
                            , @TF171, @TF172, @TF173, @TF174, @TF175, @TF176, @TF177
                            , @TF500, @TF501, @TF502, @TF503, @TF504
                            , @TF505, @TF506, @TF507, @TF508, @TF509)", coptFParams);
                }

                ts.Complete();
            } // TransactionScope 在這裡 Dispose，釋放 ambient transaction

            // === Phase 2: 更新本地狀態（TransactionScope 已結束，不會 enlist 衝突）===
            using var localConn = _db.CreateConnection();
            await localConn.ExecuteAsync(
                @"UPDATE OrderChangeHeaders 
                  SET TransferStatus = 2, TransferMessage = NULL, ErpOrderNo = @ErpOrderNo
                  WHERE Id = @Id",
                new { Id = headerId, ErpOrderNo = $"{erpPrefix}-{erpNo}" });

            return $"OK|{erpPrefix}-{erpNo}";
        }
        catch (Exception ex)
        {
            // TransactionScope 自動 rollback
            try
            {
                using var localConn = _db.CreateConnection();
                await localConn.ExecuteAsync(
                    @"UPDATE OrderChangeHeaders 
                      SET TransferStatus = 3, TransferMessage = @Msg
                      WHERE Id = @Id",
                    new { Id = headerId, Msg = ex.Message.Length > 400 ? ex.Message[..400] : ex.Message });
            }
            catch { }

            return $"ERR|{ex.Message}";
        }
    }

    // ========== Private Helpers ==========

    /// <summary>Excel 未填品名時，依品號查詢 ERP 品號主檔 (INVMB.MB001/MB002) 帶出品名</summary>
    private async Task FillMissingProductNamesAsync(IEnumerable<OrderChangeDetail> details)
    {
        var codes = details
            .Where(d => !string.IsNullOrWhiteSpace(d.ProductCode) && string.IsNullOrWhiteSpace(d.ProductName))
            .Select(d => d.ProductCode!.Trim())
            .Distinct()
            .ToList();

        if (codes.Count == 0) return;

        try
        {
            using var erpConn = new SqlConnection(_erpConnectionString);
            var rows = await erpConn.QueryAsync<ProductLookup>(
                "SELECT RTRIM(MB001) AS ProductCode, RTRIM(MB002) AS ProductName FROM INVMB WHERE RTRIM(MB001) IN @Codes",
                new { Codes = codes });

            var map = rows.ToDictionary(r => r.ProductCode, r => r.ProductName, StringComparer.OrdinalIgnoreCase);

            foreach (var d in details)
            {
                if (!string.IsNullOrWhiteSpace(d.ProductCode) && string.IsNullOrWhiteSpace(d.ProductName)
                    && map.TryGetValue(d.ProductCode.Trim(), out var name))
                {
                    d.ProductName = name;
                }
            }
        }
        catch
        {
            // ERP 無法連線時略過帶入品名，維持原有空白讓使用者手動補齊
        }
    }

    private sealed record ProductLookup(string ProductCode, string ProductName);

    private async Task<string> GenerateBatchNoAsync(SqlConnection conn, SqlTransaction transaction, string today)
    {
        var sql = @"
            SELECT ISNULL(MAX(ImportBatchNo), @Prefix + '000')
            FROM OrderChangeHeaders
            WHERE ImportBatchNo LIKE @Prefix + '%'";

        var lastNo = await conn.ExecuteScalarAsync<string>(sql, new { Prefix = today }, transaction);

        var seq = 1;
        if (!string.IsNullOrEmpty(lastNo) && lastNo.Length >= 11)
        {
            var seqPart = lastNo[8..];
            if (int.TryParse(seqPart, out var lastSeq))
                seq = lastSeq + 1;
        }

        if (seq > 999)
            throw new InvalidOperationException($"今日匯入單號已達上限 ({today}999)");

        return $"{today}{seq:D3}";
    }

    private static DateTime? ParseDateTime(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime();
        if (cell.TryGetValue(out DateTime dt))
            return dt;
        if (cell.TryGetValue(out double d) && d > 1)
        {
            try { return DateTime.FromOADate(d); } catch { }
        }
        var text = cell.GetString().Trim();
        if (DateTime.TryParse(text, out var parsed))
            return parsed;
        return null;
    }
}
