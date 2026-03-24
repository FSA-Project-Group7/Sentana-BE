using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Resident
{
    public class AssignResidentRequestDto
    {
        [Required(ErrorMessage = "Vui lòng chọn tài khoản cư dân.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn căn hộ.")]
        public int ApartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn mối quan hệ với căn hộ (Chủ hộ, Vợ/Chồng, Con cái...).")]
        public int RelationshipId { get; set; }
    }
}