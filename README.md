# rendering-management-spike

C# rendering Manager for GPU enabled rendering VMs

Metrics exposed by sample:
- Prometheus metrics:
    - Session level (based on PID) GPU RAM load
    - Overall GPU RAM load
- Application Insights live metrics:
    - Overall GPU RAM load 
    - Requested GPU RAM load
    - Capacity for GPU RAM load request
    - Number of max-size sessions VM could handle

## Pre requisite
- [Azure Application Insights resource](https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview?tabs=net)

## Setup 

1. Env variables

Set the following two env vars to be able to send live metrics to Application Inights. 

```sh
export API_KEY="<AppInsights-API-KEY>"
export INSTRUMENTATION_KEY="<AppInsights-INSTRUMENTATION_KEY>"
```

2. Run project 

```sh 
cd rende.manager
dotnet run  --project rende.manager
```

---

## Query Metrics

Sample query to visualize data in Log analytics

```kusto
let endtime =now();
let window = 30m;
let starttime = endtime - window;
let vm_id="VM-ID-0002" ; 
customEvents
| extend gpu_load=toint(customMeasurements["GPU_RAM_load"])
| extend gpu_capaciy=toint(customMeasurements["GPU_RAM_capacity"])
| extend max_model_space=toint(customMeasurements["Space_for_max_model"])
| extend vm=tostring(customDimensions["VM"])
| where timestamp > starttime
| where vm contains vm_id
| extend load_type=tostring(customDimensions["GPU"])
| where load_type contains "Requested"
| project timestamp, gpu_load, load_type, gpu_capaciy, max_model_space
| sort by timestamp desc
| render timechart 
```
