using System;
namespace rende.manager.Controllers;

public class Session
{
    public string Id { get; private set; }
    public int Requested_gpu_ram_load { get; set; }
    public Session(int gpu_ram_load)
    {
        Requested_gpu_ram_load = gpu_ram_load;

        System.Guid guid = System.Guid.NewGuid();
        Id = guid.ToString();
    }
}


