using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;

namespace SilvuViewfinder
{
    public class Form1 : Form, ISilvuHost
    {

        bool darkMode = false;
        bool dirty = false;

        Project? project;
        string? projectPath;

        LibraryPart? dragging;
        PlacedInstance? selected;
        PartType? pendingAddMode = null;
        string? pendingAddName = null;
        Point mousePos;

        MenuStrip menu = null!;
        TreeView projectTree = null!, libraryTree = null!;
        PictureBox viewport = null!;
        Label twrValueLabel = null!, hoverValueLabel = null!, flightValueLabel = null!, sagValueLabel = null!, tempValueLabel = null!;
        Label massMetricLabel = null!, powerMetricLabel = null!, enduranceMetricLabel = null!, payloadMetricLabel = null!;
        ListBox warningsList = null!;
        Button saveBuildButton = null!, runSimButton = null!, exportConfigButton = null!;
        int uiRefreshTick = 0;
        readonly Random random = new Random();
        readonly Stopwatch simClock = new Stopwatch();
        readonly List<TelemetrySample> telemetry = new List<TelemetrySample>();
        readonly List<Waypoint> waypoints = new List<Waypoint>();
        readonly List<string> loadedPlugins = new List<string>();
        readonly List<BuildBenchmark> benchmarkHistory = new List<BuildBenchmark>();
        SimulationMode simulationMode = SimulationMode.ManualFpv;
        SimulationEnvironment environmentModel = new SimulationEnvironment();
        FaultInjection faultInjection = new FaultInjection();
        PayloadType payloadType = PayloadType.None;
        FirmwareProfile firmwareProfile = FirmwareProfile.Betaflight;
        SensorProfile sensorProfile = SensorProfile.Nominal;
        bool obstacleAvoidanceEnabled = true;
        float payloadMassKg = 0f;
        float payloadOffsetCm = 0f;
        float motorTempC = 30f;
        float escTempC = 28f;
        float frameStressPct = 0f;
        float stabilityMarginPct = 100f;
        float yawImbalancePct = 0f;
        float imuVibrationPct = 0f;
        float escFailureRiskPct = 0f;
        string lastCrashSummary = "No crash events";
        double lastCrashTimeSec = -1;
        int crashCount = 0;
        int telemetryDecimator = 0;
        bool educationMode = false;

        // Status bar
        StatusStrip statusStrip = null!;
        ToolStripStatusLabel statusLabel = null!, frameStatus = null!, motorsStatus = null!, batteryStatus = null!, errorsStatus = null!;

        // clipboard for copy/paste of placed instances
        PlacedInstance? clipboardPart = null;

        // context menu for the project tree (layers)
        ContextMenuStrip projectContextMenu = null!;
        // context menu for the library tree
        ContextMenuStrip libraryContextMenu = null!;
        ToolTip partToolTip = null!;
        System.Windows.Forms.Timer renderTimer = null!;
        ToolStripProfessionalRenderer toolStripRenderer = null!;
        Panel contentHost = null!;
        SplitContainer navigationSplit = null!;
        Image? logoImage;

        readonly struct UiPalette
        {
            public UiPalette(
                Color windowBackground,
                Color surface,
                Color surfaceAlt,
                Color cardBackground,
                Color border,
                Color textPrimary,
                Color textMuted,
                Color accent,
                Color accentSoft,
                Color viewportBackground,
                Color hudBackground,
                Color hudText,
                Color success,
                Color warning
            )
            {
                WindowBackground = windowBackground;
                Surface = surface;
                SurfaceAlt = surfaceAlt;
                CardBackground = cardBackground;
                Border = border;
                TextPrimary = textPrimary;
                TextMuted = textMuted;
                Accent = accent;
                AccentSoft = accentSoft;
                ViewportBackground = viewportBackground;
                HudBackground = hudBackground;
                HudText = hudText;
                Success = success;
                Warning = warning;
            }

            public Color WindowBackground { get; }
            public Color Surface { get; }
            public Color SurfaceAlt { get; }
            public Color CardBackground { get; }
            public Color Border { get; }
            public Color TextPrimary { get; }
            public Color TextMuted { get; }
            public Color Accent { get; }
            public Color AccentSoft { get; }
            public Color ViewportBackground { get; }
            public Color HudBackground { get; }
            public Color HudText { get; }
            public Color Success { get; }
            public Color Warning { get; }
        }

        static readonly UiPalette LightPalette = new(
            windowBackground: Color.FromArgb(242, 246, 252),
            surface: Color.FromArgb(255, 255, 255),
            surfaceAlt: Color.FromArgb(236, 243, 252),
            cardBackground: Color.FromArgb(255, 255, 255),
            border: Color.FromArgb(213, 223, 238),
            textPrimary: Color.FromArgb(26, 36, 54),
            textMuted: Color.FromArgb(92, 106, 130),
            accent: Color.FromArgb(33, 117, 223),
            accentSoft: Color.FromArgb(219, 234, 255),
            viewportBackground: Color.FromArgb(244, 249, 255),
            hudBackground: Color.FromArgb(224, 234, 248),
            hudText: Color.FromArgb(26, 36, 54),
            success: Color.FromArgb(22, 145, 76),
            warning: Color.FromArgb(186, 118, 25)
        );

        static readonly UiPalette DarkPalette = new(
            windowBackground: Color.FromArgb(16, 22, 34),
            surface: Color.FromArgb(22, 30, 45),
            surfaceAlt: Color.FromArgb(29, 39, 58),
            cardBackground: Color.FromArgb(24, 33, 50),
            border: Color.FromArgb(54, 67, 92),
            textPrimary: Color.FromArgb(233, 240, 255),
            textMuted: Color.FromArgb(154, 172, 204),
            accent: Color.FromArgb(82, 154, 255),
            accentSoft: Color.FromArgb(41, 67, 103),
            viewportBackground: Color.FromArgb(14, 20, 31),
            hudBackground: Color.FromArgb(33, 44, 65),
            hudText: Color.FromArgb(228, 239, 255),
            success: Color.FromArgb(71, 196, 117),
            warning: Color.FromArgb(240, 172, 81)
        );

        UiPalette CurrentPalette => darkMode ? DarkPalette : LightPalette;

// ================= PID =================
float altitude = 0.0f;
float verticalVelocity = 0.0f;
float targetAltitude = 1.0f; // meters

float pidP = 1.2f;
float pidI = 0.4f;
float pidD = 0.2f;

float pidIntegral = 0;
float lastError = 0;

        // ===== Helpers for physics =====
        float ThrustFromRPM(float rpm)
        {
            // Simplified prop thrust curve: T ≈ k * RPM²
            const float k = 1.2e-6f;
            return k * rpm * rpm;
        }

        float PID(float error, float dt)
        {
            pidIntegral += error * dt;
            float derivative = (error - lastError) / dt;
            lastError = error;

            return pidP * error + pidI * pidIntegral + pidD * derivative;
        }

        // ===== Physics state =====
        // (single consolidated set of fields)
        float totalMassKg = 0.0f;
        float totalThrustN = 0.0f;
        float totalCurrentA = 0.0f;

        float batteryCapacityAh = 1.3f;   // default 1300mAh
        float batteryVoltageNominal = 22.2f;     // 6S
        float batteryVoltage = 22.2f;
        float batteryRemainingAh = 1.3f;

        const float GRAVITY = 9.81f;

        public Form1()
        {
            Text = "SILVU VIEWFINDER";
            Width = 1400;
            Height = 860;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            BuildUI();
            Shown += (_, __) => ConfigureNavigationSplit();
            simClock.Start();
            LoadPluginsAtStartup();
        }

        void BuildUI()
        {
            SuspendLayout();

            DoubleBuffered = true;
            MinimumSize = new Size(1150, 720);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            const int outerGutter = 14;
            const int laneGap = 10;

            toolStripRenderer = new ToolStripProfessionalRenderer(new ThemedColorTable(() => CurrentPalette));

            // MENU
            menu = new MenuStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(outerGutter, 5, outerGutter, 5),
                AutoSize = false,
                Height = 44,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = toolStripRenderer
            };
            logoImage = LoadBrandLogo();

            var proj = new ToolStripMenuItem("Project");
            proj.DropDownItems.Add(CreateMenuItem("New", Keys.Control | Keys.N, (_,__) => NewProject()));
            proj.DropDownItems.Add(CreateMenuItem("Open...", Keys.Control | Keys.O, (_,__) => OpenProject()));
            proj.DropDownItems.Add(CreateMenuItem("Save", Keys.Control | Keys.S, (_,__) => SaveProject()));
            proj.DropDownItems.Add(new ToolStripSeparator());
            proj.DropDownItems.Add(CreateMenuItem("Exit", Keys.Alt | Keys.F4, (_,__) => Close()));

            var tools = new ToolStripMenuItem("Tools");
            tools.DropDownItems.Add(CreateMenuItem("Toggle Dark Mode", Keys.Control | Keys.D, (_,__) => ToggleDark()));

            // ASSETS menu
            var assetsMenu = new ToolStripMenuItem("Assets");
            assetsMenu.DropDownItems.Add(CreateMenuItem("Import Asset...", Keys.Control | Keys.I, (_,__) => ImportAsset()));
            var newAsset = new ToolStripMenuItem("New Asset");
            newAsset.DropDownItems.Add("Motor", null, (_,__) => CreateNewAsset("Motors"));
            newAsset.DropDownItems.Add("Battery", null, (_,__) => CreateNewAsset("Batteries"));
            newAsset.DropDownItems.Add("ESC", null, (_,__) => CreateNewAsset("ESC"));
            newAsset.DropDownItems.Add("Frame", null, (_,__) => CreateNewAsset("Frames"));
            newAsset.DropDownItems.Add("FC", null, (_,__) => CreateNewAsset("FC"));
            newAsset.DropDownItems.Add("Props", null, (_,__) => CreateNewAsset("Props"));
            newAsset.DropDownItems.Add("Receivers", null, (_,__) => CreateNewAsset("Receivers"));
            assetsMenu.DropDownItems.Add(newAsset);

            var simMenu = new ToolStripMenuItem("Simulation");
            var modeMenu = new ToolStripMenuItem("Mode");
            void AddModeItem(string text, SimulationMode mode)
            {
                var item = new ToolStripMenuItem(text) { Checked = simulationMode == mode, CheckOnClick = true };
                item.Click += (_, __) =>
                {
                    SetSimulationMode(mode);
                    foreach (ToolStripMenuItem mi in modeMenu.DropDownItems.OfType<ToolStripMenuItem>())
                        mi.Checked = mi == item;
                };
                modeMenu.DropDownItems.Add(item);
            }
            AddModeItem("Manual FPV", SimulationMode.ManualFpv);
            AddModeItem("Autonomous Mission", SimulationMode.AutonomousMission);
            AddModeItem("Emergency Failure", SimulationMode.EmergencyFailure);
            AddModeItem("Swarm", SimulationMode.Swarm);
            AddModeItem("VTOL / Hybrid", SimulationMode.VtolHybrid);
            AddModeItem("Heavy Lift", SimulationMode.HeavyLift);

            var envMenu = new ToolStripMenuItem("Environment");
            envMenu.DropDownItems.Add("Calm", null, (_, __) => ApplyEnvironmentPreset(0.5f, 0.1f, 0.07f));
            envMenu.DropDownItems.Add("Breezy", null, (_, __) => ApplyEnvironmentPreset(3.0f, 0.2f, 0.09f));
            envMenu.DropDownItems.Add("Windy", null, (_, __) => ApplyEnvironmentPreset(6.0f, 0.35f, 0.11f));
            envMenu.DropDownItems.Add("Storm Test", null, (_, __) => ApplyEnvironmentPreset(10.0f, 0.5f, 0.16f));

            var payloadMenu = new ToolStripMenuItem("Payload");
            payloadMenu.DropDownItems.Add("None", null, (_, __) => SetPayload(PayloadType.None, 0f, 0f));
            payloadMenu.DropDownItems.Add("Camera Gimbal", null, (_, __) => SetPayload(PayloadType.CameraGimbal, 0.18f, 3f));
            payloadMenu.DropDownItems.Add("LiDAR Module", null, (_, __) => SetPayload(PayloadType.LiDAR, 0.26f, 4f));
            payloadMenu.DropDownItems.Add("Delivery Box", null, (_, __) => SetPayload(PayloadType.DeliveryBox, 0.55f, 7f));

            var faultMenu = new ToolStripMenuItem("Fault Injection");
            var motorFailItem = new ToolStripMenuItem("Motor Failure") { CheckOnClick = true };
            motorFailItem.CheckedChanged += (_, __) => faultInjection.MotorFailure = motorFailItem.Checked;
            var sensorNoiseItem = new ToolStripMenuItem("Sensor Noise") { CheckOnClick = true };
            sensorNoiseItem.CheckedChanged += (_, __) => faultInjection.SensorNoise = sensorNoiseItem.Checked;
            var gpsDropItem = new ToolStripMenuItem("GPS Drop") { CheckOnClick = true };
            gpsDropItem.CheckedChanged += (_, __) => faultInjection.GpsDrop = gpsDropItem.Checked;
            var escCutItem = new ToolStripMenuItem("ESC Thermal Cutback") { CheckOnClick = true };
            escCutItem.CheckedChanged += (_, __) => faultInjection.EscThermalCutback = escCutItem.Checked;
            faultMenu.DropDownItems.Add(motorFailItem);
            faultMenu.DropDownItems.Add(sensorNoiseItem);
            faultMenu.DropDownItems.Add(gpsDropItem);
            faultMenu.DropDownItems.Add(escCutItem);

            var missionMenu = new ToolStripMenuItem("Mission");
            missionMenu.DropDownItems.Add("Add Waypoint", null, (_, __) => AddWaypoint());
            missionMenu.DropDownItems.Add("Clear Waypoints", null, (_, __) => { waypoints.Clear(); UpdateStatusBar(); viewport.Invalidate(); });
            missionMenu.DropDownItems.Add("Add Survey Pattern (8)", null, (_, __) => AddSurveyPattern());

            var controlMenu = new ToolStripMenuItem("Control Stack");

            var pidMenu = new ToolStripMenuItem("PID Presets");
            pidMenu.DropDownItems.Add("Cinematic", null, (_, __) => SetPidPreset(1.0f, 0.35f, 0.18f, "Cinematic"));
            pidMenu.DropDownItems.Add("Balanced", null, (_, __) => SetPidPreset(1.2f, 0.40f, 0.20f, "Balanced"));
            pidMenu.DropDownItems.Add("Aggressive", null, (_, __) => SetPidPreset(1.45f, 0.52f, 0.27f, "Aggressive"));

            var firmwareMenu = new ToolStripMenuItem("Firmware Layer");
            void AddFirmwareOption(string text, FirmwareProfile profile)
            {
                var item = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = firmwareProfile == profile };
                item.Click += (_, __) =>
                {
                    SetFirmwareProfile(profile);
                    foreach (ToolStripMenuItem mi in firmwareMenu.DropDownItems.OfType<ToolStripMenuItem>())
                        mi.Checked = mi == item;
                };
                firmwareMenu.DropDownItems.Add(item);
            }
            AddFirmwareOption("Betaflight", FirmwareProfile.Betaflight);
            AddFirmwareOption("ArduPilot", FirmwareProfile.ArduPilot);
            AddFirmwareOption("PX4", FirmwareProfile.PX4);

            var sensorMenu = new ToolStripMenuItem("Sensor Model");
            sensorMenu.DropDownItems.Add("Survey Grade", null, (_, __) => SetSensorProfile(SensorProfile.SurveyGrade));
            sensorMenu.DropDownItems.Add("Nominal", null, (_, __) => SetSensorProfile(SensorProfile.Nominal));
            sensorMenu.DropDownItems.Add("Degraded", null, (_, __) => SetSensorProfile(SensorProfile.Degraded));

            var obstacleItem = new ToolStripMenuItem("Obstacle Detection") { CheckOnClick = true, Checked = obstacleAvoidanceEnabled };
            obstacleItem.CheckedChanged += (_, __) =>
            {
                obstacleAvoidanceEnabled = obstacleItem.Checked;
                UpdateStatusBar();
            };

            controlMenu.DropDownItems.Add(pidMenu);
            controlMenu.DropDownItems.Add(firmwareMenu);
            controlMenu.DropDownItems.Add(sensorMenu);
            controlMenu.DropDownItems.Add(obstacleItem);
            controlMenu.DropDownItems.Add(new ToolStripSeparator());
            controlMenu.DropDownItems.Add("Run Pre-Flight Validation", null, (_, __) => RunPreFlightValidation());

            simMenu.DropDownItems.Add(modeMenu);
            simMenu.DropDownItems.Add(envMenu);
            simMenu.DropDownItems.Add(payloadMenu);
            simMenu.DropDownItems.Add(faultMenu);
            simMenu.DropDownItems.Add(missionMenu);
            simMenu.DropDownItems.Add(controlMenu);

            var dataMenu = new ToolStripMenuItem("Data");
            dataMenu.DropDownItems.Add("Telemetry Dashboard", null, (_, __) => ShowTelemetryDashboard());
            dataMenu.DropDownItems.Add("Export Telemetry CSV...", null, (_, __) => ExportTelemetryCsv());
            dataMenu.DropDownItems.Add("Export Telemetry JSON...", null, (_, __) => ExportTelemetryJson());
            dataMenu.DropDownItems.Add("Crash Replay Analysis", null, (_, __) => ShowCrashReplayAnalysis());
            dataMenu.DropDownItems.Add(new ToolStripSeparator());
            dataMenu.DropDownItems.Add("Save Benchmark Snapshot", null, (_, __) => SaveBenchmarkSnapshot());
            dataMenu.DropDownItems.Add("Compare Benchmarks", null, (_, __) => CompareBenchmarks());
            dataMenu.DropDownItems.Add(new ToolStripSeparator());
            dataMenu.DropDownItems.Add("Clear Telemetry", null, (_, __) => { telemetry.Clear(); UpdateStatusBar(); viewport.Invalidate(); });

            var educationMenu = new ToolStripMenuItem("Education");
            var educationToggle = new ToolStripMenuItem("Guided Explanations") { CheckOnClick = true };
            educationToggle.CheckedChanged += (_, __) =>
            {
                educationMode = educationToggle.Checked;
                if (educationMode)
                    MessageBox.Show(GetEducationHint(), "Education Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStatusBar();
            };
            educationMenu.DropDownItems.Add(educationToggle);

            if (logoImage != null)
            {
                var logoLabel = new ToolStripLabel
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Image,
                    Image = logoImage,
                    ImageScaling = ToolStripItemImageScaling.None,
                    AutoSize = false,
                    Size = new Size(138, 30),
                    Margin = new Padding(0, 0, 14, 0),
                    ToolTipText = "SILVU"
                };
                menu.Items.Add(logoLabel);
            }

            var brandSubtitle = new ToolStripLabel("Component-Level Drone Builder")
            {
                ForeColor = CurrentPalette.TextMuted,
                Margin = new Padding(0, 0, 16, 0)
            };
            menu.Items.Add(brandSubtitle);

            menu.Items.Add(proj);
            menu.Items.Add(tools);
            menu.Items.Add(assetsMenu);
            menu.Items.Add(simMenu);
            menu.Items.Add(dataMenu);
            menu.Items.Add(educationMenu);
            menu.Items.Add(new ToolStripSeparator());

            var quickSaveButton = new ToolStripButton("Save")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            quickSaveButton.Click += (_, __) => SaveProject();
            var quickRunButton = new ToolStripButton("Run")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            quickRunButton.Click += (_, __) => viewport.Invalidate();
            menu.Items.Add(quickSaveButton);
            menu.Items.Add(quickRunButton);

            MainMenuStrip = menu;

            // LAYOUT
            var left = new Panel
            {
                Dock = DockStyle.Left,
                Width = 330,
                Padding = new Padding(outerGutter, outerGutter, laneGap / 2, outerGutter)
            };
            var right = new Panel
            {
                Dock = DockStyle.Right,
                Width = 340,
                Padding = new Padding(laneGap / 2, outerGutter, outerGutter, outerGutter)
            };
            var center = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(laneGap / 2, outerGutter, laneGap / 2, outerGutter)
            };
            var centerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
            center.Controls.Add(centerLayout);
            contentHost = new Panel
            {
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            contentHost.Controls.Add(center);
            contentHost.Controls.Add(right);
            contentHost.Controls.Add(left);
            Controls.Add(contentHost);

            // STATUS STRIP
            statusStrip = new StatusStrip
            {
                SizingGrip = false,
                Padding = new Padding(outerGutter, 4, outerGutter, 4),
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = toolStripRenderer
            };
            statusLabel = new ToolStripStatusLabel("Ready");
            frameStatus = new ToolStripStatusLabel(" | Frame: 0");
            motorsStatus = new ToolStripStatusLabel(" | Motors: 0");
            batteryStatus = new ToolStripStatusLabel(" | Battery: None");
            errorsStatus = new ToolStripStatusLabel(" | Errors: 0");
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, frameStatus, motorsStatus, batteryStatus, errorsStatus });
            Controls.Add(statusStrip);
            Controls.Add(menu);
            menu.BringToFront();
            statusStrip.BringToFront();
            Resize += (_, __) => LayoutRootPanels();
            menu.SizeChanged += (_, __) => LayoutRootPanels();
            statusStrip.SizeChanged += (_, __) => LayoutRootPanels();
            LayoutRootPanels();
            UpdateStatusBar();

            // PROJECT TREE
            projectTree = new TreeView { Dock = DockStyle.Top, Height = 260 };
            ConfigureTree(projectTree);
            projectTree.AfterSelect += (_, e) =>
            {
                selected = e.Node?.Tag as PlacedInstance;
                viewport.Invalidate();
            }; 

            projectTree.NodeMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                if (project == null) return;

                projectTree.SelectedNode = e.Node;

                // create context menu lazily so it can reference runtime state like clipboardPart
                if (projectContextMenu == null)
                {
                    projectContextMenu = new ContextMenuStrip
                    {
                        RenderMode = ToolStripRenderMode.Professional,
                        Renderer = toolStripRenderer
                    };

                    var infoItem = new ToolStripMenuItem("Info", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is PlacedInstance p)
                            MessageBox.Show(GetPlacedPartInfo(p), "Part Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });

                    var deleteItem = new ToolStripMenuItem("Delete", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is PlacedInstance p)
                        {
                            if (p.Type == PartType.Frame)
                            {
                                MessageBox.Show("Cannot delete the frame.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            project.Instances.Remove(p);
                            OnProjectStructureChanged();
                        }
                    });

                    var saveCustomItem = new ToolStripMenuItem("Save as Custom", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is not PlacedInstance placed) return;
                        var sourceAsset = AssetLibrary.Get(placed.AssetId);
                        if (sourceAsset == null) return;

                        var copy = CloneAsset(sourceAsset);
                        copy.Name += " Custom";
                        copy.Meta.IsCustom = true;
                        AssetLibrary.SaveAssetToUserDir(copy);
                        AssetLibrary.LoadAll(AssetLibrary.UserAssetRoot);
                        BuildLibrary();
                    });

                    var copyItem = new ToolStripMenuItem("Copy", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is PlacedInstance p)
                        {
                            clipboardPart = new PlacedInstance { AssetId = p.AssetId, Type = p.Type, Position = p.Position, MountIndex = p.MountIndex };
                        }
                    });

                    var pasteItem = new ToolStripMenuItem("Paste", null, (_,__) =>
                    {
                        if (clipboardPart != null && project != null)
                        {
                            var p = new PlacedInstance
                            {
                                AssetId = clipboardPart.AssetId,
                                Type = clipboardPart.Type,
                                Position = new PointF(clipboardPart.Position.X + 10, clipboardPart.Position.Y + 10),
                                MountIndex = clipboardPart.MountIndex
                            };
                            project.Instances.Add(p);
                            OnProjectStructureChanged();
                        }
                    });

                    projectContextMenu.Items.AddRange(new ToolStripItem[] { infoItem, deleteItem, saveCustomItem, new ToolStripSeparator(), copyItem, pasteItem });

                    projectContextMenu.Opening += (_, __) =>
                    {
                        var node = projectTree.SelectedNode;
                        bool hasPlaced = node?.Tag is PlacedInstance;
                        infoItem.Enabled = hasPlaced;
                        deleteItem.Enabled = hasPlaced && (node?.Tag as PlacedInstance)?.Type != PartType.Frame;
                        saveCustomItem.Enabled = hasPlaced;
                        copyItem.Enabled = hasPlaced;
                        pasteItem.Enabled = clipboardPart != null;
                    }; 
                }

                ApplyContextMenuTheme(projectContextMenu);
                projectContextMenu.Show(projectTree, e.Location);
            };

            // LIBRARY TREE
            libraryTree = new TreeView { Dock = DockStyle.Fill };
            ConfigureTree(libraryTree);
            libraryTree.ItemDrag += (_, e) =>
            {
                if (e.Item is TreeNode n && n.Parent != null)
                {
                    dragging = new LibraryPart { Category = n.Parent.Text, Name = n.Text };
                    DoDragDrop(dragging, DragDropEffects.Copy);
                }
            };
            libraryTree.NodeMouseDoubleClick += (s, e) =>
            {
                if (project == null) return;

                if (e.Node?.Parent?.Text == "Frames")
                {
                    project.Instances.Clear();

                    var frameAsset = AssetLibrary.FindByName(e.Node.Text);
                    project.Instances.Add(new PlacedInstance
                    {
                        AssetId = frameAsset?.Id ?? "",
                        Type = PartType.Frame,
                        Position = new PointF(viewport.Width / 2, viewport.Height / 2)
                    });

                    OnProjectStructureChanged();
                    return; 
                }

                if (e.Node?.Parent?.Text == "Motors")
                {
                    // next click in viewport places motor
                    pendingAddMode = PartType.Motor;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (e.Node?.Parent?.Text == "Batteries")
                {
                    pendingAddMode = PartType.Battery;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }
            };

            libraryTree.NodeMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                libraryTree.SelectedNode = e.Node;
                if (e.Node == null || e.Node.Parent == null) return; // must be an asset entry
                var asset = e.Node.Tag as Asset;
                if (asset == null) return;

                if (libraryContextMenu == null)
                {
                    libraryContextMenu = new ContextMenuStrip
                    {
                        RenderMode = ToolStripRenderMode.Professional,
                        Renderer = toolStripRenderer
                    };
                    libraryContextMenu.Items.Add(new ToolStripMenuItem("Edit", null, (_,__) => { if (libraryTree.SelectedNode?.Tag is Asset a) OpenAssetEditor(a); }));
                    libraryContextMenu.Items.Add(new ToolStripMenuItem("Duplicate", null, (_,__) => { if (libraryTree.SelectedNode?.Tag is Asset a) { var copy = CloneAsset(a); copy.Name += " Copy"; copy.Meta.IsCustom = true; AssetLibrary.SaveAssetToUserDir(copy); AssetLibrary.LoadAll(AssetLibrary.UserAssetRoot); BuildLibrary(); } }));
                    libraryContextMenu.Items.Add(new ToolStripMenuItem("Export...", null, (_,__) => { if (libraryTree.SelectedNode?.Tag is Asset a) ExportAsset(a); }));
                    libraryContextMenu.Items.Add(new ToolStripMenuItem("Delete", null, (_,__) => { if (libraryTree.SelectedNode?.Tag is Asset a) { DeleteAsset(a); } }));
                    libraryContextMenu.Items.Add(new ToolStripMenuItem("Reveal in Explorer", null, (_,__) => { if (libraryTree.SelectedNode?.Tag is Asset a) RevealInExplorer(a); }));
                }

                // enable/disable delete depending on whether asset is custom and has a path
                var deleteItem = libraryContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Text == "Delete") as ToolStripMenuItem;
                if (deleteItem != null && libraryTree.SelectedNode?.Tag is Asset selectedAsset)
                    deleteItem.Enabled = selectedAsset.Meta.IsCustom && AssetLibrary.Paths.ContainsKey(selectedAsset.Id);

                ApplyContextMenuTheme(libraryContextMenu);
                libraryContextMenu.Show(libraryTree, e.Location);
            };

            BuildLibrary();

            navigationSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 8,
                FixedPanel = FixedPanel.None
            };
            navigationSplit.SizeChanged += (_, __) => ConfigureNavigationSplit();

            var layersLabel = new Label
            {
                Text = "LAYERS",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
            };

            projectTree.Dock = DockStyle.Fill;

            var layersPanel = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                BorderThickness = 1,
                Padding = new Padding(10)
            };
            layersPanel.Controls.Add(projectTree);
            layersPanel.Controls.Add(layersLabel);

            var partsLabel = new Label
            {
                Text = "AVAILABLE PARTS",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
            };

            libraryTree.Dock = DockStyle.Fill;

            var partsPanel = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                BorderThickness = 1,
                Padding = new Padding(10)
            };
            partsPanel.Controls.Add(libraryTree);
            partsPanel.Controls.Add(partsLabel);

            navigationSplit.Panel1.Padding = new Padding(0, 0, 0, 8);
            navigationSplit.Panel2.Padding = new Padding(0, 8, 0, 0);
            navigationSplit.Panel1.Controls.Add(layersPanel);
            navigationSplit.Panel2.Controls.Add(partsPanel);

            left.Controls.Add(navigationSplit);

            partToolTip = new ToolTip();

            libraryTree.NodeMouseHover += (s, e) =>
            {
                if (e.Node == null || e.Node.Parent == null)
                    return;

                string info = GetPartInfo(e.Node.Parent.Text, e.Node.Text);
                partToolTip.SetToolTip(libraryTree, info);
            };

            Panel CreateAnalysisRow(string labelText, out Label valueLabel)
            {
                var row = new Panel { Dock = DockStyle.Top, Height = 34 };
                var label = new Label
                {
                    Dock = DockStyle.Left,
                    Width = 160,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = labelText,
                    Padding = new Padding(2, 0, 0, 0)
                };
                valueLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = new Font(Font.FontFamily, 11f, FontStyle.Bold, GraphicsUnit.Point),
                    Text = "--"
                };
                row.Controls.Add(valueLabel);
                row.Controls.Add(label);
                return row;
            }

            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 72f));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 28f));
            right.Controls.Add(rightLayout);

            var analysisCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                BorderThickness = 1,
                Padding = new Padding(10)
            };
            var analysisHeader = new Label
            {
                Text = "BUILD ANALYSIS",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
            };
            var analysisBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 8, 4, 4)
            };

            var r1 = CreateAnalysisRow("Thrust-to-Weight", out twrValueLabel);
            var r2 = CreateAnalysisRow("Hover Throttle", out hoverValueLabel);
            var r3 = CreateAnalysisRow("Flight Time Est.", out flightValueLabel);
            var r4 = CreateAnalysisRow("Voltage Sag", out sagValueLabel);
            var r5 = CreateAnalysisRow("Motor Temp", out tempValueLabel);
            r1.Dock = DockStyle.Top;
            r2.Dock = DockStyle.Top;
            r3.Dock = DockStyle.Top;
            r4.Dock = DockStyle.Top;
            r5.Dock = DockStyle.Top;
            analysisBody.Controls.Add(r5);
            analysisBody.Controls.Add(r4);
            analysisBody.Controls.Add(r3);
            analysisBody.Controls.Add(r2);
            analysisBody.Controls.Add(r1);
            analysisCard.Controls.Add(analysisBody);
            analysisCard.Controls.Add(analysisHeader);

            var warningsCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                BorderThickness = 1,
                Padding = new Padding(10)
            };
            var warningsHeader = new Label
            {
                Text = "BUILD WARNINGS",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
            };
            warningsList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                SelectionMode = SelectionMode.None
            };
            warningsCard.Controls.Add(warningsList);
            warningsCard.Controls.Add(warningsHeader);

            rightLayout.Controls.Add(analysisCard, 0, 0);
            rightLayout.Controls.Add(warningsCard, 0, 1);




            // VIEWPORT
            viewport = new BufferedPictureBox { Dock = DockStyle.Fill, AllowDrop = true };
            viewport.Paint += DrawViewport;
            viewport.DragEnter += (_, e) => e.Effect = DragDropEffects.Copy;
            viewport.DragDrop += (_, e) =>
            {
                if (dragging == null || project == null) return;
                var p = viewport.PointToClient(new Point(e.X, e.Y));
                var pt = new PointF(p.X, p.Y);

                if (dragging.Category == "Frames")
                {
                    project.Instances.Clear();

                    var frameAsset = AssetLibrary.FindByName(dragging.Name);
                    project.Instances.Add(new PlacedInstance
                    {
                        AssetId = frameAsset?.Id ?? "",
                        Type = PartType.Frame,
                        Position = pt
                    });

                    OnProjectStructureChanged();

                    // clear drag/add state
                    dragging = null;
                    pendingAddMode = null;
                    pendingAddName = null;
                    viewport.Cursor = Cursors.Default;

                    return;
                }

                bool added = false;

                if (dragging.Category == "Motors")
                {
                    added = AddMotor(pt, dragging.Name);
                }
                else if (dragging.Category == "Batteries")
                {
                    added = AddBattery(pt, dragging.Name);
                }

                if (added)
                {
                    OnProjectStructureChanged();

                    // clear drag/add state
                    dragging = null;
                    pendingAddMode = null;
                    pendingAddName = null;
                    viewport.Cursor = Cursors.Default;
                }
            };

            var viewportCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 18,
                BorderThickness = 1,
                Padding = new Padding(12)
            };
            var viewportHeader = new Label
            {
                Text = "VIEWPORT",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
            };

            var viewportHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };

            viewportHeaderPanel.Controls.Add(viewportHeader);

            viewportCard.Controls.Add(viewport);
            viewportCard.Controls.Add(viewportHeaderPanel);

            var quickMetricsCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 12,
                BorderThickness = 1,
                Padding = new Padding(8, 6, 8, 6)
            };
            var metricsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            Label CreateMetricLabel(string text)
            {
                return new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point),
                    Text = text
                };
            }

            massMetricLabel = CreateMetricLabel("AUW: --");
            powerMetricLabel = CreateMetricLabel("Power Draw: --");
            enduranceMetricLabel = CreateMetricLabel("Est. Flight: --");
            payloadMetricLabel = CreateMetricLabel("Stress/Stab: --");

            metricsTable.Controls.Add(massMetricLabel, 0, 0);
            metricsTable.Controls.Add(powerMetricLabel, 1, 0);
            metricsTable.Controls.Add(enduranceMetricLabel, 2, 0);
            metricsTable.Controls.Add(payloadMetricLabel, 3, 0);
            quickMetricsCard.Controls.Add(metricsTable);

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0),
                WrapContents = false
            };

            exportConfigButton = new Button { Name = "btnExportConfig", Text = "EXPORT CONFIG", Width = 170, Height = 40 };
            exportConfigButton.Click += (_, __) =>
            {
                if (project == null)
                {
                    MessageBox.Show("No active project to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var sfd = new SaveFileDialog
                {
                    Filter = "JSON (*.json)|*.json",
                    FileName = (project.Name + "_config.json").Replace(" ", "_")
                };
                if (sfd.ShowDialog() != DialogResult.OK) return;

                var exportPayload = new
                {
                    project.Name,
                    Instances = project.Instances.Count,
                    Metrics = new
                    {
                        MassKg = totalMassKg,
                        ThrustN = totalThrustN,
                        CurrentA = totalCurrentA,
                        Voltage = batteryVoltage
                    }
                };
                File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = true }));
            };

            runSimButton = new Button { Name = "btnRunSimulation", Text = "RUN SIMULATION", Width = 170, Height = 40 };
            runSimButton.Click += (_, __) =>
            {
                viewport.Focus();
                viewport.Invalidate();
            };

            saveBuildButton = new Button { Name = "btnSaveBuild", Text = "SAVE BUILD", Width = 170, Height = 40 };
            saveBuildButton.Click += (_, __) => SaveProject();

            actionPanel.Controls.Add(exportConfigButton);
            actionPanel.Controls.Add(runSimButton);
            actionPanel.Controls.Add(saveBuildButton);

            centerLayout.Controls.Add(viewportCard, 0, 0);
            centerLayout.Controls.Add(quickMetricsCard, 0, 1);
            centerLayout.Controls.Add(actionPanel, 0, 2);

            viewport.MouseMove += (s, e) =>
            {
                mousePos = e.Location;
                if (pendingAddMode != null)
                    viewport.Invalidate();
            };

            viewport.MouseDown += (s, e) =>
            { 
                // Right-click deletes non-frame parts under the cursor
                if (e.Button == MouseButtons.Right && project != null)
                {
                    var toRemove = new List<PlacedInstance>();
                    foreach (var p in project.Instances)
                    {
                        if (p.Type == PartType.Frame) continue;
                        var worldPos = GetPartWorldPosition(p);
                        if (Distance(worldPos, e.Location) < 20)
                            toRemove.Add(p);
                    }
                    project.Instances.RemoveAll(p => toRemove.Contains(p));
                    if (toRemove.Count > 0)
                    {
                        OnProjectStructureChanged();
                    }
                    return; 
                }

                // Left-click: if user selected a part in library, place it now
                if (e.Button == MouseButtons.Left && project != null && pendingAddMode != null)
                {
                    if (pendingAddMode == PartType.Motor)
                    {
                        bool added = AddMotor(e.Location, pendingAddName ?? "Motor");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added) OnProjectStructureChanged();
                        return;
                    }
                    else if (pendingAddMode == PartType.Battery)
                    {
                        bool added = AddBattery(e.Location, pendingAddName ?? "Battery");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added) OnProjectStructureChanged();
                        return;
                    }
                }
            };

            renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
            renderTimer.Tick += (_, __) =>
            {
                if (!IsDisposed && IsHandleCreated && Visible)
                {
                    viewport.Invalidate();
                    uiRefreshTick++;
                    if (uiRefreshTick >= 12)
                    {
                        uiRefreshTick = 0;
                        UpdateStatusBar();
                    }
                }
            };
            renderTimer.Start();

            ApplyTheme();
            NewProject();

            ResumeLayout(true);
        }

        ToolStripMenuItem CreateMenuItem(string text, Keys shortcuts, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text, null, handler);
            if (shortcuts != Keys.None)
                item.ShortcutKeys = shortcuts;
            return item;
        }

        void ConfigureTree(TreeView tree)
        {
            tree.BorderStyle = BorderStyle.None;
            tree.FullRowSelect = true;
            tree.HideSelection = false;
            tree.HotTracking = true;
            tree.ShowLines = false;
            tree.ShowRootLines = false;
            tree.ItemHeight = 24;
        }

        void SetSimulationMode(SimulationMode mode)
        {
            simulationMode = mode;
            statusLabel.Text = $"Mode: {mode}";
            UpdateStatusBar();
            viewport.Invalidate();
        }

        void ApplyEnvironmentPreset(float wind, float turbulence, float drag)
        {
            environmentModel.WindSpeedMps = wind;
            environmentModel.TurbulenceStrength = turbulence;
            environmentModel.DragCoefficient = drag;
            UpdateStatusBar();
            viewport.Invalidate();
        }

        void SetPayload(PayloadType type, float massKg, float offsetCm)
        {
            payloadType = type;
            payloadMassKg = massKg;
            payloadOffsetCm = offsetCm;
            ResetPhysicsState();
            UpdateStatusBar();
            viewport.Invalidate();
        }

        void AddWaypoint()
        {
            waypoints.Add(new Waypoint
            {
                X = random.Next(-120, 120),
                Y = random.Next(-120, 120),
                AltitudeM = (float)Math.Round(0.8 + random.NextDouble() * 2.2, 2)
            });
            UpdateStatusBar();
            viewport.Invalidate();
        }

        void AddSurveyPattern()
        {
            waypoints.Clear();
            const int points = 8;
            const float radius = 95f;
            for (int i = 0; i < points; i++)
            {
                float angle = (float)(Math.PI * 2.0 * i / points);
                waypoints.Add(new Waypoint
                {
                    X = (float)Math.Cos(angle) * radius,
                    Y = (float)Math.Sin(angle) * radius,
                    AltitudeM = 1.2f + (i % 2 == 0 ? 0.3f : 0.7f)
                });
            }
            UpdateStatusBar();
            viewport.Invalidate();
        }

        void SetPidPreset(float p, float i, float d, string presetName)
        {
            pidP = p;
            pidI = i;
            pidD = d;
            statusLabel.Text = $"PID preset: {presetName}";
            UpdateStatusBar();
        }

        void SetFirmwareProfile(FirmwareProfile profile)
        {
            firmwareProfile = profile;
            statusLabel.Text = $"Firmware profile: {profile}";
            UpdateStatusBar();
        }

        void SetSensorProfile(SensorProfile profile)
        {
            sensorProfile = profile;
            statusLabel.Text = $"Sensor profile: {profile}";
            UpdateStatusBar();
        }

        void RunPreFlightValidation()
        {
            if (project == null)
            {
                MessageBox.Show("No active project.", "Pre-Flight Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int frames = project.Instances.Count(i => i.Type == PartType.Frame);
            int motors = project.Instances.Count(i => i.Type == PartType.Motor);
            bool hasBattery = project.Instances.Any(i => i.Type == PartType.Battery);
            float twr = totalMassKg > 0.01f ? totalThrustN / (totalMassKg * GRAVITY) : 0f;
            float flightMins = totalCurrentA > 0.1f ? (batteryRemainingAh / totalCurrentA) * 60f : 0f;

            var failed = new List<string>();
            var warnings = new List<string>();

            if (frames == 0) failed.Add("Frame missing");
            if (motors < 4) failed.Add("At least 4 motors are required for baseline stability");
            if (!hasBattery) failed.Add("Battery missing");
            if (twr > 0 && twr < 1.8f) failed.Add($"Thrust-to-weight too low ({twr:0.0}:1)");
            if (motorTempC > 120f) failed.Add($"Motor thermal overload risk ({motorTempC:0}C)");
            if (escTempC > 100f) failed.Add($"ESC thermal overload risk ({escTempC:0}C)");

            if (frameStressPct > 100f) warnings.Add($"Frame stress elevated ({frameStressPct:0}%)");
            if (stabilityMarginPct < 45f) warnings.Add($"Low stability margin ({stabilityMarginPct:0}%)");
            if (flightMins > 0 && flightMins < 3.5f) warnings.Add($"Limited endurance ({flightMins:0.0} min)");
            if (waypoints.Count == 0 && simulationMode == SimulationMode.AutonomousMission) warnings.Add("No mission waypoints defined");
            if (faultInjection.MotorFailure || faultInjection.EscThermalCutback || faultInjection.GpsDrop || faultInjection.SensorNoise)
                warnings.Add("Fault injection is active");

            var sb = new StringBuilder();
            sb.AppendLine($"Mode: {simulationMode} | Firmware: {firmwareProfile}");
            sb.AppendLine($"Sensors: {sensorProfile} | Obstacle Detection: {(obstacleAvoidanceEnabled ? "ON" : "OFF")}");
            sb.AppendLine($"Payload: {payloadType} ({payloadMassKg * 1000f:0} g)");
            sb.AppendLine();
            sb.AppendLine($"Hard Failures: {failed.Count}");
            if (failed.Count > 0)
                sb.AppendLine(" - " + string.Join("\n - ", failed));
            sb.AppendLine();
            sb.AppendLine($"Warnings: {warnings.Count}");
            if (warnings.Count > 0)
                sb.AppendLine(" - " + string.Join("\n - ", warnings));
            sb.AppendLine();
            sb.AppendLine(failed.Count == 0 ? "Result: PASS (with warnings if listed)." : "Result: FAIL");

            MessageBox.Show(sb.ToString(), "Pre-Flight Validation", MessageBoxButtons.OK,
                failed.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        void ShowTelemetryDashboard()
        {
            if (telemetry.Count == 0)
            {
                MessageBox.Show("No telemetry captured yet.", "Telemetry Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            float avgCurrent = telemetry.Average(t => t.CurrentA);
            float maxCurrent = telemetry.Max(t => t.CurrentA);
            float avgVoltage = telemetry.Average(t => t.VoltageV);
            float minVoltage = telemetry.Min(t => t.VoltageV);
            float maxTemp = telemetry.Max(t => Math.Max(t.MotorTempC, t.EscTempC));
            float maxAltitude = telemetry.Max(t => t.AltitudeM);
            float maxThrust = telemetry.Max(t => t.ThrustN);
            var first = telemetry[0];
            var last = telemetry[telemetry.Count - 1];

            var sb = new StringBuilder();
            sb.AppendLine($"Samples: {telemetry.Count}");
            sb.AppendLine($"Window: {first.TimeSec:0.0}s -> {last.TimeSec:0.0}s");
            sb.AppendLine($"Avg Current: {avgCurrent:0.0} A | Peak: {maxCurrent:0.0} A");
            sb.AppendLine($"Avg Voltage: {avgVoltage:0.0} V | Min: {minVoltage:0.0} V");
            sb.AppendLine($"Peak Thrust: {maxThrust:0.0} N");
            sb.AppendLine($"Peak Altitude: {maxAltitude:0.00} m");
            sb.AppendLine($"Peak Temp: {maxTemp:0.0} C");
            sb.AppendLine($"Last Mode: {last.Mode}");

            MessageBox.Show(sb.ToString(), "Telemetry Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void SaveBenchmarkSnapshot()
        {
            if (project == null)
            {
                MessageBox.Show("No active project.", "Benchmark", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            float twr = totalMassKg > 0.01f ? totalThrustN / (totalMassKg * GRAVITY) : 0f;
            float flightMins = totalCurrentA > 0.1f ? (batteryRemainingAh / totalCurrentA) * 60f : 0f;
            float avgCurrent = telemetry.Count > 0 ? telemetry.Average(t => t.CurrentA) : totalCurrentA;

            var snap = new BuildBenchmark
            {
                Name = project.Name,
                CapturedAtUtc = DateTime.UtcNow,
                MassKg = totalMassKg,
                ThrustToWeight = twr,
                FlightTimeMin = flightMins,
                AvgCurrentA = avgCurrent,
                StabilityMarginPct = stabilityMarginPct
            };
            benchmarkHistory.Add(snap);

            MessageBox.Show(
                $"Saved benchmark #{benchmarkHistory.Count}\n" +
                $"TWR: {snap.ThrustToWeight:0.00}:1\n" +
                $"Est. Flight: {(snap.FlightTimeMin > 0 ? $"{snap.FlightTimeMin:0.0} min" : "--")}\n" +
                $"Stability: {snap.StabilityMarginPct:0}%",
                "Benchmark",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        void CompareBenchmarks()
        {
            if (benchmarkHistory.Count < 2)
            {
                MessageBox.Show("Save at least two benchmark snapshots first.", "Benchmark Compare", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var previous = benchmarkHistory[benchmarkHistory.Count - 2];
            var current = benchmarkHistory[benchmarkHistory.Count - 1];

            string Delta(float now, float prev, string unit)
            {
                float d = now - prev;
                string sign = d >= 0 ? "+" : "";
                return $"{sign}{d:0.##} {unit}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Previous: {previous.Name} @ {previous.CapturedAtUtc:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine($"Current:  {current.Name} @ {current.CapturedAtUtc:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine();
            sb.AppendLine($"Mass: {current.MassKg:0.###} kg ({Delta(current.MassKg, previous.MassKg, "kg")})");
            sb.AppendLine($"TWR: {current.ThrustToWeight:0.00}:1 ({Delta(current.ThrustToWeight, previous.ThrustToWeight, "")})");
            sb.AppendLine($"Est. Flight: {current.FlightTimeMin:0.0} min ({Delta(current.FlightTimeMin, previous.FlightTimeMin, "min")})");
            sb.AppendLine($"Avg Current: {current.AvgCurrentA:0.0} A ({Delta(current.AvgCurrentA, previous.AvgCurrentA, "A")})");
            sb.AppendLine($"Stability: {current.StabilityMarginPct:0}% ({Delta(current.StabilityMarginPct, previous.StabilityMarginPct, "%")})");

            MessageBox.Show(sb.ToString(), "Benchmark Compare", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void RegisterCrashEvent(float impactVelocityMps)
        {
            if (impactVelocityMps > -1.8f) return;

            crashCount++;
            lastCrashTimeSec = simClock.Elapsed.TotalSeconds;
            lastCrashSummary = $"Crash #{crashCount}: impact {Math.Abs(impactVelocityMps):0.00} m/s at {lastCrashTimeSec:0.0}s, " +
                               $"mode {simulationMode}, payload {payloadType}";
        }

        void ShowCrashReplayAnalysis()
        {
            if (telemetry.Count == 0)
            {
                MessageBox.Show("No telemetry captured yet.", "Crash Replay", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var window = telemetry.Skip(Math.Max(0, telemetry.Count - 120)).ToList();
            var minAltitude = window.Min(t => t.AltitudeM);
            var peakDescent = window.Min(t => t.VerticalVelocityMps);
            var peakCurrent = window.Max(t => t.CurrentA);
            var lowestVoltage = window.Min(t => t.VoltageV);

            var sb = new StringBuilder();
            sb.AppendLine(lastCrashSummary);
            sb.AppendLine($"Replay window: {window.First().TimeSec:0.0}s -> {window.Last().TimeSec:0.0}s");
            sb.AppendLine($"Lowest Altitude: {minAltitude:0.00} m");
            sb.AppendLine($"Peak Descent: {peakDescent:0.00} m/s");
            sb.AppendLine($"Peak Current: {peakCurrent:0.0} A");
            sb.AppendLine($"Lowest Voltage: {lowestVoltage:0.0} V");
            sb.AppendLine($"Faults: M:{faultInjection.MotorFailure} S:{faultInjection.SensorNoise} G:{faultInjection.GpsDrop} E:{faultInjection.EscThermalCutback}");

            MessageBox.Show(sb.ToString(), "Crash Replay Analysis", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void AppendTelemetrySample()
        {
            telemetry.Add(new TelemetrySample
            {
                TimeSec = simClock.Elapsed.TotalSeconds,
                AltitudeM = altitude,
                VerticalVelocityMps = verticalVelocity,
                MassKg = totalMassKg,
                ThrustN = totalThrustN,
                CurrentA = totalCurrentA,
                VoltageV = batteryVoltage,
                MotorTempC = motorTempC,
                EscTempC = escTempC,
                Mode = simulationMode.ToString()
            });

            if (telemetry.Count > 6000)
                telemetry.RemoveRange(0, telemetry.Count - 6000);
        }

        void ExportTelemetryCsv()
        {
            if (telemetry.Count == 0)
            {
                MessageBox.Show("No telemetry captured yet.", "Export Telemetry", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"{(project?.Name ?? "silvu")}_telemetry.csv".Replace(" ", "_")
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            var sb = new StringBuilder();
            sb.AppendLine("time_s,altitude_m,velocity_mps,mass_kg,thrust_n,current_a,voltage_v,motor_temp_c,esc_temp_c,mode");
            foreach (var t in telemetry)
            {
                sb.AppendLine(
                    $"{t.TimeSec:0.###},{t.AltitudeM:0.###},{t.VerticalVelocityMps:0.###},{t.MassKg:0.###}," +
                    $"{t.ThrustN:0.###},{t.CurrentA:0.###},{t.VoltageV:0.###},{t.MotorTempC:0.###},{t.EscTempC:0.###},{t.Mode}");
            }
            File.WriteAllText(sfd.FileName, sb.ToString());
        }

        void ExportTelemetryJson()
        {
            if (telemetry.Count == 0)
            {
                MessageBox.Show("No telemetry captured yet.", "Export Telemetry", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "JSON (*.json)|*.json",
                FileName = $"{(project?.Name ?? "silvu")}_telemetry.json".Replace(" ", "_")
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(telemetry, new JsonSerializerOptions { WriteIndented = true }));
        }

        string GetEducationHint()
        {
            return "Tip: Hover stability depends on thrust-to-weight, battery sag, and motor thermal headroom. " +
                   "Use Simulation > Environment and Payload to see engineering tradeoffs live.";
        }

        void LoadPluginsAtStartup()
        {
            try
            {
                string pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
                if (!Directory.Exists(pluginDir))
                    Directory.CreateDirectory(pluginDir);
                loadedPlugins.Clear();
                loadedPlugins.AddRange(PluginLoader.LoadPlugins(this, pluginDir));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public void ReportPluginMessage(string message)
        {
            Logger.Log(message);
            if (statusLabel != null && !IsDisposed)
                statusLabel.Text = message;
        }

        void LayoutRootPanels()
        {
            if (contentHost == null || contentHost.IsDisposed) return;
            if (menu == null || menu.IsDisposed) return;
            if (statusStrip == null || statusStrip.IsDisposed) return;

            int top = menu.Bottom + 2;
            int bottom = statusStrip.Height;
            int width = Math.Max(0, ClientSize.Width);
            int height = Math.Max(0, ClientSize.Height - top - bottom);
            contentHost.SetBounds(0, top, width, height);
        }

        void ConfigureNavigationSplit()
        {
            if (navigationSplit == null || navigationSplit.IsDisposed) return;

            int total = navigationSplit.Orientation == Orientation.Horizontal
                ? navigationSplit.Height
                : navigationSplit.Width;

            if (total <= navigationSplit.SplitterWidth + 24) return;

            int safeMin = Math.Max(40, Math.Min(220, (total - navigationSplit.SplitterWidth) / 2 - 8));
            if (safeMin < 20) safeMin = 20;

            navigationSplit.Panel1MinSize = safeMin;
            navigationSplit.Panel2MinSize = safeMin;

            int maxDistance = total - navigationSplit.Panel2MinSize - navigationSplit.SplitterWidth;
            int minDistance = navigationSplit.Panel1MinSize;
            if (maxDistance < minDistance) return;

            int desired = Math.Clamp(300, minDistance, maxDistance);
            if (navigationSplit.SplitterDistance != desired)
                navigationSplit.SplitterDistance = desired;
        }

        Image? LoadBrandLogo()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "assets", "silvu-logo.png"),
                Path.Combine(baseDir, "silvu-logo.png"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "assets", "silvu-logo.png"))
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                        return Image.FromFile(candidate);
                }
                catch
                {
                    // skip broken image path
                }
            }

            return null;
        }

        void ApplyTheme()
        {
            var palette = CurrentPalette;

            BackColor = palette.WindowBackground;
            ForeColor = palette.TextPrimary;

            if (menu != null)
            {
                menu.BackColor = palette.Surface;
                menu.ForeColor = palette.TextPrimary;
                menu.Renderer = toolStripRenderer;
                ApplyToolStripTheme(menu.Items, palette);
            }

            if (statusStrip != null)
            {
                statusStrip.BackColor = palette.Surface;
                statusStrip.ForeColor = palette.TextPrimary;
                statusStrip.Renderer = toolStripRenderer;
                foreach (ToolStripItem item in statusStrip.Items)
                {
                    item.BackColor = palette.Surface;
                    item.ForeColor = palette.TextPrimary;
                }
            }

            if (partToolTip != null)
            {
                partToolTip.BackColor = palette.Surface;
                partToolTip.ForeColor = palette.TextPrimary;
            }

            ApplyThemeToControlTree(this, palette);

            if (projectContextMenu != null)
                ApplyContextMenuTheme(projectContextMenu);
            if (libraryContextMenu != null)
                ApplyContextMenuTheme(libraryContextMenu);

            viewport?.Invalidate();
            Invalidate(true);
        }

        void ApplyThemeToControlTree(Control root, UiPalette palette)
        {
            foreach (Control child in root.Controls)
            {
                if (child is RoundedPanel card)
                {
                    card.FillColor = palette.CardBackground;
                    card.BorderColor = palette.Border;
                }
                else if (child is TreeView tree)
                {
                    tree.BackColor = palette.SurfaceAlt;
                    tree.ForeColor = palette.TextPrimary;
                    tree.LineColor = palette.Border;
                }
                else if (child is Label label)
                {
                    bool isSectionTitle = label.Text == label.Text.ToUpperInvariant();
                    label.BackColor = Color.Transparent;
                    label.ForeColor = isSectionTitle ? palette.TextMuted : palette.TextPrimary;
                }
                else if (child is ListBox listBox)
                {
                    listBox.BackColor = palette.SurfaceAlt;
                    listBox.ForeColor = palette.TextPrimary;
                }
                else if (child is Button button)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = palette.Border;
                    button.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point);

                    if (button.Name == "btnExportConfig")
                    {
                        button.BackColor = Color.FromArgb(236, 144, 48);
                        button.ForeColor = Color.White;
                        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(222, 132, 39);
                        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(209, 119, 30);
                    }
                    else if (button.Name == "btnRunSimulation")
                    {
                        button.BackColor = Color.FromArgb(225, 234, 249);
                        button.ForeColor = palette.TextPrimary;
                        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(214, 226, 245);
                        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(202, 218, 240);
                    }
                    else
                    {
                        button.BackColor = palette.Accent;
                        button.ForeColor = Color.White;
                        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 126, 231);
                        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(27, 104, 208);
                    }
                }
                else if (child is SplitContainer split)
                {
                    split.BackColor = palette.WindowBackground;
                    split.Panel1.BackColor = palette.WindowBackground;
                    split.Panel2.BackColor = palette.WindowBackground;
                }
                else if (child is Panel panel)
                {
                    panel.BackColor = palette.WindowBackground;
                }
            }

            foreach (Control child in root.Controls)
                ApplyThemeToControlTree(child, palette);

            if (viewport != null)
                viewport.BackColor = palette.ViewportBackground;
        }

        void ApplyToolStripTheme(ToolStripItemCollection items, UiPalette palette)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripSeparator) continue;

                item.BackColor = palette.Surface;
                item.ForeColor = item is ToolStripLabel lbl && (lbl.Text?.Contains("Component-Level") ?? false)
                    ? palette.TextMuted
                    : palette.TextPrimary;

                if (item is ToolStripDropDownItem dd)
                {
                    dd.DropDown.BackColor = palette.Surface;
                    dd.DropDown.ForeColor = palette.TextPrimary;
                    ApplyToolStripTheme(dd.DropDownItems, palette);
                }
            }
        }

        void ApplyContextMenuTheme(ContextMenuStrip contextMenu)
        {
            var palette = CurrentPalette;
            contextMenu.BackColor = palette.Surface;
            contextMenu.ForeColor = palette.TextPrimary;
            contextMenu.Renderer = toolStripRenderer;
            ApplyToolStripTheme(contextMenu.Items, palette);
        }

        void UpdatePhysics(float dt)
        {
            if (project == null) return;

            dt = Math.Clamp(dt, 0.001f, 0.05f);
            telemetryDecimator++;

            totalMassKg = 0;
            totalCurrentA = 0;
            totalThrustN = 0;

            int motorCount = 0;
            int cwMotors = 0;
            int ccwMotors = 0;
            float batteryMaxDischargeC = 75f;
            BatteryChemistry activeChemistry = BatteryChemistry.LiPo;
            float frameDensity = 1.6f;
            float frameCgOffsetCm = 0f;

            // ===== CONTROL TARGETING =====
            if (simulationMode == SimulationMode.AutonomousMission && waypoints.Count > 0)
            {
                int index = (int)((simClock.Elapsed.TotalSeconds * 0.3) % waypoints.Count);
                targetAltitude = waypoints[index].AltitudeM;
            }
            else if (simulationMode == SimulationMode.HeavyLift)
            {
                targetAltitude = 1.4f;
            }
            else if (simulationMode == SimulationMode.EmergencyFailure)
            {
                targetAltitude = 0.7f;
            }

            float sensorNoiseBias = sensorProfile switch
            {
                SensorProfile.SurveyGrade => 0.35f,
                SensorProfile.Degraded => 2.2f,
                _ => 1.0f
            };

            float error = targetAltitude - altitude;
            if (faultInjection.SensorNoise || sensorProfile == SensorProfile.Degraded)
                error += ((float)random.NextDouble() * 2f - 1f) * 0.03f * sensorNoiseBias;

            float throttle = PID(error, dt);
            throttle = Math.Clamp(throttle, 0.0f, 1.0f);

            float firmwareControlGain = firmwareProfile switch
            {
                FirmwareProfile.Betaflight => 1.05f,
                FirmwareProfile.ArduPilot => 1.00f,
                FirmwareProfile.PX4 => 0.98f,
                _ => 1f
            };
            throttle = Math.Clamp(throttle * firmwareControlGain, 0f, 1f);

            foreach (var p in project.Instances)
            {
                if (p.Type == PartType.Motor)
                {
                    motorCount++;
                    if ((p.MountIndex & 1) == 0) cwMotors++; else ccwMotors++;

                    var motorAsset = AssetLibrary.Get(p.AssetId) as MotorAsset;
                    var motorName = motorAsset?.Name ?? p.AssetId;
                    totalMassKg += motorAsset?.MassKg > 0 ? motorAsset.MassKg : PhysicsDatabase.MotorMass(motorName);

                    float maxRpm = motorAsset?.MaxRPM > 0 ? motorAsset.MaxRPM : PhysicsDatabase.MaxRPM(motorName);
                    float maxCurrent = motorAsset?.MaxCurrent > 0 ? motorAsset.MaxCurrent : PhysicsDatabase.MaxCurrent(motorName);
                    float rpm = throttle * maxRpm;
                    totalThrustN += ThrustFromRPM(rpm);
                    totalCurrentA += throttle * maxCurrent;
                }
                else if (p.Type == PartType.Frame)
                {
                    var frameAsset = AssetLibrary.Get(p.AssetId) as FrameAsset;
                    totalMassKg += frameAsset?.MassKg > 0 ? frameAsset.MassKg : PhysicsDatabase.FrameMass();
                    if (frameAsset != null)
                    {
                        frameDensity = frameAsset.MaterialDensity;
                        frameCgOffsetCm = MathF.Sqrt(frameAsset.CgOffsetXcm * frameAsset.CgOffsetXcm + frameAsset.CgOffsetYcm * frameAsset.CgOffsetYcm);
                    }
                }
                else if (p.Type == PartType.Battery)
                {
                    var batteryAsset = AssetLibrary.Get(p.AssetId) as BatteryAsset;
                    if (batteryAsset != null)
                    {
                        totalMassKg += batteryAsset.MassKg > 0 ? batteryAsset.MassKg : PhysicsDatabase.BatteryMass();
                        if (batteryAsset.CapacityAh > 0) batteryCapacityAh = batteryAsset.CapacityAh;
                        if (batteryAsset.VoltageNominal > 0) batteryVoltageNominal = batteryAsset.VoltageNominal;
                        if (batteryAsset.MaxDischargeC > 0) batteryMaxDischargeC = batteryAsset.MaxDischargeC;
                        activeChemistry = batteryAsset.Chemistry;
                    }
                    else
                    {
                        totalMassKg += PhysicsDatabase.BatteryMass();
                    }
                }
            }

            totalMassKg += payloadMassKg;
            if (motorCount == 0) return;

            yawImbalancePct = motorCount > 1
                ? Math.Abs(cwMotors - ccwMotors) * 100f / motorCount
                : 100f;

            float modeThrustMultiplier = simulationMode switch
            {
                SimulationMode.VtolHybrid => 0.92f,
                SimulationMode.HeavyLift => 0.86f,
                SimulationMode.Swarm => 0.95f,
                _ => 1f
            };

            float effectiveThrust = totalThrustN * modeThrustMultiplier;
            effectiveThrust *= Math.Clamp(1f - yawImbalancePct * 0.0025f, 0.70f, 1f);

            if (faultInjection.MotorFailure || simulationMode == SimulationMode.EmergencyFailure)
                effectiveThrust *= 0.72f;

            if (obstacleAvoidanceEnabled && simulationMode == SimulationMode.AutonomousMission)
                totalCurrentA *= 1.05f;

            if (faultInjection.GpsDrop && simulationMode == SimulationMode.AutonomousMission)
            {
                targetAltitude += ((float)random.NextDouble() * 2f - 1f) * 0.08f;
                targetAltitude = Math.Clamp(targetAltitude, 0.3f, 3.0f);
            }

            float windPenalty = Math.Clamp(1f - environmentModel.WindSpeedMps * 0.02f, 0.65f, 1f);
            float turbulenceNoise = ((float)random.NextDouble() * 2f - 1f) * environmentModel.TurbulenceStrength * 0.06f;
            effectiveThrust *= Math.Clamp(windPenalty + turbulenceNoise, 0.55f, 1.08f);

            if (environmentModel.EnableGroundEffect && altitude < 0.7f)
            {
                float boost = 1f + ((0.7f - altitude) / 0.7f) * 0.14f;
                effectiveThrust *= boost;
            }

            // ===== VERTICAL DYNAMICS =====
            float weight = Math.Max(0.1f, totalMassKg * GRAVITY);
            float dragForce = environmentModel.DragCoefficient * verticalVelocity * Math.Abs(verticalVelocity);
            float netForce = effectiveThrust - weight - dragForce;
            float acceleration = netForce / Math.Max(0.1f, totalMassKg);
            float previousAltitude = altitude;

            verticalVelocity += acceleration * dt;
            altitude += verticalVelocity * dt;

            if (altitude < 0)
            {
                RegisterCrashEvent(verticalVelocity);
                altitude = 0;
                verticalVelocity = 0;
            }

            float chemistrySagCoeff = activeChemistry switch
            {
                BatteryChemistry.LiIon => 0.028f,
                BatteryChemistry.LiHV => 0.016f,
                _ => 0.020f
            };

            if (faultInjection.EscThermalCutback)
            {
                float derate = Math.Clamp(1f - ((escTempC - 70f) / 90f), 0.5f, 1f);
                totalCurrentA *= derate;
                effectiveThrust *= Math.Clamp(derate + 0.08f, 0.55f, 1f);
            }

            if (faultInjection.SensorNoise)
                altitude += ((float)random.NextDouble() * 2f - 1f) * 0.005f * sensorNoiseBias;

            // ===== BATTERY MODEL =====
            batteryRemainingAh -= (totalCurrentA * dt) / 3600f;
            batteryRemainingAh = Math.Clamp(batteryRemainingAh, 0, Math.Max(0.1f, batteryCapacityAh));

            float sag = chemistrySagCoeff * totalCurrentA;
            float maxDischargeA = batteryCapacityAh * Math.Max(1f, batteryMaxDischargeC);
            if (maxDischargeA > 0f && totalCurrentA > maxDischargeA)
                sag += (totalCurrentA - maxDischargeA) * 0.015f;
            batteryVoltage = Math.Max(0, batteryVoltageNominal - sag);

            motorTempC += (totalCurrentA * 0.12f + throttle * 8f) * dt;
            motorTempC -= (motorTempC - 28f) * dt * 0.04f;
            escTempC += (totalCurrentA * 0.08f + (faultInjection.EscThermalCutback ? 4f : 0f)) * dt;
            escTempC -= (escTempC - 27f) * dt * 0.05f;
            motorTempC = Math.Clamp(motorTempC, 20f, 180f);
            escTempC = Math.Clamp(escTempC, 20f, 160f);

            if (motorTempC > 120f)
                effectiveThrust *= Math.Clamp(1f - (motorTempC - 120f) * 0.004f, 0.72f, 1f);

            imuVibrationPct = Math.Clamp(
                environmentModel.TurbulenceStrength * 95f +
                yawImbalancePct * 0.65f +
                Math.Max(0f, motorTempC - 70f) * 0.20f, 0f, 100f);

            frameStressPct = Math.Clamp(
                (effectiveThrust / Math.Max(0.1f, weight)) * 52f +
                Math.Abs(payloadOffsetCm) * 3.8f +
                frameCgOffsetCm * 2.1f +
                frameDensity * 5.0f +
                environmentModel.WindSpeedMps * 1.8f, 0f, 180f);

            stabilityMarginPct = Math.Clamp(
                100f -
                environmentModel.WindSpeedMps * 4.8f -
                environmentModel.TurbulenceStrength * 46f -
                payloadOffsetCm * 2.2f -
                frameCgOffsetCm * 1.7f -
                yawImbalancePct * 0.55f -
                (sensorProfile == SensorProfile.Degraded ? 12f : 0f) -
                (faultInjection.GpsDrop ? 10f : 0f), 4f, 100f);

            escFailureRiskPct = Math.Clamp(
                Math.Max(0f, escTempC - 68f) * 1.15f +
                Math.Max(0f, totalCurrentA - 55f) * 0.65f +
                (faultInjection.EscThermalCutback ? 12f : 0f), 0f, 100f);

            if (previousAltitude > 0.15f && altitude == 0f && verticalVelocity == 0f)
                escFailureRiskPct = Math.Clamp(escFailureRiskPct + 7f, 0f, 100f);

            totalThrustN = effectiveThrust;

            if (telemetryDecimator >= 6)
            {
                telemetryDecimator = 0;
                AppendTelemetrySample();
            }
        }


        // ================= PROJECT =================
        void NewProject()
        {
            if (!ConfirmProceedWithDirtyProject()) return;

            project = new Project { Name = "New Drone Project" };
            projectPath = null;
            dirty = false;
            waypoints.Clear();

            ResetPhysicsState();

            RefreshTree();
            UpdateStatusBar();
            viewport.Invalidate();
        }

        void SaveProject()
        {
            if (project == null) return;

            if (projectPath == null)
            {
                var sfd = new SaveFileDialog { Filter = "SILVU Project|*.svproj" };
                if (sfd.ShowDialog() != DialogResult.OK) return;
                projectPath = sfd.FileName;
            }

            File.WriteAllText(projectPath,
                JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));

            dirty = false;
            UpdateTitle();
        }

        void OpenProject()
        {
            if (!ConfirmProceedWithDirtyProject()) return;

            var ofd = new OpenFileDialog { Filter = "SILVU Project|*.svproj" };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            project = JsonSerializer.Deserialize<Project>(File.ReadAllText(ofd.FileName));
            if (project == null)
            {
                MessageBox.Show("The selected project file is invalid.", "Open Project", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            projectPath = ofd.FileName;
            dirty = false;
            waypoints.Clear();
            ResetPhysicsState();
            RefreshTree();
            UpdateStatusBar();
            viewport.Invalidate();
        }

        // ================= CORE =================
        void AddPart(string cat, string name, int x, int y)
        {
            var pt = new PointF(x, y);
            bool added = false;

            if (cat == "Motors")
            {
                added = AddMotor(pt, name);
            }
            else if (cat == "Batteries")
            {
                added = AddBattery(pt, name);
            }
            else if (cat == "Frames")
            {
                project!.Instances.Clear();
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                    Type = PartType.Frame,
                    Position = pt
                });
                added = true;
            }

            if (added)
                OnProjectStructureChanged();
        }

        void RefreshTree()
        {
            projectTree.Nodes.Clear();
            if (project == null) return;

            var root = new TreeNode(project.Name);
            var map = new Dictionary<string, TreeNode>();

            foreach (var p in project.Instances)
            {
                string group = p.Type.ToString();

                if (!map.ContainsKey(group))
                {
                    map[group] = new TreeNode(group);
                    root.Nodes.Add(map[group]);
                }

                var asset = AssetLibrary.Get(p.AssetId);
                var display = asset?.Name ?? p.AssetId;
                map[group].Nodes.Add(new TreeNode(display) { Tag = p });
            }

            // append counts to group headers for clarity
            foreach (var kv in map)
            {
                kv.Value.Text = $"{kv.Key} ({kv.Value.Nodes.Count})";
            }

            root.ExpandAll();
            projectTree.Nodes.Add(root);
            UpdateTitle();
        }

        // ================= VIEWPORT =================
        void DrawViewport(object? s, PaintEventArgs e)
{
    var g = e.Graphics;
    g.SmoothingMode = SmoothingMode.AntiAlias;
    var palette = CurrentPalette;

    using (var bg = new LinearGradientBrush(viewport.ClientRectangle, palette.ViewportBackground, palette.SurfaceAlt, LinearGradientMode.Vertical))
        g.FillRectangle(bg, viewport.ClientRectangle);

    using (var borderPen = new Pen(Color.FromArgb(120, palette.Border)))
        g.DrawRectangle(borderPen, 0, 0, Math.Max(0, viewport.Width - 1), Math.Max(0, viewport.Height - 1));

    if (project == null)
    {
        var title = "Start a project to begin building";
        var subtitle = "Add a frame, then place motors and a battery.";
        var titleSize = g.MeasureString(title, Font);
        var subtitleSize = g.MeasureString(subtitle, Font);
        var x = (viewport.Width - Math.Max(titleSize.Width, subtitleSize.Width)) / 2f;
        var y = (viewport.Height - (titleSize.Height + subtitleSize.Height + 8f)) / 2f;
        using var titleBrush = new SolidBrush(palette.TextPrimary);
        using var subtitleBrush = new SolidBrush(palette.TextMuted);
        g.DrawString(title, Font, titleBrush, x, y);
        g.DrawString(subtitle, Font, subtitleBrush, x, y + titleSize.Height + 8f);
        return;
    }

    // keep physics ticking
    UpdatePhysics(0.016f);

    var frame = GetFrame();
    if (frame != null)
        DrawFrame(g, frame);

    if (frame != null)
    {
        DrawMissionWaypoints(g, frame);
        DrawPayloadOverlay(g, frame);
    }

    foreach (var p in project.Instances)
    {
        if (p.Type == PartType.Motor)
            DrawMotor(g, p);
        if (p.Type == PartType.Battery)
            DrawBattery(g, p);
    }

    // show pending add hint
    if (pendingAddMode != null)
    {
        var hint = pendingAddMode == PartType.Motor ? (pendingAddName ?? "Motor") : (pendingAddName ?? "Battery");
        var txt = $"Click to place {hint}";
        var size = g.MeasureString(txt, Font);
        var pos = new PointF(mousePos.X + 12, mousePos.Y + 12);
        var hintRect = new RectangleF(pos.X - 8, pos.Y - 4, size.Width + 16, size.Height + 8);
        using var hintPath = BuildRoundedPath(hintRect, 8f);
        using var b = new SolidBrush(Color.FromArgb(224, palette.Surface));
        using var p = new Pen(palette.Border);
        using var t = new SolidBrush(palette.TextPrimary);
        g.FillPath(b, hintPath);
        g.DrawPath(p, hintPath);
        g.DrawString(txt, Font, t, pos);
    }

    DrawPhysicsHUD(g);
    DrawTelemetryMiniCharts(g);
}

void DrawFrame(Graphics g, PlacedInstance frame)
{
    var def = FrameDB.XFrame;
    var palette = CurrentPalette;

    using (var framePen = new Pen(palette.Accent, 2.4f))
    {
        g.DrawEllipse(framePen,
            frame.Position.X - def.Size.Width / 2,
            frame.Position.Y - def.Size.Height / 2,
            def.Size.Width,
            def.Size.Height);
    }

    if (frameStressPct > 95f)
    {
        float alpha = Math.Clamp((frameStressPct - 90f) * 2f, 40f, 180f);
        using var stressPen = new Pen(Color.FromArgb((int)alpha, 208, 92, 64), 2.4f);
        g.DrawEllipse(
            stressPen,
            frame.Position.X - def.Size.Width / 2 - 6f,
            frame.Position.Y - def.Size.Height / 2 - 6f,
            def.Size.Width + 12f,
            def.Size.Height + 12f);
    }

    // determine nearest mount when placing motors
    int nearest = -1;
    if (pendingAddMode == PartType.Motor)
        nearest = FindNearestMount(mousePos);

    for (int i = 0; i < def.MotorMounts.Length; i++)
    {
        var m = def.MotorMounts[i];
        var world = new PointF(frame.Position.X + m.X, frame.Position.Y + m.Y);

        using var mountBrush = new SolidBrush(palette.SurfaceAlt);
        using var mountPen = new Pen(palette.Border, 1.4f);
        g.FillEllipse(mountBrush, world.X - 10, world.Y - 10, 20, 20);
        g.DrawEllipse(mountPen, world.X - 10, world.Y - 10, 20, 20);

        // highlight candidate
        if (i == nearest)
        {
            using var highlightPen = new Pen(palette.Accent, 2f);
            g.DrawEllipse(highlightPen, world.X - 14, world.Y - 14, 28, 28);
        }
    }

    var bay = def.BatteryBay;
    var worldBay = new RectangleF(
        frame.Position.X + bay.X,
        frame.Position.Y + bay.Y,
        bay.Width,
        bay.Height);

    using (var bayPath = BuildRoundedPath(worldBay, 6f))
    using (var pen = new Pen(palette.Warning, 2f))
        g.DrawPath(pen, bayPath);

    if (pendingAddMode == PartType.Battery)
    {
        if (worldBay.Contains(mousePos))
        {
            using var bayPath = BuildRoundedPath(worldBay, 6f);
            using var brush = new SolidBrush(Color.FromArgb(70, palette.Accent));
            using var pen = new Pen(palette.Accent, 2f);
            g.FillPath(brush, bayPath);
            g.DrawPath(pen, bayPath);
        }
    }
}

void DrawMotor(Graphics g, PlacedInstance motor)
{
    if (motor.MountIndex < 0 || motor.MountIndex >= FrameDB.XFrame.MotorMounts.Length) return;

    var frame = GetFrame();
    if (frame == null) return;

    var mount = FrameDB.XFrame.MotorMounts[motor.MountIndex];
    var palette = CurrentPalette;

    var pos = new PointF(
        frame.Position.X + mount.X,
        frame.Position.Y + mount.Y
    );

    var outer = new RectangleF(pos.X - 13, pos.Y - 13, 26, 26);
    var inner = new RectangleF(pos.X - 9, pos.Y - 9, 18, 18);

    using var outerBrush = new SolidBrush(Color.FromArgb(220, 98, 122, 160));
    using var outerPen = new Pen(palette.Border);
    using var innerBrush = new SolidBrush(palette.Accent);
    g.FillEllipse(outerBrush, outer);
    g.DrawEllipse(outerPen, outer);
    g.FillEllipse(innerBrush, inner);

    if (selected == motor)
    {
        using var selectedPen = new Pen(palette.Accent, 2f);
        g.DrawEllipse(selectedPen, pos.X - 17, pos.Y - 17, 34, 34);
    }
}

void DrawBattery(Graphics g, PlacedInstance battery)
{
    var palette = CurrentPalette;
    var rect = new RectangleF(
        battery.Position.X - 34,
        battery.Position.Y - 18,
        68,
        36
    );

    using var bodyPath = BuildRoundedPath(rect, 10f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(198, 90, 132, 255));
    using var bodyPen = new Pen(palette.Border, 1.3f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);

    var cap = new RectangleF(rect.Right - 5, rect.Y + 11, 7, 14);
    using var capBrush = new SolidBrush(Color.FromArgb(190, palette.SurfaceAlt));
    g.FillRectangle(capBrush, cap.X, cap.Y, cap.Width, cap.Height);

    if (selected == battery)
    {
        using var selectedPen = new Pen(palette.Accent, 2f);
        using var selectedPath = BuildRoundedPath(new RectangleF(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), 12f);
        g.DrawPath(selectedPen, selectedPath);
    }
}

void DrawMissionWaypoints(Graphics g, PlacedInstance frame)
{
    if (waypoints.Count == 0) return;

    var palette = CurrentPalette;
    using var linePen = new Pen(Color.FromArgb(155, palette.Accent), 1.7f)
    {
        DashStyle = DashStyle.Dash
    };

    PointF? previous = null;
    for (int i = 0; i < waypoints.Count; i++)
    {
        var wp = waypoints[i];
        var point = new PointF(frame.Position.X + wp.X, frame.Position.Y + wp.Y);
        var nodeRect = new RectangleF(point.X - 7, point.Y - 7, 14, 14);

        using var fill = new SolidBrush(Color.FromArgb(190, 58, 139, 255));
        using var border = new Pen(palette.Accent, 1.5f);
        g.FillEllipse(fill, nodeRect);
        g.DrawEllipse(border, nodeRect);
        g.DrawString((i + 1).ToString(), Font, Brushes.White, point.X + 9, point.Y - 8);

        if (previous != null)
            g.DrawLine(linePen, previous.Value, point);
        previous = point;
    }
}

void DrawPayloadOverlay(Graphics g, PlacedInstance frame)
{
    if (payloadType == PayloadType.None || payloadMassKg <= 0f) return;

    var palette = CurrentPalette;
    float width = 44f;
    float height = 24f;
    var center = new PointF(frame.Position.X + payloadOffsetCm * 2.4f, frame.Position.Y + 46f);
    var rect = new RectangleF(center.X - width / 2f, center.Y - height / 2f, width, height);
    using var path = BuildRoundedPath(rect, 8f);
    using var fill = new SolidBrush(Color.FromArgb(210, 255, 165, 87));
    using var border = new Pen(Color.FromArgb(220, 204, 111, 42), 1.2f);
    using var text = new SolidBrush(Color.FromArgb(50, 32, 18));

    g.FillPath(fill, path);
    g.DrawPath(border, path);
    g.DrawString($"{payloadType}", Font, text, rect.X - 4, rect.Bottom + 3);
}

void DrawTelemetryMiniCharts(Graphics g)
{
    if (telemetry.Count < 10) return;

    var palette = CurrentPalette;
    int chartCount = 3;
    float chartWidth = 180f;
    float chartHeight = 48f;
    float gap = 8f;
    float panelPadding = 10f;
    float panelWidth = chartWidth + panelPadding * 2f;
    float panelHeight = panelPadding * 2f + chartCount * chartHeight + (chartCount - 1) * gap + 24f;
    float x = viewport.Width - panelWidth - 12f;
    float y = 12f;
    var panelRect = new RectangleF(x, y, panelWidth, panelHeight);

    using var panelPath = BuildRoundedPath(panelRect, 12f);
    using var panelFill = new SolidBrush(Color.FromArgb(220, palette.HudBackground));
    using var panelBorder = new Pen(palette.Border, 1f);
    using var panelText = new SolidBrush(palette.HudText);
    g.FillPath(panelFill, panelPath);
    g.DrawPath(panelBorder, panelPath);
    g.DrawString("Telemetry", Font, panelText, x + 10f, y + 6f);

    var window = telemetry.Skip(Math.Max(0, telemetry.Count - 120)).ToList();

    var thrustRect = new RectangleF(x + panelPadding, y + 26f, chartWidth, chartHeight);
    var currentRect = new RectangleF(x + panelPadding, thrustRect.Bottom + gap, chartWidth, chartHeight);
    var voltageRect = new RectangleF(x + panelPadding, currentRect.Bottom + gap, chartWidth, chartHeight);

    DrawSparkline(g, thrustRect, window.Select(t => t.ThrustN).ToList(), Color.FromArgb(236, 133, 55), "Thrust");
    DrawSparkline(g, currentRect, window.Select(t => t.CurrentA).ToList(), Color.FromArgb(45, 132, 238), "Current");
    DrawSparkline(g, voltageRect, window.Select(t => t.VoltageV).ToList(), Color.FromArgb(221, 88, 88), "Voltage");
}

void DrawSparkline(Graphics g, RectangleF rect, List<float> values, Color lineColor, string label)
{
    var palette = CurrentPalette;
    using var bg = new SolidBrush(Color.FromArgb(85, palette.SurfaceAlt));
    using var border = new Pen(Color.FromArgb(130, palette.Border), 1f);
    using var linePen = new Pen(lineColor, 1.9f);
    using var labelBrush = new SolidBrush(palette.HudText);

    g.FillRectangle(bg, rect.X, rect.Y, rect.Width, rect.Height);
    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
    g.DrawString(label, Font, labelBrush, rect.X + 4f, rect.Y + 2f);

    if (values.Count < 2) return;
    float min = values.Min();
    float max = values.Max();
    float range = Math.Max(0.0001f, max - min);
    float usableX = rect.Width - 8f;
    float usableY = rect.Height - 16f;
    float originX = rect.X + 4f;
    float originY = rect.Y + 12f;

    var points = new PointF[values.Count];
    for (int i = 0; i < values.Count; i++)
    {
        float tx = values.Count > 1 ? (float)i / (values.Count - 1) : 0f;
        float ty = (values[i] - min) / range;
        points[i] = new PointF(originX + tx * usableX, originY + (1f - ty) * usableY);
    }

    if (points.Length >= 2)
        g.DrawLines(linePen, points);
}

void DrawPhysicsHUD(Graphics g)
{
    var palette = CurrentPalette;
    float batteryPercent = batteryCapacityAh > 0 ? (batteryRemainingAh / batteryCapacityAh) * 100f : 0f;
    var lines = new[]
    {
        $"Mode: {simulationMode} / {firmwareProfile}",
        $"Sensors: {sensorProfile}",
        $"Altitude: {altitude:F2} m",
        $"Mass: {totalMassKg:F2} kg",
        $"Thrust: {totalThrustN:F1} N",
        $"Current: {totalCurrentA:F1} A",
        $"Voltage: {batteryVoltage:F1} V",
        $"Battery: {batteryPercent:0}%",
        $"Temps M/ESC: {motorTempC:0}/{escTempC:0} C",
        $"Stress/Stability: {frameStressPct:0}%/{stabilityMarginPct:0}%",
        $"Waypoints: {waypoints.Count} | Payload: {payloadType}",
        $"ESC Risk: {escFailureRiskPct:0}% | IMU Vib: {imuVibrationPct:0}%"
    };

    float lineHeight = Font.Height + 2;
    float panelWidth = 300;
    float panelHeight = 16 + (lineHeight * (lines.Length + 1)) + 8;
    var panelRect = new RectangleF(12, 12, panelWidth, panelHeight);

    using var panelPath = BuildRoundedPath(panelRect, 12f);
    using var panelBrush = new SolidBrush(Color.FromArgb(220, palette.HudBackground));
    using var panelPen = new Pen(palette.Border, 1f);
    using var textBrush = new SolidBrush(palette.HudText);
    g.FillPath(panelBrush, panelPath);
    g.DrawPath(panelPen, panelPath);

    float y = panelRect.Y + 10;
    float x = panelRect.X + 12;
    foreach (var line in lines)
    {
        g.DrawString(line, Font, textBrush, x, y);
        y += lineHeight;
    }

    bool hoverOk = totalThrustN >= totalMassKg * GRAVITY;
    string hover = hoverOk ? "HOVER OK" : "NO HOVER";
    using var hoverBrush = new SolidBrush(hoverOk ? palette.Success : palette.Warning);
    g.DrawString(hover, Font, hoverBrush, x, y);

    if (educationMode)
    {
        y += lineHeight;
        using var eduBrush = new SolidBrush(Color.FromArgb(180, palette.HudText));
        g.DrawString("Tip: Raise stability by reducing payload offset and turbulence.", Font, eduBrush, x, y);
    }
}

        GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            radius = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
            float diameter = radius * 2f;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }



        void BuildLibrary()
        {
            libraryTree.Nodes.Clear();

            // built-in assets
            AssetLibrary.LoadDefaults();
            // user assets on disk
            AssetLibrary.LoadAll(AssetLibrary.UserAssetRoot);

            var categories = AssetLibrary.Assets.Values.Select(a => a.Category).Distinct().OrderBy(c => c);
            foreach (var cat in categories)
            {
                var parent = new TreeNode(cat);
                foreach (var a in AssetLibrary.GetByCategory(cat))
                {
                    var node = new TreeNode(a.Name) { Tag = a };
                    parent.Nodes.Add(node);
                }
                libraryTree.Nodes.Add(parent);
            }

            libraryTree.ExpandAll();
            ApplyTheme();
        }
        void OnProjectStructureChanged()
{
    ResetPhysicsState();
    dirty = true;
    RefreshTree();
    viewport.Invalidate();
    UpdateStatusBar();
}


        // ================= UX =================
        void ToggleDark()
        {
            darkMode = !darkMode;
            ApplyTheme();
            UpdateStatusBar();
        }

        bool ConfirmProceedWithDirtyProject()
        {
            if (!dirty || project == null) return true;

            var result = MessageBox.Show(
                $"Save changes to '{project.Name}' before continuing?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.No) return true;

            SaveProject();
            return !dirty;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ConfirmProceedWithDirtyProject())
            {
                e.Cancel = true;
                return;
            }

            if (renderTimer != null)
                renderTimer.Stop();

            logoImage?.Dispose();
            logoImage = null;

            base.OnFormClosing(e);
        }

        void UpdateTitle()
        {
            Text = $"SILVU VIEWFINDER — {project?.Name}{(dirty ? " *" : "")}";
        }

        void UpdateStatusBar()
        {
            if (statusLabel == null) return;

            if (project == null)
            {
                statusLabel.Text = "Ready";
                frameStatus.Text = " | Frame: 0";
                motorsStatus.Text = " | Motors: 0";
                batteryStatus.Text = " | Battery: None";
                errorsStatus.Text = " | Errors: 0";
                errorsStatus.ForeColor = CurrentPalette.Success;

                if (twrValueLabel != null) twrValueLabel.Text = "--";
                if (hoverValueLabel != null) hoverValueLabel.Text = "--";
                if (flightValueLabel != null) flightValueLabel.Text = "--";
                if (sagValueLabel != null) sagValueLabel.Text = "--";
                if (tempValueLabel != null) tempValueLabel.Text = "--";
                if (massMetricLabel != null) massMetricLabel.Text = "AUW: --";
                if (powerMetricLabel != null) powerMetricLabel.Text = "Power Draw: --";
                if (enduranceMetricLabel != null) enduranceMetricLabel.Text = "Est. Flight: --";
                if (payloadMetricLabel != null) payloadMetricLabel.Text = "Stress/Stab: --";
                if (warningsList != null)
                {
                    warningsList.Items.Clear();
                    warningsList.Items.Add("Start by placing a frame.");
                }
                return;
            }

            int frames = project.Instances.Count(i => i.Type == PartType.Frame);
            int motors = project.Instances.Count(i => i.Type == PartType.Motor);
            var batteryInst = project.Instances.FirstOrDefault(i => i.Type == PartType.Battery);
            float payloadMass = payloadMassKg;

            string batteryText = "None";
            if (batteryInst != null)
            {
                var batAsset = AssetLibrary.Get(batteryInst.AssetId) as BatteryAsset;
                if (batAsset != null && batAsset.VoltageNominal > 0)
                {
                    int s = batAsset.Cells > 0 ? batAsset.Cells : (int)Math.Round(batAsset.VoltageNominal / 3.7f);
                    batteryText = $"{s}S {batAsset.CapacityAh:0.0}Ah {batAsset.Chemistry}";
                }
                else
                {
                    batteryText = "Unknown";
                }
            }

            int errors = 0;

            // quick checks
            if (frames == 0) errors++;
            if (motors == 0) errors++;
            if (batteryInst == null) errors++;

            // estimate thrust vs weight
            float estMass = 0f;
            float estThrust = 0f;
            float estCurrentAtHover = 0f;
            foreach (var p in project.Instances)
            {
                if (p.Type == PartType.Motor)
                {
                    var motorAsset = AssetLibrary.Get(p.AssetId) as MotorAsset;
                    var motorName = motorAsset?.Name ?? p.AssetId;
                    estMass += motorAsset?.MassKg > 0 ? motorAsset.MassKg : PhysicsDatabase.MotorMass(motorName);
                    float maxr = motorAsset?.MaxRPM > 0 ? motorAsset.MaxRPM : PhysicsDatabase.MaxRPM(motorName);
                    estThrust += ThrustFromRPM(maxr);
                    float maxCurrent = motorAsset?.MaxCurrent > 0 ? motorAsset.MaxCurrent : PhysicsDatabase.MaxCurrent(motorName);
                    estCurrentAtHover += maxCurrent * 0.45f;
                }
                else if (p.Type == PartType.Frame)
                {
                    estMass += PhysicsDatabase.FrameMass();
                }
                else if (p.Type == PartType.Battery)
                {
                    var bat = AssetLibrary.Get(p.AssetId) as BatteryAsset;
                    estMass += bat?.MassKg > 0 ? bat.MassKg : PhysicsDatabase.BatteryMass();
                }
            }

            estMass += payloadMass;
            if (estThrust < estMass * GRAVITY) errors++;

            float liveMass = totalMassKg > 0.01f ? totalMassKg : estMass;
            float liveThrust = totalThrustN > 0.1f ? totalThrustN : estThrust;
            float twr = liveMass > 0 ? liveThrust / (liveMass * GRAVITY) : 0f;
            float hoverPct = liveThrust > 0 ? Math.Clamp((liveMass * GRAVITY / liveThrust) * 100f, 0f, 100f) : 0f;
            float currentForEstimate = totalCurrentA > 0.1f ? totalCurrentA : estCurrentAtHover;
            float flightMins = currentForEstimate > 0.1f ? (batteryRemainingAh / currentForEstimate) * 60f : 0f;
            float sagVolts = Math.Max(0f, batteryVoltageNominal - batteryVoltage);
            int criticalAlerts = 0;

            statusLabel.Text = pendingAddMode == null
                ? $"Mode: {simulationMode} | FW: {firmwareProfile} | Sensors: {sensorProfile}"
                : $"Placing {pendingAddName ?? pendingAddMode.ToString()}";
            frameStatus.Text = $" | Frame: {frames}";
            motorsStatus.Text = $" | Motors: {motors}";
            batteryStatus.Text = $" | Battery: {batteryText}";
            errorsStatus.Text = $" | Errors: {errors}";
            errorsStatus.ForeColor = errors == 0 ? CurrentPalette.Success : CurrentPalette.Warning;

            if (twrValueLabel != null) twrValueLabel.Text = $"{twr:0.0}:1";
            if (hoverValueLabel != null) hoverValueLabel.Text = $"{hoverPct:0}%";
            if (flightValueLabel != null) flightValueLabel.Text = flightMins > 0 ? $"{flightMins:0.0} min" : "--";
            if (sagValueLabel != null) sagValueLabel.Text = $"{sagVolts:0.0} V";
            if (tempValueLabel != null) tempValueLabel.Text = $"{motorTempC:0}C / {escTempC:0}C";

            if (twrValueLabel != null) twrValueLabel.ForeColor = twr >= 2.0f ? CurrentPalette.Success : CurrentPalette.Warning;
            if (hoverValueLabel != null) hoverValueLabel.ForeColor = hoverPct <= 65f ? CurrentPalette.Success : CurrentPalette.Warning;
            if (sagValueLabel != null) sagValueLabel.ForeColor = sagVolts <= 2.5f ? CurrentPalette.TextPrimary : CurrentPalette.Warning;
            if (tempValueLabel != null) tempValueLabel.ForeColor = (motorTempC > 95f || escTempC > 85f) ? CurrentPalette.Warning : CurrentPalette.TextPrimary;

            if (massMetricLabel != null) massMetricLabel.Text = $"AUW: {liveMass * 1000f:0} g";
            if (powerMetricLabel != null) powerMetricLabel.Text = $"Draw: {totalCurrentA:0.0} A";
            if (enduranceMetricLabel != null) enduranceMetricLabel.Text = $"Est. Flight: {(flightMins > 0 ? $"{flightMins:0.0} min" : "--")}";
            if (payloadMetricLabel != null) payloadMetricLabel.Text = $"Stress/Stab: {frameStressPct:0}%/{stabilityMarginPct:0}%";

            if (warningsList != null)
            {
                warningsList.BeginUpdate();
                warningsList.Items.Clear();

                if (frames == 0) warningsList.Items.Add("Missing frame");
                if (frames == 0) criticalAlerts++;
                if (motors < 4) warningsList.Items.Add("Low motor count");
                if (motors < 4) criticalAlerts++;
                if (batteryInst == null) warningsList.Items.Add("Battery not installed");
                if (batteryInst == null) criticalAlerts++;
                if (twr > 0 && twr < 1.8f) warningsList.Items.Add($"Low thrust-to-weight ({twr:0.0}:1)");
                if (twr > 0 && twr < 1.8f) criticalAlerts++;
                if (sagVolts > 3.5f) warningsList.Items.Add("High voltage sag");
                if (totalCurrentA > 80f) warningsList.Items.Add("High current draw");
                if (motorTempC > 95f) warningsList.Items.Add($"Motor overheating risk ({motorTempC:0}C)");
                if (escTempC > 85f) warningsList.Items.Add($"ESC overheating risk ({escTempC:0}C)");
                if (escFailureRiskPct > 35f) warningsList.Items.Add($"ESC failure probability elevated ({escFailureRiskPct:0}%)");
                if (frameStressPct > 100f) warningsList.Items.Add($"Frame stress high ({frameStressPct:0}%)");
                if (stabilityMarginPct < 45f) warningsList.Items.Add($"Stability margin low ({stabilityMarginPct:0}%)");
                if (yawImbalancePct > 15f) warningsList.Items.Add($"Yaw imbalance detected ({yawImbalancePct:0}%)");
                if (imuVibrationPct > 65f) warningsList.Items.Add($"IMU vibration transfer high ({imuVibrationPct:0}%)");
                if (simulationMode == SimulationMode.AutonomousMission && waypoints.Count == 0)
                    warningsList.Items.Add("Autonomous mode active with no waypoints");
                if (faultInjection.MotorFailure || faultInjection.SensorNoise || faultInjection.GpsDrop || faultInjection.EscThermalCutback)
                    warningsList.Items.Add("Fault injection enabled");
                if (crashCount > 0)
                    warningsList.Items.Add($"Crash events: {crashCount}");
                warningsList.Items.Add($"Telemetry samples: {telemetry.Count}");
                warningsList.Items.Add($"Plugins loaded: {loadedPlugins.Count}");
                warningsList.Items.Add($"Education mode: {(educationMode ? "On" : "Off")}");
                if (warningsList.Items.Count == 0) warningsList.Items.Add("No critical warnings");

                warningsList.EndUpdate();
            }

            errorsStatus.Text = $" | Errors: {Math.Max(errors, criticalAlerts)}";
        }

        // ===== Asset management & editor =====

        void ImportAsset()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Silvu Asset (*.svasset)|*.svasset"
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            var asset = AssetIO.LoadAsset(ofd.FileName);

            string targetDir = Path.Combine(AssetLibrary.UserAssetRoot, asset.Category);
            Directory.CreateDirectory(targetDir);
            File.Copy(ofd.FileName, Path.Combine(targetDir, Path.GetFileName(ofd.FileName)), overwrite: true);

            AssetLibrary.LoadAll(AssetLibrary.UserAssetRoot);
            BuildLibrary();
        }

        void CreateNewAsset(string category)
        {
            Asset asset = category switch
            {
                "Motors" => new MotorAsset { Name = "New Motor", Category = category, MaxRPM = 20000, MaxCurrent = 30, MassKg = 0.03f },
                "Batteries" => new BatteryAsset { Name = "New Battery", Category = category, Cells = 4, VoltageNominal = 14.8f, CapacityAh = 1.3f, MaxDischargeC = 75f, Chemistry = BatteryChemistry.LiPo },
                "ESC" => new ESCAsset { Name = "New ESC", Category = category },
                "Frames" => new FrameAsset { Name = "New Frame", Category = category },
                "FC" => new FlightControllerAsset { Name = "New FC", Category = category },
                "Props" => new PropellerAsset { Name = "New Prop", Category = category },
                "Receivers" => new ReceiverAsset { Name = "New Receiver", Category = category },
                _ => new MotorAsset { Name = "New Motor", Category = "Motors" }
            };

            asset.Meta.IsCustom = true;
            OpenAssetEditor(asset, saveAfter => {
                if (saveAfter) { AssetLibrary.SaveAssetToUserDir(asset); AssetLibrary.LoadAll(AssetLibrary.UserAssetRoot); BuildLibrary(); }
            });
        }

        void OpenAssetEditor(Asset asset, Action<bool>? onClose = null)
        {
            var f = new AssetEditorForm(asset);
            var res = f.ShowDialog(this);
            if (res == DialogResult.OK)
            {
                AssetLibrary.SaveAssetToUserDir(asset);
                AssetLibrary.LoadAll(AssetLibrary.UserAssetRoot);
                BuildLibrary();
                onClose?.Invoke(true);
            }
            else
            {
                onClose?.Invoke(false);
            }
        }

        void ExportAsset(Asset a)
        {
            var sfd = new SaveFileDialog { Filter = "Silvu Asset (*.svasset)|*.svasset", FileName = a.Name + ".svasset" };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            AssetIO.SaveAsset(a, sfd.FileName);
        }

        void DeleteAsset(Asset a)
        {
            if (!a.Meta.IsCustom || !AssetLibrary.Paths.ContainsKey(a.Id))
            {
                MessageBox.Show("Can only delete custom assets.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Delete asset '{a.Name}'?", "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            AssetLibrary.DeleteAsset(a);
            BuildLibrary();
        }

        void RevealInExplorer(Asset a)
        {
            if (!AssetLibrary.Paths.TryGetValue(a.Id, out var path) || !File.Exists(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch { }
        }

        Asset CloneAsset(Asset a)
        {
            var json = JsonSerializer.Serialize(a, a.GetType());
            var clone = (Asset)JsonSerializer.Deserialize(json, a.GetType())!;
            clone.Id = Guid.NewGuid().ToString();
            clone.Meta = new AssetMeta { IsCustom = true, Created = DateTime.Now, Modified = DateTime.Now };
            return clone;
        }

        sealed class BufferedPictureBox : PictureBox
        {
            public BufferedPictureBox()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            }
        }

        sealed class RoundedPanel : Panel
        {
            int cornerRadius = 12;
            int borderThickness = 1;
            Color fillColor = Color.White;
            Color borderColor = Color.LightGray;

            [Browsable(false)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public int CornerRadius
            {
                get => cornerRadius;
                set
                {
                    cornerRadius = Math.Max(2, value);
                    ApplyRoundedRegion();
                    Invalidate();
                }
            }

            [Browsable(false)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public int BorderThickness
            {
                get => borderThickness;
                set
                {
                    borderThickness = Math.Max(0, value);
                    Invalidate();
                }
            }

            [Browsable(false)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public Color FillColor
            {
                get => fillColor;
                set
                {
                    fillColor = value;
                    Invalidate();
                }
            }

            [Browsable(false)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public Color BorderColor
            {
                get => borderColor;
                set
                {
                    borderColor = value;
                    Invalidate();
                }
            }

            public RoundedPanel()
            {
                DoubleBuffered = true;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.UserPaint,
                    true);
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                ApplyRoundedRegion();
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                var parentBack = Parent?.BackColor ?? SystemColors.Control;
                using (var parentBrush = new SolidBrush(parentBack))
                    e.Graphics.FillRectangle(parentBrush, ClientRectangle);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

                var rect = ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                rect.Inflate(-1, -1);
                using var path = CreateRoundedPath(rect, cornerRadius);
                using var fill = new SolidBrush(fillColor);
                e.Graphics.FillPath(fill, path);

                if (borderThickness > 0)
                {
                    using var border = new Pen(borderColor, borderThickness);
                    e.Graphics.DrawPath(border, path);
                }
            }

            void ApplyRoundedRegion()
            {
                if (Width <= 1 || Height <= 1) return;

                using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), cornerRadius);
                var newRegion = new Region(path);
                Region?.Dispose();
                Region = newRegion;
            }

            static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
            {
                var path = new GraphicsPath();
                int safeRadius = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
                int d = safeRadius * 2;

                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        sealed class ThemedColorTable : ProfessionalColorTable
        {
            readonly Func<UiPalette> paletteProvider;

            UiPalette Palette => paletteProvider();

            public ThemedColorTable(Func<UiPalette> paletteProvider)
            {
                this.paletteProvider = paletteProvider;
                UseSystemColors = false;
            }

            public override Color MenuStripGradientBegin => Palette.Surface;
            public override Color MenuStripGradientEnd => Palette.Surface;
            public override Color ToolStripDropDownBackground => Palette.Surface;
            public override Color ImageMarginGradientBegin => Palette.Surface;
            public override Color ImageMarginGradientMiddle => Palette.Surface;
            public override Color ImageMarginGradientEnd => Palette.Surface;
            public override Color MenuBorder => Palette.Border;
            public override Color MenuItemBorder => Palette.Border;
            public override Color MenuItemSelected => Palette.AccentSoft;
            public override Color MenuItemSelectedGradientBegin => Palette.AccentSoft;
            public override Color MenuItemSelectedGradientEnd => Palette.AccentSoft;
            public override Color MenuItemPressedGradientBegin => Palette.SurfaceAlt;
            public override Color MenuItemPressedGradientMiddle => Palette.SurfaceAlt;
            public override Color MenuItemPressedGradientEnd => Palette.SurfaceAlt;
            public override Color SeparatorDark => Palette.Border;
            public override Color SeparatorLight => Palette.Border;
            public override Color StatusStripGradientBegin => Palette.Surface;
            public override Color StatusStripGradientEnd => Palette.Surface;
            public override Color ToolStripBorder => Palette.Border;
        }

        // ================= DATA =================
        class Project
        {
            public string Name { get; set; } = "";
            public List<PlacedInstance> Instances { get; set; } = new();
        }





        enum PartType
        {
            Frame,
            Motor,
            Battery
        }
        enum MountType
{
    None,
    Motor,
    Battery,
    ESC,
    FlightController,
    Receiver,
    GPS,
    Camera,
    VTX,
    Propeller
}


        class FrameDefinition
        {
            public PointF[] MotorMounts = Array.Empty<PointF>();   // relative positions
            public RectangleF BatteryBay = new RectangleF();
            public SizeF Size = new SizeF();
        }

        class MountPoint
{
    public MountType Type;
    public PointF Position;        // relative to frame center
    public SizeF Size;             // mounting area
    public float RotationDeg;      // orientation
    public string? Label;           // e.g. "ESC1", "FC Stack"
}


        class PlacedPart
        {
            public PartType Type;
            public string Name = "";

            // world position
            public PointF Position = new PointF();

            // attachment
            public int AttachedMountIndex = -1; // for motors
        }

        // FrameAsset consolidated further down (duplicate removed)



        class PlacedInstance
        {
            public string AssetId = "";
            public PartType Type;
            public PointF Position = new PointF();
            public int MountIndex = -1; // for motors
        }

        class LibraryPart
        {
            public string Category { get; set; } = "";
            public string Name { get; set; } = "";
            public string AssetId { get; set; } = ""; // optional bridge to AssetLibrary
        }

        class AssetMeta
        {
            public string Version { get; set; } = "1.0";
            public DateTime Created { get; set; } = DateTime.Now;
            public DateTime Modified { get; set; } = DateTime.Now;
            public bool IsCustom { get; set; } = true;
        }

        abstract class Asset
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; } = "";
            public string Category { get; set; } = ""; // Motors, ESC, FC, Frame, etc
            public string Manufacturer { get; set; } = "";
            public string Description { get; set; } = "";

            public float MassKg { get; set; }

            public AssetMeta Meta { get; set; } = new();

            public abstract MountType RequiredMount { get; }
        }

        class MotorAsset : Asset
        {
            public override MountType RequiredMount => MountType.Motor;
            public int KV { get; set; }
            public float MaxRPM { get; set; }
            public float MaxCurrent { get; set; }
            public float MaxThrust { get; set; }
            public float VoltageMin { get; set; }
            public float VoltageMax { get; set; }
            public float ShaftDiameter { get; set; }
            public float MountHoleSize { get; set; }
            public float RotorInertia { get; set; } = 0.00002f;
            public string TorqueCurve { get; set; } = "0.0:0.00;0.5:0.54;1.0:1.00";
            public string EfficiencyMap { get; set; } = "0.2:0.78;0.5:0.86;0.8:0.82";
        }

        class BatteryAsset : Asset
        {
            public override MountType RequiredMount => MountType.Battery;
            public int Cells { get; set; }
            public float VoltageNominal { get; set; }
            public float CapacityAh { get; set; }
            public float MaxDischargeC { get; set; }
            public BatteryChemistry Chemistry { get; set; } = BatteryChemistry.LiPo;
        }

        class ESCAsset : Asset
        {
            public override MountType RequiredMount => MountType.ESC;
            public float ContinuousCurrent { get; set; }
            public float BurstCurrent { get; set; }
            public bool SupportsDShot { get; set; }
            public bool SupportsBLHeli { get; set; }
            public float ResponseDelayMs { get; set; } = 8f;
            public float ThermalLimitC { get; set; } = 95f;
            public float FailureProbabilityPct { get; set; } = 1.5f;
        }

        class FlightControllerAsset : Asset
        {
            public override MountType RequiredMount => MountType.FlightController;
            public string MCU { get; set; } = "";
            public int UARTCount { get; set; }
            public bool HasOSD { get; set; }
            public bool HasBlackbox { get; set; }
            public float GyroUpdateRate { get; set; }
        }

        class ReceiverAsset : Asset
        {
            public override MountType RequiredMount => MountType.Receiver;
            public string Protocol { get; set; } = ""; // SBUS, CRSF, ELRS
            public float FrequencyGHz { get; set; }
            public bool Telemetry { get; set; }
        }

        class PropellerAsset : Asset
        {
            public override MountType RequiredMount => MountType.Propeller;
            public float DiameterInch { get; set; }
            public float Pitch { get; set; }
            public int BladeCount { get; set; }
            public string ThrustCurve { get; set; } = "0.2:1.2;0.5:6.8;1.0:15.0";
            public float AerodynamicDragCoeff { get; set; } = 0.08f;
        }

        class FrameAsset : Asset
        {
            public float WheelbaseMm { get; set; }
            public int ArmCount { get; set; }
            public float ArmThicknessMm { get; set; }
            public float MaterialDensity { get; set; } = 1.6f;
            public float CgOffsetXcm { get; set; }
            public float CgOffsetYcm { get; set; }
            public FrameDefinition? Geometry { get; set; }
            public List<MountPoint> Mounts { get; set; } = new();
            public override MountType RequiredMount => MountType.None;
        }

        static class AssetIO
        {
            public static void SaveAsset(Asset asset, string path)
            {
                asset.Meta.Modified = DateTime.Now;

                var json = JsonSerializer.Serialize(
                    asset,
                    asset.GetType(),
                    new JsonSerializerOptions { WriteIndented = true }
                );

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");
                File.WriteAllText(path, json);
            }

            public static Asset LoadAsset(string path)
            {
                var json = File.ReadAllText(path);

                using var doc = JsonDocument.Parse(json);
                string category = doc.RootElement.GetProperty("Category").GetString() ?? string.Empty;

                return category switch
                {
                    "Motors" => JsonSerializer.Deserialize<MotorAsset>(json)!,
                    "Batteries" => JsonSerializer.Deserialize<BatteryAsset>(json)!,
                    "ESC" => JsonSerializer.Deserialize<ESCAsset>(json)!,
                    "Frames" => JsonSerializer.Deserialize<FrameAsset>(json)!,
                    "FC" => JsonSerializer.Deserialize<FlightControllerAsset>(json)!,
                    "Props" => JsonSerializer.Deserialize<PropellerAsset>(json)!,
                    "Receivers" => JsonSerializer.Deserialize<ReceiverAsset>(json)!,
                    _ => throw new Exception("Unknown asset type")
                };
            }
        }

        static class AssetLibrary
        {
            public static readonly Dictionary<string, Asset> Assets = new();
            public static readonly Dictionary<string, string> Paths = new();

            public static readonly string UserAssetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SilvuViewfinder", "Assets");

            public static void LoadDefaults()
            {
                // keep a small set of built-in assets for a nice initial experience
                Assets.Clear();
                Paths.Clear();

                var a = new MotorAsset
                {
                    Name = "2207 1750KV",
                    Category = "Motors",
                    KV = 1750,
                    MaxRPM = 22000f,
                    MaxCurrent = 35f,
                    MaxThrust = 15f,
                    MassKg = 0.031f,
                    Description = "5-inch FPV motor"
                };
                Assets[a.Id] = a;

                var b = new MotorAsset
                {
                    Name = "2306 1950KV",
                    Category = "Motors",
                    KV = 1950,
                    MaxRPM = 24000f,
                    MaxCurrent = 40f,
                    MaxThrust = 17f,
                    MassKg = 0.031f,
                    Description = "High rpm motor"
                };
                Assets[b.Id] = b;

                var ba4 = new BatteryAsset
                {
                    Name = "4S LiPo",
                    Category = "Batteries",
                    VoltageNominal = 14.8f,
                    CapacityAh = 1.3f,
                    MaxDischargeC = 75f,
                    MassKg = 0.22f,
                    Cells = 4,
                    Chemistry = BatteryChemistry.LiPo,
                    Description = "4S battery"
                };
                Assets[ba4.Id] = ba4;

                var ba6 = new BatteryAsset
                {
                    Name = "6S LiPo",
                    Category = "Batteries",
                    VoltageNominal = 22.2f,
                    CapacityAh = 1.3f,
                    MaxDischargeC = 75f,
                    MassKg = 0.22f,
                    Cells = 6,
                    Chemistry = BatteryChemistry.LiPo,
                    Description = "6S battery"
                };
                Assets[ba6.Id] = ba6;

                var f = new FrameAsset
                {
                    Name = "X Frame",
                    Category = "Frames",
                    Geometry = FrameDB.XFrame,
                    ArmCount = 4,
                    ArmThicknessMm = 4.0f,
                    WheelbaseMm = 300,
                    Description = "Standard 5-inch X frame"
                };

                // Add realistic mount points for the default frame
                f.Mounts.AddRange(new[]
                {
                    // Motors
                    new MountPoint { Type = MountType.Motor, Position = new PointF(-120,-120), Size = new SizeF(20,20), Label = "M1" },
                    new MountPoint { Type = MountType.Motor, Position = new PointF(120,-120),  Size = new SizeF(20,20), Label = "M2" },
                    new MountPoint { Type = MountType.Motor, Position = new PointF(120,120),   Size = new SizeF(20,20), Label = "M3" },
                    new MountPoint { Type = MountType.Motor, Position = new PointF(-120,120),  Size = new SizeF(20,20), Label = "M4" },

                    // ESCs (one per arm)
                    new MountPoint { Type = MountType.ESC, Position = new PointF(-80,-80), Size=new SizeF(25,40), Label = "ESC1" },
                    new MountPoint { Type = MountType.ESC, Position = new PointF(80,-80),  Size=new SizeF(25,40), Label = "ESC2" },
                    new MountPoint { Type = MountType.ESC, Position = new PointF(80,80),   Size=new SizeF(25,40), Label = "ESC3" },
                    new MountPoint { Type = MountType.ESC, Position = new PointF(-80,80),  Size=new SizeF(25,40), Label = "ESC4" },

                    // FC stack (center)
                    new MountPoint { Type = MountType.FlightController, Position = new PointF(0,0), Size=new SizeF(30,30), Label = "FC Stack" },

                    // Receiver
                    new MountPoint { Type = MountType.Receiver, Position = new PointF(0,40), Size=new SizeF(20,20), Label = "RX" },

                    // Battery
                    new MountPoint { Type = MountType.Battery, Position = new PointF(0,-40), Size=new SizeF(40,20), Label = "Battery Tray" }
                });

                Assets[f.Id] = f;
            }

            public static void LoadAll(string rootDir)
            {
                if (!Directory.Exists(rootDir)) Directory.CreateDirectory(rootDir);

                foreach (var file in Directory.GetFiles(rootDir, "*.svasset", SearchOption.AllDirectories))
                {
                    try
                    {
                        var asset = AssetIO.LoadAsset(file);
                        Assets[asset.Id] = asset;
                        Paths[asset.Id] = file;
                    }
                    catch { /* skip invalid */ }
                }
            }

            public static IEnumerable<Asset> GetByCategory(string category)
                => Assets.Values.Where(a => a.Category == category).OrderBy(a => a.Name);

            public static Asset? FindByName(string name)
                => Assets.Values.FirstOrDefault(a => a.Name == name);

            public static Asset? Get(string id)
                => Assets.TryGetValue(id, out var a) ? a : null;

            public static string SaveAssetToUserDir(Asset asset)
            {
                var dir = Path.Combine(UserAssetRoot, asset.Category);
                Directory.CreateDirectory(dir);
                string fileName = SanitizeFileName(asset.Name);
                string path = Path.Combine(dir, fileName + ".svasset");

                // ensure unique filename
                int i = 1;
                while (File.Exists(path))
                {
                    path = Path.Combine(dir, fileName + $"_{i}.svasset");
                    i++;
                }

                AssetIO.SaveAsset(asset, path);
                Assets[asset.Id] = asset;
                Paths[asset.Id] = path;
                return path;
            }

            public static void DeleteAsset(Asset asset)
            {
                if (Paths.TryGetValue(asset.Id, out var path) && File.Exists(path))
                {
                    File.Delete(path);
                }

                Assets.Remove(asset.Id);
                Paths.Remove(asset.Id);
            }

            static string SanitizeFileName(string name)
            {
                var invalid = Path.GetInvalidFileNameChars();
                return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            }
        }

        class AssetEditorForm : Form
        {
            public Asset Asset { get; }
            PropertyGrid grid = new PropertyGrid();
            Button btnSave = new Button { Text = "Save", Dock = DockStyle.Right, Width = 108 };
            Button btnCancel = new Button { Text = "Cancel", Dock = DockStyle.Right, Width = 108 };

            public AssetEditorForm(Asset asset)
            {
                Asset = asset;
                Text = $"Edit Asset - {asset.Name}";
                Width = 500;
                Height = 600;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                Padding = new Padding(12);
                BackColor = Color.FromArgb(246, 249, 255);

                grid.Dock = DockStyle.Fill;
                grid.SelectedObject = asset;
                grid.HelpVisible = true;
                grid.ToolbarVisible = false;
                grid.PropertyValueChanged += (s, e) => { Text = $"Edit Asset - {asset.Name}"; };

                var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.Transparent };
                StyleButton(btnSave, true);
                StyleButton(btnCancel, false);
                btnSave.Click += (_,__) => { DialogResult = DialogResult.OK; Close(); };
                btnCancel.Click += (_,__) => { DialogResult = DialogResult.Cancel; Close(); };
                bottom.Controls.Add(btnCancel);
                bottom.Controls.Add(btnSave);

                AcceptButton = btnSave;
                CancelButton = btnCancel;
                Controls.Add(grid);
                Controls.Add(bottom);
            }

            void StyleButton(Button button, bool primary)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = primary
                    ? Color.FromArgb(63, 134, 233)
                    : Color.FromArgb(232, 238, 250);
                button.FlatAppearance.MouseDownBackColor = primary
                    ? Color.FromArgb(45, 117, 214)
                    : Color.FromArgb(220, 228, 243);
                button.ForeColor = primary ? Color.White : Color.FromArgb(37, 52, 79);
                button.BackColor = primary ? Color.FromArgb(52, 124, 221) : Color.FromArgb(241, 246, 255);
                button.Padding = new Padding(2);
            }
        }

        static class FrameDB
        {
            public static FrameDefinition XFrame = new FrameDefinition
            {
                Size = new SizeF(300, 300),
                MotorMounts = new[]
                {
                    new PointF(-120, -120),
                    new PointF(120, -120),
                    new PointF(120, 120),
                    new PointF(-120, 120),
                },
                BatteryBay = new RectangleF(-40, -20, 80, 40)
            };
        }

        static class PhysicsDatabase
        {
            public static float GetMass(string category)
            {
                return category switch
                {
                    "Motors" => 0.031f,
                    "Batteries" => 0.22f,
                    "Frames" => 0.15f,
                    _ => 0.05f
                };
            }

            public static float MotorMass(string _) => 0.031f;
            public static float FrameMass() => 0.15f;
            public static float BatteryMass() => 0.22f;

            public static float GetMaxThrust(string name)
            {
                return name switch
                {
                    "2207 1750KV" => 15.0f, // Newtons
                    "2306 1950KV" => 17.0f,
                    _ => 10.0f
                };
            }

            public static float GetCurrentDraw(string name)
            {
                return name switch
                {
                    "2207 1750KV" => 18.0f, // Amps at hover
                    "2306 1950KV" => 22.0f,
                    _ => 10.0f
                };
            }

            public static float MaxRPM(string name) => name switch
            {
                "2207 1750KV" => 22000f,
                "2306 1950KV" => 24000f,
                _ => 20000f
            };

            public static float MaxCurrent(string name) => name switch
            {
                "2207 1750KV" => 35f,
                "2306 1950KV" => 40f,
                _ => 25f
            };
        }

        bool HasFrame()
        {
            return project!.Instances.Exists(p => p.Type == PartType.Frame);
        }

        PlacedInstance? GetFrame()
        {
            return project!.Instances.Find(p => p.Type == PartType.Frame);
        }

        float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        string GetPartInfo(string category, string name)
        {
            if (category == "Motors")
            {
                return $"{name}\nThrust: {PhysicsDatabase.GetMaxThrust(name)} N\nHover Current: {PhysicsDatabase.GetCurrentDraw(name)} A\nMass: {PhysicsDatabase.MotorMass(name)} kg";
            }
            else if (category == "Batteries")
            {
                var battery = AssetLibrary.FindByName(name) as BatteryAsset;
                if (battery != null)
                {
                    return $"{name}\nVoltage: {battery.VoltageNominal:0.0}V\nCapacity: {battery.CapacityAh:0.0}Ah\n" +
                           $"Chemistry: {battery.Chemistry}\nMass: {battery.MassKg:0.###} kg";
                }
                return $"{name}\nMass: {PhysicsDatabase.BatteryMass()} kg";
            }
            else if (category == "Frames")
            {
                if (name == "X Frame")
                    return $"{name}\nSize: {FrameDB.XFrame.Size.Width} x {FrameDB.XFrame.Size.Height}";
                return $"{name}\nFrame";
            }
            return name;
        }

        string GetPlacedPartInfo(PlacedInstance p)
        {
            var sb = new System.Text.StringBuilder();
            var asset = AssetLibrary.Get(p.AssetId);
            var assetName = asset?.Name ?? p.AssetId;
            sb.AppendLine($"{p.Type} - {assetName}");
            if (p.Type == PartType.Motor)
            {
                sb.AppendLine($"Mount: {p.MountIndex}");
                sb.AppendLine($"Thrust: {PhysicsDatabase.GetMaxThrust(assetName)} N");
                sb.AppendLine($"Hover Current: {PhysicsDatabase.GetCurrentDraw(assetName)} A");
                sb.AppendLine($"Mass: {PhysicsDatabase.MotorMass(assetName)} kg");
            }
            else if (p.Type == PartType.Battery)
            {
                if (asset is BatteryAsset battery)
                {
                    sb.AppendLine($"Voltage: {battery.VoltageNominal:0.0}V");
                    sb.AppendLine($"Capacity: {battery.CapacityAh:0.0}Ah");
                    sb.AppendLine($"Chemistry: {battery.Chemistry}");
                    sb.AppendLine($"Mass: {battery.MassKg:0.###} kg");
                }
                else
                {
                    sb.AppendLine($"Mass: {PhysicsDatabase.BatteryMass()} kg");
                }
            }
            else if (p.Type == PartType.Frame)
            {
                sb.AppendLine($"Size: {FrameDB.XFrame.Size.Width} x {FrameDB.XFrame.Size.Height}");
            }
            sb.AppendLine($"Position: {p.Position.X:0},{p.Position.Y:0}");
            return sb.ToString();
        }

        int FindNearestMount(PointF mousePos)
        {
            var frame = GetFrame();
            if (frame == null) return -1;

            var def = FrameDB.XFrame;

            for (int i = 0; i < def.MotorMounts.Length; i++)
            {
                PointF world = new(
                    frame.Position.X + def.MotorMounts[i].X,
                    frame.Position.Y + def.MotorMounts[i].Y
                );

                if (Distance(mousePos, world) < 25)
                    return i;
            }
            return -1;
        }

        bool AddMotor(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;

            int mount = FindNearestMount(mousePos);
            if (mount == -1) return false;

            // Prevent placing multiple motors on the same mount
            if (project!.Instances.Any(p => p.Type == PartType.Motor && p.MountIndex == mount))
                return false;

            project!.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Motor,
                MountIndex = mount
            });

            return true;
        }
        bool TryPlaceAsset(Asset asset, PointF mouse)
{
    var frame = GetFrameAsset();
    if (frame == null) return false;

    var compatibleMounts = frame.Mounts
        .Where(m => m.Type == asset.RequiredMount)
        .ToList();

    foreach (var mount in compatibleMounts)
    {
        var worldPos = FrameToWorld(mount.Position);
        if (Distance(mouse, worldPos) < mount.Size.Width)
        {
            PlaceInstance(asset, mount);
            return true;
        }
    }
    return false;
}

        FrameAsset? GetFrameAsset()
        {
            var framePlaced = GetFrame();
            if (framePlaced == null) return null;
            return AssetLibrary.Get(framePlaced.AssetId) as FrameAsset;
        }

        PointF FrameToWorld(PointF relPos)
        {
            var frame = GetFrame();
            if (frame == null) return relPos;
            return new PointF(frame.Position.X + relPos.X, frame.Position.Y + relPos.Y);
        }

        void PlaceInstance(Asset asset, MountPoint mount)
        {
            if (project == null) return;
            var framePlaced = GetFrame();
            if (framePlaced == null) return;
            var frameAsset = AssetLibrary.Get(framePlaced.AssetId) as FrameAsset;
            if (frameAsset == null) return;
            int mountIndex = frameAsset.Mounts.IndexOf(mount);

            if (asset.RequiredMount == MountType.Motor)
            {
                if (project.Instances.Any(p => p.Type == PartType.Motor && p.MountIndex == mountIndex)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Motor,
                    MountIndex = mountIndex
                });
            }
            else if (asset.RequiredMount == MountType.Battery)
            {
                if (project.Instances.Any(p => p.Type == PartType.Battery)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Battery,
                    Position = FrameToWorld(mount.Position)
                });
            }

            OnProjectStructureChanged();
        }

        

        bool AddBattery(PointF mousePos, string name)
        {
            var frame = GetFrame();
            if (frame == null) return false;

            var bay = FrameDB.XFrame.BatteryBay;
            var worldBay = new RectangleF(
                frame.Position.X + bay.X,
                frame.Position.Y + bay.Y,
                bay.Width,
                bay.Height
            );

            if (!worldBay.Contains(mousePos)) return false;

            // Prevent multiple batteries on the same frame
            if (project!.Instances.Any(p => p.Type == PartType.Battery))
                return false;

            project!.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Battery,
                Position = frame.Position
            });

            return true;
        }



        PointF GetPartWorldPosition(PlacedInstance p)
        {
            if (p.Type == PartType.Motor)
            {
                var frame = GetFrame();
                if (frame == null) return p.Position;
                if (p.MountIndex < 0 || p.MountIndex >= FrameDB.XFrame.MotorMounts.Length)
                    return p.Position;

                var mount = FrameDB.XFrame.MotorMounts[p.MountIndex];
                return new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
            }

            return p.Position;
        }

        void ResetPhysicsState()
        {
            var battery = project?.Instances
                .Where(i => i.Type == PartType.Battery)
                .Select(i => AssetLibrary.Get(i.AssetId) as BatteryAsset)
                .FirstOrDefault(i => i != null);

            if (battery != null)
            {
                if (battery.CapacityAh > 0) batteryCapacityAh = battery.CapacityAh;
                if (battery.VoltageNominal > 0) batteryVoltageNominal = battery.VoltageNominal;
            }

            altitude = 0.0f;
            verticalVelocity = 0.0f;
            pidIntegral = 0.0f;
            lastError = 0.0f;
            batteryRemainingAh = batteryCapacityAh;
            batteryVoltage = batteryVoltageNominal;
            motorTempC = 30f;
            escTempC = 28f;
            frameStressPct = 0f;
            stabilityMarginPct = 100f;
            yawImbalancePct = 0f;
            imuVibrationPct = 0f;
            escFailureRiskPct = 0f;
            telemetryDecimator = 0;
            telemetry.Clear();
            crashCount = 0;
            lastCrashSummary = "No crash events";
            lastCrashTimeSec = -1;
            simClock.Restart();
        }

    }
}
