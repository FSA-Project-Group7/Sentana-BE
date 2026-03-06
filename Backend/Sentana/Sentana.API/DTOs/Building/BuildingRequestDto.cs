namespace Sentana.API.DTOs.Building
{
    public class BuildingRequestDto
    {
        public string BuildingName { get; set; } = string.Empty;
        public string BuildingCode { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? City { get; set; }
        public int? FloorNumber { get; set; }
        public int? ApartmentNumber { get; set; }
        // Bạn có thể thêm Status vào đây nếu cho phép người dùng chọn khi tạo
    }
}