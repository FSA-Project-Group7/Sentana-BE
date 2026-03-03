namespace Sentana.API.Enums
{
    public enum PaymentTransactionStatus : byte
    {
        Pending = 0,  // chờ duyệt
        Approved = 1, // thành công
        Rejected = 2  // từ chối 
    }
}