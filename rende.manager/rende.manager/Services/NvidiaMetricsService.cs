using System.Runtime.InteropServices;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Prometheus;
using rende.manager.Controllers;
using System.Diagnostics;

public class NvidiaMetricsService : IHostedService, IDisposable
{
    private static int TOTAL_GPU_CAPACITY = 16;
    private static int MAX_MODEL_SIZE = 8;
    private int executionCount = 0;
    private readonly ILogger<NvidiaMetricsService> _logger;
    private TelemetryClient client;
    private Timer _timer;
    private SessionDB _sessions;
    private string vm_id = "VM-ID-0001";

    private readonly Gauge TotalGpuLoadGauge = Metrics
    .CreateGauge("device_gpu_memory_load", "Total Memory load of a NVIDIA GPU.");
    private readonly Gauge TotalGpuLoadGauge2 = Metrics
    .CreateGauge("device_gpu_memory_load2", "Total Memory load of a NVIDIA GPU 2.");
    private Dictionary<string, Gauge> ProcessGpuMemoryLoadGauges = new Dictionary<string, Gauge> { };


    public NvidiaMetricsService(ILogger<NvidiaMetricsService> logger, SessionDB db)
    {
        _logger = logger;
        _sessions = db;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Start mertics publishing service.");
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

        _timer = new Timer(DoWork, null, TimeSpan.Zero,
            TimeSpan.FromSeconds(10));

        return Task.CompletedTask;
    }

    private void DoWork(object state)
    {
        var count = Interlocked.Increment(ref executionCount);

        RequestedGpuMemoryLoadMetrics();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {

            TotalGpuMemoryLoad();
            ProcessGpuMemoryLoad();
        }

        _logger.LogInformation(
            "Published metrics count: {Count}", count);
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Mertics Service is stopping.");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }






    private void RequestedGpuMemoryLoadMetrics()
    {
        int free_gpu_capactity = GetFreeCapacity();
        int total_requested_gpu = GetTotalRequestedGPUCapacity();
        int has_space_for_max_model = HasSpaceForMaxModel();
        // Application Insights live metrics
        var properties = new Dictionary<string, string> { { "Device", "NVIDIA" }, { "GPU", "Requested" }, { "VM", vm_id } };


        var metrics = new Dictionary<string, double> { { "GPU_RAM_capacity", free_gpu_capactity }, { "GPU_RAM_load", total_requested_gpu }, { "Space_for_max_model", has_space_for_max_model } };
        client.TrackEvent("GPU Metrics", properties, metrics);
    }



    private void TotalGpuMemoryLoad()
    {

        var properties = new Dictionary<string, string> { { "Device", "NVIDIA" }, { "GPU", "Used" }, { "VM", vm_id } };
        var category = new PerformanceCounterCategory("GPU Adapter Memory");
        // TODO: Get instance properly. One is Nvidia GPU the other is internal GPU with constant load
        var instances = category.GetInstanceNames();
        var _gpu_counter1 = category.GetCounters(instances[0]);
        var _gpu_counter2 = category.GetCounters(instances[1]);

        var metrics = new Dictionary<string, double> { };
        foreach (var counter in _gpu_counter1)
        {
            if (counter.CounterName == "Total Committed")
            {
                var value = counter.NextValue();
                _logger.LogInformation("GPU load is {GpuLoad}", value);
                TotalGpuLoadGauge.Set(value);
                metrics.Add("GPU_RAM_load_1", value);
            }

        }
        foreach (var counter in _gpu_counter2)
        {
            if (counter.CounterName == "Total Committed")
            {
                var value = counter.NextValue();
                _logger.LogInformation("GPU load is {GpuLoad}", value);
                TotalGpuLoadGauge2.Set(value);
                metrics.Add("GPU_RAM_load_2", value);
            }

        }
        client.TrackEvent("GPU Metrics", properties, metrics);


    }
    private void ProcessGpuMemoryLoad()
    {
        var _gpu_process_counter = new PerformanceCounterCategory("GPU Process Memory");
        var processes = _gpu_process_counter.GetInstanceNames();

        if (processes != null)
        {
            foreach (var process in processes)
            {

                Gauge? process_gauge;
                if (!ProcessGpuMemoryLoadGauges.ContainsKey(process))
                {

                    process_gauge = Metrics.CreateGauge($"{process}_gpu_committed", $"Memory load of Process wirth PID: {process}.");
                    ProcessGpuMemoryLoadGauges.Add(process, process_gauge);
                }

                process_gauge = ProcessGpuMemoryLoadGauges.GetValueOrDefault(process);

                var counters = _gpu_process_counter.GetCounters(process);
                foreach (var counter in counters)
                {
                    if (counter.CounterName == "Total Committed")
                    {
                        var value = counter.NextValue();
                        _logger.LogInformation($"Process {process} metrics value {value}", process, value);
                        // Prometheus metrics
                        process_gauge.Set(value);
                    }

                }
            }
        }
    }

    private int GetTotalRequestedGPUCapacity()
    {
        int total_requested_GPU = 0;
        foreach (Session session in _sessions.GetSessions())
        {
            total_requested_GPU += session.Requested_gpu_ram_load;
        }
        return total_requested_GPU;
    }
    private int GetFreeCapacity()
    {
        return TOTAL_GPU_CAPACITY - GetTotalRequestedGPUCapacity();

    }
    private int HasSpaceForMaxModel()
    {
        int freeSpace = GetFreeCapacity();
        if (freeSpace >= MAX_MODEL_SIZE)
        {
            return 1;
        }
        return 0;

    }

}