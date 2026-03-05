using Sentana.API.Services;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Service;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly IServiceService _serviceService;

        public ServiceController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }
        [HttpPost]
        public async Task<IActionResult> CreateService(CreateServiceRequestDto request)
        {
            var result = await _serviceService.CreateServiceAsync(request);

            return Ok(result);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteService(int id)
        {
            try
            {
                await _serviceService.DeleteServiceAsync(id);

                return Ok(new
                {
                    message = "Service deleted successfully"
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

        [HttpPost("room")]
        public async Task<IActionResult> AssignServiceToRoom(AssignRoomServiceRequestDto request)
        {
            var result = await _serviceService.AssignServiceToRoom(request);

            if (!result)
                return BadRequest("Service already assigned to room");

            return Ok("Service assigned to room successfully");
            [HttpDelete("room")]
            public async Task<IActionResult> RemoveServiceFromRoom(RemoveRoomServiceRequestDto request)
            {
                var result = await _serviceService.RemoveServiceFromRoom(request);

                if (!result)
                    return NotFound("Service not found in room");

                return Ok("Service removed from room");
            }
        }
    } }