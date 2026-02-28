using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SilvuViewfinder
{
    public enum SimulationMode
    {
        ManualFpv,
        AutonomousMission,
        EmergencyFailure,
        Swarm,
        VtolHybrid,
        HeavyLift
    }

    public enum PayloadType
    {
        None,
        CameraGimbal,
        LiDAR,
        DeliveryBox
    }

    public enum BatteryChemistry
    {
        LiPo,
        LiIon,
        LiHV
    }

    public enum FirmwareProfile
    {
        Betaflight,
        ArduPilot,
        PX4
    }

    public enum SensorProfile
    {
        Nominal,
        SurveyGrade,
        Degraded
    }

    public sealed class SimulationEnvironment
    {
        public float WindSpeedMps { get; set; } = 0.5f;
        public float TurbulenceStrength { get; set; } = 0.15f;
        public float DragCoefficient { get; set; } = 0.08f;
        public bool EnableGroundEffect { get; set; } = true;
    }

    public sealed class FaultInjection
    {
        public bool MotorFailure { get; set; }
        public bool SensorNoise { get; set; }
        public bool GpsDrop { get; set; }
        public bool EscThermalCutback { get; set; }
    }

    public sealed class TelemetrySample
    {
        public double TimeSec { get; set; }
        public float AltitudeM { get; set; }
        public float VerticalVelocityMps { get; set; }
        public float MassKg { get; set; }
        public float ThrustN { get; set; }
        public float CurrentA { get; set; }
        public float VoltageV { get; set; }
        public float MotorTempC { get; set; }
        public float EscTempC { get; set; }
        public string Mode { get; set; } = "";
    }

    public sealed class Waypoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float AltitudeM { get; set; }
    }

    public sealed class BuildBenchmark
    {
        public string Name { get; set; } = "";
        public DateTime CapturedAtUtc { get; set; }
        public float MassKg { get; set; }
        public float ThrustToWeight { get; set; }
        public float FlightTimeMin { get; set; }
        public float AvgCurrentA { get; set; }
        public float StabilityMarginPct { get; set; }
    }

    public interface ISilvuHost
    {
        void ReportPluginMessage(string message);
    }

    public interface ISilvuPlugin
    {
        string Name { get; }
        void Initialize(ISilvuHost host);
    }

    public static class PluginLoader
    {
        public static IReadOnlyList<string> LoadPlugins(ISilvuHost host, string pluginDir)
        {
            var loaded = new List<string>();
            if (!Directory.Exists(pluginDir)) return loaded;

            foreach (var dll in Directory.GetFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var asm = Assembly.LoadFrom(dll);
                    var pluginTypes = asm.GetTypes()
                        .Where(t => !t.IsAbstract && typeof(ISilvuPlugin).IsAssignableFrom(t))
                        .ToList();

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is not ISilvuPlugin plugin) continue;
                        plugin.Initialize(host);
                        loaded.Add(plugin.Name);
                        host.ReportPluginMessage($"Plugin loaded: {plugin.Name}");
                    }
                }
                catch (Exception ex)
                {
                    host.ReportPluginMessage($"Plugin load failed: {Path.GetFileName(dll)} ({ex.GetType().Name})");
                }
            }

            return loaded;
        }
    }
}
