using System.Data;
using System.Transactions;
using ClosedXML.Excel;
using Dapper;
using ImportDataToERP.Data;
using ImportDataToERP.Models;
using Microsoft.Data.SqlClient;

namespace ImportDataToERP.Services;

public class OrderImportService
{
    private readonly DbConnectionFactory _db;
    private readonly ErpConnectionAccessor _erpConnectionAccessor;
    private readonly ErpPermissionService _erpPermissionService;

    public OrderImportService(DbConnectionFactory db, ErpConnectionAccessor erpConnectionAccessor, ErpPermissionService erpPermissionService)
    {
        _db = db;
        _erpConnectionAccessor = erpConnectionAccessor;
        _erpPermissionService = erpPermissionService;
    }

    // ========== 查詢 ==========

    public async Task<IEnumerable<OrderImportHeader>> GetAllHeadersAsync()
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            SELECT h.*, ISNULL(d.DetailCount, 0) AS DetailCount
            FROM OrderImportHeaders h
            LEFT JOIN (
                SELECT HeaderId, COUNT(*) AS DetailCount
                FROM OrderImportDetails
                GROUP BY HeaderId
            ) d ON d.HeaderId = h.Id
            ORDER BY h.Id DESC";
        return await conn.QueryAsync<OrderImportHeader>(sql);
    }

    public async Task<IEnumerable<OrderImportDetail>> GetDetailsByHeaderIdAsync(int headerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<OrderImportDetail>(
            "SELECT * FROM OrderImportDetails WHERE HeaderId = @HeaderId ORDER BY Id",
            new { HeaderId = headerId });
    }

    public async Task<OrderImportHeader?> GetHeaderByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<OrderImportHeader>(
            "SELECT * FROM OrderImportHeaders WHERE Id = @Id", new { Id = id });
    }

    // ========== 單頭 CRUD ==========

    public async Task<int> CreateHeaderAsync(OrderImportHeader header)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            header.ImportBatchNo = await GenerateBatchNoAsync(conn, transaction, today);
            header.CreatedAt = DateTime.Now;

            var sql = @"
                INSERT INTO OrderImportHeaders 
                    (ImportBatchNo, OrderType, OrderNo, CustomerCode, DeptCode, Remarks,
                     OrderDate, SalesRep, FactoryCode, DocDate, TaxType,
                     ImportStatus, ImportedAt, CreatedAt)
                VALUES 
                    (@ImportBatchNo, @OrderType, @OrderNo, @CustomerCode, @DeptCode, @Remarks,
                     @OrderDate, @SalesRep, @FactoryCode, @DocDate, @TaxType,
                     @ImportStatus, @ImportedAt, @CreatedAt);
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

    public async Task<bool> UpdateHeaderAsync(OrderImportHeader header)
    {
        using var conn = _db.CreateConnection();
        var transferStatus = await conn.ExecuteScalarAsync<int?>(
            "SELECT TransferStatus FROM OrderImportHeaders WHERE Id = @Id", new { header.Id });
        if (transferStatus == 2)
            throw new InvalidOperationException("此訂單已拋轉ERP，單頭無法再編輯，避免與ERP資料不一致");

        var sql = @"
            UPDATE OrderImportHeaders SET
                OrderType    = @OrderType,
                OrderNo      = @OrderNo,
                CustomerCode = @CustomerCode,
                DeptCode     = @DeptCode,
                Remarks      = @Remarks,
                OrderDate    = @OrderDate,
                SalesRep     = @SalesRep,
                FactoryCode  = @FactoryCode,
                DocDate      = @DocDate,
                TaxType      = @TaxType
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
            var transferStatus = await conn.ExecuteScalarAsync<int?>(
                "SELECT TransferStatus FROM OrderImportHeaders WHERE Id = @Id", new { Id = id }, transaction);
            if (transferStatus == 2)
                throw new InvalidOperationException("此訂單已拋轉ERP，無法刪除，避免與ERP資料不一致");

            await conn.ExecuteAsync("DELETE FROM OrderImportDetails WHERE HeaderId = @Id", new { Id = id }, transaction);
            var rows = await conn.ExecuteAsync("DELETE FROM OrderImportHeaders WHERE Id = @Id", new { Id = id }, transaction);
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

    public async Task<int> CreateDetailAsync(OrderImportDetail detail)
    {
        using var conn = _db.CreateConnection();
        var transferStatus = await conn.ExecuteScalarAsync<int?>(
            "SELECT TransferStatus FROM OrderImportHeaders WHERE Id = @HeaderId", new { detail.HeaderId });
        if (transferStatus == 2)
            throw new InvalidOperationException("此訂單已拋轉ERP，無法新增明細，避免與ERP資料不一致");

        detail.CreatedAt = DateTime.Now;
        var sql = @"
            INSERT INTO OrderImportDetails
                (HeaderId, OrderType, SeqNo, ProductCode, ProductName,
                 Warehouse, OrderQty, Unit, UnitPrice, Amount, Currency, CreatedAt)
            VALUES
                (@HeaderId, @OrderType, @SeqNo, @ProductCode, @ProductName,
                 @Warehouse, @OrderQty, @Unit, @UnitPrice, @Amount, @Currency, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await conn.ExecuteScalarAsync<int>(sql, detail);
    }

    public async Task<bool> UpdateDetailAsync(OrderImportDetail detail)
    {
        using var conn = _db.CreateConnection();
        var transferStatus = await conn.ExecuteScalarAsync<int?>(
            @"SELECT h.TransferStatus FROM OrderImportDetails d
              JOIN OrderImportHeaders h ON h.Id = d.HeaderId
              WHERE d.Id = @Id", new { detail.Id });
        if (transferStatus == 2)
            throw new InvalidOperationException("此訂單已拋轉ERP，明細無法再編輯，避免與ERP資料不一致");

        var sql = @"
            UPDATE OrderImportDetails SET
                OrderType   = @OrderType,
                SeqNo       = @SeqNo,
                ProductCode = @ProductCode,
                ProductName = @ProductName,
                Warehouse   = @Warehouse,
                OrderQty    = @OrderQty,
                Unit        = @Unit,
                UnitPrice   = @UnitPrice,
                Amount      = @Amount,
                Currency    = @Currency
            WHERE Id = @Id";
        return await conn.ExecuteAsync(sql, detail) > 0;
    }

    public async Task<bool> DeleteDetailAsync(int id)
    {
        using var conn = _db.CreateConnection();
        var transferStatus = await conn.ExecuteScalarAsync<int?>(
            @"SELECT h.TransferStatus FROM OrderImportDetails d
              JOIN OrderImportHeaders h ON h.Id = d.HeaderId
              WHERE d.Id = @Id", new { Id = id });
        if (transferStatus == 2)
            throw new InvalidOperationException("此訂單已拋轉ERP，明細無法刪除，避免與ERP資料不一致");

        return await conn.ExecuteAsync("DELETE FROM OrderImportDetails WHERE Id = @Id", new { Id = id }) > 0;
    }

    // ========== Excel 解析 / 匯入 / 範本 ==========

    public async Task<OrderImportViewModel> ParseExcelAsync(Stream fileStream, string fileName)
    {
        var result = new OrderImportViewModel { FileName = fileName };
        var rawRows = new List<(OrderImportHeader hdr, OrderImportDetail dtl)>();

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
            var header = new OrderImportHeader
            {
                OrderType   = ws.Cell(row, 2).GetString().Trim(),
                OrderNo     = ws.Cell(row, 3).GetString().NullIfEmpty(),
                CustomerCode= ws.Cell(row, 4).GetString().Trim(),
                DeptCode    = ws.Cell(row, 5).GetString().NullIfEmpty(),
                Remarks     = ws.Cell(row, 6).GetString().NullIfEmpty(),
                OrderDate   = ParseDateTime(ws.Cell(row, 7)),
                SalesRep    = ws.Cell(row, 8).GetString().NullIfEmpty(),
                FactoryCode = ws.Cell(row, 9).GetString().NullIfEmpty(),
                DocDate     = ParseDateTime(ws.Cell(row, 10)),
                TaxType     = ws.Cell(row, 11).GetString().NullIfEmpty(),
            };

            var detail = new OrderImportDetail
            {
                OrderType   = ws.Cell(row, 55).GetString().Trim(),
                SeqNo       = ws.Cell(row, 56).GetString().NullIfEmpty(),
                ProductCode = ws.Cell(row, 57).GetString().NullIfEmpty(),
                ProductName = ws.Cell(row, 58).GetString().NullIfEmpty(),
                Warehouse   = ws.Cell(row, 59).GetString().NullIfEmpty(),
                OrderQty    = ws.Cell(row, 60).TryGetValue(out double qty) ? (decimal)qty : 0,
                Unit        = ws.Cell(row, 61).GetString().NullIfEmpty(),
                UnitPrice   = ws.Cell(row, 62).TryGetValue(out double up) ? (decimal)up : 0,
                Amount      = ws.Cell(row, 63).TryGetValue(out double amt) ? (decimal)amt : 0,
                Currency    = ws.Cell(row, 64).GetString().NullIfEmpty(),
            };

            if (string.IsNullOrWhiteSpace(header.CustomerCode) && string.IsNullOrWhiteSpace(header.OrderType))
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
                r.hdr.OrderType,
                OrderDate = r.hdr.OrderDate?.Date
            })
            .ToList();

        foreach (var g in groups)
        {
            var firstHdr = g.First().hdr;
            firstHdr.Remarks = g.Select(r => r.hdr.Remarks)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
            firstHdr.DetailCount = g.Count();

            result.HeaderGroups.Add(new OrderImportHeaderGroup
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
        ConfirmImportAsync(List<OrderImportHeaderGroup> groups)
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
                    SELECT COUNT(1) FROM OrderImportHeaders
                    WHERE CustomerCode = @CustomerCode
                      AND OrderType = @OrderType
                      AND OrderDate = @OrderDate
                      AND ImportStatus = N'已匯入'";
                var dupCount = await conn.ExecuteScalarAsync<int>(dupCheckSql, new
                {
                    hdr.CustomerCode,
                    hdr.OrderType,
                    hdr.OrderDate
                }, transaction);

                if (dupCount > 0)
                {
                    errors.Add($"第{idx + 1}組：客戶[{hdr.CustomerCode}] 單別[{hdr.OrderType}] 日期[{hdr.OrderDate:yyyy-MM-dd}] 已重複匯入，略過");
                    continue;
                }

                var batchNo = await GenerateBatchNoAsync(conn, transaction, today);

                var insertHeaderSql = @"
                    INSERT INTO OrderImportHeaders 
                        (ImportBatchNo, OrderType, OrderNo, CustomerCode, DeptCode, Remarks,
                         OrderDate, SalesRep, FactoryCode, DocDate, TaxType,
                         ImportStatus, ImportedAt, CreatedAt)
                    VALUES 
                        (@ImportBatchNo, @OrderType, @OrderNo, @CustomerCode, @DeptCode, @Remarks,
                         @OrderDate, @SalesRep, @FactoryCode, @DocDate, @TaxType,
                         @ImportStatus, @ImportedAt, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                hdr.ImportBatchNo = batchNo;
                hdr.ImportStatus = "已匯入";
                hdr.ImportedAt = DateTime.Now;
                hdr.CreatedAt = DateTime.Now;

                var headerId = await conn.ExecuteScalarAsync<int>(insertHeaderSql, hdr, transaction);
                headersDone++;

                foreach (var dtl in group.Details)
                {
                    dtl.HeaderId = headerId;
                    dtl.CreatedAt = DateTime.Now;

                    var insertDetailSql = @"
                        INSERT INTO OrderImportDetails
                            (HeaderId, OrderType, SeqNo, ProductCode, ProductName,
                             Warehouse, OrderQty, Unit, UnitPrice, Amount, Currency, CreatedAt)
                        VALUES
                            (@HeaderId, @OrderType, @SeqNo, @ProductCode, @ProductName,
                             @Warehouse, @OrderQty, @Unit, @UnitPrice, @Amount, @Currency, @CreatedAt)";

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
        var ws = wb.AddWorksheet("批次匯入表");

        ws.Cell(1, 1).Value = "說明欄";
        ws.Cell(1, 11).Value = "1.內含\n2.外加\n3.零稅率\n4.免稅\n9.不計稅";
        ws.Cell(1, 61).Value = "PCS";
        ws.Cell(1, 64).Value = "NTD 台幣\nUSD 美金\nHKD 港幣";

        ws.Cell(2, 2).Value = "單別";
        ws.Cell(2, 3).Value = "單號";
        ws.Cell(2, 4).Value = "客戶代號";
        ws.Cell(2, 5).Value = "部門代號";
        ws.Cell(2, 6).Value = "備註(需要依照單身品項去對應到平台ORDER單號)";
        ws.Cell(2, 7).Value = "訂單日期";
        ws.Cell(2, 8).Value = "業務人員";
        ws.Cell(2, 9).Value = "廠別代號";
        ws.Cell(2, 10).Value = "單據日期";
        ws.Cell(2, 11).Value = "課稅別";

        ws.Cell(2, 55).Value = "單別";
        ws.Cell(2, 56).Value = "序號";
        ws.Cell(2, 57).Value = "品號";
        ws.Cell(2, 58).Value = "品名";
        ws.Cell(2, 59).Value = "庫別";
        ws.Cell(2, 60).Value = "訂單數量";
        ws.Cell(2, 61).Value = "單位";
        ws.Cell(2, 62).Value = "單價";
        ws.Cell(2, 63).Value = "金額";
        ws.Cell(2, 64).Value = "交易幣別";

        var headerRange = ws.Range(2, 2, 2, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var detailRange = ws.Range(2, 55, 2, 64);
        detailRange.Style.Font.Bold = true;
        detailRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        ws.Columns(2, 11).Width = 14;
        ws.Columns(55, 64).Width = 14;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ========== 拋轉 ERP ==========

    public async Task<string> TransferToErpAsync(int headerId, string operatorAccount)
    {
        // 1. 讀取單頭+單身
        var header = await GetHeaderByIdAsync(headerId);
        if (header == null) return "單據不存在";

        if (header.TransferStatus == 2)
            return "此單據已拋轉過ERP";

        var details = (await GetDetailsByHeaderIdAsync(headerId)).ToList();
        if (details.Count == 0) return "無明細資料，無法拋轉";

        if (details.Any(d => string.IsNullOrWhiteSpace(d.ProductCode)))
            return "部分明細無品號，無法拋轉";

        if (!await _erpPermissionService.HasPermissionAsync(operatorAccount, "COPI06"))
            return "ERP帳號無訂單建立權限";

        var dateNow = DateTime.Now.ToString("yyyyMMdd");
        var erpPrefix = header.OrderType;
        var erpNo = "";
        var factory = "";
        string companyNo = "";

        try
        {
            // === Phase 1: ERP 端操作 (TransactionScope 包裹) ===
            {
                using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                using var erpConn = new SqlConnection(_erpConnectionAccessor.GetConnectionString());
                await erpConn.OpenAsync();

                // 檢查 ERP 廠別
                var factoryResult = await erpConn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT TOP 1 RTRIM(LTRIM(COMPANY)) COMPANY, RTRIM(LTRIM(MB001)) MB001 FROM CMSMB");
                if (factoryResult != null)
                {
                    companyNo = factoryResult.COMPANY ?? "";
                    factory = factoryResult.MB001 ?? "";
                }

                // 查詢客戶主檔 COPMA 補齊地址/連絡方式/交易條件等欄位
                var custInfo = await erpConn.QueryFirstOrDefaultAsync<CustomerLookup>(
                    @"SELECT RTRIM(MA003) AS FullName, RTRIM(MA005) AS Contact,
                             RTRIM(MA006) AS Tel, RTRIM(MA008) AS Fax,
                             RTRIM(MA025) AS InvoiceAddress,
                             RTRIM(MA027) AS ShipAddress1, RTRIM(MA040) AS PostalCode, RTRIM(MA064) AS ShipAddress2,
                             RTRIM(MA030) AS PriceTerm, RTRIM(MA031) AS PaymentTerm, RTRIM(MA083) AS PaymentTermCode,
                             RTRIM(MA048) AS ShipMethod, RTRIM(MA109) AS TradeTerm, RTRIM(MA118) AS TaxCode
                      FROM COPMA WHERE RTRIM(MA001) = @CustomerCode",
                    new { header.CustomerCode });
                var shipAddress1 = custInfo == null
                    ? ""
                    : string.Join(" ", new[] { custInfo.PostalCode, custInfo.ShipAddress1 }.Where(s => !string.IsNullOrEmpty(s)));

                // 查詢品號主檔 INVMB 取得規格
                var productCodes = details.Select(d => d.ProductCode!.Trim()).Distinct().ToList();
                var specRows = await erpConn.QueryAsync<ProductSpecLookup>(
                    "SELECT RTRIM(MB001) AS ProductCode, RTRIM(MB003) AS Spec FROM INVMB WHERE RTRIM(MB001) IN @Codes",
                    new { Codes = productCodes });
                var specMap = specRows.ToDictionary(r => r.ProductCode, r => r.Spec, StringComparer.OrdinalIgnoreCase);

                // 檢查單據設定 CMSMQ
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

                // 產生 ERP 單號 TC002
                var refDate = header.DocDate ?? header.OrderDate ?? DateTime.Now;
                string datePart = encode switch
                {
                    "1" => refDate.ToString(new string('y', Math.Max(yearLength, 1)) + "MMdd"),
                    "2" => refDate.ToString(new string('y', Math.Max(yearLength, 1)) + "MM"),
                    "4" => header.OrderNo ?? "",
                    _   => ""
                };

                var numResult = await erpConn.QueryFirstOrDefaultAsync<dynamic>(
                    $@"SELECT CONVERT(INT, RIGHT(ISNULL(MAX(RTRIM(LTRIM(TC002))), '{new string('0', lineLength)}'), {lineLength})) + 1 CurrentNum
                       FROM COPTC WHERE TC001 = @ErpPrefix" +
                       (string.IsNullOrEmpty(datePart) ? "" : " AND RTRIM(LTRIM(TC002)) LIKE @DatePart + '" + new string('_', lineLength) + "'"),
                    new { ErpPrefix = erpPrefix, DatePart = datePart });

                int currentNum = numResult != null ? Convert.ToInt32(numResult.CurrentNum) : 1;
                erpNo = datePart + currentNum.ToString(new string('0', lineLength));

                // 檢查 COPTC.TC027
                var existCheck = await erpConn.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT TOP 1 TC027 FROM COPTC WHERE TC001 = @TC001 AND TC002 = @TC002",
                    new { TC001 = erpPrefix, TC002 = erpNo });

                if (existCheck != null)
                {
                    var tc027 = existCheck.TC027?.ToString() ?? "";
                    if (tc027 != "N")
                        return $"ERP 訂單 [{erpPrefix}-{erpNo}] 確認碼為 [{tc027}]，非 N 狀態無法拋轉";
                }

                // 彙總
                var totalQty = details.Sum(d => d.OrderQty);
                var totalAmt = details.Sum(d => d.Amount);
                var currency = details.FirstOrDefault()?.Currency ?? "NTD";

                // 寫入 COPTC
                var coptcParams = new
                {
                    COMPANY = companyNo, CREATOR = "IMPORT", USR_GROUP = "",
                    CREATE_DATE = dateNow,
                    MODIFIER = "", MODI_DATE = "", FLAG = "1",
                    TC001 = erpPrefix, TC002 = erpNo,
                    TC003 = (header.OrderDate ?? refDate).ToString("yyyyMMdd"),
                    TC004 = header.CustomerCode ?? "", TC005 = header.DeptCode ?? "",
                    TC006 = header.SalesRep ?? "", TC007 = header.FactoryCode ?? factory,
                    TC008 = currency, TC009 = 1m, TC010 = shipAddress1, TC011 = custInfo?.ShipAddress2 ?? "",
                    TC012 = header.OrderNo ?? "", TC013 = custInfo?.PriceTerm ?? "", TC014 = custInfo?.PaymentTerm ?? "",
                    TC015 = header.Remarks ?? "", TC016 = header.TaxType ?? "",
                    TC017 = "", TC018 = custInfo?.Contact ?? "", TC019 = custInfo?.ShipMethod ?? "", TC020 = "", TC021 = "",
                    TC022 = "", TC023 = "", TC024 = "", TC025 = "", TC026 = 0m,
                    TC027 = "N", TC028 = 0, TC029 = totalAmt, TC030 = 0m, TC031 = totalQty,
                    TC032 = header.CustomerCode ?? "",
                    TC033 = "", TC034 = "", TC035 = "", TC036 = "", TC037 = "", TC038 = "",
                    TC039 = (header.DocDate ?? refDate).ToString("yyyyMMdd"),
                    TC040 = "", TC041 = 0m, TC042 = custInfo?.PaymentTermCode ?? "", TC043 = 0m, TC044 = 0m, TC045 = 0m, TC046 = 0m,
                    TC047 = "", TC048 = "N", TC049 = "", TC050 = "N",
                    TC051 = "", TC052 = 0, TC053 = custInfo?.FullName ?? "", TC054 = "", TC055 = "", TC056 = "1",
                    TC057 = "N", TC058 = "", TC059 = "", TC060 = "N", TC061 = "", TC062 = "",
                    TC063 = custInfo?.InvoiceAddress ?? "", TC064 = "", TC065 = custInfo?.FullName ?? "", TC066 = custInfo?.Tel ?? "", TC067 = custInfo?.Fax ?? "", TC068 = custInfo?.TradeTerm ?? "",
                    TC069 = "", TC070 = "N", TC071 = "", TC072 = 0m, TC073 = 0m, TC074 = "",
                    TC075 = "", TC076 = "", TC077 = "N", TC078 = custInfo?.TaxCode ?? "", TC079 = "", TC080 = "",
                    TC081 = "", TC082 = "", TC083 = "", TC084 = "", TC085 = "", TC086 = "", TC087 = "",
                    TC088 = "", TC089 = "", TC090 = "",
                    UDF01 = "", UDF02 = "", UDF03 = "", UDF04 = "", UDF05 = "",
                    UDF06 = 0m, UDF07 = 0m, UDF08 = 0m, UDF09 = 0m, UDF10 = 0m,
                };

                await erpConn.ExecuteAsync(@"
                    INSERT INTO COPTC (COMPANY, CREATOR, USR_GROUP, CREATE_DATE
                        , MODIFIER, MODI_DATE, FLAG
                        , TC001, TC002, TC003, TC004, TC005, TC006, TC007, TC008, TC009, TC010
                        , TC011, TC012, TC013, TC014, TC015, TC016, TC017, TC018, TC019, TC020
                        , TC021, TC022, TC023, TC024, TC025, TC026, TC027, TC028, TC029, TC030
                        , TC031, TC032, TC033, TC034, TC035, TC036, TC037, TC038, TC039, TC040
                        , TC041, TC042, TC043, TC044, TC045, TC046, TC047, TC048, TC049, TC050
                        , TC051, TC052, TC053, TC054, TC055, TC056, TC057, TC058, TC059, TC060
                        , TC061, TC062, TC063, TC064, TC065, TC066, TC067, TC068, TC069, TC070
                        , TC071, TC072, TC073, TC074, TC075, TC076, TC077, TC078, TC079, TC080
                        , TC081, TC082, TC083, TC084, TC085, TC086, TC087, TC088, TC089, TC090
                        , UDF01, UDF02, UDF03, UDF04, UDF05, UDF06, UDF07, UDF08, UDF09, UDF10)
                    VALUES (@COMPANY, @CREATOR, @USR_GROUP, @CREATE_DATE
                        , @MODIFIER, @MODI_DATE, @FLAG
                        , @TC001, @TC002, @TC003, @TC004, @TC005, @TC006, @TC007, @TC008, @TC009, @TC010
                        , @TC011, @TC012, @TC013, @TC014, @TC015, @TC016, @TC017, @TC018, @TC019, @TC020
                        , @TC021, @TC022, @TC023, @TC024, @TC025, @TC026, @TC027, @TC028, @TC029, @TC030
                        , @TC031, @TC032, @TC033, @TC034, @TC035, @TC036, @TC037, @TC038, @TC039, @TC040
                        , @TC041, @TC042, @TC043, @TC044, @TC045, @TC046, @TC047, @TC048, @TC049, @TC050
                        , @TC051, @TC052, @TC053, @TC054, @TC055, @TC056, @TC057, @TC058, @TC059, @TC060
                        , @TC061, @TC062, @TC063, @TC064, @TC065, @TC066, @TC067, @TC068, @TC069, @TC070
                        , @TC071, @TC072, @TC073, @TC074, @TC075, @TC076, @TC077, @TC078, @TC079, @TC080
                        , @TC081, @TC082, @TC083, @TC084, @TC085, @TC086, @TC087, @TC088, @TC089, @TC090
                        , @UDF01, @UDF02, @UDF03, @UDF04, @UDF05, @UDF06, @UDF07, @UDF08, @UDF09, @UDF10)", coptcParams);

                // 寫入 COPTD
                foreach (var d in details)
                {
                    var coptdParams = new
                    {
                        COMPANY = companyNo, CREATOR = "IMPORT", USR_GROUP = "",
                        CREATE_DATE = dateNow,
                        MODIFIER = "", MODI_DATE = "", FLAG = "1",
                        TD001 = erpPrefix, TD002 = erpNo,
                        TD003 = d.SeqNo ?? (details.IndexOf(d) + 1).ToString("D4"),
                        TD004 = d.ProductCode ?? "", TD005 = d.ProductName ?? "",
                        TD006 = specMap.TryGetValue((d.ProductCode ?? "").Trim(), out var spec) ? spec : "",
                        TD007 = d.Warehouse ?? "", TD008 = d.OrderQty, TD009 = 0m,
                        TD010 = d.Unit ?? "", TD011 = d.UnitPrice, TD012 = d.Amount,
                        TD013 = "", TD014 = "", TD015 = "", TD016 = "N", TD017 = "", TD018 = "", TD019 = "",
                        TD020 = "", TD021 = "N", TD022 = 0m, TD023 = "", TD024 = 0m, TD025 = 0m, TD026 = 0m,
                        TD027 = "", TD028 = "", TD029 = "", TD030 = 0m, TD031 = 0m,
                        TD032 = 0m, TD033 = 0m, TD034 = 0m, TD035 = 0m, TD036 = "",
                        TD037 = "", TD038 = "", TD039 = "", TD040 = "", TD041 = "", TD042 = 0m,
                        TD043 = "", TD044 = "", TD045 = "9", TD046 = "", TD047 = "", TD048 = "",
                        TD049 = "", TD050 = 0m, TD051 = 0m, TD052 = 0m, TD053 = 0m,
                        TD054 = 0m, TD055 = 0m, TD056 = "", TD057 = "", TD058 = "",
                        TD059 = 0m, TD060 = "", TD061 = 0m, TD062 = "",
                        TD063 = "", TD064 = "", TD065 = "", TD066 = "", TD067 = "",
                        TD068 = "", TD069 = "",
                        TD500 = "", TD501 = 0m, TD502 = "", TD503 = "", TD504 = "N",
                        UDF01 = "", UDF02 = "", UDF03 = "", UDF04 = "", UDF05 = "",
                        UDF06 = 0m, UDF07 = 0m, UDF08 = 0m, UDF09 = 0m, UDF10 = 0m,
                    };

                    await erpConn.ExecuteAsync(@"
                        INSERT INTO COPTD (COMPANY, CREATOR, USR_GROUP, CREATE_DATE
                            , MODIFIER, MODI_DATE, FLAG
                            , TD001, TD002, TD003, TD004, TD005, TD006, TD007, TD008, TD009, TD010
                            , TD011, TD012, TD013, TD014, TD015, TD016, TD017, TD018, TD019, TD020
                            , TD021, TD022, TD023, TD024, TD025, TD026, TD027, TD028, TD029, TD030
                            , TD031, TD032, TD033, TD034, TD035, TD036, TD037, TD038, TD039, TD040
                            , TD041, TD042, TD043, TD044, TD045, TD046, TD047, TD048, TD049, TD050
                            , TD051, TD052, TD053, TD054, TD055, TD056, TD057, TD058, TD059, TD060
                            , TD061, TD062, TD063, TD064, TD065, TD066, TD067, TD068, TD069
                            , TD500, TD501, TD502, TD503, TD504
                            , UDF01, UDF02, UDF03, UDF04, UDF05, UDF06, UDF07, UDF08, UDF09, UDF10)
                        VALUES (@COMPANY, @CREATOR, @USR_GROUP, @CREATE_DATE
                            , @MODIFIER, @MODI_DATE, @FLAG
                            , @TD001, @TD002, @TD003, @TD004, @TD005, @TD006, @TD007, @TD008, @TD009, @TD010
                            , @TD011, @TD012, @TD013, @TD014, @TD015, @TD016, @TD017, @TD018, @TD019, @TD020
                            , @TD021, @TD022, @TD023, @TD024, @TD025, @TD026, @TD027, @TD028, @TD029, @TD030
                            , @TD031, @TD032, @TD033, @TD034, @TD035, @TD036, @TD037, @TD038, @TD039, @TD040
                            , @TD041, @TD042, @TD043, @TD044, @TD045, @TD046, @TD047, @TD048, @TD049, @TD050
                            , @TD051, @TD052, @TD053, @TD054, @TD055, @TD056, @TD057, @TD058, @TD059, @TD060
                            , @TD061, @TD062, @TD063, @TD064, @TD065, @TD066, @TD067, @TD068, @TD069
                            , @TD500, @TD501, @TD502, @TD503, @TD504
                            , @UDF01, @UDF02, @UDF03, @UDF04, @UDF05, @UDF06, @UDF07, @UDF08, @UDF09, @UDF10)", coptdParams);
                }

                ts.Complete();
            } // TransactionScope 在這裡 Dispose，釋放 ambient transaction

            // === Phase 2: 更新本地狀態（TransactionScope 已結束，不會 enlist 衝突）===
            using var localConn = _db.CreateConnection();
            await localConn.ExecuteAsync(
                @"UPDATE OrderImportHeaders 
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
                    @"UPDATE OrderImportHeaders 
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
    private async Task FillMissingProductNamesAsync(IEnumerable<OrderImportDetail> details)
    {
        var codes = details
            .Where(d => !string.IsNullOrWhiteSpace(d.ProductCode) && string.IsNullOrWhiteSpace(d.ProductName))
            .Select(d => d.ProductCode!.Trim())
            .Distinct()
            .ToList();

        if (codes.Count == 0) return;

        try
        {
            using var erpConn = new SqlConnection(_erpConnectionAccessor.GetConnectionString());
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

    private sealed record ProductSpecLookup(string ProductCode, string Spec);

    private sealed record CustomerLookup(
        string FullName, string Contact, string Tel, string Fax, string InvoiceAddress,
        string ShipAddress1, string PostalCode, string ShipAddress2,
        string PriceTerm, string PaymentTerm, string PaymentTermCode,
        string ShipMethod, string TradeTerm, string TaxCode);

    private async Task<string> GenerateBatchNoAsync(SqlConnection conn, SqlTransaction transaction, string today)
    {
        var sql = @"
            SELECT ISNULL(MAX(ImportBatchNo), @Prefix + '000')
            FROM OrderImportHeaders
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

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
