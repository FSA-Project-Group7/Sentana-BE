namespace Sentana.API.Enums
{
    public enum InvoiceStatus : byte
    {
        Draft = 0,               // nháp
        Unpaid = 1,              // chưa thanh toán
        PendingVerification = 2, // đang chờ duyệt
        Paid = 3                 // đã thanh toán
    }
}