using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Maintenance
{
    public class CreateMaintenanceDto
    {
        [Required(ErrorMessage = "Vui lòng chọn Căn hộ")]
        public int ApartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn Danh mục sự cố")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Mô tả chi tiết không được để trống")]
        public string Description { get; set; }

        // Chỉ có ảnh là được phép null
        public IFormFile? Photo { get; set; }
    }
}