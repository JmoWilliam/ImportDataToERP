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
    private readonly ErpConnectionAccessor _erpConnectionAccessor;
    private readonly ErpPermissionService _erpPermissionService;

    public OrderChangeImportService(DbConnectionFactory db, ErpConnectionAccessor erpConnectionAccessor, ErpPermissionService erpPermissionService)
    {
        _db = db;
        _erpConnectionAccessor = erpConnectionAccessor;
        _erpPermissionService = erpPermissionService;
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
            header.OriginalOrderNo = header.SoErpPrefix + header.SoErpNo;
            header.ImportStatus = "已匯入";
            header.ImportedAt = DateTime.Now;
            header.CreatedAt = DateTime.Now;

            var sql = @"
                INSERT INTO OrderChangeHeaders
                    (ImportBatchNo, ErpOrderNo, SoErpPrefix, SoErpNo, OriginalOrderNo,
                     DetailCount, ImportStatus, ImportedAt, TransferStatus, CreatedAt)
                VALUES
                    (@ImportBatchNo, @ErpOrderNo, @SoErpPrefix, @SoErpNo, @OriginalOrderNo,
                     @DetailCount, @ImportStatus, @ImportedAt, @TransferStatus, @CreatedAt);
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
        var transferStatus = await conn.ExecuteScalarAsync<int?>(
            "SELECT TransferStatus FROM OrderChangeHeaders WHERE Id = @Id", new { header.Id });
        if (transferStatus == 2)
            throw new InvalidOperationException("此變更單已拋轉ERP，單頭無法再編輯，避免與ERP資料不一致");

        header.OriginalOrderNo = header.SoErpPrefix + header.SoErpNo;
        var sql = @"
            UPDATE OrderChangeHeaders SET
                SoErpPrefix     = @SoErpPrefix,
                SoErpNo         = @SoErpNo,
                OriginalOrderNo = @OriginalOrderNo
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
                "SELECT TransferStatus FROM OrderChangeHeaders WHERE Id = @Id", new { Id = id }, transaction);
            if (transferStatus == 2)
                throw new InvalidOperationException("此變更單已拋轉ERP，無法刪除，避免與ERP資料不一致");

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
        var header = await conn.QueryFirstOrDefaultAsync<OrderChangeHeader>(
            "SELECT * FROM OrderChangeHeaders WHERE Id = @HeaderId", new { detail.HeaderId });
        if (header == null)
            throw new InvalidOperationException("找不到對應的變更單頭");
        if (header.TransferStatus == 2)
            throw new InvalidOperationException("此變更單已拋轉ERP，無法新增明細，避免與ERP資料不一致");

        await FillOriginalDataAsync(header.SoErpPrefix, header.SoErpNo, detail);

        detail.CreatedAt = DateTime.Now;
        var sql = @"
            INSERT INTO OrderChangeDetails
                (HeaderId, SeqNo, ProductCode, ProductName, Warehouse,
                 Quantity, Unit, UnitPrice, Amount,
                 OriginalDeliveryDate, NewDeliveryDate, CreatedAt)
            VALUES
                (@HeaderId, @SeqNo, @ProductCode, @ProductName, @Warehouse,
                 @Quantity, @Unit, @UnitPrice, @Amount,
                 @OriginalDeliveryDate, @NewDeliveryDate, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await conn.ExecuteScalarAsync<int>(sql, detail);
    }

    public async Task<bool> UpdateDetailAsync(OrderChangeDetail detail)
    {
        using var conn = _db.CreateConnection();
        var header = await conn.QueryFirstOrDefaultAsync<OrderChangeHeader>(
            @"SELECT h.* FROM OrderChangeHeaders h
              JOIN OrderChangeDetails d ON d.HeaderId = h.Id
              WHERE d.Id = @Id", new { detail.Id });
        if (header == null)
            throw new InvalidOperationException("找不到對應的變更單頭");
        if (header.TransferStatus == 2)
            throw new InvalidOperationException("此變更單已拋轉ERP，明細無法再編輯，避免與ERP資料不一致");

        await FillOriginalDataAsync(header.SoErpPrefix, header.SoErpNo, detail);

        var sql = @"
            UPDATE OrderChangeDetails SET
                SeqNo                = @SeqNo,
                ProductCode           = @ProductCode,
                ProductName           = @ProductName,
                Warehouse             = @Warehouse,
                Quantity              = @Quantity,
                Unit                  = @Unit,
                UnitPrice             = @UnitPrice,
                Amount                = @Amount,
                OriginalDeliveryDate  = @OriginalDeliveryDate,
                NewDeliveryDate       = @NewDeliveryDate
            WHERE Id = @Id";
        return await conn.ExecuteAsync(sql, detail) > 0;
    }

    public async Task<bool> DeleteDetailAsync(int id)
    {
        using var conn = _db.CreateConnection();
        var transferStatus = await conn.ExecuteScalarAsync<int?>(
            @"SELECT h.TransferStatus FROM OrderChangeDetails d
              JOIN OrderChangeHeaders h ON h.Id = d.HeaderId
              WHERE d.Id = @Id", new { Id = id });
        if (transferStatus == 2)
            throw new InvalidOperationException("此變更單已拋轉ERP，明細無法刪除，避免與ERP資料不一致");

        return await conn.ExecuteAsync("DELETE FROM OrderChangeDetails WHERE Id = @Id", new { Id = id }) > 0;
    }

    // ========== Excel 解析 / 匯入 / 範本 ==========
    // 匯入格式參考 Data\匯入格式_訂單交期變更.xlsx：僅 單別(col45)/單號(col46)/序號(col47)/新交期(col48) 四欄
    // 品號/品名/庫別/數量/單位/單價/金額/原交期 一律向 ERP COPTD 查詢帶出 (依 單別+單號+序號)

    public async Task<OrderChangeViewModel> ParseExcelAsync(Stream fileStream, string fileName)
    {
        var result = new OrderChangeViewModel { FileName = fileName };
        var rawRows = new List<(string Prefix, string No, OrderChangeDetail Detail)>();

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
            var prefix = ws.Cell(row, 45).GetString().Trim();
            var no = ws.Cell(row, 46).GetString().Trim();
            var seqNo = ws.Cell(row, 47).GetString().Trim();
            var newDeliveryDate = ParseDateTime(ws.Cell(row, 48));

            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(no))
                continue;

            var detail = new OrderChangeDetail
            {
                SeqNo = seqNo,
                NewDeliveryDate = newDeliveryDate,
            };

            rawRows.Add((prefix, no, detail));
        }

        if (rawRows.Count == 0)
        {
            result.Errors.Add("Excel 無有效資料列");
            return result;
        }

        await FillOriginalDataAsync(rawRows.Select(r => (r.Prefix, r.No, r.Detail)));

        var groups = rawRows
            .GroupBy(r => new { r.Prefix, r.No })
            .ToList();

        foreach (var g in groups)
        {
            var header = new OrderChangeHeader
            {
                SoErpPrefix = g.Key.Prefix,
                SoErpNo = g.Key.No,
                OriginalOrderNo = g.Key.Prefix + g.Key.No,
                DetailCount = g.Count(),
            };

            result.HeaderGroups.Add(new OrderChangeHeaderGroup
            {
                Header = header,
                Details = g.Select(r => r.Detail).ToList()
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

            foreach (var group in groups)
            {
                var hdr = group.Header;
                var batchNo = await GenerateBatchNoAsync(conn, transaction, today);

                hdr.ImportBatchNo = batchNo;
                hdr.OriginalOrderNo = hdr.SoErpPrefix + hdr.SoErpNo;
                hdr.ImportStatus = "已匯入";
                hdr.ImportedAt = DateTime.Now;
                hdr.CreatedAt = DateTime.Now;
                hdr.TransferStatus = 1;

                var insertHeaderSql = @"
                    INSERT INTO OrderChangeHeaders
                        (ImportBatchNo, ErpOrderNo, SoErpPrefix, SoErpNo, OriginalOrderNo,
                         DetailCount, ImportStatus, ImportedAt, TransferStatus, CreatedAt)
                    VALUES
                        (@ImportBatchNo, @ErpOrderNo, @SoErpPrefix, @SoErpNo, @OriginalOrderNo,
                         @DetailCount, @ImportStatus, @ImportedAt, @TransferStatus, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var headerId = await conn.ExecuteScalarAsync<int>(insertHeaderSql, hdr, transaction);
                headersDone++;

                foreach (var dtl in group.Details)
                {
                    dtl.HeaderId = headerId;
                    dtl.CreatedAt = DateTime.Now;

                    var insertDetailSql = @"
                        INSERT INTO OrderChangeDetails
                            (HeaderId, SeqNo, ProductCode, ProductName, Warehouse,
                             Quantity, Unit, UnitPrice, Amount,
                             OriginalDeliveryDate, NewDeliveryDate, CreatedAt)
                        VALUES
                            (@HeaderId, @SeqNo, @ProductCode, @ProductName, @Warehouse,
                             @Quantity, @Unit, @UnitPrice, @Amount,
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
        var ws = wb.AddWorksheet("批次匯入表");

        ws.Cell(1, 1).Value = "說明欄";
        ws.Cell(1, 48).Value = "格式:YYYYMMDD";

        ws.Cell(2, 45).Value = "單別";
        ws.Cell(2, 46).Value = "單號";
        ws.Cell(2, 47).Value = "序號";
        ws.Cell(2, 48).Value = "新交期";

        var headerRange = ws.Range(2, 45, 2, 48);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        ws.Columns(45, 48).Width = 14;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ========== 拋轉 ERP (訂單交期變更 → COPTE / COPTF) ==========

    public async Task<string> TransferToErpAsync(int headerId, string operatorAccount)
    {
        var header = await GetHeaderByIdAsync(headerId);
        if (header == null) return "單據不存在";

        if (header.TransferStatus == 2)
            return "此單據已拋轉過ERP";

        var details = (await GetDetailsByHeaderIdAsync(headerId)).ToList();
        if (details.Count == 0) return "無明細資料，無法拋轉";

        if (details.Any(d => string.IsNullOrWhiteSpace(d.ProductCode)))
            return "部分明細於ERP查無原始品號資料，無法拋轉";

        if (!await _erpPermissionService.HasPermissionAsync(operatorAccount, "COPI07"))
            return "ERP帳號無訂單變更權限";

        var dateNow = DateTime.Now.ToString("yyyyMMdd");
        var erpPrefix = header.SoErpPrefix;
        var erpNo = header.SoErpNo;
        string companyNo = "";
        string changeVer = "0001";

        try
        {
            // === Phase 1: ERP 端操作 (TransactionScope 包裹) ===
            {
                using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                using var erpConn = new SqlConnection(_erpConnectionAccessor.GetConnectionString());
                await erpConn.OpenAsync();

                // 取得公司別 (CMSMB)
                var factoryResult = await erpConn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT TOP 1 RTRIM(LTRIM(COMPANY)) COMPANY FROM CMSMB");
                if (factoryResult != null)
                    companyNo = factoryResult.COMPANY ?? "";

                // 產生變更版次 TE003 (同一張原訂單可能已變更過，取現有最大版次+1)
                var maxVer = await erpConn.ExecuteScalarAsync<string?>(
                    "SELECT MAX(RTRIM(LTRIM(TE003))) FROM COPTE WHERE RTRIM(TE001) = @Prefix AND RTRIM(TE002) = @No",
                    new { Prefix = erpPrefix, No = erpNo });
                if (int.TryParse(maxVer, out var lastVer))
                    changeVer = (lastVer + 1).ToString("D4");

                // ========== 寫入 COPTE (訂單變更單頭) ==========
                var coptEParams = new
                {
                    COMPANY = companyNo, CREATOR = "IMPORT", USR_GROUP = "",
                    CREATE_DATE = dateNow,
                    MODIFIER = "", MODI_DATE = "", FLAG = "1",
                    TE001 = erpPrefix, TE002 = erpNo, TE003 = changeVer,
                    TE004 = dateNow, TE005 = "N", TE006 = "交期變更",
                    TE029 = "Y",
                };

                await erpConn.ExecuteAsync(@"
                    INSERT INTO COPTE (COMPANY, CREATOR, USR_GROUP, CREATE_DATE, MODIFIER, MODI_DATE, FLAG,
                        TE001, TE002, TE003, TE004, TE005, TE006, TE029)
                    VALUES (@COMPANY, @CREATOR, @USR_GROUP, @CREATE_DATE, @MODIFIER, @MODI_DATE, @FLAG,
                        @TE001, @TE002, @TE003, @TE004, @TE005, @TE006, @TE029)", coptEParams);

                // ========== 寫入 COPTF (訂單變更單身) ==========
                // 新值區塊(TF)與原值區塊(TF1xx)：僅交期不同(TF015=新交期/TF115=原交期)，其餘品號/數量/單價維持不變
                foreach (var d in details)
                {
                    var coptFParams = new
                    {
                        COMPANY = companyNo, CREATOR = "IMPORT", USR_GROUP = "",
                        CREATE_DATE = dateNow,
                        MODIFIER = "", MODI_DATE = "", FLAG = "1",

                        TF001 = erpPrefix, TF002 = erpNo, TF003 = changeVer, TF004 = d.SeqNo,
                        TF005 = d.ProductCode ?? "", TF006 = d.ProductName ?? "",
                        TF008 = d.Warehouse ?? "", TF009 = d.Quantity ?? 0, TF010 = d.Unit ?? "",
                        TF013 = d.UnitPrice ?? 0, TF014 = d.Amount ?? 0,
                        TF015 = d.NewDeliveryDate?.ToString("yyyyMMdd") ?? "",
                        TF017 = "N",

                        TF104 = d.SeqNo, TF105 = d.ProductCode ?? "", TF106 = d.ProductName ?? "",
                        TF108 = d.Warehouse ?? "", TF109 = d.Quantity ?? 0, TF110 = d.Unit ?? "",
                        TF113 = d.UnitPrice ?? 0, TF114 = d.Amount ?? 0,
                        TF115 = d.OriginalDeliveryDate?.ToString("yyyyMMdd") ?? "",
                        TF117 = "N",
                    };

                    await erpConn.ExecuteAsync(@"
                        INSERT INTO COPTF (COMPANY, CREATOR, USR_GROUP, CREATE_DATE, MODIFIER, MODI_DATE, FLAG,
                            TF001, TF002, TF003, TF004, TF005, TF006, TF008, TF009, TF010, TF013, TF014, TF015, TF017,
                            TF104, TF105, TF106, TF108, TF109, TF110, TF113, TF114, TF115, TF117)
                        VALUES (@COMPANY, @CREATOR, @USR_GROUP, @CREATE_DATE, @MODIFIER, @MODI_DATE, @FLAG,
                            @TF001, @TF002, @TF003, @TF004, @TF005, @TF006, @TF008, @TF009, @TF010, @TF013, @TF014, @TF015, @TF017,
                            @TF104, @TF105, @TF106, @TF108, @TF109, @TF110, @TF113, @TF114, @TF115, @TF117)", coptFParams);

                    // ========== 同步更新原訂單 COPTD：預交日/確認碼(預設Y)/確認者(Login帳號) ==========
                    await erpConn.ExecuteAsync(@"
                        UPDATE COPTD SET
                            TD013 = @NewDeliveryDate,
                            TD021 = 'Y',
                            MODIFIER = @Operator,
                            MODI_DATE = @ModiDate
                        WHERE RTRIM(TD001) = @Prefix AND RTRIM(TD002) = @No AND RTRIM(TD003) = @SeqNo",
                        new
                        {
                            NewDeliveryDate = d.NewDeliveryDate?.ToString("yyyyMMdd") ?? "",
                            Operator = operatorAccount.Length > 10 ? operatorAccount[..10] : operatorAccount,
                            ModiDate = dateNow,
                            Prefix = erpPrefix, No = erpNo, SeqNo = d.SeqNo
                        });
                }

                ts.Complete();
            } // TransactionScope 在這裡 Dispose，釋放 ambient transaction

            // === Phase 2: 更新本地狀態（TransactionScope 已結束，不會 enlist 衝突）===
            using var localConn = _db.CreateConnection();
            await localConn.ExecuteAsync(
                @"UPDATE OrderChangeHeaders
                  SET TransferStatus = 2, TransferMessage = NULL, ErpOrderNo = @ErpOrderNo
                  WHERE Id = @Id",
                new { Id = headerId, ErpOrderNo = $"{erpPrefix}-{erpNo}-{changeVer}" });

            return $"OK|{erpPrefix}-{erpNo}-{changeVer}";
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

    /// <summary>依單別+單號+序號查詢 ERP COPTD，帶出品號/品名/庫別/數量/單位/單價/金額/原交期 (批次版本，供 Excel 解析用)</summary>
    private async Task FillOriginalDataAsync(IEnumerable<(string Prefix, string No, OrderChangeDetail Detail)> rows)
    {
        try
        {
            using var erpConn = new SqlConnection(_erpConnectionAccessor.GetConnectionString());
            await erpConn.OpenAsync();

            foreach (var (prefix, no, detail) in rows)
            {
                await TryFillDetailFromErpAsync(erpConn, prefix, no, detail);
            }
        }
        catch
        {
            // ERP 無法連線時略過帶入原始資料，維持空白讓使用者於拋轉前手動確認
        }
    }

    /// <summary>依單別+單號+序號查詢 ERP COPTD，帶出品號/品名/庫別/數量/單位/單價/金額/原交期 (單筆版本，供手動新增/編輯明細用)</summary>
    private async Task FillOriginalDataAsync(string prefix, string no, OrderChangeDetail detail)
    {
        try
        {
            using var erpConn = new SqlConnection(_erpConnectionAccessor.GetConnectionString());
            await erpConn.OpenAsync();
            await TryFillDetailFromErpAsync(erpConn, prefix, no, detail);
        }
        catch
        {
            // ERP 無法連線時略過帶入原始資料
        }
    }

    private static async Task<bool> TryFillDetailFromErpAsync(SqlConnection erpConn, string prefix, string no, OrderChangeDetail detail)
    {
        var line = await erpConn.QueryFirstOrDefaultAsync<OriginalDetailLookup>(
            @"SELECT RTRIM(TD004) AS ProductCode, RTRIM(TD005) AS ProductName,
                     RTRIM(TD007) AS Warehouse, TD008 AS Quantity, RTRIM(TD010) AS Unit,
                     TD011 AS UnitPrice, TD012 AS Amount, RTRIM(TD013) AS DeliveryDateRaw
              FROM COPTD WHERE RTRIM(TD001) = @Prefix AND RTRIM(TD002) = @No AND RTRIM(TD003) = @SeqNo",
            new { Prefix = prefix, No = no, SeqNo = detail.SeqNo.Trim() });

        if (line == null) return false;

        detail.ProductCode = line.ProductCode;
        detail.ProductName = line.ProductName;
        detail.Warehouse = line.Warehouse;
        detail.Quantity = line.Quantity;
        detail.Unit = line.Unit;
        detail.UnitPrice = line.UnitPrice;
        detail.Amount = line.Amount;
        detail.OriginalDeliveryDate = ParseYmd(line.DeliveryDateRaw);
        return true;
    }

    private sealed record OriginalDetailLookup(
        string ProductCode, string ProductName, string Warehouse,
        decimal Quantity, string Unit, decimal UnitPrice, decimal Amount, string DeliveryDateRaw);

    private static DateTime? ParseYmd(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParseExact(s.Trim(), "yyyyMMdd", null,
            System.Globalization.DateTimeStyles.None, out var dt) ? dt : null;
    }

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

        // ERP 交期慣例：以 YYYYMMDD 存放 (文字或一般數字格式)，非 Excel 日期序列值
        if (cell.TryGetValue(out double d))
        {
            if (d is >= 19000101 and <= 99991231 && DateTime.TryParseExact(
                    ((long)d).ToString(), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var ymd))
                return ymd;

            if (d > 1)
            {
                try { return DateTime.FromOADate(d); } catch { }
            }
        }

        var text = cell.GetString().Trim();
        if (text.Length == 8 && DateTime.TryParseExact(
                text, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var ymdText))
            return ymdText;
        if (DateTime.TryParse(text, out var parsed))
            return parsed;
        return null;
    }
}
