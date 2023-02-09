using Microsoft.AspNetCore.Mvc;

namespace rende.manager.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private SessionDB _sessions;


    public HealthController(ILogger<HealthController> logger, SessionDB db)
    {
        _logger = logger;
        _sessions = db;

    }

    [HttpGet("requestedGPUload", Name = "GetRequestedGPU")]
    public int GetRequestedGPU()
    {
        int total_requested_GPU = 0;
        foreach (Session session in _sessions.GetSessions())
        {
            total_requested_GPU += session.Requested_gpu_ram_load;
        }
        return total_requested_GPU;
    }


}




