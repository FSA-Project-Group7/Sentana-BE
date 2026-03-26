namespace Sentana.API.Enums
{
    public enum MaintenanceRequestStatus : byte
    {
        Pending = 1,    // Chờ xử lý (Khi Admin vừa assign cho thợ)
        Processing = 2, // Đang xử lý (Khi thợ bắt đầu làm)
        Fixed = 3,      // Đã sửa xong 
        Closed = 4,     // Đã đóng
        Canceled = 5    // Đã hủy
    }
}