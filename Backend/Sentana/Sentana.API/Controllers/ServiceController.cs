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
                return BadRequest(new { message = "Service name cannot be empty." });

            if (request.ServiceName.Length > 100)
                return BadRequest(new { message = "Service name is too long." });

            if (request.ServiceFee < 0)
                return BadRequest(new { message = "Service fee must be >= 0." });

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
                    message = "Invalid service ID. ID must be greater than 0."
                });
            }

            try
            {
                await _serviceService.DeleteServiceAsync(id);

                return Ok(new
                {
                    message = "Service deleted successfully"
                });
            }
            catch (KeyNotFoundException) 
            {
                return NotFound(new
                {
                    message = "Service not found."
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
                    message = "Invalid room id."
                });
            }

            var apartmentExists = await _serviceService.ApartmentExistsAsync(roomId);

            if (!apartmentExists)
            {
                return NotFound(new
                {
                    message = "Apartment not found."
                });
            }

            var roomExists = await _serviceService.ApartmentExistsAsync(roomId);
            if (!roomExists)
            {
                return NotFound(new
                {
                    message = "Room not found."
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
                    message = "Apartment not found."
                });
            }
            var serviceExists = await _serviceService.ServiceExistsAsync(request.ServiceId);
            if (!serviceExists)
            {
                return NotFound(new
                {
                    message = "Service not found."
                });
            }

            var result = await _serviceService.AssignServiceToRoom(request);

            if (!result)
            {
                return BadRequest(new
                {
                    message = "Service already assigned to room."
                });
            }

            return Ok(new
            {
                message = "Service assigned to room successfully."
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
                    message = "Service not found in room."
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
                    message = "Service not found in room."
                });
            }

            return Ok(new
            {
                message = "Service removed from room successfully."
            });
        }

        [HttpPut("room/price")]
        public async Task<IActionResult> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request)
        {

            if (request.ActualPrice < 0)
            {
                return BadRequest(new
                {
                    message = "Actual price must be greater than or equal to 0."
                });
            }

            var isServiceAssigned = await _serviceService.CheckRoomServiceRelationAsync(request.ApartmentId, request.ServiceId);
            if (!isServiceAssigned)
            {
                return NotFound(new
                {
                    message = "Service not found in room."
                });
            }

            var result = await _serviceService.UpdateRoomServicePrice(request);

            if (!result)
            {
                return NotFound(new
                {
                    message = "Service not found in room."
                });
            }

            return Ok(new
            {
                message = "Room service price updated successfully."
            });
        }
    }
}