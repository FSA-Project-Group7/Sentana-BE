using Sentana.API.Enums;

namespace Sentana.API.DTOs.Apartment
{
	public class ApartmentResponseDto
	{
		public int ApartmentId { get; set; }
		public int? BuildingId { get; set; }
		public string? BuildingCode { get; set; }
		public string? ApartmentCode { get; set; }
		public string? ApartmentName { get; set; }
		public int? ApartmentNumber { get; set; }
		public int? FloorNumber { get; set; }
		public double? Area { get; set; }
		public byte? Status { get; set; }
	}
}