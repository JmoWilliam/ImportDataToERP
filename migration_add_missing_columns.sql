-- =========================================
-- Migration: 補齊 OrderChangeHeaders 缺漏的 ERP 欄位
-- 執行於 ImportDataToERP 資料庫
-- =========================================
USE ImportDataToERP;
GO

-- ChangeType: 補欄位或補預設值
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'ChangeType')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD ChangeType NVARCHAR(20) NOT NULL DEFAULT N'';
    PRINT '已新增 ChangeType';
END
ELSE
BEGIN
    -- 欄位已存在但可能沒有 DEFAULT，補上保護
    BEGIN TRY
        ALTER TABLE OrderChangeHeaders ADD CONSTRAINT DF_OrderChangeHeaders_ChangeType DEFAULT N'' FOR ChangeType;
        PRINT '已為 ChangeType 補上 DEFAULT 限制';
    END TRY
    BEGIN CATCH
        PRINT 'ChangeType DEFAULT 已存在或無法新增，略過';
    END CATCH;
END

-- 原訂單單別
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'SoErpPrefix')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD SoErpPrefix NVARCHAR(10) NULL;
    PRINT '已新增 SoErpPrefix';
END

-- 原訂單單號
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'SoErpNo')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD SoErpNo NVARCHAR(20) NULL;
    PRINT '已新增 SoErpNo';
END

-- 單據日期
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'DocDate')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD DocDate DATETIME2 NULL;
    PRINT '已新增 DocDate';
END

-- 客戶採購單
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'CustomerPurchaseOrder')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD CustomerPurchaseOrder NVARCHAR(20) NULL;
    PRINT '已新增 CustomerPurchaseOrder';
END

-- 客戶地址(一)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'CustomerAddressFirst')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD CustomerAddressFirst NVARCHAR(200) NULL;
    PRINT '已新增 CustomerAddressFirst';
END

-- 客戶地址(二)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'CustomerAddressSecond')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD CustomerAddressSecond NVARCHAR(200) NULL;
    PRINT '已新增 CustomerAddressSecond';
END

-- 部門代號
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'DepartmentId')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD DepartmentId NVARCHAR(20) NULL;
    PRINT '已新增 DepartmentId';
END

-- 業務人員
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'SalesRep')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD SalesRep NVARCHAR(20) NULL;
    PRINT '已新增 SalesRep';
END

-- 備註
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'Remarks')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD Remarks NVARCHAR(500) NULL;
    PRINT '已新增 Remarks';
END

-- 交易幣別
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'Currency')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD Currency NVARCHAR(10) NULL;
    PRINT '已新增 Currency';
END

-- 匯率
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'ExchangeRate')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD ExchangeRate DECIMAL(18,4) NOT NULL DEFAULT 1;
    PRINT '已新增 ExchangeRate';
END

-- 交易條件
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'TradeTerm')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD TradeTerm NVARCHAR(10) NULL;
    PRINT '已新增 TradeTerm';
END

-- 稅別碼
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'TaxNo')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD TaxNo NVARCHAR(10) NULL;
    PRINT '已新增 TaxNo';
END

-- 課稅別
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'Taxation')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD Taxation NVARCHAR(10) NULL;
    PRINT '已新增 Taxation';
END

-- 營業稅率
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'BusinessTaxRate')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD BusinessTaxRate DECIMAL(8,5) NOT NULL DEFAULT 0;
    PRINT '已新增 BusinessTaxRate';
END

-- 付款條件
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'PaymentTerm')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD PaymentTerm NVARCHAR(20) NULL;
    PRINT '已新增 PaymentTerm';
END

-- 價格條件
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'PriceTerm')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD PriceTerm NVARCHAR(40) NULL;
    PRINT '已新增 PriceTerm';
END

-- 訂金分批
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'DepositPartial')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD DepositPartial NVARCHAR(1) NOT NULL DEFAULT 'N';
    PRINT '已新增 DepositPartial';
END

-- 訂金比率
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'DepositRate')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD DepositRate DECIMAL(8,5) NOT NULL DEFAULT 0;
    PRINT '已新增 DepositRate';
END

-- 單身多稅率
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'DetailMultiTax')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD DetailMultiTax NVARCHAR(1) NOT NULL DEFAULT 'N';
    PRINT '已新增 DetailMultiTax';
END

-- 運輸方式
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'ShipMethod')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD ShipMethod NVARCHAR(10) NULL;
    PRINT '已新增 ShipMethod';
END

-- 整張結案
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'ClosureStatus')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD ClosureStatus NVARCHAR(1) NOT NULL DEFAULT 'N';
    PRINT '已新增 ClosureStatus';
END

-- 確認碼
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'ConfirmStatus')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD ConfirmStatus NVARCHAR(1) NOT NULL DEFAULT 'N';
    PRINT '已新增 ConfirmStatus';
END

-- 明細筆數
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND name = 'DetailCount')
BEGIN
    ALTER TABLE OrderChangeHeaders ADD DetailCount INT NOT NULL DEFAULT 0;
    PRINT '已新增 DetailCount';
END

PRINT '✅ OrderChangeHeaders 欄位遷移完成！';
GO
