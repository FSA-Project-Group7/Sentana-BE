using ApartmentBuildingManagement.API.Services;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Service;

namespace ApartmentBuildingManagement.API.Controllers
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
        [HttpPut]
        public async Task<IActionResult> UpdateService(UpdateServiceRequestDto request)
        {
            try
            {
                var result = await _serviceService.UpdateServiceAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
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
    }
}