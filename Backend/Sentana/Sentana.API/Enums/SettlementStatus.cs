namespace Sentana.API.Enums
{
    public enum SettlementStatus
    {
        NotRequired = 0,       // Không cần quyết toán
        PendingInspection = 1, // CHỜ KIỂM TRA PHÒNG (Do hệ thống tự ngắt)
        PendingSettlement = 2, // CHỜ THU/CHI TIỀN (Có phát sinh phạt nhưng chưa đóng)
        Settled = 3            // ĐÃ HOÀN TẤT
    }
}