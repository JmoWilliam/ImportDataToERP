-- ============================================
-- ImportDataToERP 資料庫初始化腳本
-- 請先在 SQL Server 建立資料庫 ImportDataToERP
-- ============================================

-- 建立資料庫（如果尚未建立）
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ImportDataToERP')
BEGIN
    CREATE DATABASE ImportDataToERP;
END
GO

USE ImportDataToERP;
GO

-- ============================================
-- 1. 使用者資料表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'Users') AND type = 'U')
BEGIN
    CREATE TABLE Users (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Account       NVARCHAR(50)  NOT NULL,
        Name          NVARCHAR(50)  NOT NULL,
        Email         NVARCHAR(100) NULL,
        PasswordHash  NVARCHAR(64)  NOT NULL,
        IsActive      BIT           NOT NULL DEFAULT 1,
        CreatedAt     DATETIME2     NOT NULL DEFAULT GETDATE(),
        UpdatedAt     DATETIME2     NOT NULL DEFAULT GETDATE()
    );
END
GO

-- 預設管理員帳號 (admin / admin123)
-- PasswordHash = SHA256("admin123")
IF NOT EXISTS (SELECT 1 FROM Users WHERE Account = 'admin')
BEGIN
    INSERT INTO Users (Account, Name, Email, PasswordHash, IsActive, CreatedAt, UpdatedAt)
    VALUES ('admin', N'系統管理員', 'admin@company.com',
            '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9',
            1, GETDATE(), GETDATE());
END
GO

-- ============================================
-- 2. 報價單匯入資料表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'QuotationImports') AND type = 'U')
BEGIN
    CREATE TABLE QuotationImports (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        QuotationNo     NVARCHAR(50)   NOT NULL,
        CustomerCode    NVARCHAR(50)   NOT NULL,
        CustomerName    NVARCHAR(100)  NOT NULL,
        ProductCode     NVARCHAR(50)   NOT NULL,
        ProductName     NVARCHAR(100)  NOT NULL,
        Quantity        DECIMAL(18,4)  NOT NULL DEFAULT 0,
        UnitPrice       DECIMAL(18,4)  NOT NULL DEFAULT 0,
        Currency        NVARCHAR(10)   NOT NULL DEFAULT N'TWD',
        ImportStatus    NVARCHAR(20)   NOT NULL DEFAULT N'待匯入',
        ImportedAt      DATETIME2      NULL,
        CreatedAt       DATETIME2      NOT NULL DEFAULT GETDATE()
    );
END
GO

-- ============================================
-- 3. 訂單匯入 - 單頭 (Excel A~BC欄位)
--    匯入單號: YYYYMMDD + 3碼流水號 (自動產生)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND type = 'U')
BEGIN
    CREATE TABLE OrderImportHeaders (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        ImportBatchNo   NVARCHAR(20)   NOT NULL,       -- YYYYMMDD + 3碼流水號
        ErpOrderNo      NVARCHAR(50)   NULL,           -- 拋轉後ERP單號 (TC001+TC002)
        OrderType       NVARCHAR(10)   NOT NULL,       -- 單別 (Col B)
        OrderNo         NVARCHAR(50)   NULL,           -- 單號 (Col C)
        CustomerCode    NVARCHAR(50)   NOT NULL,       -- 客戶代號 (Col D)
        DeptCode        NVARCHAR(20)   NULL,           -- 部門代號 (Col E)
        Remarks         NVARCHAR(500)  NULL,           -- 備註 (Col F)
        OrderDate       DATETIME2      NULL,           -- 訂單日期 (Col G)
        SalesRep        NVARCHAR(20)   NULL,           -- 業務人員 (Col H)
        FactoryCode     NVARCHAR(20)   NULL,           -- 廠別代號 (Col I)
        DocDate         DATETIME2      NULL,           -- 單據日期 (Col J)
        TaxType         NVARCHAR(10)   NULL,           -- 課稅別 (依 COPMA.MA038 帶出)
        PaymentTermName NVARCHAR(20)   NULL,           -- 付款條件名稱 (依 COPMA.MA031 帶出)
        PaymentTermCode NVARCHAR(10)   NULL,           -- 付款條件代碼 (依 COPMA.MA083 帶出)
        ImportStatus    NVARCHAR(20)   NOT NULL DEFAULT N'待匯入',
        ImportedAt      DATETIME2      NULL,
        CreatedByAccount NVARCHAR(50)  NULL,           -- 建立/匯入此單頭的登入帳號
        TransferStatus  INT            NOT NULL DEFAULT 1,   -- 1=未拋轉 2=已拋轉 3=拋轉失敗
        TransferMessage NVARCHAR(500)  NULL,           -- 拋轉失敗訊息
        CreatedAt       DATETIME2      NOT NULL DEFAULT GETDATE()
    );

    -- 索引：避免重複匯入 (相同客戶+單別+訂單日期)
    CREATE UNIQUE INDEX IX_OrderImportHeaders_BatchNo ON OrderImportHeaders(ImportBatchNo);
END
GO

-- ============================================
-- 4. 訂單匯入 - 單身 (Excel BC欄以後)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderImportDetails') AND type = 'U')
BEGIN
    CREATE TABLE OrderImportDetails (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        HeaderId        INT            NOT NULL,       -- 關聯 OrderImportHeaders.Id
        OrderType       NVARCHAR(10)   NOT NULL,       -- 單別 (Col BC)
        SeqNo           NVARCHAR(20)   NULL,           -- 序號 (Col BD)
        ProductCode     NVARCHAR(50)   NULL,           -- 品號 (Col BE)
        ProductName     NVARCHAR(100)  NULL,           -- 品名 (Col BF)
        Warehouse       NVARCHAR(20)   NULL,           -- 庫別 (Col BG)
        OrderQty        DECIMAL(18,4)  NOT NULL DEFAULT 0,  -- 訂單數量 (Col BH)
        Unit            NVARCHAR(10)   NULL,           -- 單位 (Col BI)
        UnitPrice       DECIMAL(18,4)  NOT NULL DEFAULT 0,  -- 單價 (Col BJ，稅金換算後)
        Amount          DECIMAL(18,4)  NOT NULL DEFAULT 0,  -- 金額 (Col BK，稅金換算後)
        Currency        NVARCHAR(10)   NULL,           -- 交易幣別 (Col BL)
        CustItemNo      NVARCHAR(50)   NULL,           -- 客戶品號 (依品號查 INVMB/COPMG 帶出)
        TaxAmount       DECIMAL(18,4)  NOT NULL DEFAULT 0,  -- 稅額 (依客戶課稅別/稅率換算)
        CreatedAt       DATETIME2      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_OrderImportDetails_Header
            FOREIGN KEY (HeaderId) REFERENCES OrderImportHeaders(Id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_OrderImportDetails_HeaderId ON OrderImportDetails(HeaderId);
END
GO

-- ============================================
-- 5. 訂單變更匯入資料表
-- ============================================
-- 訂單交期變更：僅需 單別/單號 識別原訂單，明細只記序號與新交期，
-- 品號/品名/庫別/數量/單位/單價/原交期一律於匯入/拋轉時向 ERP COPTD 查詢帶出
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND type = 'U')
BEGIN
    CREATE TABLE OrderChangeHeaders (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        ImportBatchNo       NVARCHAR(20)   NOT NULL,
        ErpOrderNo          NVARCHAR(50)   NULL,
        SoErpPrefix         NVARCHAR(10)   NOT NULL,   -- 原訂單單別
        SoErpNo             NVARCHAR(20)   NOT NULL,   -- 原訂單單號
        OriginalOrderNo     NVARCHAR(50)   NOT NULL,   -- SoErpPrefix + SoErpNo
        DetailCount         INT            NOT NULL DEFAULT 0,
        ImportStatus        NVARCHAR(20)   NOT NULL DEFAULT N'待匯入',
        ImportedAt          DATETIME2      NULL,
        TransferStatus      INT            NOT NULL DEFAULT 1,
        TransferMessage     NVARCHAR(500)  NULL,
        CreatedAt           DATETIME2      NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX IX_OrderChangeHeaders_BatchNo ON OrderChangeHeaders(ImportBatchNo);
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderChangeDetails') AND type = 'U')
BEGIN
    CREATE TABLE OrderChangeDetails (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        HeaderId            INT            NOT NULL,
        SeqNo               NVARCHAR(10)   NOT NULL,   -- 原訂單明細序號 (COPTD.TD003)
        ProductCode         NVARCHAR(50)   NULL,
        ProductName         NVARCHAR(100)  NULL,
        Warehouse           NVARCHAR(10)   NULL,
        Quantity            DECIMAL(18,4)  NULL,
        Unit                NVARCHAR(4)    NULL,
        UnitPrice           DECIMAL(18,4)  NULL,
        Amount              DECIMAL(18,4)  NULL,
        OriginalDeliveryDate DATETIME2     NULL,
        NewDeliveryDate     DATETIME2      NULL,
        CreatedAt           DATETIME2      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_OrderChangeDetails_Header
            FOREIGN KEY (HeaderId) REFERENCES OrderChangeHeaders(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_OrderChangeDetails_HeaderId ON OrderChangeDetails(HeaderId);
END
GO

-- OrderChangeImports：不存在則建立，已存在則保留
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderChangeImports') AND type = 'U')
BEGIN
    CREATE TABLE OrderChangeImports (
        Id                    INT IDENTITY(1,1) PRIMARY KEY,
        OriginalOrderNo       NVARCHAR(50)   NOT NULL,
        ChangeNo              NVARCHAR(50)   NOT NULL,
        CustomerCode          NVARCHAR(50)   NOT NULL,
        CustomerName          NVARCHAR(100)  NOT NULL,
        ProductCode           NVARCHAR(50)   NOT NULL,
        ProductName           NVARCHAR(100)  NOT NULL,
        ChangeType            NVARCHAR(20)   NOT NULL,
        OriginalQuantity      DECIMAL(18,4)  NULL,
        NewQuantity           DECIMAL(18,4)  NULL,
        OriginalUnitPrice     DECIMAL(18,4)  NULL,
        NewUnitPrice          DECIMAL(18,4)  NULL,
        OriginalDeliveryDate  DATETIME2      NULL,
        NewDeliveryDate       DATETIME2      NULL,
        ChangeReason          NVARCHAR(500)  NULL,
        ImportStatus          NVARCHAR(20)   NOT NULL DEFAULT N'待匯入',
        ImportedAt            DATETIME2      NULL,
        CreatedAt             DATETIME2      NOT NULL DEFAULT GETDATE()
    );
END
ELSE
BEGIN
    PRINT N'偵測到舊版 OrderChangeImports 表，已保留 (可手動刪除)';
END
GO

-- ============================================
-- 6. 若舊版 OrderImports 表存在則保留備份後移除
-- ============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderImports') AND type = 'U')
BEGIN
    PRINT N'偵測到舊版 OrderImports 表，已保留 (可手動刪除)';
END
GO

-- ============================================
-- 7. 遷移：為現有 OrderImportHeaders 增加拋轉欄位
-- ============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND type = 'U')
BEGIN
    -- 增加 ErpOrderNo
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND name = 'ErpOrderNo')
    BEGIN
        ALTER TABLE OrderImportHeaders ADD ErpOrderNo NVARCHAR(50) NULL;
        PRINT N'已新增 OrderImportHeaders.ErpOrderNo 欄位';
    END

    -- 增加 TransferStatus (1=未拋轉 2=已拋轉 3=拋轉失敗)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND name = 'TransferStatus')
    BEGIN
        ALTER TABLE OrderImportHeaders ADD TransferStatus INT NOT NULL DEFAULT 1;
        PRINT N'已新增 OrderImportHeaders.TransferStatus 欄位';
    END

    -- 增加 TransferMessage
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND name = 'TransferMessage')
    BEGIN
        ALTER TABLE OrderImportHeaders ADD TransferMessage NVARCHAR(500) NULL;
        PRINT N'已新增 OrderImportHeaders.TransferMessage 欄位';
    END
END
GO

-- ============================================
-- 8. 遷移：為現有 OrderImportDetails 增加客戶品號/稅額欄位
-- ============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderImportDetails') AND type = 'U')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportDetails') AND name = 'CustItemNo')
    BEGIN
        ALTER TABLE OrderImportDetails ADD CustItemNo NVARCHAR(50) NULL;
        PRINT N'已新增 OrderImportDetails.CustItemNo 欄位';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportDetails') AND name = 'TaxAmount')
    BEGIN
        ALTER TABLE OrderImportDetails ADD TaxAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
        PRINT N'已新增 OrderImportDetails.TaxAmount 欄位';
    END
END
GO

-- ============================================
-- 9. 遷移：為現有 OrderImportHeaders 增加付款條件欄位
-- ============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND type = 'U')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND name = 'PaymentTermName')
    BEGIN
        ALTER TABLE OrderImportHeaders ADD PaymentTermName NVARCHAR(20) NULL;
        PRINT N'已新增 OrderImportHeaders.PaymentTermName 欄位';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND name = 'PaymentTermCode')
    BEGIN
        ALTER TABLE OrderImportHeaders ADD PaymentTermCode NVARCHAR(10) NULL;
        PRINT N'已新增 OrderImportHeaders.PaymentTermCode 欄位';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'OrderImportHeaders') AND name = 'CreatedByAccount')
    BEGIN
        ALTER TABLE OrderImportHeaders ADD CreatedByAccount NVARCHAR(50) NULL;
        PRINT N'已新增 OrderImportHeaders.CreatedByAccount 欄位';
    END
END
GO

-- ============================================
-- 10. 遷移：舊版 OrderChangeImports → OrderChangeImports_OLD
-- ============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderChangeImports') AND type = 'U')
    AND NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderChangeImports_OLD') AND type = 'U')
BEGIN
    EXEC sp_rename 'OrderChangeImports', 'OrderChangeImports_OLD';
    PRINT N'已將舊版 OrderChangeImports 更名為 OrderChangeImports_OLD';
END
GO

PRINT N'資料庫初始化完成！';
GO
