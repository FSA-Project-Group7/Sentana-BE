using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Building
{
    public class BuildingRequestDto
    {
        [Required(ErrorMessage = "Tên tòa nhà là bắt buộc.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên tòa nhà phải từ 2 đến 100 ký tự.")]
        public string BuildingName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã tòa nhà là bắt buộc.")]
        [StringLength(50, ErrorMessage = "Mã tòa nhà tối đa 50 ký tự.")]
        public string BuildingCode { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Địa chỉ tối đa 200 ký tự.")]
        public string? Address { get; set; }

        [StringLength(100, ErrorMessage = "Thành phố tối đa 100 ký tự.")]
        public string? City { get; set; }

        [Range(0, 1000, ErrorMessage = "Số tầng phải lớn hơn hoặc bằng 0 và nhỏ hơn hoặc bằng 1000.")]
        public int? FloorNumber { get; set; }

        [Range(0, 100000, ErrorMessage = "Số căn hộ phải lớn hơn hoặc bằng 0.")]
        public int? ApartmentNumber { get; set; }
		public byte? Status { get; set; }
	}
}