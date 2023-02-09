using System.Runtime.InteropServices;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Prometheus;
using rende.manager.Controllers;
using Nvidia.Nvml;

public class NvidiaMetricsService : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<NvidiaMetricsService> _logger;
    private TelemetryClient client;
    private Timer _timer;
    private SessionDB _sessions;
    private IntPtr _nvidia_device;
    private static readonly Gauge TotalGpuMemoryLoad = Metrics
    .CreateGauge("device_gpu_memory_load", "Total Memory load of a NVIDIA GPU.");
    private static Dictionary<string, Gauge> ProcessGpuMemoryLoad = new Dictionary<string, Gauge> { };


    public NvidiaMetricsService(ILogger<NvidiaMetricsService> logger, SessionDB db)
    {
        _logger = logger;
        _sessions = db;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service running.");
        TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();
        config.InstrumentationKey = Environment.GetEnvironmentVariable("INSTRUMENTATION_KEY");
        QuickPulseTelemetryProcessor quickPulseProcessor = null;
        config.DefaultTelemetrySink.TelemetryProcessorChainBuilder
            .Use((next) =>
            {
                quickPulseProcessor = new QuickPulseTelemetryProcessor(next);
                return quickPulseProcessor;
            })
            .Build();

        var quickPulseModule = new QuickPulseTelemetryModule();

        // Secure the control channel.
        // This is optional, but recommended.
        quickPulseModule.AuthenticationApiKey = Environment.GetEnvironmentVariable("API_KEY");
        quickPulseModule.Initialize(config);
        quickPulseModule.RegisterTelemetryProcessor(quickPulseProcessor);

        // Create a TelemetryClient instance. It is important
        // to use the same TelemetryConfiguration here as the one
        // used to set up Live Metrics.
        client = new TelemetryClient(config);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // NVML client
            NvGpu.NvmlInitV2();

            Console.WriteLine("Retrieving device count in this machine ...");
            var deviceCount = NvGpu.NvmlDeviceGetCountV2();
            Console.WriteLine("Listing devices ...");
            _nvidia_device = NvGpu.NvmlDeviceGetHandleByIndex((uint)0);
        }

        _timer = new Timer(DoWork, null, TimeSpan.Zero,
            TimeSpan.FromSeconds(10));

        return Task.CompletedTask;
    }

    private void DoWork(object state)
    {
        var count = Interlocked.Increment(ref executionCount);
        _logger.LogInformation("Do metrics and stuff...");
        GetTotalRequestedGpuMemoryLoad();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {

            GetTotalGpuMemoryLoad();
            GetProcessGpuMemoryLoad();
        }
        else
        {

            GetTotalGpuMemoryLoadMock();
            GetProcessGpuMemoryLoadMock();

        }

        _logger.LogInformation(
            "Timed Hosted Service is working. Count: {Count}", count);
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service is stopping.");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    // extrall all this stuff into another util class

    private void GetTotalRequestedGpuMemoryLoad()
    {
        // Application Insights live metrics
        var properties = new Dictionary<string, string> { { "Device", "NVIDIA" }, { "Level", "Device" }, { "VM", "VM-ID-0001" } };

        int total_requested_GPU = 0;
        foreach (Session session in _sessions.GetSessions())
        {
            total_requested_GPU += session.Requested_gpu_ram_load;
        }
        var metrics = new Dictionary<string, double> { { "GPU_RAM_load", total_requested_GPU } };
        client.TrackEvent("GPU Metrics", properties, metrics);
    }
    private void GetTotalGpuMemoryLoadMock()
    {

        // Get Metrics
        Random rd = new Random();
        int rand_num = rd.Next(10, 200);
        _logger.LogInformation("GPU load is {GpuLoad}", rand_num);

        // Prometheus metrics
        TotalGpuMemoryLoad.Set(rand_num);
    }

    private void GetTotalGpuMemoryLoad()
    {

        // Get Metrics
        Random rd = new Random();
        int rand_num = rd.Next(10, 200);
        _logger.LogInformation("GPU load is {GpuLoad}", rand_num);

        // Prometheus metrics
        TotalGpuMemoryLoad.Set(rand_num);
    }

    private void GetProcessGpuMemoryLoad()
    {
        var (processes, count) = NvGpu.NvmlDeviceGetComputeRunningProcesses(_nvidia_device);

        foreach (var process in processes)
        {
            Gauge? process_gauge;
            if (!ProcessGpuMemoryLoad.ContainsKey(process.Pid.ToString()))
            {

                process_gauge = Metrics.CreateGauge($"process_gpu_memory_load_{process}", $"Memory load of Process wirth PID: {process}.");
                ProcessGpuMemoryLoad.Add(process.Pid.ToString(), process_gauge);
            }
            else
            {
                process_gauge = ProcessGpuMemoryLoad.GetValueOrDefault(process.Pid.ToString());
            }
            process_gauge.Set(process.UsedGpuMemory);

        }
    }


    private void GetProcessGpuMemoryLoadMock()
    {
        Random rd = new Random();
        List<string> processes = new List<string>() { "PID_XXXXX1", "PID_XXXXX2", "PID_XXXXX3" };
        foreach (var process in processes)
        {
            Gauge? process_gauge;
            if (!ProcessGpuMemoryLoad.ContainsKey(process))
            {
                process_gauge = Metrics.CreateGauge($"process_gpu_memory_load_{process}", $"Memory load of Process wirth PID: {process}.");
                ProcessGpuMemoryLoad.Add(process, process_gauge);
            }
            else
            {
                process_gauge = ProcessGpuMemoryLoad.GetValueOrDefault(process);
            }
            int rand_num = rd.Next(10, 200);
            process_gauge.Set(rand_num);
        }


    }
}