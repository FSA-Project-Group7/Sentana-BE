using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Apartment
{
	public class CreateApartmentDto
	{
		[Required(ErrorMessage = "ID tòa nhà không được để trống.")]
		public int BuildingId { get; set; }

		[Range(1, int.MaxValue, ErrorMessage = "Số tầng (Floor Number) phải lớn hơn 0.")]
		public int FloorNumber { get; set; }

		[Range(1, 99, ErrorMessage = "Số phòng trên tầng phải từ 01 đến 99.")]
		public int ApartmentNumber { get; set; }

		[Range(0.1, double.MaxValue, ErrorMessage = "Diện tích phòng phải lớn hơn 0.")]
		public double Area { get; set; }

		public byte Status { get; set; }
	}
}