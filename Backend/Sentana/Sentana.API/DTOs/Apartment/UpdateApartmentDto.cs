using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Apartment
{
	public class UpdateApartmentDto
	{
		[Range(101, 9999, ErrorMessage = "Số căn hộ không hợp lệ (VD hợp lệ: 709, 1205).")]
		public int? ApartmentNumber { get; set; } 

		[Range(0.1, double.MaxValue, ErrorMessage = "Diện tích phòng phải lớn hơn 0.")]
		public double? Area { get; set; }

		public byte? Status { get; set; }
	}
}