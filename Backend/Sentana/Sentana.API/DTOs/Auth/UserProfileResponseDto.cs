namespace Sentana.API.DTOs.Auth
{
    public class UserProfileResponseDto
    {
        // account
        public int AccountId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }

        // info
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? BirthDay { get; set; }
        public string? Address { get; set; }
        public string? CmndCccd { get; set; }

		public string? ApartmentCode { get; set; }
		public string? BuildingName { get; set; }
		public DateOnly? ContractStart { get; set; }
		public DateOnly? ContractEnd { get; set; }
		public string? Status { get; set; }
	}
}