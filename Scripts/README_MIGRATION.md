# 📋 Hướng Dẫn Migration - Cập Nhật RefundAmount

## 🎯 Mục đích

Script này dùng để cập nhật lại `RefundAmount` cho các hợp đồng đã thanh lý TRƯỚC KHI có logic tính toán mới.

## 📊 Công thức

```
RefundAmount = TotalPaid - TotalInvoice - AdditionalCost
```

Trong đó:
- **TotalPaid**: Tổng số tiền khách đã thanh toán (từ bảng `PaymentTransaction`)
- **TotalInvoice**: Tổng số tiền hóa đơn phải trả (từ bảng `Invoice`)
- **AdditionalCost**: Chi phí phát sinh khi thanh lý hợp đồng

## 🚀 Cách chạy

### Cách 1: Sử dụng SQL Server Management Studio (SSMS)

1. Mở SSMS và kết nối đến SQL Server
2. Mở file `UpdateRefundAmountForOldContracts.sql`
3. Đảm bảo đang chọn database `SENTANA`
4. Nhấn F5 hoặc click "Execute"

### Cách 2: Sử dụng sqlcmd (Command Line)

```bash
sqlcmd -S YoungerOnlyAim -U sa -P 123 -d SENTANA -i UpdateRefundAmountForOldContracts.sql
```

### Cách 3: Sử dụng PowerShell

```powershell
Invoke-Sqlcmd -ServerInstance "YoungerOnlyAim" -Username "sa" -Password "123" -Database "SENTANA" -InputFile "UpdateRefundAmountForOldContracts.sql"
```

## 📝 Kết quả mong đợi

Script sẽ thực hiện 3 bước:

### Bước 1: Kiểm tra dữ liệu TRƯỚC khi update
Hiển thị bảng so sánh:
- `CurrentRefundAmount`: Giá trị hiện tại trong DB
- `CalculatedRefundAmount`: Giá trị tính toán theo công thức mới

### Bước 2: Cập nhật RefundAmount
- Tính toán lại `RefundAmount` cho tất cả hợp đồng đã thanh lý
- Cập nhật vào bảng `Contract`
- Hiển thị số lượng bản ghi đã cập nhật

### Bước 3: Kiểm tra dữ liệu SAU khi update
Hiển thị kết quả sau khi update với trạng thái:
- "BQL hoàn trả cho khách" (nếu RefundAmount > 0)
- "Khách còn nợ BQL" (nếu RefundAmount < 0)
- "Đã thanh toán đủ" (nếu RefundAmount = 0)

## ⚠️ Lưu ý quan trọng

1. **Backup trước khi chạy**: Nên backup database trước khi chạy script
2. **Chạy trên môi trường test trước**: Kiểm tra kỹ trên test environment
3. **Transaction safety**: Script sử dụng transaction, nếu có lỗi sẽ tự động rollback
4. **Chỉ ảnh hưởng hợp đồng đã thanh lý**: Script chỉ update các hợp đồng có `Status = 0` (Inactive)

## 🔍 Kiểm tra thủ công

Sau khi chạy script, bạn có thể kiểm tra thủ công bằng query:

```sql
SELECT 
    c.ContractId,
    c.ContractCode,
    c.RefundAmount,
    ISNULL(SUM(pt.AmountPaid), 0) AS TotalPaid,
    ISNULL(SUM(i.TotalMoney), 0) AS TotalInvoice,
    ISNULL(c.AdditionalCost, 0) AS AdditionalCost,
    -- Kiểm tra công thức
    ISNULL(SUM(pt.AmountPaid), 0) - ISNULL(SUM(i.TotalMoney), 0) - ISNULL(c.AdditionalCost, 0) AS ExpectedRefund,
    -- So sánh
    CASE 
        WHEN c.RefundAmount = (ISNULL(SUM(pt.AmountPaid), 0) - ISNULL(SUM(i.TotalMoney), 0) - ISNULL(c.AdditionalCost, 0))
        THEN '✅ ĐÚNG'
        ELSE '❌ SAI'
    END AS ValidationResult
FROM Contract c
LEFT JOIN Invoice i ON c.ContractId = i.ContractId
LEFT JOIN PaymentTransaction pt ON i.InvoiceId = pt.InvoiceId
WHERE c.Status = 0 AND c.IsDeleted = 0
GROUP BY c.ContractId, c.ContractCode, c.RefundAmount, c.AdditionalCost
ORDER BY c.ContractId;
```

## 📞 Hỗ trợ

Nếu gặp vấn đề khi chạy script, vui lòng kiểm tra:
1. Quyền truy cập database
2. Tên database có đúng là `SENTANA` không
3. Các bảng `Contract`, `Invoice`, `PaymentTransaction` có tồn tại không
4. Log lỗi trong output của script

## ✅ Checklist

- [ ] Đã backup database
- [ ] Đã test trên môi trường test
- [ ] Đã kiểm tra kết quả Bước 1 (dữ liệu trước update)
- [ ] Đã chạy script thành công
- [ ] Đã kiểm tra kết quả Bước 3 (dữ liệu sau update)
- [ ] Đã verify bằng query kiểm tra thủ công
- [ ] Đã test API `/contract/view-contract/{id}` để xem RefundAmount hiển thị đúng
