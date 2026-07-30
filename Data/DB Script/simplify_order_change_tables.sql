-- ============================================
-- 簡化「訂單交期變更」資料表
-- 原本欄位過多(客戶資料/付款條件/價格條件/稅別等)，
-- 實際 Excel 格式只有：單別、單號、序號、新交期，
-- 其餘資料(品號/品名/庫別/數量/單位/單價/原交期)一律於拋轉/匯入時向 ERP COPTD 查詢帶出。
-- 兩張表目前皆無資料 (0 rows)，直接重建。
-- ============================================

USE ImportDataToERP;
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderChangeDetails') AND type = 'U')
BEGIN
    DROP TABLE OrderChangeDetails;
END
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'OrderChangeHeaders') AND type = 'U')
BEGIN
    DROP TABLE OrderChangeHeaders;
END
GO

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
    TransferStatus      INT            NOT NULL DEFAULT 1,   -- 1=未拋轉 2=已拋轉 3=拋轉失敗
    TransferMessage     NVARCHAR(500)  NULL,
    CreatedAt           DATETIME2      NOT NULL DEFAULT GETDATE()
);
CREATE UNIQUE INDEX IX_OrderChangeHeaders_BatchNo ON OrderChangeHeaders(ImportBatchNo);
GO

CREATE TABLE OrderChangeDetails (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    HeaderId            INT            NOT NULL,
    SeqNo               NVARCHAR(10)   NOT NULL,   -- 原訂單明細序號 (COPTD.TD003)
    ProductCode         NVARCHAR(50)   NULL,       -- 品號 (查COPTD帶出)
    ProductName         NVARCHAR(100)  NULL,       -- 品名 (查COPTD帶出)
    Warehouse           NVARCHAR(10)   NULL,       -- 庫別 (查COPTD帶出)
    Quantity            DECIMAL(18,4)  NULL,       -- 數量 (查COPTD帶出，不異動)
    Unit                NVARCHAR(4)    NULL,       -- 單位 (查COPTD帶出)
    UnitPrice           DECIMAL(18,4)  NULL,       -- 單價 (查COPTD帶出，不異動)
    Amount              DECIMAL(18,4)  NULL,       -- 金額 (查COPTD帶出，不異動)
    OriginalDeliveryDate DATETIME2     NULL,       -- 原交期 (查COPTD帶出)
    NewDeliveryDate     DATETIME2      NULL,       -- 新交期 (Excel輸入)
    CreatedAt           DATETIME2      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_OrderChangeDetails_Header
        FOREIGN KEY (HeaderId) REFERENCES OrderChangeHeaders(Id) ON DELETE CASCADE
);
CREATE INDEX IX_OrderChangeDetails_HeaderId ON OrderChangeDetails(HeaderId);
GO
