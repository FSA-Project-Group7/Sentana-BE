using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Sentana.API.DTOs.Contracts
{
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

        // 1. Danh sách thành viên thêm vào phòng (Nút + thứ nhất)
        public List<ContractAdditionalResidentDto>? AdditionalResidents { get; set; }

        // 2. Danh sách dịch vụ thêm vào phòng (Nút + thứ hai)
        public List<ContractServiceDto>? Services { get; set; }
    }

    // Class hứng dữ liệu thành viên
    public class ContractAdditionalResidentDto
    {
        [Required]
        public int AccountId { get; set; }

        [Required]
        public int RelationshipId { get; set; } // Ví dụ: 2 (Vợ/chồng), 3 (Con cái)...
    }

    // Class hứng dữ liệu dịch vụ
    public class ContractServiceDto
    {
        [Required]
        public int ServiceId { get; set; }

        // Giá tùy chỉnh (nếu Manager nhập giá khác mặc định). Nếu null, BE sẽ tự lấy giá gốc.
        public decimal? ActualPrice { get; set; }
    }
}