using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Service;
using Microsoft.AspNetCore.Authorization;
using Sentana.API.Services.SService;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ServiceController(IServiceService serviceService) : ControllerBase
    {
        private readonly IServiceService _serviceService = serviceService;

        [HttpPost]
        public async Task<IActionResult> CreateService([FromBody] CreateServiceRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.ServiceName))
                return BadRequest(new { message = "Tên dịch vụ không thể để trống." });

            if (request.ServiceName.Length > 100)
                return BadRequest(new { message = "Tên dịch vụ quá dài." });

            if (request.ServiceFee < 0)
                return BadRequest(new { message = "Phí dịch vụ phải lớn hơn hoặc bằng 0." });

            var result = await _serviceService.CreateServiceAsync(request);

            return Ok(new
            {
                message = "Tạo dịch vụ thành công.",
                data = result
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateService(int id, [FromBody] UpdateServiceRequestDto request)
        {
            try
            {
                request.ServiceId = id;

                var result = await _serviceService.UpdateServiceAsync(request);

                return Ok(new
                {
                    message = "Cập nhật dịch vụ thành công",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceList()
        {
            var services = await _serviceService.GetServiceListAsync();
            return Ok(services);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteService(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "Mã dịch vụ không hợp lệ." });

            try
            {
                await _serviceService.DeleteServiceAsync(id);

                return Ok(new
                {
                    message = "Đã xóa dịch vụ thành công."
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Không tìm thấy dịch vụ." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("room/{roomId}")]
        public async Task<IActionResult> GetRoomServiceList(int roomId)
        {
            if (roomId <= 0)
                return BadRequest(new { message = "Mã phòng không hợp lệ." });

            var apartmentExists = await _serviceService.ApartmentExistsAsync(roomId);

            if (!apartmentExists)
                return NotFound(new { message = "Không tìm thấy căn hộ." });

            var services = await _serviceService.GetRoomServiceListAsync(roomId);

            return Ok(services);
        }

        [HttpPost("room")]
        public async Task<IActionResult> AssignServiceToRoom([FromBody] AssignRoomServiceRequestDto request)
        {
            var apartmentExists = await _serviceService.ApartmentExistsAsync(request.ApartmentId);

            if (!apartmentExists)
                return NotFound(new { message = "Không tìm thấy căn hộ." });

            var serviceExists = await _serviceService.ServiceExistsAsync(request.ServiceId);

            if (!serviceExists)
                return NotFound(new { message = "Không tìm thấy dịch vụ." });

            var result = await _serviceService.AssignServiceToRoom(request);

            if (!result)
                return BadRequest(new { message = "Dịch vụ đã được gán cho phòng." });

            return Ok(new { message = "Gán dịch vụ cho phòng thành công." });
        }

        [HttpDelete("room")]
        public async Task<IActionResult> RemoveServiceFromRoom([FromBody] RemoveRoomServiceRequestDto request)
        {
            var relationExists = await _serviceService.CheckRoomServiceRelationAsync(request.ApartmentId, request.ServiceId);

            if (!relationExists)
                return NotFound(new { message = "Không tìm thấy dịch vụ trong phòng." });

            var result = await _serviceService.RemoveServiceFromRoom(request);

            if (!result)
                return NotFound(new { message = "Không tìm thấy dịch vụ trong phòng." });

            return Ok(new { message = "Đã gỡ dịch vụ khỏi phòng." });
        }

        [HttpPut("room/price")]
        public async Task<IActionResult> UpdateRoomServicePrice([FromBody] UpdateRoomServicePriceRequestDto request)
        {
            if (request.ActualPrice < 0)
                return BadRequest(new { message = "Giá dịch vụ phải >= 0." });

            var relationExists = await _serviceService.CheckRoomServiceRelationAsync(request.ApartmentId, request.ServiceId);

            if (!relationExists)
                return NotFound(new { message = "Không tìm thấy dịch vụ trong phòng." });

            var result = await _serviceService.UpdateRoomServicePrice(request);

            if (!result)
                return NotFound(new { message = "Không tìm thấy dịch vụ trong phòng." });

            return Ok(new { message = "Cập nhật giá dịch vụ thành công." });
        }
    }
}