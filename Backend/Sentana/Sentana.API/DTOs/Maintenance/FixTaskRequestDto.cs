using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Maintenance
{
    public class FixTaskRequestDto
    {
        [Required(ErrorMessage = "Ghi chú giải quyết (Resolution note) là bắt buộc.")]
        public string ResolutionNote { get; set; } = null!;
    }
}