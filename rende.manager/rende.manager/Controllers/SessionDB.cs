using System;
namespace rende.manager.Controllers;

public class SessionDB
{
    private List<Session> _sessions;
    public SessionDB()
    {
        _sessions = new List<Session>() { };
    }

    public void AddSession(Session session)
    {
        _sessions.Add(session);
    }
    public List<Session> GetSessions()
    {
        return _sessions;
    }
    public List<Session> DeleteSession(String id)
    {
        _sessions.RemoveAll(x => x.Id == id);
        return _sessions;
    }

}