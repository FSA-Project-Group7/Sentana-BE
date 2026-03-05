namespace Sentana.API.DTOs.Apartment
{
	public class CreateApartmentDto
	{
		public int BuildingId { get; set; }
		public string ApartmentCode { get; set; } = null!;
		public string ApartmentName { get; set; } = null!;
		public int ApartmentNumber { get; set; }
		public int FloorNumber { get; set; }
		public double Area { get; set; }
		public byte Status { get; set; }
	}
}