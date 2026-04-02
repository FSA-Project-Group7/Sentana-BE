namespace Sentana.API.Enums
{
	public enum MaintenanceRequestStatus : byte
	{
		/// <summary>
		/// 1 - Cư dân vừa tạo, chờ Quản lý phân công
		/// </summary>
		Pending = 1,

		/// <summary>
		/// 2 - Đã phân công Kỹ thuật viên, đang trong quá trình sửa chữa
		/// </summary>
		Processing = 2,

		/// <summary>
		/// 3 - Thợ đã báo cáo sửa xong, CHỜ CƯ DÂN NGHIỆM THU
		/// </summary>
		Fixed = 3,

		/// <summary>
		/// 4 - Cư dân / Quản lý đã xác nhận nghiệm thu thành công (Đóng thẻ)
		/// </summary>
		Closed = 4,

		/// <summary>
		/// 5 - Sự cố bị hủy bỏ (Do quản lý hoặc cư dân hủy)
		/// </summary>
		Canceled = 5,

		/// <summary>
		/// 6 - Cư dân báo chưa đạt, yêu cầu thợ quay lại làm lại
		/// </summary>
		Reopened = 6
	}
}