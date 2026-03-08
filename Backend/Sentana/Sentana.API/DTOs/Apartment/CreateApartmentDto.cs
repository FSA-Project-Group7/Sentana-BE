using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Apartment
{
	public class CreateApartmentDto
	{
		public int BuildingId { get; set; }

		[Required(ErrorMessage = "Mã phòng không được để trống.")]
		public string ApartmentCode { get; set; } = null!;

		[Required(ErrorMessage = "Tên phòng không được để trống.")]
		public string ApartmentName { get; set; } = null!;

		public int ApartmentNumber { get; set; }

		[Range(0, int.MaxValue, ErrorMessage = "Số tầng không hợp lệ.")]
		public int FloorNumber { get; set; }

		[Range(0.1, double.MaxValue, ErrorMessage = "Diện tích phòng phải lớn hơn 0.")]
		public double Area { get; set; }

		public byte Status { get; set; }
	}
}