-- MêTruyệnPlus: fix CHECK constraint for topping up Xu (coins).
-- Chạy script này trên CSDL WebDocTruyenDb đang dùng. Có thể chạy lại.
-- Không xóa bảng, không xóa bản ghi hoặc thay đổi trạng thái thanh toán.
USE [WebDocTruyenDb];
GO
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.PaymentTransactions', N'U') IS NULL
    THROW 51001, N'Không tồn tại bảng dbo.PaymentTransactions. Hãy chạy SQL/upgrade_case_study.sql trước.', 1;
GO
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.PaymentTransactions')
          AND name = N'CK_PaymentTransactions_Type'
    )
        ALTER TABLE dbo.PaymentTransactions DROP CONSTRAINT CK_PaymentTransactions_Type;

    ALTER TABLE dbo.PaymentTransactions WITH CHECK
        ADD CONSTRAINT CK_PaymentTransactions_Type
        CHECK ([Type] IN ('plan', 'story', 'coins'));
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
-- Kiểm tra ràng buộc vừa cập nhật.
SELECT name, definition, is_disabled, is_not_trusted
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.PaymentTransactions')
  AND name = N'CK_PaymentTransactions_Type';
GO
