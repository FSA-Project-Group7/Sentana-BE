using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Resident
{
    public class RemoveResidentRequestDto
    {
        [Required(ErrorMessage = "Vui lòng cung cấp AccountId.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Vui lòng cung cấp ApartmentId.")]
        public int ApartmentId { get; set; }

        /// <summary>
        /// Optional: nếu muốn chỉ rõ hợp đồng liên quan khi gỡ cư dân.
        /// </summary>
        public int? ContractId { get; set; }

        /// <summary>
        /// Optional: RelationshipId trong trường hợp cần thông tin quan hệ khi gỡ.
        /// </summary>
        public int? RelationshipId { get; set; }

        [StringLength(1000, ErrorMessage = "Lý do tối đa 1000 ký tự.")]
        public string? Reason { get; set; }
    }
}
