namespace Sentana.API.DTOs.Resident
{
    public class RemoveResidentResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;

        // Trả về thông tin để FE biết chính xác đối tượng nào vừa bị gỡ
        public int AccountId { get; set; }
        public int ApartmentId { get; set; }

        // Thông tin để hiển thị ở lịch sử hoặc thông báo
        public DateTime RemovedAt { get; set; }
        public string? ApartmentStatus { get; set; } // Ví dụ: "Vacant" (Trống)
    }
}