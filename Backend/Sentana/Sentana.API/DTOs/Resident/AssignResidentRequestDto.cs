using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Resident
{
    public class AssignResidentRequestDto
    {
        [Required(ErrorMessage = "Vui lòng chọn tài khoản cư dân.")]
        public int AccountId { get; set; } // Đổi tên thành AccountId để ánh xạ chuẩn với DB

        [Required(ErrorMessage = "Vui lòng chọn căn hộ.")]
        public int ApartmentId { get; set; }

        // Bạn có thể mở rộng thêm RelationshipId (Chủ hộ, Vợ/Chồng, Con cái...) nếu UI có cho phép chọn
        public int? RelationshipId { get; set; }
    }
}