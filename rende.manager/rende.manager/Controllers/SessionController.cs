using Microsoft.AspNetCore.Mvc;

namespace rende.manager.Controllers;

[ApiController]
[Route("[controller]")]
public class SessionController : ControllerBase
{
    private readonly ILogger<SessionController> _logger;
    private SessionDB _sessions;


    public SessionController(ILogger<SessionController> logger, SessionDB db)
    {
        _logger = logger;
        _sessions = db;

    }

    [HttpPost(Name = "StartNewSession")]
    public ActionResult<Session> StartNewSession(int requested_gpu_ram_load)
    {
        Session session = new Session(requested_gpu_ram_load);
        _sessions.AddSession(session);
        return session;
    }

    [HttpGet("{id}", Name = "GetSession")]
    public ActionResult<List<Session>> GetSession(string id)
    {
        return _sessions.GetSessions();
    }
    [HttpGet(Name = "GetSessions")]
    public ActionResult<List<Session>> GetSessions()
    {
        return _sessions.GetSessions();
    }

    [HttpDelete("{id}", Name = "DeleteSession")]
    public ActionResult<List<Session>> DeleteSession(string id)
    {
        return _sessions.DeleteSession(id);
    }

}




