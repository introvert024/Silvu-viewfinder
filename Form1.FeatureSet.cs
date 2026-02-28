using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SilvuViewfinder
{
    public partial class Form1
    {
        enum WorkspaceArea
        {
            Assemble,
            Configurations,
            FluidAirSimulation,
            Realtime3D,
            SoftwareConfiguration
        }

        sealed class FeatureSetProfile
        {
            public float MotorKvOverride { get; set; } = 1850f;
            public float TorqueFactor { get; set; } = 1f;
            public float EfficiencyBias { get; set; } = 1f;
            public float PropDiameterInch { get; set; } = 5f;
            public float PropPitchInch { get; set; } = 4.3f;
            public int PropBladeCount { get; set; } = 3;
            public float EscCurrentLimitA { get; set; } = 45f;
            public float EscResponseDelayMs { get; set; } = 8f;
            public float EscThermalLimitC { get; set; } = 95f;
            public float BatteryCRating { get; set; } = 75f;
            public float BatterySagBias { get; set; } = 1f;
            public float ArmLengthMm { get; set; } = 150f;
            public float MaterialDensity { get; set; } = 1.6f;
            public float CgOffsetXcm { get; set; }
            public float CgOffsetYcm { get; set; }
            public float WeightBiasPct { get; set; }
            public int SwarmSize { get; set; } = 4;
        }

        TabControl featureTabs = null!;
        Label builderSummary = null!;
        Label physicsSummary = null!;
        Label twinSummary = null!;
        Label controlSummary = null!;
        Label modeSummary = null!;
        Label dataSummary = null!;
        Label openSummary = null!;
        Label eduSummary = null!;
        ListBox pluginBox = null!;
        ToolStrip workspaceStrip = null!;
        ComboBox modeSelector = null!;
        ComboBox firmwareSelector = null!;
        ComboBox sensorSelector = null!;
        ComboBox payloadSelector = null!;
        NumericUpDown payloadMassSelector = null!;
        CheckBox obstacleToggle = null!;
        CheckBox groundEffectToggle = null!;
        CheckBox educationToggle = null!;
        readonly Dictionary<WorkspaceArea, ToolStripButton> workspaceButtons = new();
        WorkspaceArea currentWorkspace = WorkspaceArea.Assemble;
        bool syncingFeaturePanel = false;

        ToolStrip BuildWorkspaceStrip()
        {
            workspaceStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                Height = 38,
                GripStyle = ToolStripGripStyle.Hidden,
                AutoSize = false,
                Padding = new Padding(8, 3, 8, 3),
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = toolStripRenderer
            };

            workspaceStrip.Items.Add(new ToolStripLabel("WORKSPACE")
            {
                Margin = new Padding(2, 0, 14, 0),
                ForeColor = CurrentPalette.TextMuted
            });

            AddWorkspaceButton("Assemble", WorkspaceArea.Assemble, "Place frame, motors, battery, payload");
            AddWorkspaceButton("Configurations", WorkspaceArea.Configurations, "Tune component values and profile");
            AddWorkspaceButton("Fluid / Air Sim", WorkspaceArea.FluidAirSimulation, "Wind, turbulence, drag, stability");
            AddWorkspaceButton("3D Live Sim", WorkspaceArea.Realtime3D, "Realtime simulation-focused view");
            AddWorkspaceButton("Software Config", WorkspaceArea.SoftwareConfiguration, "PID, firmware, sensor stack");
            ApplyWorkspaceStripTheme();

            return workspaceStrip;
        }

        void AddWorkspaceButton(string label, WorkspaceArea area, string tooltip)
        {
            var button = new ToolStripButton(label)
            {
                CheckOnClick = true,
                ToolTipText = tooltip,
                Margin = new Padding(0, 0, 8, 0),
                AutoSize = false,
                Height = 24,
                Padding = new Padding(10, 2, 10, 2)
            };
            button.Click += (_, __) => SetWorkspace(area);
            workspaceButtons[area] = button;
            workspaceStrip.Items.Add(button);
        }

        void SetWorkspace(WorkspaceArea area)
        {
            currentWorkspace = area;
            foreach (var kv in workspaceButtons)
                kv.Value.Checked = kv.Key == area;

            if (featureTabs != null && !featureTabs.IsDisposed)
            {
                switch (area)
                {
                    case WorkspaceArea.Assemble:
                        featureTabs.SelectedIndex = 0;
                        break;
                    case WorkspaceArea.Configurations:
                        featureTabs.SelectedIndex = 0;
                        break;
                    case WorkspaceArea.FluidAirSimulation:
                        featureTabs.SelectedIndex = 1;
                        break;
                    case WorkspaceArea.Realtime3D:
                        featureTabs.SelectedIndex = 4;
                        break;
                    case WorkspaceArea.SoftwareConfiguration:
                        featureTabs.SelectedIndex = 3;
                        break;
                }
            }

            if (area == WorkspaceArea.Realtime3D)
                runSimButton?.PerformClick();

            ApplyWorkspaceStripTheme();
            UpdateStatusBar();
            viewport?.Invalidate();
        }

        string WorkspaceDisplayName(WorkspaceArea area) => area switch
        {
            WorkspaceArea.Assemble => "Assemble",
            WorkspaceArea.Configurations => "Configurations",
            WorkspaceArea.FluidAirSimulation => "Fluid/Air Simulation",
            WorkspaceArea.Realtime3D => "3D Live Simulation",
            WorkspaceArea.SoftwareConfiguration => "Software Configuration",
            _ => "Assemble"
        };

        void ApplyWorkspaceStripTheme()
        {
            if (workspaceStrip == null || workspaceStrip.IsDisposed) return;

            workspaceStrip.BackColor = CurrentPalette.Surface;
            workspaceStrip.ForeColor = CurrentPalette.TextPrimary;
            workspaceStrip.Renderer = toolStripRenderer;

            foreach (var kv in workspaceButtons)
            {
                bool active = kv.Key == currentWorkspace;
                var button = kv.Value;
                button.Checked = active;
                button.BackColor = active ? CurrentPalette.AccentSoft : CurrentPalette.Surface;
                button.ForeColor = active ? CurrentPalette.Accent : CurrentPalette.TextMuted;
                button.Font = Font;
            }
        }

        RoundedPanel BuildFeatureSetCard()
        {
            var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 14, BorderThickness = 1, Padding = new Padding(10) };
            var title = new Label
            {
                Text = "FEATURE SET SECTIONS",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };

            featureTabs = new TabControl { Dock = DockStyle.Fill, Multiline = true };
            featureTabs.TabPages.Add(BuildBuilderTab());
            featureTabs.TabPages.Add(BuildPhysicsTab());
            featureTabs.TabPages.Add(BuildTwinTab());
            featureTabs.TabPages.Add(BuildControlTab());
            featureTabs.TabPages.Add(BuildModesTab());
            featureTabs.TabPages.Add(BuildDataTab());
            featureTabs.TabPages.Add(BuildOpenTab());
            featureTabs.TabPages.Add(BuildEduTab());

            card.Controls.Add(featureTabs);
            card.Controls.Add(title);
            return card;
        }

        TabPage BuildBuilderTab()
        {
            var page = CreateSectionPage("1 Builder", out var stack, out builderSummary,
                "Component-level drone builder inputs.");
            stack.Controls.Add(CreateNumeric("Motor KV", 800, 3200, 25, 0, featureProfile.MotorKvOverride, v => featureProfile.MotorKvOverride = v));
            stack.Controls.Add(CreateNumeric("Torque factor", 0.5m, 1.8m, 0.01m, 2, featureProfile.TorqueFactor, v => featureProfile.TorqueFactor = v));
            stack.Controls.Add(CreateNumeric("Efficiency bias", 0.6m, 1.2m, 0.01m, 2, featureProfile.EfficiencyBias, v => featureProfile.EfficiencyBias = v));
            stack.Controls.Add(CreateNumeric("Prop diameter (in)", 2, 20, 0.1m, 1, featureProfile.PropDiameterInch, v => featureProfile.PropDiameterInch = v));
            stack.Controls.Add(CreateNumeric("Prop pitch (in)", 1, 12, 0.1m, 1, featureProfile.PropPitchInch, v => featureProfile.PropPitchInch = v));
            stack.Controls.Add(CreateNumeric("Blade count", 2, 8, 1, 0, featureProfile.PropBladeCount, v => featureProfile.PropBladeCount = Math.Max(2, (int)v)));
            stack.Controls.Add(CreateNumeric("ESC current limit", 10, 180, 1, 0, featureProfile.EscCurrentLimitA, v => featureProfile.EscCurrentLimitA = v));
            stack.Controls.Add(CreateNumeric("ESC delay (ms)", 1, 35, 1, 0, featureProfile.EscResponseDelayMs, v => featureProfile.EscResponseDelayMs = v));
            stack.Controls.Add(CreateNumeric("ESC thermal limit", 55, 150, 1, 0, featureProfile.EscThermalLimitC, v => featureProfile.EscThermalLimitC = v));
            stack.Controls.Add(CreateNumeric("Battery C-rating", 10, 220, 1, 0, featureProfile.BatteryCRating, v => featureProfile.BatteryCRating = v));
            stack.Controls.Add(CreateNumeric("Battery sag bias", 0.6m, 1.8m, 0.01m, 2, featureProfile.BatterySagBias, v => featureProfile.BatterySagBias = v));
            stack.Controls.Add(CreateNumeric("Arm length (mm)", 80, 700, 1, 0, featureProfile.ArmLengthMm, v => featureProfile.ArmLengthMm = v));
            stack.Controls.Add(CreateNumeric("Material density", 0.4m, 3m, 0.01m, 2, featureProfile.MaterialDensity, v => featureProfile.MaterialDensity = v));
            stack.Controls.Add(CreateNumeric("CG offset X (cm)", -30, 30, 0.5m, 1, featureProfile.CgOffsetXcm, v => featureProfile.CgOffsetXcm = v));
            stack.Controls.Add(CreateNumeric("CG offset Y (cm)", -30, 30, 0.5m, 1, featureProfile.CgOffsetYcm, v => featureProfile.CgOffsetYcm = v));
            stack.Controls.Add(CreateNumeric("Weight bias (%)", -40, 40, 1, 0, featureProfile.WeightBiasPct, v => featureProfile.WeightBiasPct = v));

            payloadSelector = CreateCombo(new[] { "None", "Camera Gimbal", "LiDAR Module", "Delivery Box" });
            payloadSelector.SelectedIndexChanged += (_, __) =>
            {
                if (syncingFeaturePanel || payloadSelector.SelectedItem is not string name) return;
                ApplyPayloadPreset(PayloadTypeFromDisplay(name), true);
            };
            stack.Controls.Add(CreateRow("Payload type", payloadSelector));

            payloadMassSelector = CreateNumericControl(0, 8, 0.01m, 2, payloadMassKg);
            payloadMassSelector.ValueChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                payloadMassKg = (float)payloadMassSelector.Value;
                TriggerFeatureRefresh(true);
            };
            stack.Controls.Add(CreateRow("Payload mass (kg)", payloadMassSelector));
            stack.Controls.Add(CreateNumeric("Payload offset (cm)", -40, 40, 0.1m, 1, payloadOffsetCm, v =>
            {
                payloadOffsetCm = v;
                TriggerFeatureRefresh(true);
            }));
            return page;
        }

        TabPage BuildPhysicsTab()
        {
            var page = CreateSectionPage("2 Physics", out var stack, out physicsSummary,
                "Advanced physics: thrust, drag, wind, turbulence, and ground effect.");
            stack.Controls.Add(CreateNumeric("Wind speed m/s", 0, 20, 0.1m, 1, environmentModel.WindSpeedMps, v => environmentModel.WindSpeedMps = v));
            stack.Controls.Add(CreateNumeric("Turbulence", 0, 1, 0.01m, 2, environmentModel.TurbulenceStrength, v => environmentModel.TurbulenceStrength = v));
            stack.Controls.Add(CreateNumeric("Drag coeff", 0.01m, 0.3m, 0.01m, 2, environmentModel.DragCoefficient, v => environmentModel.DragCoefficient = v));

            groundEffectToggle = new CheckBox { Text = "Enable ground effect", Checked = environmentModel.EnableGroundEffect, AutoSize = true };
            groundEffectToggle.CheckedChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                environmentModel.EnableGroundEffect = groundEffectToggle.Checked;
                TriggerFeatureRefresh(false);
            };
            stack.Controls.Add(groundEffectToggle);
            return page;
        }

        TabPage BuildTwinTab()
        {
            var page = CreateSectionPage("3 Twin", out var stack, out twinSummary,
                "Digital twin checks and fault simulation.");
            stack.Controls.Add(CreateToggle("Motor failure", faultInjection.MotorFailure, v => faultInjection.MotorFailure = v));
            stack.Controls.Add(CreateToggle("Sensor noise", faultInjection.SensorNoise, v => faultInjection.SensorNoise = v));
            stack.Controls.Add(CreateToggle("GPS drop", faultInjection.GpsDrop, v => faultInjection.GpsDrop = v));
            stack.Controls.Add(CreateToggle("ESC cutback", faultInjection.EscThermalCutback, v => faultInjection.EscThermalCutback = v));

            var run = CreateSmallButton("Run Pre-Flight", RunPreFlightValidation);
            var randomFault = CreateSmallButton("Random Fault", InjectRandomFault);
            var actions = new FlowLayoutPanel { AutoSize = true };
            actions.Controls.Add(run);
            actions.Controls.Add(randomFault);
            stack.Controls.Add(actions);
            return page;
        }

        TabPage BuildControlTab()
        {
            var page = CreateSectionPage("4 Control", out var stack, out controlSummary,
                "Flight control and software stack.");
            stack.Controls.Add(CreateNumeric("PID P", 0.2m, 3m, 0.01m, 2, pidP, v => pidP = v));
            stack.Controls.Add(CreateNumeric("PID I", 0m, 2m, 0.01m, 2, pidI, v => pidI = v));
            stack.Controls.Add(CreateNumeric("PID D", 0m, 2m, 0.01m, 2, pidD, v => pidD = v));

            firmwareSelector = CreateCombo(new[] { "Betaflight", "ArduPilot", "PX4" });
            firmwareSelector.SelectedIndexChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                if (firmwareSelector.SelectedItem is string value && Enum.TryParse<FirmwareProfile>(value, out var fw))
                    SetFirmwareProfile(fw);
            };
            stack.Controls.Add(CreateRow("Firmware", firmwareSelector));

            sensorSelector = CreateCombo(new[] { "SurveyGrade", "Nominal", "Degraded" });
            sensorSelector.SelectedIndexChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                if (sensorSelector.SelectedItem is string value && Enum.TryParse<SensorProfile>(value, out var sensor))
                    SetSensorProfile(sensor);
            };
            stack.Controls.Add(CreateRow("Sensor profile", sensorSelector));

            obstacleToggle = new CheckBox { Text = "Obstacle detection", Checked = obstacleAvoidanceEnabled, AutoSize = true };
            obstacleToggle.CheckedChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                obstacleAvoidanceEnabled = obstacleToggle.Checked;
                TriggerFeatureRefresh(false);
            };
            stack.Controls.Add(obstacleToggle);
            stack.Controls.Add(CreateSmallButton("Add Waypoint", AddWaypoint));
            stack.Controls.Add(CreateSmallButton("Survey Pattern", AddSurveyPattern));
            return page;
        }

        TabPage BuildModesTab()
        {
            var page = CreateSectionPage("5 Modes", out var stack, out modeSummary,
                "Simulation modes: manual, autonomous, emergency, swarm, VTOL, heavy-lift.");
            modeSelector = CreateCombo(new[] { "ManualFpv", "AutonomousMission", "EmergencyFailure", "Swarm", "VtolHybrid", "HeavyLift" });
            modeSelector.SelectedIndexChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                if (modeSelector.SelectedItem is string value && Enum.TryParse<SimulationMode>(value, out var mode))
                    SetSimulationMode(mode);
            };
            stack.Controls.Add(CreateRow("Simulation mode", modeSelector));
            stack.Controls.Add(CreateNumeric("Swarm size", 1, 60, 1, 0, featureProfile.SwarmSize, v => featureProfile.SwarmSize = Math.Max(1, (int)v)));
            return page;
        }

        TabPage BuildDataTab()
        {
            var page = CreateSectionPage("6 Data", out var stack, out dataSummary,
                "Telemetry, analytics, crash replay, and benchmarks.");
            stack.Controls.Add(CreateSmallButton("Telemetry", ShowTelemetryDashboard));
            stack.Controls.Add(CreateSmallButton("Export CSV", ExportTelemetryCsv));
            stack.Controls.Add(CreateSmallButton("Export JSON", ExportTelemetryJson));
            stack.Controls.Add(CreateSmallButton("Crash Replay", ShowCrashReplayAnalysis));
            stack.Controls.Add(CreateSmallButton("Save Benchmark", SaveBenchmarkSnapshot));
            return page;
        }

        TabPage BuildOpenTab()
        {
            var page = CreateSectionPage("7 Open", out var stack, out openSummary,
                "Open-source ecosystem: plugins, API, and extension points.");
            stack.Controls.Add(CreateSmallButton("Reload Plugins", () =>
            {
                LoadPluginsAtStartup();
                TriggerFeatureRefresh(false);
            }));
            stack.Controls.Add(CreateSmallButton("Export API Snapshot", ExportApiSnapshot));
            stack.Controls.Add(CreateSmallButton("Open Plugin Folder", OpenPluginFolder));

            pluginBox = new ListBox { Height = 92, Width = 280, IntegralHeight = false, SelectionMode = SelectionMode.None };
            stack.Controls.Add(pluginBox);
            return page;
        }

        TabPage BuildEduTab()
        {
            var page = CreateSectionPage("8 Education", out var stack, out eduSummary,
                "Guided mode with beginner-friendly steps and explanations.");
            educationToggle = new CheckBox { Text = "Enable guided explanations", Checked = educationMode, AutoSize = true };
            educationToggle.CheckedChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                educationMode = educationToggle.Checked;
                if (educationMode) MessageBox.Show(GetEducationHint(), "Education", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TriggerFeatureRefresh(false);
            };
            stack.Controls.Add(educationToggle);
            stack.Controls.Add(CreateSmallButton("Show Learning Hint", () =>
            {
                MessageBox.Show(GetEducationHint(), "Education", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
            return page;
        }

        TabPage CreateSectionPage(string title, out FlowLayoutPanel stack, out Label summary, string intro)
        {
            var page = new TabPage(title);
            stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
            page.Controls.Add(stack);
            stack.Controls.Add(new Label { Text = intro, Width = 280, Height = 32 });
            summary = new Label { Width = 280, Height = 108, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8) };
            stack.Controls.Add(summary);
            return page;
        }

        Panel CreateNumeric(string label, decimal min, decimal max, decimal step, int decimals, float value, Action<float> setValue)
        {
            var numeric = CreateNumericControl(min, max, step, decimals, value);
            numeric.ValueChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                setValue((float)numeric.Value);
                TriggerFeatureRefresh(false);
            };
            return CreateRow(label, numeric);
        }

        NumericUpDown CreateNumericControl(decimal min, decimal max, decimal step, int decimals, float value)
        {
            var numeric = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Increment = step,
                DecimalPlaces = decimals,
                TextAlign = HorizontalAlignment.Right
            };
            SetNumericValue(numeric, (decimal)value);
            return numeric;
        }

        ComboBox CreateCombo(IEnumerable<string> values)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 116 };
            combo.Items.AddRange(values.Cast<object>().ToArray());
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return combo;
        }

        CheckBox CreateToggle(string text, bool initial, Action<bool> setValue)
        {
            var toggle = new CheckBox { Text = text, Checked = initial, AutoSize = true };
            toggle.CheckedChanged += (_, __) =>
            {
                if (syncingFeaturePanel) return;
                setValue(toggle.Checked);
                TriggerFeatureRefresh(false);
            };
            return toggle;
        }

        Button CreateSmallButton(string text, Action click)
        {
            var button = new Button { Text = text, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            button.Click += (_, __) => click();
            return button;
        }

        Panel CreateRow(string labelText, Control control)
        {
            var row = new Panel { Width = 280, Height = 28 };
            row.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Left, Width = 160, TextAlign = ContentAlignment.MiddleLeft });
            control.Dock = DockStyle.Right;
            control.Width = 116;
            row.Controls.Add(control);
            return row;
        }

        void SetNumericValue(NumericUpDown numeric, decimal value)
        {
            numeric.Value = Math.Clamp(value, numeric.Minimum, numeric.Maximum);
        }

        void TriggerFeatureRefresh(bool resetPhysics)
        {
            if (resetPhysics) ResetPhysicsState();
            UpdateStatusBar();
            viewport?.Invalidate();
        }

        void InjectRandomFault()
        {
            int pick = random.Next(0, 4);
            faultInjection.MotorFailure = pick == 0;
            faultInjection.SensorNoise = pick == 1;
            faultInjection.GpsDrop = pick == 2;
            faultInjection.EscThermalCutback = pick == 3;
            TriggerFeatureRefresh(false);
        }

        void OpenPluginFolder()
        {
            string pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
            Directory.CreateDirectory(pluginDir);
            Process.Start(new ProcessStartInfo { FileName = pluginDir, UseShellExecute = true });
        }

        void ExportApiSnapshot()
        {
            if (project == null)
            {
                MessageBox.Show("No active project to export.", "API Snapshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var save = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = $"{project.Name.Replace(' ', '_')}_api_snapshot.json" };
            if (save.ShowDialog() != DialogResult.OK) return;

            var payload = new
            {
                project.Name,
                Mode = simulationMode,
                Firmware = firmwareProfile,
                Sensors = sensorProfile,
                Environment = environmentModel,
                Faults = faultInjection,
                FeatureProfile = featureProfile,
                Metrics = new { totalMassKg, totalThrustN, totalCurrentA, batteryVoltage, motorTempC, escTempC, frameStressPct, stabilityMarginPct, escFailureRiskPct }
            };

            File.WriteAllText(save.FileName, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            MessageBox.Show("API snapshot exported.", "API Snapshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ApplyPayloadPreset(PayloadType type, bool resetPhysics)
        {
            payloadType = type;
            payloadMassKg = type switch
            {
                PayloadType.CameraGimbal => 0.18f,
                PayloadType.LiDAR => 0.26f,
                PayloadType.DeliveryBox => 0.55f,
                _ => 0f
            };
            payloadOffsetCm = type switch
            {
                PayloadType.CameraGimbal => 3f,
                PayloadType.LiDAR => 4f,
                PayloadType.DeliveryBox => 7f,
                _ => 0f
            };

            if (payloadMassSelector != null) SetNumericValue(payloadMassSelector, (decimal)payloadMassKg);
            TriggerFeatureRefresh(resetPhysics);
        }

        static PayloadType PayloadTypeFromDisplay(string value) => value switch
        {
            "Camera Gimbal" => PayloadType.CameraGimbal,
            "LiDAR Module" => PayloadType.LiDAR,
            "Delivery Box" => PayloadType.DeliveryBox,
            _ => PayloadType.None
        };

        static string PayloadDisplay(PayloadType payload) => payload switch
        {
            PayloadType.CameraGimbal => "Camera Gimbal",
            PayloadType.LiDAR => "LiDAR Module",
            PayloadType.DeliveryBox => "Delivery Box",
            _ => "None"
        };

        void UpdateFeatureSetPanel()
        {
            if (featureTabs == null || featureTabs.IsDisposed) return;

            syncingFeaturePanel = true;
            try
            {
                if (modeSelector != null) modeSelector.SelectedItem = simulationMode.ToString();
                if (firmwareSelector != null) firmwareSelector.SelectedItem = firmwareProfile.ToString();
                if (sensorSelector != null) sensorSelector.SelectedItem = sensorProfile.ToString();
                if (payloadSelector != null) payloadSelector.SelectedItem = PayloadDisplay(payloadType);
                if (payloadMassSelector != null) SetNumericValue(payloadMassSelector, (decimal)payloadMassKg);
                if (obstacleToggle != null) obstacleToggle.Checked = obstacleAvoidanceEnabled;
                if (groundEffectToggle != null) groundEffectToggle.Checked = environmentModel.EnableGroundEffect;
                if (educationToggle != null) educationToggle.Checked = educationMode;
            }
            finally
            {
                syncingFeaturePanel = false;
            }

            float twr = totalMassKg > 0.01f ? totalThrustN / (totalMassKg * GRAVITY) : 0f;
            builderSummary.Text = $"Builder\\r\\nKV {featureProfile.MotorKvOverride:0} | Prop {featureProfile.PropDiameterInch:0.0}x{featureProfile.PropPitchInch:0.0}\\r\\nESC {featureProfile.EscCurrentLimitA:0}A | C-rating {featureProfile.BatteryCRating:0}\\r\\nPayload {PayloadDisplay(payloadType)} {payloadMassKg:0.00}kg @ {payloadOffsetCm:0.0}cm";
            physicsSummary.Text = $"Physics\\r\\nTWR {twr:0.00}:1 | Yaw {yawImbalancePct:0}%\\r\\nWind {environmentModel.WindSpeedMps:0.0} m/s | Turb {environmentModel.TurbulenceStrength:0.00}\\r\\nIMU vib {imuVibrationPct:0}% | Ground {(environmentModel.EnableGroundEffect ? "On" : "Off")}";
            twinSummary.Text = $"Digital Twin\\r\\nMotor {motorTempC:0}C | ESC {escTempC:0}C\\r\\nESC failure {escFailureRiskPct:0}%\\r\\nStress {frameStressPct:0}% | Stability {stabilityMarginPct:0}%";
            controlSummary.Text = $"Control\\r\\nPID P:{pidP:0.00} I:{pidI:0.00} D:{pidD:0.00}\\r\\nFW {firmwareProfile} | Sensor {sensorProfile}\\r\\nObstacle {(obstacleAvoidanceEnabled ? "On" : "Off")} | Waypoints {waypoints.Count}";
            modeSummary.Text = $"Modes\\r\\nActive {simulationMode}\\r\\nSwarm size {featureProfile.SwarmSize}\\r\\nTarget altitude {targetAltitude:0.00} m\\r\\nCrash events {crashCount}";
            dataSummary.Text = $"Data\\r\\nTelemetry samples {telemetry.Count}\\r\\nBenchmarks {benchmarkHistory.Count}\\r\\nLast crash: {lastCrashSummary}";
            openSummary.Text = $"Open Source\\r\\nPlugins {loadedPlugins.Count}\\r\\nAssets {AssetLibrary.Assets.Count}\\r\\nAPI snapshot export enabled";
            eduSummary.Text = $"Education\\r\\nGuided mode {(educationMode ? "On" : "Off")}\\r\\nUse tabs 1->8 for beginner workflow.";

            if (pluginBox != null)
            {
                var plugins = loadedPlugins.Count == 0 ? new[] { "No plugins loaded" } : loadedPlugins.ToArray();
                if (!pluginBox.Items.Cast<string>().SequenceEqual(plugins))
                {
                    pluginBox.Items.Clear();
                    pluginBox.Items.AddRange(plugins);
                }
            }
        }

        float GetPropellerThrustScale()
        {
            float diameterScale = MathF.Pow(Math.Clamp(featureProfile.PropDiameterInch / 5f, 0.45f, 3.2f), 2.85f);
            float pitchScale = Math.Clamp(1f + (featureProfile.PropPitchInch - 4.3f) * 0.085f, 0.70f, 1.55f);
            float bladeScale = Math.Clamp(0.88f + featureProfile.PropBladeCount * 0.06f, 0.82f, 1.36f);
            return diameterScale * pitchScale * bladeScale;
        }

        float EvaluateCurve(string? curve, float x, float fallback)
        {
            if (string.IsNullOrWhiteSpace(curve)) return fallback;
            var points = new List<(float X, float Y)>();
            foreach (var point in curve.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = point.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pair.Length != 2) continue;
                if (!float.TryParse(pair[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var px)) continue;
                if (!float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var py)) continue;
                points.Add((px, py));
            }

            if (points.Count == 0) return fallback;
            points.Sort((a, b) => a.X.CompareTo(b.X));
            if (x <= points[0].X) return points[0].Y;
            if (x >= points[^1].X) return points[^1].Y;

            for (int i = 1; i < points.Count; i++)
            {
                if (x <= points[i].X)
                {
                    float x0 = points[i - 1].X;
                    float x1 = points[i].X;
                    float y0 = points[i - 1].Y;
                    float y1 = points[i].Y;
                    float t = (x - x0) / Math.Max(0.0001f, x1 - x0);
                    return y0 + (y1 - y0) * t;
                }
            }

            return fallback;
        }
    }
}
