using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternalApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly SystemMetricsService _metrics;

        public MetricsController(SystemMetricsService metrics)
        {
            _metrics = metrics;
        }

        [HttpGet]
        public IActionResult Get() => Ok(_metrics.Sample());
    }
}
