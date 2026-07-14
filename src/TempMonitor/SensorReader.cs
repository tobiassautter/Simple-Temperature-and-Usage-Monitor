using LibreHardwareMonitor.Hardware;

namespace TempMonitor;

public record HardwareStats(
    float? CpuTemp, float? CpuLoad, float? CpuPower,
    float? GpuTemp, float? GpuLoad, float? GpuPower, float? GpuPowerLimit);

/// <summary>
/// Thin wrapper around LibreHardwareMonitor. Sensors are resolved once at
/// startup so each poll is just a hardware.Update() plus direct field reads.
/// </summary>
public sealed class SensorReader : IDisposable
{
    private readonly Computer _computer;
    private IHardware? _cpu;
    private IHardware? _gpu;

    private ISensor? _cpuTemp, _cpuLoad, _cpuPower;
    private ISensor? _gpuTemp, _gpuLoad, _gpuPower;
    private float? _gpuPowerLimit;

    public SensorReader()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
        };
        _computer.Open();
        ResolveSensors();
    }

    private void ResolveSensors()
    {
        foreach (var hw in _computer.Hardware)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    _cpu = hw;
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    // Prefer a discrete GPU over an iGPU if both exist.
                    if (_gpu is null || hw.HardwareType != HardwareType.GpuIntel)
                        _gpu = hw;
                    break;
            }
        }

        if (_cpu is not null)
        {
            _cpu.Update();
            _cpuTemp = PickSensor(_cpu, SensorType.Temperature,
                "CPU Package", "Core (Tctl/Tdie)", "Core Average", "Core Max");
            _cpuLoad = PickSensor(_cpu, SensorType.Load, "CPU Total");
            _cpuPower = PickSensor(_cpu, SensorType.Power,
                "CPU Package", "Package", "CPU Cores");
        }

        if (_gpu is not null)
        {
            _gpu.Update();
            _gpuTemp = PickSensor(_gpu, SensorType.Temperature,
                "GPU Core", "GPU Hot Spot");
            _gpuLoad = PickSensor(_gpu, SensorType.Load, "GPU Core", "D3D 3D");
            _gpuPower = PickSensor(_gpu, SensorType.Power,
                "GPU Package", "GPU Power", "GPU Core");
        }
    }

    /// <summary>First sensor matching a preferred name, else first of that type.</summary>
    private static ISensor? PickSensor(IHardware hw, SensorType type, params string[] preferredNames)
    {
        var ofType = hw.Sensors.Where(s => s.SensorType == type).ToArray();
        foreach (var name in preferredNames)
        {
            var match = ofType.FirstOrDefault(s => s.Name == name);
            if (match is not null) return match;
        }
        return ofType.FirstOrDefault();
    }

    /// <summary>Poll current values. Call from a background thread; takes a few ms.</summary>
    public HardwareStats Read()
    {
        _cpu?.Update();
        _gpu?.Update();

        // NVML exposes the board power limit; grab it once when available so
        // GPU power can also be shown as a percentage of its limit.
        if (_gpuPowerLimit is null && _gpu is not null)
        {
            var limit = _gpu.Sensors.FirstOrDefault(s =>
                s.SensorType == SensorType.Power && s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase));
            _gpuPowerLimit = limit?.Value;
        }

        return new HardwareStats(
            _cpuTemp?.Value, _cpuLoad?.Value, _cpuPower?.Value,
            _gpuTemp?.Value, _gpuLoad?.Value, _gpuPower?.Value, _gpuPowerLimit);
    }

    public void Dispose() => _computer.Close();
}
