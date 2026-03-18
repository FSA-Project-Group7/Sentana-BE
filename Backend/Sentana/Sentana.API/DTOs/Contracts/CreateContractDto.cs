using System.ComponentModel.DataAnnotations;

public class CreateContractDto
{
    [Required(ErrorMessage = "Apartment ID là bắt buộc.")]
    public int ApartmentId { get; set; }

    [Required(ErrorMessage = "Resident AccountId là bắt buộc.")]
    public int ResidentAccountId { get; set; }

    [Required(ErrorMessage = "Ngày bắt đầu hợp đồng là bắt buộc.")]
    public DateOnly StartDay { get; set; }

    [Required(ErrorMessage = "Ngày kết thúc hợp đồng là bắt buộc.")]
    public DateOnly EndDay { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MonthlyRent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Deposit { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}