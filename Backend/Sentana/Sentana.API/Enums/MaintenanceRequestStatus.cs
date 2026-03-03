namespace Sentana.API.Enums
{
    public enum MaintenanceRequestStatus : byte
    {
        Pending = 1,    // chờ xử lý
        Processing = 2, // đang xử lý
        Completed = 3,  // đã hoàn thành
        Canceled = 4    // đã hủy
    }
}