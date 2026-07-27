-- ============================================
-- 移除報價單匯入功能 (QuotationImport)
-- 對應功能已從系統與選單中移除
-- ============================================

USE ImportDataToERP;
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'QuotationImports') AND type = 'U')
BEGIN
    DROP TABLE QuotationImports;
END
GO
