using System.Runtime.InteropServices;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Prometheus;
using rende.manager.Controllers;
using System.Diagnostics;

public class NvidiaMetricsService : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<NvidiaMetricsService> _logger;
    private TelemetryClient client;
    private Timer _timer;
    private SessionDB _sessions;

    private  readonly Gauge TotalGpuMemoryLoad = Metrics
    .CreateGauge("device_gpu_memory_load", "Total Memory load of a NVIDIA GPU.");
    private  readonly Gauge TotalGpuMemoryLoad2 = Metrics
    .CreateGauge("device_gpu_memory_load2", "Total Memory load of a NVIDIA GPU 2.");
    private  Dictionary<string, Gauge> ProcessGpuMemoryLoad = new Dictionary<string, Gauge> { };


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

        _timer = new Timer(DoWork, null, TimeSpan.Zero,
            TimeSpan.FromSeconds(10));

        return Task.CompletedTask;
    }

    private void DoWork(object state)
    {
        var count = Interlocked.Increment(ref executionCount);
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
        var properties = new Dictionary<string, string> { { "Device", "NVIDIA" }, { "GPU", "Requested" }, { "VM", "VM-ID-0001" } };

        int total_requested_GPU = 0;
        foreach (Session session in _sessions.GetSessions())
        {
            total_requested_GPU += session.Requested_gpu_ram_load;
        }
        var metrics = new Dictionary<string, double> { { "GPU_RAM_load", total_requested_GPU } };
        client.TrackEvent("GPU Metrics", properties, metrics);
    }


    private void GetTotalGpuMemoryLoad()
    {

        var properties = new Dictionary<string, string> { { "Device", "NVIDIA" }, { "GPU", "Used" }, { "VM", "VM-ID-0001" } };
        var category = new PerformanceCounterCategory("GPU Adapter Memory");
        // TODO: Get instance properly
        var instances = category.GetInstanceNames();
        var _gpu_counter1 = category.GetCounters(instances[0]);
        var _gpu_counter2 = category.GetCounters(instances[1]);

        var metrics = new Dictionary<string, double> {};
        foreach (var counter in _gpu_counter1)
        {
            if (counter.CounterName == "Total Committed")
            {
                var value = counter.NextValue();
                _logger.LogInformation("GPU load is {GpuLoad}", value);
                TotalGpuMemoryLoad.Set(value);
                metrics.Add("GPU_RAM_load_1", value );
            }

        }
        foreach (var counter in _gpu_counter2)
        {
            if (counter.CounterName == "Total Committed")
            {
                var value = counter.NextValue();
                _logger.LogInformation("GPU load is {GpuLoad}", value);
                TotalGpuMemoryLoad2.Set(value);
                metrics.Add("GPU_RAM_load_2", value );
            }

        }
        client.TrackEvent("GPU Metrics", properties, metrics);


    }
    private void GetProcessGpuMemoryLoad()
    {
        var _gpu_process_counter = new PerformanceCounterCategory("GPU Process Memory");
        var processes = _gpu_process_counter.GetInstanceNames();

        if (processes != null)
        {
            foreach (var process in processes)
            {

                Gauge? process_gauge;
                if (!ProcessGpuMemoryLoad.ContainsKey(process))
                {

                    process_gauge = Metrics.CreateGauge($"{process}_gpu_committed", $"Memory load of Process wirth PID: {process}.");
                    ProcessGpuMemoryLoad.Add(process, process_gauge);
                }
               
                process_gauge = ProcessGpuMemoryLoad.GetValueOrDefault(process);
                
                var counters = _gpu_process_counter.GetCounters(process);
                foreach (var counter in counters)
                {
                    if (counter.CounterName == "Total Committed")
                    {
                        var value = counter.NextValue();
                        _logger.LogInformation($"Process {process} metrics value {value}",process,value);
                        // Prometheus metrics
                        process_gauge.Set(value);
                    }

                }
            }
        }
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