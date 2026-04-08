-- =====================================================
-- Script: Cập nhật RefundAmount cho các hợp đồng cũ
-- Mục đích: Tính lại RefundAmount cho các hợp đồng đã thanh lý trước khi có logic mới
-- Công thức: RefundAmount = TotalPaid - TotalInvoice - AdditionalCost
-- Ngày tạo: 2026-04-07
-- =====================================================

USE SENTANA;
GO

-- Bước 1: Kiểm tra dữ liệu trước khi update
PRINT '========================================';
PRINT 'BƯỚC 1: KIỂM TRA DỮ LIỆU TRƯỚC KHI UPDATE';
PRINT '========================================';
PRINT '';

SELECT 
    c.ContractId,
    c.ContractCode,
    c.Status,
    c.Deposit,
    c.AdditionalCost AS CurrentAdditionalCost,
    c.RefundAmount AS CurrentRefundAmount,
    ISNULL(SUM(i.TotalMoney), 0) AS TotalInvoice,
    ISNULL(SUM(pt.AmountPaid), 0) AS TotalPaid,
    ISNULL(SUM(pt.AmountPaid), 0) - ISNULL(SUM(i.TotalMoney), 0) - ISNULL(c.AdditionalCost, 0) AS CalculatedRefundAmount
FROM Contract c
LEFT JOIN Invoice i ON c.ContractId = i.ContractId
LEFT JOIN PaymentTransaction pt ON i.InvoiceId = pt.InvoiceId
WHERE c.Status = 0  -- Inactive (đã thanh lý)
  AND c.IsDeleted = 0
GROUP BY 
    c.ContractId, 
    c.ContractCode, 
    c.Status, 
    c.Deposit, 
    c.AdditionalCost, 
    c.RefundAmount
ORDER BY c.ContractId;

PRINT '';
PRINT '========================================';
PRINT 'BƯỚC 2: CÂP NHẬT REFUNDAMOUNT';
PRINT '========================================';
PRINT '';

-- Bước 2: Cập nhật RefundAmount
BEGIN TRANSACTION;

BEGIN TRY
    -- Tạo bảng tạm để lưu kết quả tính toán
    IF OBJECT_ID('tempdb..#RefundCalculation') IS NOT NULL
        DROP TABLE #RefundCalculation;

    CREATE TABLE #RefundCalculation (
        ContractId INT,
        TotalInvoice DECIMAL(18, 2),
        TotalPaid DECIMAL(18, 2),
        AdditionalCost DECIMAL(18, 2),
        CalculatedRefund DECIMAL(18, 2)
    );

    -- Tính toán RefundAmount cho từng hợp đồng
    INSERT INTO #RefundCalculation (ContractId, TotalInvoice, TotalPaid, AdditionalCost, CalculatedRefund)
    SELECT 
        c.ContractId,
        ISNULL(SUM(i.TotalMoney), 0) AS TotalInvoice,
        ISNULL(SUM(pt.AmountPaid), 0) AS TotalPaid,
        ISNULL(c.AdditionalCost, 0) AS AdditionalCost,
        ISNULL(SUM(pt.AmountPaid), 0) - ISNULL(SUM(i.TotalMoney), 0) - ISNULL(c.AdditionalCost, 0) AS CalculatedRefund
    FROM Contract c
    LEFT JOIN Invoice i ON c.ContractId = i.ContractId
    LEFT JOIN PaymentTransaction pt ON i.InvoiceId = pt.InvoiceId
    WHERE c.Status = 0  -- Inactive
      AND c.IsDeleted = 0
    GROUP BY c.ContractId, c.AdditionalCost;

    -- Cập nhật RefundAmount vào bảng Contract
    UPDATE c
    SET 
        c.RefundAmount = rc.CalculatedRefund,
        c.UpdatedAt = GETDATE()
    FROM Contract c
    INNER JOIN #RefundCalculation rc ON c.ContractId = rc.ContractId;

    -- Hiển thị số lượng bản ghi đã cập nhật
    DECLARE @UpdatedCount INT = @@ROWCOUNT;
    PRINT 'Đã cập nhật ' + CAST(@UpdatedCount AS VARCHAR(10)) + ' hợp đồng.';
    PRINT '';

    -- Commit transaction
    COMMIT TRANSACTION;
    PRINT 'Transaction đã được COMMIT thành công!';
    PRINT '';

END TRY
BEGIN CATCH
    -- Rollback nếu có lỗi
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT 'LỖI XẢY RA! Transaction đã được ROLLBACK.';
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR(10));
END CATCH;

PRINT '========================================';
PRINT 'BƯỚC 3: KIỂM TRA DỮ LIỆU SAU KHI UPDATE';
PRINT '========================================';
PRINT '';

-- Bước 3: Kiểm tra lại sau khi update
SELECT 
    c.ContractId,
    c.ContractCode,
    c.Status,
    c.Deposit,
    c.AdditionalCost,
    c.RefundAmount AS UpdatedRefundAmount,
    ISNULL(SUM(i.TotalMoney), 0) AS TotalInvoice,
    ISNULL(SUM(pt.AmountPaid), 0) AS TotalPaid,
    CASE 
        WHEN c.RefundAmount > 0 THEN 'BQL hoàn trả cho khách'
        WHEN c.RefundAmount < 0 THEN 'Khách còn nợ BQL'
        ELSE 'Đã thanh toán đủ'
    END AS PaymentStatus
FROM Contract c
LEFT JOIN Invoice i ON c.ContractId = i.ContractId
LEFT JOIN PaymentTransaction pt ON i.InvoiceId = pt.InvoiceId
WHERE c.Status = 0
  AND c.IsDeleted = 0
GROUP BY 
    c.ContractId, 
    c.ContractCode, 
    c.Status, 
    c.Deposit, 
    c.AdditionalCost, 
    c.RefundAmount
ORDER BY c.ContractId;

PRINT '';
PRINT '========================================';
PRINT 'HOÀN TẤT!';
PRINT '========================================';

-- Dọn dẹp bảng tạm
IF OBJECT_ID('tempdb..#RefundCalculation') IS NOT NULL
    DROP TABLE #RefundCalculation;

GO
