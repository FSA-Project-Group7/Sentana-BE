namespace Sentana.API.DTOs.Apartment
{
	public class ApartmentDto
	{
		public int ApartmentId { get; set; }
		public string? ApartmentCode { get; set; }
		public string? ApartmentName { get; set; }
		public int? FloorNumber { get; set; }
		public double? Area { get; set; }
		public byte? Status { get; set; }
		public bool HasTenant { get; set; }
	}
}