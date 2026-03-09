using Sentana.API.Services;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Service;
using Microsoft.AspNetCore.Authorization;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class ServiceController(IServiceService serviceService) : ControllerBase
    {
        private readonly IServiceService _serviceService = serviceService;

        [HttpPost]
        public async Task<IActionResult> CreateService(CreateServiceRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.ServiceName))
                return BadRequest(new { message = "Tên dịch vụ không thể để trống." });

            if (request.ServiceName.Length > 100)
                return BadRequest(new { message = "Tên dịch vụ quá dài " });

            if (request.ServiceFee < 0)
                return BadRequest(new { message = "Phí dịch vụ phải lớn hơn hoặc bằng 0.." });

            var result = await _serviceService.CreateServiceAsync(request);
            return Ok(result);
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
                return BadRequest(new
                {
                    message = ex.Message
                });
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
            {
                return BadRequest(new
                {
                    message = "Mã định danh dịch vụ không hợp lệ. Mã định danh phải lớn hơn 0."
                });
            }

            try
            {
                await _serviceService.DeleteServiceAsync(id);

                return Ok(new
                {
                    message = "Đã xóa dịch vụ thành công"
                });
            }
            catch (KeyNotFoundException) 
            {
                return NotFound(new
                {
                    message = "Không tìm thấy dịch vụ."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpGet("room/{roomId}")]
        public async Task<IActionResult> GetRoomServiceList(int roomId)
        {
            if (roomId <= 0)
            {
                return BadRequest(new
                {
                    message = "Mã phòng không hợp lệ."
                });
            }

            var apartmentExists = await _serviceService.ApartmentExistsAsync(roomId);

            if (!apartmentExists)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy căn hộ."
                });
            }

            var roomExists = await _serviceService.ApartmentExistsAsync(roomId);
            if (!roomExists)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy phòng."
                });
            }

            var services = await _serviceService.GetRoomServiceListAsync(roomId);

            return Ok(services);
        }

        [HttpPost("room")]
        public async Task<IActionResult> AssignServiceToRoom(AssignRoomServiceRequestDto request)
        {
            var apartmentExists = await _serviceService.ApartmentExistsAsync(request.ApartmentId);
            if (!apartmentExists)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy căn hộ."
                });
            }
            var serviceExists = await _serviceService.ServiceExistsAsync(request.ServiceId);
            if (!serviceExists)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy dịch vụ."
                });
            }

            var result = await _serviceService.AssignServiceToRoom(request);

            if (!result)
            {
                return BadRequest(new
                {
                    message = "Dịch vụ đã được chỉ định cho phòng."
                });
            }

            return Ok(new
            {
                message = "Dịch vụ được chỉ định cho phòng đã thành công."
            });
        }

        [HttpDelete("room")]
        [Authorize] 
        public async Task<IActionResult> RemoveServiceFromRoom([FromBody] RemoveRoomServiceRequestDto request)
        {
            var relationExists = await _serviceService.CheckRoomServiceRelationAsync(request.ApartmentId, request.ServiceId);
            if (!relationExists)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy dịch vụ trong phòng."
                });
            }

            var isAuthorized = await _serviceService.IsUserAuthorizedToModifyRoomService(User, request.ApartmentId);
            if (!isAuthorized)
            {
                return Forbid(); 
            }

            var result = await _serviceService.RemoveServiceFromRoom(request);

            if (!result)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy dịch vụ trong phòng."
                });
            }

            return Ok(new
            {
                message = "Dịch vụ đã được gỡ bỏ khỏi phòng thành công."
            });
        }

        [HttpPut("room/price")]
        public async Task<IActionResult> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request)
        {

            if (request.ActualPrice < 0)
            {
                return BadRequest(new
                {
                    message = "Giá thực tế phải lớn hơn hoặc bằng 0."
                });
            }

            var isServiceAssigned = await _serviceService.CheckRoomServiceRelationAsync(request.ApartmentId, request.ServiceId);
            if (!isServiceAssigned)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy dịch vụ trong phòng."
                });
            }

            var result = await _serviceService.UpdateRoomServicePrice(request);

            if (!result)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy dịch vụ trong phòng."
                });
            }

            return Ok(new
            {
                message = "Giá dịch vụ phòng đã được cập nhật thành công."
            });
        }
    }
}