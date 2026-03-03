using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Runtime.InteropServices;
using System.Drawing.Design;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;

namespace SilvuViewfinder
{
    public partial class Form1 : Form, ISilvuHost
    {

        bool darkMode = false;
        bool dirty = false;

        Project? project;
        string? projectPath;

        LibraryPart? dragging;
        PlacedInstance? selected;
        PartType? pendingAddMode = null;
        string? pendingAddName = null;
        string? pendingAddAssetId = null;
        Point mousePos;
        Point lastMouseScreen;
        bool draggingFrame = false;
        PointF frameDragOffset;
        bool dragUndoCaptured = false;

        readonly Stack<Project> undoStack = new();
        readonly Stack<Project> redoStack = new();

        // Viewport control
        float zoomFactor = 1.0f;
        PointF viewOffset = new PointF(0, 0);
        const float ViewportDepth = 4.0f;
        bool isPanning = false;
        Point lastMousePos;
        float viewRotation = 0f;
        bool viewportIs3D = false;

        MenuStrip menu = null!;
        Panel titleBarPanel = null!, titleMenuHost = null!;
        FlowLayoutPanel titleButtonPanel = null!;
        PictureBox titleLogo = null!;
        Button titleMinButton = null!, titleMaxButton = null!, titleCloseButton = null!;
        TreeView projectTree = null!, libraryTree = null!;
        PictureBox viewport = null!;
        Label twrValueLabel = null!, hoverValueLabel = null!, flightValueLabel = null!, sagValueLabel = null!, tempValueLabel = null!;
        Label thrustRequiredValueLabel = null!, maxThrustValueLabel = null!, thrustMarginValueLabel = null!, motorTempLimitLabel = null!;
        Label batteryMaxCurrentLabel = null!, powerHoverCurrentLabel = null!, peakCurrentLabel = null!, escRatingLabel = null!;
        Label cgValueLabel = null!, rollInertiaValueLabel = null!, pitchInertiaValueLabel = null!, yawInertiaValueLabel = null!, yawValueLabel = null!;
        Label payloadMaxLabel = null!, payloadCurrentLabel = null!, payloadRemainingLabel = null!;
        Label massFrameLabel = null!, massMotorsLabel = null!, massEscLabel = null!, massBatteryLabel = null!, massPayloadLabel = null!;
        Label missingValueLabel = null!, overcurrentValueLabel = null!, overheatValueLabel = null!, structuralValueLabel = null!;
        Label allUpWeightLabel = null!, totalThrustLabel = null!, hoverCurrentLabel = null!, efficiencyIndexLabel = null!, stabilityIndexLabel = null!;
        Button saveBuildButton = null!, exportConfigButton = null!;
        Panel cgRowPanel = null!, rollInertiaRowPanel = null!, pitchInertiaRowPanel = null!, yawInertiaRowPanel = null!, yawStabilityRowPanel = null!;
        Label stabilityHeaderLabel = null!;
        int uiRefreshTick = 0;
        readonly Random random = new Random();
        readonly Stopwatch simClock = new Stopwatch();
        readonly List<TelemetrySample> telemetry = new List<TelemetrySample>();
        readonly List<Waypoint> waypoints = new List<Waypoint>();
        readonly List<string> loadedPlugins = new List<string>();
        readonly List<BuildBenchmark> benchmarkHistory = new List<BuildBenchmark>();
        readonly Dictionary<string, Image> customImageCache = new(StringComparer.OrdinalIgnoreCase);
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
        readonly FeatureSetProfile featureProfile = new FeatureSetProfile();
        float escDelayedThrottle = 0f;

        // Status bar
        StatusStrip statusStrip = null!;
        ToolStripStatusLabel workspaceStatus = null!, modeStatus = null!, firmwareStatus = null!, sensorsStatus = null!;
        ToolStripStatusLabel statusSpacer = null!, errorsStatus = null!, simReadyStatus = null!;

        // clipboard for copy/paste of placed instances
        PlacedInstance? clipboardPart = null;

        // context menu for the project tree (layers)
        ContextMenuStrip projectContextMenu = null!;
        // context menu for the library tree
        ContextMenuStrip libraryContextMenu = null!;
        // context menu for the viewport
        ContextMenuStrip viewportContextMenu = null!;
        ToolTip partToolTip = null!;
        System.Windows.Forms.Timer renderTimer = null!;
        ToolStripProfessionalRenderer toolStripRenderer = null!;
        Panel contentHost = null!;
        Panel topToolbarHost = null!;
        TabControl modeTabs = null!;
        Workspace currentWorkspace = Workspace.Assemble;
        ToolStrip iconStrip = null!;
        Panel ribbonHost = null!;
        readonly Dictionary<string, ToolStrip> modeRibbons = new Dictionary<string, ToolStrip>(StringComparer.OrdinalIgnoreCase);
        SplitContainer navigationSplit = null!;
        Image? logoImage;
        Panel workspaceHost = null!;
        readonly Dictionary<string, Control> modeWorkspaces = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        NumericUpDown frameArmLengthInput = null!;
        ComboBox frameArmUnitSelector = null!;
        NumericUpDown frameBodySizeInput = null!;
        ComboBox frameBodyUnitSelector = null!;
        ComboBox escLayoutSelector = null!;
        EscLayout escLayout = EscLayout.FourInOne;
        bool suppressFrameUiEvents = false;
        bool autoSimEnabled = true;
        bool deterministicSim = true;
        bool autoSimUseParts = true;
        const float SimStepSeconds = 0.016f;
        readonly List<(ToolStripItem Item, RibbonIcon Icon)> ribbonIconItems = new();
        static readonly HashSet<string> CustomCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "Custom",
            "Payload",
            "Landing Gear",
            "Aerodynamic Add-ons",
            "Telemetry",
            "Power Distribution",
            "Safety",
            "Advanced",
            "Industrial"
        };

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
            windowBackground: Color.FromArgb(14, 17, 22),
            surface: Color.FromArgb(20, 24, 30),
            surfaceAlt: Color.FromArgb(27, 32, 40),
            cardBackground: Color.FromArgb(22, 27, 34),
            border: Color.FromArgb(42, 48, 58),
            textPrimary: Color.FromArgb(230, 233, 239),
            textMuted: Color.FromArgb(156, 164, 175),
            accent: Color.FromArgb(56, 62, 70),
            accentSoft: Color.FromArgb(36, 42, 50),
            viewportBackground: Color.FromArgb(12, 16, 22),
            hudBackground: Color.FromArgb(24, 29, 36),
            hudText: Color.FromArgb(226, 230, 238),
            success: Color.FromArgb(88, 182, 120),
            warning: Color.FromArgb(214, 150, 72)
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
        const int RESIZE_BORDER = 7;
        const int WM_NCLBUTTONDOWN = 0xA1;
        const int WM_NCHITTEST = 0x84;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;
        const int HTCLIENT = 1;
        const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

        [StructLayout(LayoutKind.Sequential)]
        struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        public Form1()
        {
            Text = "SILVU VIEWFINDER";
            Width = 1400;
            Height = 860;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Z)
                {
                    Undo();
                    e.Handled = true;
                    return;
                }
                if (e.Control && (e.KeyCode == Keys.Y || (e.Shift && e.KeyCode == Keys.Z)))
                {
                    Redo();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.Delete)
                {
                    DeleteSelectedPart();
                    e.Handled = true;
                    return;
                }
                if (e.Control && e.KeyCode == Keys.C)
                {
                    CopySelectedPart();
                    e.Handled = true;
                    return;
                }
                if (e.Control && e.KeyCode == Keys.V)
                {
                    PasteClipboardPart();
                    e.Handled = true;
                    return;
                }
                if (e.Control && e.Shift && e.KeyCode == Keys.D)
                {
                    DuplicateSelectedPart();
                    e.Handled = true;
                    return;
                }
                if (e.Control && e.KeyCode == Keys.D3)
                {
                    ToggleViewport3D();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.A)
                {
                    viewRotation -= 5.0f;
                    viewport.Invalidate();
                }
                else if (e.KeyCode == Keys.D)
                {
                    viewRotation += 5.0f;
                    viewport.Invalidate();
                }
            };

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
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = false,
                Height = 28,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = toolStripRenderer
            };
            logoImage = LoadBrandLogo();

            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(CreateMenuItem("New", Keys.Control | Keys.N, (_,__) => NewProject()));
            fileMenu.DropDownItems.Add(CreateMenuItem("Open...", Keys.Control | Keys.O, (_,__) => OpenProject()));
            fileMenu.DropDownItems.Add(CreateMenuItem("Save", Keys.Control | Keys.S, (_,__) => SaveProject()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Export Active Config...", null, (_, __) => exportConfigButton?.PerformClick());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(CreateMenuItem("Exit", Keys.Alt | Keys.F4, (_,__) => Close()));

            var editMenu = new ToolStripMenuItem("Edit");
            editMenu.DropDownItems.Add(CreateMenuItem("Undo", Keys.Control | Keys.Z, (_, __) => Undo()));
            editMenu.DropDownItems.Add(CreateMenuItem("Redo", Keys.Control | Keys.Y, (_, __) => Redo()));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(CreateMenuItem("Copy Selected Part", Keys.Control | Keys.C, (_, __) => CopySelectedPart()));
            editMenu.DropDownItems.Add(CreateMenuItem("Paste", Keys.Control | Keys.V, (_, __) => PasteClipboardPart()));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(CreateMenuItem("Delete Selected Part", Keys.Delete, (_, __) => DeleteSelectedPart()));
            editMenu.DropDownItems.Add(CreateMenuItem("Duplicate Selected Part", Keys.Control | Keys.Shift | Keys.D, (_, __) => DuplicateSelectedPart()));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("Clear Fault Injection", null, (_, __) => ClearFaultInjection());

            var viewMenu = new ToolStripMenuItem("View");
            viewMenu.DropDownItems.Add(CreateMenuItem("Toggle Dark Mode", Keys.Control | Keys.D, (_,__) => ToggleDark()));
            viewMenu.DropDownItems.Add(CreateMenuItem("Toggle 3D View", Keys.Control | Keys.D3, (_,__) => ToggleViewport3D()));
            
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
            newAsset.DropDownItems.Add("Camera", null, (_,__) => CreateNewAsset("Cameras"));
            newAsset.DropDownItems.Add("GPS", null, (_,__) => CreateNewAsset("GPS"));
            newAsset.DropDownItems.Add("VTX", null, (_,__) => CreateNewAsset("VTX"));
            newAsset.DropDownItems.Add("Antenna", null, (_,__) => CreateNewAsset("Antennas"));
            newAsset.DropDownItems.Add("Buzzer", null, (_,__) => CreateNewAsset("Buzzers"));
            newAsset.DropDownItems.Add("LED", null, (_,__) => CreateNewAsset("LEDs"));
            newAsset.DropDownItems.Add("Custom Component", null, (_,__) => CreateNewAsset("Custom"));
            assetsMenu.DropDownItems.Add(newAsset);

            var settingsMenu = new ToolStripMenuItem("Settings");
            // This can be populated with items from the old "simMenu" if needed

            var dataMenu = new ToolStripMenuItem("Data");
            dataMenu.DropDownItems.Add("Telemetry Dashboard", null, (_, __) => ShowTelemetryDashboard());
            dataMenu.DropDownItems.Add("Export Telemetry CSV...", null, (_, __) => ExportTelemetryCsv());
            dataMenu.DropDownItems.Add("Export Telemetry JSON...", null, (_, __) => ExportTelemetryJson());

            var workspaceMenu = new ToolStripMenuItem("Workspace");
            
            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(viewMenu);
            menu.Items.Add(assetsMenu);
            menu.Items.Add(settingsMenu);
            menu.Items.Add(dataMenu);
            menu.Items.Add(workspaceMenu);

            MainMenuStrip = menu;

            // MODE TABS
            modeTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal,
                ItemSize = new Size(80, 24),
                SizeMode = TabSizeMode.Fixed,
                DrawMode = TabDrawMode.OwnerDrawFixed
            };
            modeTabs.DrawItem += DrawModeTabs;
            modeTabs.Paint += DrawModeTabsBackground;
            var modes = new[] { "Build", "Config", "Software", "Testing", "Simulation", "Debug", "Protocols", "Telemetry" };
            foreach (var mode in modes)
            {
                modeTabs.TabPages.Add(new TabPage(mode)
                {
                    UseVisualStyleBackColor = false
                });
            }

            Panel CreateModePlaceholder(string title, string subtitle, params string[] bullets)
            {
                var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
                var card = new RoundedPanel
                {
                    Dock = DockStyle.Fill,
                    CornerRadius = 16,
                    BorderThickness = 1,
                    Padding = new Padding(18)
                };

                var titleLabel = new Label
                {
                    Text = title.ToUpperInvariant(),
                    Dock = DockStyle.Top,
                    Height = 28,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font(Font.FontFamily, 10f, FontStyle.Bold, GraphicsUnit.Point)
                };

                var subtitleLabel = new Label
                {
                    Text = subtitle,
                    Dock = DockStyle.Top,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var list = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    Padding = new Padding(2, 6, 2, 2)
                };

                foreach (var item in bullets)
                {
                    var line = new Label
                    {
                        AutoSize = true,
                        Text = "- " + item,
                        MaximumSize = new Size(720, 0)
                    };
                    list.Controls.Add(line);
                }

                card.Controls.Add(list);
                card.Controls.Add(subtitleLabel);
                card.Controls.Add(titleLabel);
                host.Controls.Add(card);
                return host;
            }

            ToolStrip CreateRibbonStrip()
            {
                return new ToolStrip
                {
                    GripStyle = ToolStripGripStyle.Hidden,
                    Padding = new Padding(5, 2, 5, 2),
                    Dock = DockStyle.Fill,
                    RenderMode = ToolStripRenderMode.Professional,
                    Renderer = toolStripRenderer
                };
            }

            void AddRibbonGroup(ToolStrip strip, string title, params string[] items)
            {
                var titleLabel = new ToolStripLabel(title)
                {
                    Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point),
                    Margin = new Padding(4, 1, 8, 1)
                };
                strip.Items.Add(titleLabel);

                for (int i = 0; i < items.Length; i++)
                {
                    var button = new ToolStripButton(items[i])
                    {
                        DisplayStyle = ToolStripItemDisplayStyle.Text,
                        AutoSize = true,
                        Margin = new Padding(0, 1, i == items.Length - 1 ? 8 : 6, 1)
                    };
                    strip.Items.Add(button);
                }
            }

            iconStrip = CreateRibbonStrip();
            ToolStripDropDownButton AddRibbonDropDown(ToolStrip strip, RibbonIcon icon, string title, params string[] items)
            {
                var button = new ToolStripDropDownButton(title)
                {
                    Image = CreateRibbonIcon(icon, CurrentPalette),
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    AutoSize = true,
                    Margin = new Padding(2, 1, 6, 1),
                    ImageScaling = ToolStripItemImageScaling.None
                };

                foreach (var item in items)
                    button.DropDownItems.Add(item);

                strip.Items.Add(button);
                ribbonIconItems.Add((button, icon));
                return button;
            }

            AddRibbonDropDown(iconStrip, RibbonIcon.Create, "Create", "Add Frame", "Add Motor", "Add ESC", "Add Battery");
            AddRibbonDropDown(iconStrip, RibbonIcon.Modify, "Modify", "Replace", "Scale", "Align");
            AddRibbonDropDown(iconStrip, RibbonIcon.Move, "Move", "Free Move", "Snap to Grid");
            AddRibbonDropDown(iconStrip, RibbonIcon.Rotate, "Rotate", "Rotate 90°", "Rotate 45°", "Flip");
            AddRibbonDropDown(iconStrip, RibbonIcon.Analyze, "Analyze", "Mass", "CG", "Power", "Thermal");
            AddRibbonDropDown(iconStrip, RibbonIcon.Mass, "Mass", "Compute Mass", "Mass Distribution");
            AddRibbonDropDown(iconStrip, RibbonIcon.Inertia, "Inertia", "Inertia Tensor", "Yaw Inertia");
            AddRibbonDropDown(iconStrip, RibbonIcon.Thermal, "Thermal", "Thermal Map", "ESC Thermal");

            var configRibbon = CreateRibbonStrip();
            AddRibbonGroup(configRibbon, "Profiles", "Load", "Save", "Duplicate", "Reset");
            configRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(configRibbon, "Mappings", "Ports", "Channels", "Failsafe");
            configRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(configRibbon, "Power", "Limits", "Cutoff", "Recovery");

            var softwareRibbon = CreateRibbonStrip();
            AddRibbonGroup(softwareRibbon, "Firmware", "Select", "Flash", "Rollback");
            softwareRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(softwareRibbon, "Modules", "Enable", "Disable", "Update");
            softwareRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(softwareRibbon, "Health", "Audit", "Verify");

            var testingRibbon = CreateRibbonStrip();
            AddRibbonGroup(testingRibbon, "Checks", "Pre-Flight", "Bench", "Sensors");
            testingRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(testingRibbon, "Runs", "Scenario A", "Scenario B", "Custom");
            testingRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(testingRibbon, "Results", "History", "Export");

            var simulationRibbon = CreateRibbonStrip();
            AddRibbonGroup(simulationRibbon, "Scenario", "New", "Load", "Save");
            simulationRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(simulationRibbon, "Environment", "Wind", "Turbulence", "Weather");
            simulationRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(simulationRibbon, "Playback", "Run", "Pause", "Replay");

            var debugRibbon = CreateRibbonStrip();
            AddRibbonGroup(debugRibbon, "Logs", "Open", "Filter", "Clear");
            debugRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(debugRibbon, "Faults", "Inject", "Reset");
            debugRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(debugRibbon, "Recovery", "Safe Mode", "Reboot");

            var protocolsRibbon = CreateRibbonStrip();
            AddRibbonGroup(protocolsRibbon, "Links", "Radio", "Serial", "USB");
            protocolsRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(protocolsRibbon, "Telemetry", "Bind", "Stream", "Inspect");
            protocolsRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(protocolsRibbon, "Adapters", "Enable", "Disable");

            var telemetryRibbon = CreateRibbonStrip();
            AddRibbonGroup(telemetryRibbon, "Live", "Start", "Stop", "Snapshot");
            telemetryRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(telemetryRibbon, "Analytics", "Trends", "Anomalies");
            telemetryRibbon.Items.Add(new ToolStripSeparator());
            AddRibbonGroup(telemetryRibbon, "Export", "CSV", "JSON");

            var modeTabsHost = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(outerGutter, 0, outerGutter, 0) };
            modeTabsHost.Controls.Add(modeTabs);

            ribbonHost = new Panel { Dock = DockStyle.Top, Height = 32 };
            ribbonHost.Controls.Add(iconStrip);

            topToolbarHost = new Panel { Dock = DockStyle.Top, Height = 60 };
            topToolbarHost.Controls.Add(ribbonHost);
            topToolbarHost.Controls.Add(modeTabsHost);
            
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
            var workspaceBody = new Panel
            {
                Dock = DockStyle.Fill
            };
            workspaceBody.Controls.Add(center);
            workspaceBody.Controls.Add(right);
            workspaceBody.Controls.Add(left);
            workspaceHost = new Panel
            {
                Dock = DockStyle.Fill
            };
            workspaceHost.Controls.Add(workspaceBody);
            contentHost = new Panel
            {
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            contentHost.Controls.Add(workspaceHost);
            Controls.Add(contentHost);

            modeWorkspaces["Build"] = workspaceBody;
            modeWorkspaces["Config"] = CreateModePlaceholder(
                "Config Workspace",
                "Profiles, mappings, and system setup.",
                "Vehicle profiles and presets",
                "IO mapping, ports, and channels",
                "Power limits and safety gates",
                "Save + restore configuration sets"
            );
            modeWorkspaces["Software"] = CreateModePlaceholder(
                "Software Workspace",
                "Firmware and runtime management.",
                "Firmware selection and flash tools",
                "Version audit and changelog",
                "Module enable/disable",
                "Dependency health checks"
            );
            modeWorkspaces["Testing"] = CreateModePlaceholder(
                "Testing Workspace",
                "Validation and bench workflows.",
                "Pre-flight checklists",
                "Bench tests and diagnostics",
                "Scenario-driven QA runs",
                "Result history and notes"
            );
            modeWorkspaces["Simulation"] = CreateModePlaceholder(
                "Simulation Workspace",
                "Scenario, environment, and replay.",
                "Environment presets and wind profiles",
                "Flight plan and waypoint sets",
                "Live replay and scrubbing",
                "Metrics overlays and exports"
            );
            modeWorkspaces["Debug"] = CreateModePlaceholder(
                "Debug Workspace",
                "Logs, faults, and recovery tools.",
                "Runtime logs and event tracing",
                "Fault injection toggles",
                "Crash reports and snapshots",
                "Recovery actions and safe mode"
            );
            modeWorkspaces["Protocols"] = CreateModePlaceholder(
                "Protocols Workspace",
                "Comms and telemetry links.",
                "Radio link configuration",
                "Telemetry transport settings",
                "Protocol adapters",
                "Signal health monitoring"
            );
            modeWorkspaces["Telemetry"] = CreateModePlaceholder(
                "Telemetry Workspace",
                "Live data and analytics.",
                "Live stream charts",
                "Performance summaries",
                "Export and sharing",
                "Alert rules and thresholds"
            );

            modeRibbons["Build"] = iconStrip;
            modeRibbons["Config"] = configRibbon;
            modeRibbons["Software"] = softwareRibbon;
            modeRibbons["Testing"] = testingRibbon;
            modeRibbons["Simulation"] = simulationRibbon;
            modeRibbons["Debug"] = debugRibbon;
            modeRibbons["Protocols"] = protocolsRibbon;
            modeRibbons["Telemetry"] = telemetryRibbon;

            void ShowRibbon(string mode)
            {
                if (ribbonHost == null) return;
                ribbonHost.SuspendLayout();
                ribbonHost.Controls.Clear();
                if (modeRibbons.TryGetValue(mode, out var strip))
                    ribbonHost.Controls.Add(strip);
                ribbonHost.ResumeLayout(true);
                ApplyTheme();
            }

            void ShowWorkspace(string mode)
            {
                if (workspaceHost == null) return;
                workspaceHost.SuspendLayout();
                workspaceHost.Controls.Clear();
                if (modeWorkspaces.TryGetValue(mode, out var view))
                    workspaceHost.Controls.Add(view);
                else
                    workspaceHost.Controls.Add(workspaceBody);
                workspaceHost.ResumeLayout(true);
            }

            modeTabs.SelectedIndexChanged += (_, __) =>
            {
                if (modeTabs.SelectedTab == null) return;
                string mode = modeTabs.SelectedTab.Text;
                ShowWorkspace(mode);
                ShowRibbon(mode);
                ApplySimulationAutomation(mode);
            };
            ShowWorkspace("Build");
            ShowRibbon("Build");
            ApplySimulationAutomation("Build");
            BuildCustomTitleBar();
            Controls.Add(topToolbarHost);

            // STATUS STRIP
            statusStrip = new StatusStrip
            {
                SizingGrip = false,
                Padding = new Padding(outerGutter, 4, outerGutter, 4),
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = toolStripRenderer
            };
            workspaceStatus = new ToolStripStatusLabel("Workspace: Assemble");
            modeStatus = new ToolStripStatusLabel(" | Mode: Manual");
            firmwareStatus = new ToolStripStatusLabel(" | Firmware: Betaflight");
            sensorsStatus = new ToolStripStatusLabel(" | Sensors: Nominal");
            statusSpacer = new ToolStripStatusLabel { Spring = true };
            errorsStatus = new ToolStripStatusLabel("Errors: 0");
            simReadyStatus = new ToolStripStatusLabel(" | Simulation Ready: No");
            statusStrip.Items.AddRange(new ToolStripItem[]
            {
                workspaceStatus,
                modeStatus,
                firmwareStatus,
                sensorsStatus,
                statusSpacer,
                errorsStatus,
                simReadyStatus
            });
            Controls.Add(statusStrip);
            titleBarPanel.BringToFront();
            topToolbarHost.BringToFront();
            statusStrip.BringToFront();
            Resize += (_, __) =>
            {
                LayoutRootPanels();
                UpdateTitleMaximizeButton();
            };
            titleBarPanel.SizeChanged += (_, __) => LayoutRootPanels();
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
                            var snapshot = CaptureUndoSnapshot();
                            project.Instances.Remove(p);
                            CommitUndoSnapshot(snapshot);
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
                            selected = p;
                        CopySelectedPart();
                    });

                    var pasteItem = new ToolStripMenuItem("Paste", null, (_,__) =>
                    {
                        PasteClipboardPart();
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
                    var category = n.Parent.Tag as string ?? n.Parent.Text;
                    var asset = n.Tag as Asset;
                    dragging = new LibraryPart { Category = category, Name = n.Text, AssetId = asset?.Id ?? "" };
                    DoDragDrop(dragging, DragDropEffects.Copy);
                }
            };
            libraryTree.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node == null || e.Node.Parent == null) return;
                if (project == null) return;
                var category = e.Node.Parent.Tag as string ?? e.Node.Parent.Text;
                pendingAddAssetId = null;
                if (category == "Frames")
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

                if (category == "Motors")
                {
                    // next click in viewport places motor
                    pendingAddMode = PartType.Motor;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "Batteries")
                {
                    pendingAddMode = PartType.Battery;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "ESC")
                {
                    pendingAddMode = PartType.ESC;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "FC")
                {
                    pendingAddMode = PartType.FlightController;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "Props")
                {
                    pendingAddMode = PartType.Propeller;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "Cameras")
                {
                    pendingAddMode = PartType.Camera;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "Receivers")
                {
                    pendingAddMode = PartType.Receiver;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "GPS")
                {
                    pendingAddMode = PartType.GPS;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "VTX")
                {
                    pendingAddMode = PartType.VTX;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "Antennas")
                {
                    pendingAddMode = PartType.Antenna;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "Buzzers")
                {
                    pendingAddMode = PartType.Buzzer;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (category == "LEDs")
                {
                    pendingAddMode = PartType.LED;
                    pendingAddName = e.Node?.Text;
                    viewport.Cursor = Cursors.Cross;
                    return;
                }

                if (IsCustomCategory(category))
                {
                    pendingAddMode = PartType.CustomComponent;
                    pendingAddName = e.Node?.Text;
                    pendingAddAssetId = (e.Node?.Tag as Asset)?.Id;
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
                Text = "Active Build",
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

            var frameTuningPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                Padding = new Padding(6, 6, 6, 4),
                BackColor = Color.Transparent
            };

            var tuningTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            tuningTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            tuningTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tuningTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
            tuningTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            frameArmUnitSelector = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            frameArmUnitSelector.Items.AddRange(new object[] { "mm", "cm", "in" });
            frameArmUnitSelector.SelectedIndex = 0;

            frameArmLengthInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 20,
                Maximum = 1000,
                DecimalPlaces = 1,
                Increment = 1,
                TextAlign = HorizontalAlignment.Right
            };

            frameBodyUnitSelector = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            frameBodyUnitSelector.Items.AddRange(new object[] { "mm", "cm", "in" });
            frameBodyUnitSelector.SelectedIndex = 0;

            frameBodySizeInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 20,
                Maximum = 250,
                DecimalPlaces = 1,
                Increment = 1,
                TextAlign = HorizontalAlignment.Right
            };

            var escLayoutLabel = new Label
            {
                Text = "ESC Layout",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            escLayoutSelector = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            escLayoutSelector.Items.AddRange(new object[] { "4-in-1", "4x Arms" });
            escLayoutSelector.SelectedIndex = escLayout == EscLayout.FourInOne ? 0 : 1;

            frameArmUnitSelector.SelectedIndexChanged += (_, __) => UpdateFrameTuningUi();
            frameArmLengthInput.ValueChanged += (_, __) =>
            {
                if (suppressFrameUiEvents) return;
                var frameAsset = GetFrameAsset();
                if (frameAsset == null) return;
                var unit = frameArmUnitSelector.SelectedItem?.ToString() ?? "mm";
                float mm = ArmLengthToMm(frameArmLengthInput.Value, unit);
                ApplyArmLength(frameAsset, mm);
                viewport.Invalidate();
                UpdateStatusBar();
            };

            frameBodyUnitSelector.SelectedIndexChanged += (_, __) => UpdateFrameTuningUi();
            frameBodySizeInput.ValueChanged += (_, __) =>
            {
                if (suppressFrameUiEvents) return;
                var frameAsset = GetFrameAsset();
                if (frameAsset == null) return;
                var unit = frameBodyUnitSelector.SelectedItem?.ToString() ?? "mm";
                float mm = ArmLengthToMm(frameBodySizeInput.Value, unit);
                ApplyBodySize(frameAsset, mm);
                viewport.Invalidate();
                UpdateStatusBar();
            };

            escLayoutSelector.SelectedIndexChanged += (_, __) =>
            {
                if (suppressFrameUiEvents) return;
                if (escLayoutSelector.SelectedIndex < 0) return;
                var desired = escLayoutSelector.SelectedIndex == 0 ? EscLayout.FourInOne : EscLayout.Arms;
                if (desired == escLayout) return;
                ApplyEscLayout(desired);
            };

            tuningTable.Controls.Add(escLayoutLabel, 0, 0);
            tuningTable.Controls.Add(escLayoutSelector, 1, 0);
            tuningTable.SetColumnSpan(escLayoutSelector, 2);

            frameTuningPanel.Controls.Add(tuningTable);
            layersPanel.Controls.Add(frameTuningPanel);
            UpdateFrameTuningUi();

            var searchBox = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Search components..." };
            searchBox.TextChanged += (_, __) => BuildLibrary(searchBox.Text);

            var partsLabel = new Label
            {
                Text = "Component Library",
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
            partsPanel.Controls.Add(searchBox);
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

                var category = e.Node.Parent.Tag as string ?? e.Node.Parent.Text;
                string info = GetPartInfo(category, e.Node.Text);
                partToolTip.SetToolTip(libraryTree, info);
            };

            Panel CreateMetricRow(string labelText, out Label valueLabel)
            {
                var row = new Panel { Dock = DockStyle.Top, Height = 34 };
                var label = new Label
                {
                    Dock = DockStyle.Left,
                    Width = 230,
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

            Label CreateSectionHeader(string text)
            {
                return new Label
                {
                    Text = text,
                    Dock = DockStyle.Top,
                    Height = 26,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(2, 4, 0, 0),
                    Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
                };
            }

            var buildHealthCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                BorderThickness = 1,
                Padding = new Padding(10)
            };
            var buildHealthHeader = new Label
            {
                Text = "BUILD HEALTH",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
            };
            var buildHealthBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 8, 4, 4),
                AutoScroll = true
            };

            var perfHeader = CreateSectionHeader("Performance");
            var perf1 = CreateMetricRow("Thrust-to-Weight", out twrValueLabel);
            var perf2 = CreateMetricRow("Hover Throttle", out hoverValueLabel);
            var perf3 = CreateMetricRow("Hover Thrust Required", out thrustRequiredValueLabel);
            var perf4 = CreateMetricRow("Max Available Thrust", out maxThrustValueLabel);
            var perf5 = CreateMetricRow("Thrust Margin", out thrustMarginValueLabel);
            var perf6 = CreateMetricRow("Voltage Sag", out sagValueLabel);
            var perf7 = CreateMetricRow("Motor Temp (Predicted @ Hover)", out tempValueLabel);
            var perf8 = CreateMetricRow("Max Limit", out motorTempLimitLabel);

            var powerHeader = CreateSectionHeader("Power Integrity");
            var power1 = CreateMetricRow("Battery Max Continuous Current", out batteryMaxCurrentLabel);
            var power2 = CreateMetricRow("Estimated Hover Current", out powerHoverCurrentLabel);
            var power3 = CreateMetricRow("Peak Estimated Current", out peakCurrentLabel);
            var power4 = CreateMetricRow("ESC Rating", out escRatingLabel);

            stabilityHeaderLabel = CreateSectionHeader("Stability");
            cgRowPanel = CreateMetricRow("CG Offset", out cgValueLabel);
            rollInertiaRowPanel = CreateMetricRow("Roll Inertia", out rollInertiaValueLabel);
            pitchInertiaRowPanel = CreateMetricRow("Pitch Inertia", out pitchInertiaValueLabel);
            yawInertiaRowPanel = CreateMetricRow("Yaw Inertia", out yawInertiaValueLabel);
            yawStabilityRowPanel = CreateMetricRow("Yaw Stability", out yawValueLabel);

            var payloadHeader = CreateSectionHeader("Payload Capacity");
            var payload1 = CreateMetricRow("Max Payload (Safe)", out payloadMaxLabel);
            var payload2 = CreateMetricRow("Current Payload", out payloadCurrentLabel);
            var payload3 = CreateMetricRow("Remaining Capacity", out payloadRemainingLabel);

            var massHeaderPanel = new Panel { Dock = DockStyle.Top, Height = 26 };
            var massHeaderLabel = new Label
            {
                Text = "Mass Breakdown",
                Dock = DockStyle.Left,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 4, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
            };
            var massToggleButton = new Button
            {
                Name = "btnMassToggle",
                Text = "Hide",
                Dock = DockStyle.Right,
                Width = 54,
                Height = 22,
                FlatStyle = FlatStyle.Flat
            };
            massToggleButton.FlatAppearance.BorderSize = 0;
            massHeaderPanel.Controls.Add(massToggleButton);
            massHeaderPanel.Controls.Add(massHeaderLabel);

            var massBody = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            var mass1 = CreateMetricRow("Frame", out massFrameLabel);
            var mass2 = CreateMetricRow("Motors", out massMotorsLabel);
            var mass3 = CreateMetricRow("ESC", out massEscLabel);
            var mass4 = CreateMetricRow("Battery", out massBatteryLabel);
            var mass5 = CreateMetricRow("Payload", out massPayloadLabel);
            massBody.Controls.Add(mass5);
            massBody.Controls.Add(mass4);
            massBody.Controls.Add(mass3);
            massBody.Controls.Add(mass2);
            massBody.Controls.Add(mass1);

            massToggleButton.Click += (_, __) =>
            {
                massBody.Visible = !massBody.Visible;
                massToggleButton.Text = massBody.Visible ? "Hide" : "Show";
            };

            var alertHeader = CreateSectionHeader("Alerts");
            var alert1 = CreateMetricRow("Missing Components", out missingValueLabel);
            var alert2 = CreateMetricRow("Overcurrent Risk", out overcurrentValueLabel);
            var alert3 = CreateMetricRow("Overheat Risk", out overheatValueLabel);
            var alert4 = CreateMetricRow("Structural Risk", out structuralValueLabel);

            buildHealthBody.Controls.Add(alert4);
            buildHealthBody.Controls.Add(alert3);
            buildHealthBody.Controls.Add(alert2);
            buildHealthBody.Controls.Add(alert1);
            buildHealthBody.Controls.Add(alertHeader);
            buildHealthBody.Controls.Add(massBody);
            buildHealthBody.Controls.Add(massHeaderPanel);
            buildHealthBody.Controls.Add(payload3);
            buildHealthBody.Controls.Add(payload2);
            buildHealthBody.Controls.Add(payload1);
            buildHealthBody.Controls.Add(payloadHeader);
            buildHealthBody.Controls.Add(yawStabilityRowPanel);
            buildHealthBody.Controls.Add(yawInertiaRowPanel);
            buildHealthBody.Controls.Add(pitchInertiaRowPanel);
            buildHealthBody.Controls.Add(rollInertiaRowPanel);
            buildHealthBody.Controls.Add(cgRowPanel);
            buildHealthBody.Controls.Add(stabilityHeaderLabel);
            buildHealthBody.Controls.Add(power4);
            buildHealthBody.Controls.Add(power3);
            buildHealthBody.Controls.Add(power2);
            buildHealthBody.Controls.Add(power1);
            buildHealthBody.Controls.Add(powerHeader);
            buildHealthBody.Controls.Add(perf8);
            buildHealthBody.Controls.Add(perf7);
            buildHealthBody.Controls.Add(perf6);
            buildHealthBody.Controls.Add(perf5);
            buildHealthBody.Controls.Add(perf4);
            buildHealthBody.Controls.Add(perf3);
            buildHealthBody.Controls.Add(perf2);
            buildHealthBody.Controls.Add(perf1);
            buildHealthBody.Controls.Add(perfHeader);

            buildHealthCard.Controls.Add(buildHealthBody);
            buildHealthCard.Controls.Add(buildHealthHeader);
            right.Controls.Add(buildHealthCard);




            // VIEWPORT
            viewport = new BufferedPictureBox { Dock = DockStyle.Fill, AllowDrop = true };
            viewport.Paint += DrawViewport;
            viewport.DragEnter += (_, e) => e.Effect = DragDropEffects.Copy;
            viewport.MouseWheel += (s, e) =>
            {
                var mouseWorld = ScreenToWorld(e.Location);
                float newZoom = zoomFactor * (e.Delta > 0 ? 1.1f : 0.9f);
                zoomFactor = Math.Clamp(newZoom, 0.1f, 10.0f);

                viewOffset = new PointF(
                    e.Location.X - mouseWorld.X * zoomFactor,
                    e.Location.Y - mouseWorld.Y * zoomFactor
                );

                viewport.Invalidate();
            };
            viewport.DragDrop += (_, e) =>
            {
                if (dragging == null || project == null) return;
                var snapshot = CaptureUndoSnapshot();
                var p = ScreenToWorld(viewport.PointToClient(new Point(e.X, e.Y)));
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

                    CommitUndoSnapshot(snapshot);
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
                else if (dragging.Category == "ESC")
                {
                    added = AddEsc(pt, dragging.Name);
                }
                else if (dragging.Category == "FC")
                {
                    added = AddFlightController(pt, dragging.Name);
                }
                else if (dragging.Category == "Props")
                {
                    added = AddPropeller(pt, dragging.Name);
                }
                else if (dragging.Category == "Cameras")
                {
                    added = AddCamera(pt, dragging.Name);
                }
                else if (dragging.Category == "Receivers")
                {
                    added = AddReceiver(pt, dragging.Name);
                }
                else if (dragging.Category == "GPS")
                {
                    added = AddGps(pt, dragging.Name);
                }
                else if (dragging.Category == "VTX")
                {
                    added = AddVtx(pt, dragging.Name);
                }
                else if (dragging.Category == "Antennas")
                {
                    added = AddAntenna(pt, dragging.Name);
                }
                else if (dragging.Category == "Buzzers")
                {
                    added = AddBuzzer(pt, dragging.Name);
                }
                else if (dragging.Category == "LEDs")
                {
                    added = AddLed(pt, dragging.Name);
                }
                else if (IsCustomCategory(dragging.Category))
                {
                    added = AddCustomComponent(pt, dragging.Name, dragging.AssetId);
                }

                if (added)
                {
                    CommitUndoSnapshot(snapshot);
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
            viewportCard.Controls.Add(viewport);

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
                ColumnCount = 5,
                RowCount = 1
            };
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

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

            allUpWeightLabel = CreateMetricLabel("All-Up Weight: --");
            totalThrustLabel = CreateMetricLabel("Total Thrust: --");
            hoverCurrentLabel = CreateMetricLabel("Hover Current: --");
            efficiencyIndexLabel = CreateMetricLabel("Power-to-Weight Efficiency Score: --");
            stabilityIndexLabel = CreateMetricLabel("Stability Index: --");

            metricsTable.Controls.Add(allUpWeightLabel, 0, 0);
            metricsTable.Controls.Add(totalThrustLabel, 1, 0);
            metricsTable.Controls.Add(hoverCurrentLabel, 2, 0);
            metricsTable.Controls.Add(efficiencyIndexLabel, 3, 0);
            metricsTable.Controls.Add(stabilityIndexLabel, 4, 0);
            quickMetricsCard.Controls.Add(metricsTable);

            var actionPanel = new Panel { Dock = DockStyle.Fill };
            var actionButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };

            exportConfigButton = new Button { Name = "btnExportConfig", Text = "Export Config", Width = 150, Height = 38 };
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

            saveBuildButton = new Button { Name = "btnSaveBuild", Text = "Save Build", Width = 150, Height = 38 };
            saveBuildButton.Click += (_, __) => SaveProject();

            actionButtons.Controls.Add(saveBuildButton);
            actionButtons.Controls.Add(exportConfigButton);
            actionPanel.Controls.Add(actionButtons);
            actionPanel.SizeChanged += (_, __) =>
            {
                actionButtons.Left = Math.Max(0, (actionPanel.Width - actionButtons.Width) / 2);
                actionButtons.Top = Math.Max(0, (actionPanel.Height - actionButtons.Height) / 2);
            };

            centerLayout.Controls.Add(viewportCard, 0, 0);
            centerLayout.Controls.Add(quickMetricsCard, 0, 1);
            centerLayout.Controls.Add(actionPanel, 0, 2);

            viewport.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Middle)
                {
                    isPanning = false;
                    viewport.Cursor = Cursors.Default;
                }

                if (draggingFrame)
                {
                    draggingFrame = false;
                    dirty = true;
                    UpdateStatusBar();
                    viewport.Cursor = Cursors.Default;
                }
            };

            viewport.MouseMove += (s, e) =>
            {
                lastMouseScreen = e.Location;
                var worldPos = ScreenToWorld(e.Location);
                mousePos = new Point((int)worldPos.X, (int)worldPos.Y);

                if (isPanning)
                {
                    viewOffset.X += e.Location.X - lastMousePos.X;
                    viewOffset.Y += e.Location.Y - lastMousePos.Y;
                    lastMousePos = e.Location;
                    viewport.Invalidate();
                    return;
                }
                
                if (draggingFrame && project != null)
                {
                    var frame = GetFrame();
                    if (frame != null)
                    {
                        var newPos = new PointF(worldPos.X - frameDragOffset.X, worldPos.Y - frameDragOffset.Y);
                        var delta = new PointF(newPos.X - frame.Position.X, newPos.Y - frame.Position.Y);
                        frame.Position = newPos;
                        foreach (var p in project.Instances)
                        {
                            if (p.Type == PartType.Battery ||
                                p.Type == PartType.FlightController ||
                                p.Type == PartType.Camera ||
                                p.Type == PartType.Receiver ||
                                p.Type == PartType.GPS ||
                                p.Type == PartType.VTX ||
                                p.Type == PartType.Antenna ||
                                p.Type == PartType.Buzzer ||
                                p.Type == PartType.LED)
                            {
                                p.Position = new PointF(p.Position.X + delta.X, p.Position.Y + delta.Y);
                            }
                        }
                        viewport.Invalidate();
                    }
                    return;
                }
                if (pendingAddMode != null)
                    viewport.Invalidate();
                else
                    viewport.Cursor = (project != null && HitTestFrame(worldPos)) ? Cursors.SizeAll : Cursors.Default;
            };

            viewport.MouseDown += (s, e) =>
            { 
                if (e.Button == MouseButtons.Middle)
                {
                    isPanning = true;
                    lastMousePos = e.Location;
                    viewport.Cursor = Cursors.NoMove2D;
                    return;
                }

                var worldPos = ScreenToWorld(e.Location);
                // Right-click deletes non-frame parts under the cursor
                if (e.Button == MouseButtons.Right && project != null)
                {
                    selected = HitTestPart(worldPos);
                    ShowViewportContextMenu(e.Location);
                    return;
                }

                // Left-click: if user selected a part in library, place it now
                if (e.Button == MouseButtons.Left && project != null && pendingAddMode != null)
                {
                    var snapshot = CaptureUndoSnapshot();
                    if (pendingAddMode == PartType.Motor)
                    {
                        bool added = AddMotor(worldPos, pendingAddName ?? "Motor");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.Battery)
                    {
                        bool added = AddBattery(worldPos, pendingAddName ?? "Battery");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.ESC)
                    {
                        bool added = AddEsc(worldPos, pendingAddName ?? "ESC");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.FlightController)
                    {
                        bool added = AddFlightController(worldPos, pendingAddName ?? "Flight Controller");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.Propeller)
                    {
                        bool added = AddPropeller(worldPos, pendingAddName ?? "Propeller");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.Camera)
                    {
                        bool added = AddCamera(worldPos, pendingAddName ?? "Camera");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.Receiver)
                    {
                        bool added = AddReceiver(worldPos, pendingAddName ?? "Receiver");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.GPS)
                    {
                        bool added = AddGps(worldPos, pendingAddName ?? "GPS");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.VTX)
                    {
                        bool added = AddVtx(worldPos, pendingAddName ?? "VTX");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.Antenna)
                    {
                        bool added = AddAntenna(worldPos, pendingAddName ?? "Antenna");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.Buzzer)
                    {
                        bool added = AddBuzzer(worldPos, pendingAddName ?? "Buzzer");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.LED)
                    {
                        bool added = AddLed(worldPos, pendingAddName ?? "LED");
                        pendingAddMode = null;
                        pendingAddName = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                    else if (pendingAddMode == PartType.CustomComponent)
                    {
                        bool added = AddCustomComponent(worldPos, pendingAddName ?? "Custom Component", pendingAddAssetId);
                        pendingAddMode = null;
                        pendingAddName = null;
                        pendingAddAssetId = null;
                        viewport.Cursor = Cursors.Default;
                        if (added)
                        {
                            CommitUndoSnapshot(snapshot);
                            OnProjectStructureChanged();
                        }
                        return;
                    }
                }

                if (e.Button == MouseButtons.Left && project != null && pendingAddMode == null && HitTestFrame(worldPos))
                {
                    var frame = GetFrame();
                    if (frame != null)
                    {
                        if (!dragUndoCaptured)
                        {
                            CommitUndoSnapshot(CaptureUndoSnapshot());
                            dragUndoCaptured = true;
                        }
                        draggingFrame = true;
                        frameDragOffset = new PointF(worldPos.X - frame.Position.X, worldPos.Y - frame.Position.Y);
                        viewport.Cursor = Cursors.SizeAll;
                    }
                }
            };
            
            viewport.MouseUp += (_, __) =>
            {
                if (draggingFrame)
                {
                    draggingFrame = false;
                    dragUndoCaptured = false;
                    dirty = true;
                    UpdateStatusBar();
                    viewport.Cursor = Cursors.Default;
                }
            };

            renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
            renderTimer.Tick += (_, __) =>
            {
                if (!IsDisposed && IsHandleCreated && Visible)
                {
                    if (autoSimEnabled && project != null)
                        AdvanceSimulation();
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

        void BuildCustomTitleBar()
        {
            titleBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(6, 2, 6, 2)
            };

            titleLogo = new PictureBox
            {
                Dock = DockStyle.Left,
                Width = 88,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0)
            };
            if (logoImage != null)
                titleLogo.Image = logoImage;

            titleButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 138,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            titleCloseButton = CreateTitleButton("btnWindowClose", "X");
            titleCloseButton.Click += (_, __) => Close();
            titleMaxButton = CreateTitleButton("btnWindowMax", "□");
            titleMaxButton.Click += (_, __) => ToggleMaximizeRestore();
            titleMinButton = CreateTitleButton("btnWindowMin", "—");
            titleMinButton.Click += (_, __) => WindowState = FormWindowState.Minimized;

            titleCloseButton.Margin = new Padding(0);
            titleMaxButton.Margin = new Padding(0);
            titleMinButton.Margin = new Padding(0);
            titleButtonPanel.Controls.Add(titleCloseButton);
            titleButtonPanel.Controls.Add(titleMaxButton);
            titleButtonPanel.Controls.Add(titleMinButton);

            titleMenuHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 0, 8, 0)
            };
            titleMenuHost.Controls.Add(menu);
            
            titleBarPanel.Controls.Add(titleMenuHost);
            titleBarPanel.Controls.Add(titleButtonPanel);
            titleBarPanel.Controls.Add(titleLogo);
            Controls.Add(titleBarPanel);

            WireTitleDrag(titleBarPanel);
            WireTitleDrag(titleLogo);
            WireTitleDrag(titleMenuHost);
            WireMenuDrag(menu);
            UpdateTitleMaximizeButton();
        }

        void WireMenuDrag(ToolStrip strip)
        {
            if (strip == null) return;
            strip.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                if (strip.GetItemAt(e.Location) != null) return; // allow normal menu clicks
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            };
        }

        Button CreateTitleButton(string name, string text)
        {
            return new Button
            {
                Name = name,
                Text = text,
                Width = 46,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                TabStop = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
                BackColor = SystemColors.Control,
                ForeColor = SystemColors.ControlText
            };
        }

        void WireTitleDrag(Control control)
        {
            control.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            };
            control.DoubleClick += (_, __) => ToggleMaximizeRestore();
        }

        void ToggleMaximizeRestore()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            UpdateTitleMaximizeButton();
        }

        void UpdateTitleMaximizeButton()
        {
            if (titleMaxButton == null || titleMaxButton.IsDisposed) return;
            titleMaxButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateMaximizedBounds();
            EnableWindowEffects();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateMaximizedBounds();
            Invalidate();
        }

        void UpdateMaximizedBounds()
        {
            var screen = Screen.FromHandle(Handle);
            MaximizedBounds = screen.WorkingArea;
        }

        void EnableWindowEffects()
        {
            try
            {
                // Rounded corners / native frame rendering hint for modern Windows.
                int cornerPref = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(Handle, 33, ref cornerPref, sizeof(int));

                var shadowMargins = new MARGINS { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                DwmExtendFrameIntoClientArea(Handle, ref shadowMargins);
            }
            catch
            {
                // Keep working on older Windows without DWM attributes.
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && FormBorderStyle == FormBorderStyle.None && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    Point p = PointToClient(LParamToPoint(m.LParam));
                    bool left = p.X <= RESIZE_BORDER;
                    bool right = p.X >= Width - RESIZE_BORDER;
                    bool top = p.Y <= RESIZE_BORDER;
                    bool bottom = p.Y >= Height - RESIZE_BORDER;

                    if (left && top) m.Result = (IntPtr)HTTOPLEFT;
                    else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }

            base.WndProc(ref m);
        }

        static Point LParamToPoint(IntPtr lParam)
        {
            int value = lParam.ToInt32();
            return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (WindowState == FormWindowState.Maximized) return;

            using var pen = new Pen(darkMode ? Color.FromArgb(54, 67, 92) : Color.FromArgb(184, 194, 214));
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
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
            tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            tree.DrawNode -= DrawTreeNode;
            tree.DrawNode += DrawTreeNode;
        }

        void DrawTreeNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (sender is not TreeView tree) return;
            if (e.Node == null) return;
            var palette = CurrentPalette;
            var bounds = new Rectangle(0, e.Bounds.Top, tree.Width, e.Bounds.Height);
            bool selected = e.Node.IsSelected;
            Color back = selected ? palette.SurfaceAlt : palette.Surface;
            Color text = palette.TextPrimary;

            using var backBrush = new SolidBrush(back);
            e.Graphics.FillRectangle(backBrush, bounds);

            var font = e.Node.NodeFont ?? tree.Font;
            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                font,
                new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height),
                text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left
            );
        }

        void DrawModeTabs(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabs) return;
            var palette = CurrentPalette;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bounds = e.Bounds;
            bounds.Inflate(-2, -2);
            bounds.Height += 2;

            using var path = BuildRoundedPath(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), 6f);
            using var backBrush = new SolidBrush(selected ? palette.SurfaceAlt : palette.WindowBackground);
            using var borderPen = new Pen(palette.Border);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(backBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            string text = tabs.TabPages[e.Index].Text;
            var textColor = selected ? palette.TextPrimary : palette.TextMuted;
            TextRenderer.DrawText(
                e.Graphics,
                text,
                tabs.Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            );
        }

        void DrawModeTabsBackground(object? sender, PaintEventArgs e)
        {
            if (sender is not TabControl tabs) return;
            var palette = CurrentPalette;
            using var backBrush = new SolidBrush(palette.WindowBackground);
            e.Graphics.FillRectangle(backBrush, tabs.ClientRectangle);
        }

        void SetSimulationMode(SimulationMode mode)
        {
            simulationMode = mode;
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
            UpdateStatusBar();
        }

        void SetFirmwareProfile(FirmwareProfile profile)
        {
            firmwareProfile = profile;
            UpdateStatusBar();
        }

        void SetSensorProfile(SensorProfile profile)
        {
            sensorProfile = profile;
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
        }

        void LayoutRootPanels()
        {
            if (contentHost == null || contentHost.IsDisposed) return;
            if (titleBarPanel == null || titleBarPanel.IsDisposed) return;
            if (topToolbarHost == null || topToolbarHost.IsDisposed) return;
            if (statusStrip == null || statusStrip.IsDisposed) return;
            
            topToolbarHost.Top = titleBarPanel.Bottom;

            int top = topToolbarHost.Bottom + 2;
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
                Path.Combine(baseDir, "design", "mockups", "logo.png"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "design", "mockups", "logo.png")),
                Path.Combine(baseDir, "logo.png"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "logo.png"))
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate)) // Original line for silvu-logo.png is removed, now it checks for logo.png
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
                menu.BackColor = palette.WindowBackground; // Match title bar background
                menu.ForeColor = palette.TextPrimary;
                menu.Renderer = toolStripRenderer;
                ApplyToolStripTheme(menu.Items, palette);
            }

            if (titleBarPanel != null)
            {
                titleBarPanel.BackColor = palette.WindowBackground;
            }
            if (titleMenuHost != null)
            {
                titleMenuHost.BackColor = palette.WindowBackground;
            }
            if (titleButtonPanel != null)
            {
                titleButtonPanel.BackColor = palette.WindowBackground;
            }
            if (titleLogo != null)
            {
                titleLogo.BackColor = palette.WindowBackground;
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

            ApplyWorkspaceStripTheme();
            ApplyScrollTheme();
            UpdateRibbonIcons();

            // Apply themed colors to title bar buttons
            if (titleMinButton != null) UpdateTitleButtonColors(titleMinButton, palette);
            if (titleMaxButton != null) UpdateTitleButtonColors(titleMaxButton, palette);
            if (titleCloseButton != null)
            {
                titleCloseButton.ForeColor = palette.TextPrimary;
                titleCloseButton.BackColor = palette.WindowBackground;
                titleCloseButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 76, 61);
                titleCloseButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(210, 56, 43);
            }

            viewport?.Invalidate();
            Invalidate(true);
        }

        void UpdateRibbonIcons()
        {
            var palette = CurrentPalette;
            foreach (var (item, icon) in ribbonIconItems)
            {
                if (item is ToolStripDropDownButton dd)
                {
                    dd.Image?.Dispose();
                    dd.Image = CreateRibbonIcon(icon, palette);
                }
            }
        }

        Image CreateRibbonIcon(RibbonIcon icon, UiPalette palette)
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var pen = new Pen(palette.TextMuted, 1.4f);
            using var brush = new SolidBrush(palette.TextMuted);

            switch (icon)
            {
                case RibbonIcon.Create:
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                    g.DrawLine(pen, 8, 4, 8, 12);
                    g.DrawLine(pen, 4, 8, 12, 8);
                    break;
                case RibbonIcon.Modify:
                    g.DrawPolygon(pen, new[] { new Point(3, 12), new Point(7, 4), new Point(13, 12) });
                    break;
                case RibbonIcon.Move:
                    g.DrawLine(pen, 8, 2, 8, 14);
                    g.DrawLine(pen, 2, 8, 14, 8);
                    g.FillPolygon(brush, new[] { new Point(8, 1), new Point(6, 4), new Point(10, 4) });
                    g.FillPolygon(brush, new[] { new Point(8, 15), new Point(6, 12), new Point(10, 12) });
                    g.FillPolygon(brush, new[] { new Point(1, 8), new Point(4, 6), new Point(4, 10) });
                    g.FillPolygon(brush, new[] { new Point(15, 8), new Point(12, 6), new Point(12, 10) });
                    break;
                case RibbonIcon.Rotate:
                    g.DrawArc(pen, 2, 2, 12, 12, 30, 300);
                    g.FillPolygon(brush, new[] { new Point(12, 2), new Point(14, 6), new Point(10, 5) });
                    break;
                case RibbonIcon.Analyze:
                    g.DrawRectangle(pen, 3, 3, 10, 10);
                    g.DrawLine(pen, 4, 11, 7, 8);
                    g.DrawLine(pen, 7, 8, 9, 10);
                    g.DrawLine(pen, 9, 10, 12, 6);
                    break;
                case RibbonIcon.Mass:
                    g.FillRectangle(brush, 4, 8, 8, 5);
                    g.DrawRectangle(pen, 4, 8, 8, 5);
                    g.DrawLine(pen, 6, 8, 10, 8);
                    break;
                case RibbonIcon.Inertia:
                    g.DrawEllipse(pen, 3, 3, 10, 10);
                    g.DrawEllipse(pen, 6, 6, 4, 4);
                    break;
                case RibbonIcon.Thermal:
                    g.DrawLine(pen, 8, 2, 8, 10);
                    g.DrawEllipse(pen, 6, 10, 4, 4);
                    break;
            }

            return bmp;
        }

        void ApplyScrollTheme()
        {
            if (projectTree != null) ApplyDarkScrollbars(projectTree);
            if (libraryTree != null) ApplyDarkScrollbars(libraryTree);
            if (featureTabs != null) ApplyDarkScrollbars(featureTabs);
        }

        void ApplyDarkScrollbars(Control control)
        {
            try
            {
                if (!control.IsHandleCreated)
                {
                    control.HandleCreated += (_, __) => ApplyDarkScrollbars(control);
                    return;
                }
                if (darkMode)
                    SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
                else
                    SetWindowTheme(control.Handle, "Explorer", null);
            }
            catch
            {
                // ignore if OS doesn't support theme hints
            }
        }

        // Helper method to update common title button colors
        void UpdateTitleButtonColors(Button button, UiPalette palette)
        {
            button.ForeColor = palette.TextPrimary;
            button.BackColor = palette.WindowBackground;
            button.FlatAppearance.MouseOverBackColor = palette.Surface;
            button.FlatAppearance.MouseDownBackColor = palette.SurfaceAlt;
            button.FlatAppearance.BorderSize = 0; // Ensure no border
            button.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point); // Ensure consistent font
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
                    tree.BackColor = palette.Surface;
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
                else if (child is ToolStrip strip)
                {
                    bool isTitleMenu = strip == menu;
                    strip.BackColor = isTitleMenu ? palette.WindowBackground : palette.Surface;
                    strip.ForeColor = palette.TextPrimary; // Always use palette.TextPrimary for toolstrips
                    strip.Renderer = toolStripRenderer;
                    ApplyToolStripTheme(strip.Items, palette);
                }
                else if (child is TabControl tabs)
                {
                    tabs.BackColor = palette.WindowBackground;
                    tabs.ForeColor = palette.TextPrimary;
                }
                else if (child is TabPage tabPage)
                {
                    tabPage.UseVisualStyleBackColor = false;
                    tabPage.BackColor = palette.WindowBackground;
                    tabPage.ForeColor = palette.TextPrimary;
                }
                else if (child is TextBox textBox)
                {
                    textBox.BackColor = palette.SurfaceAlt;
                    textBox.ForeColor = palette.TextPrimary;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (child is NumericUpDown numeric)
                {
                    numeric.BackColor = palette.SurfaceAlt;
                    numeric.ForeColor = palette.TextPrimary;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (child is ComboBox combo)
                {
                    combo.BackColor = palette.SurfaceAlt;
                    combo.ForeColor = palette.TextPrimary;
                    combo.FlatStyle = FlatStyle.Flat;
                }
                else if (child is CheckBox check)
                {
                    check.BackColor = Color.Transparent;
                    check.ForeColor = palette.TextPrimary;
                }
                else if (child is Button button && !button.Name.StartsWith("btnWindow", StringComparison.Ordinal)) // Exclude title bar buttons here
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point);

                    if (button.Name.StartsWith("btnWindow", StringComparison.Ordinal))
                    {
                        button.FlatAppearance.BorderSize = 0;
                        button.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
                    }
                    else if (button.Name == "btnSaveBuild" || button.Name == "btnExportConfig" || button.Name == "btnMassToggle")
                    {
                        button.FlatAppearance.BorderSize = 1;
                        button.FlatAppearance.BorderColor = palette.Border;
                        button.BackColor = palette.SurfaceAlt;
                        button.ForeColor = palette.TextPrimary;
                        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 232, 245);
                        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(210, 221, 238);
                    }
                    else
                    {
                        button.FlatAppearance.BorderSize = 1;
                        button.FlatAppearance.BorderColor = palette.Border;
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
                    if (panel == titleBarPanel) panel.BackColor = palette.WindowBackground;
                    else if (panel == titleMenuHost) panel.BackColor = palette.WindowBackground;
                    else if (panel == titleButtonPanel) panel.BackColor = palette.WindowBackground;
                    else panel.BackColor = palette.WindowBackground;
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

                bool isTitleMenuItem = item.Owner == menu;
                item.BackColor = isTitleMenuItem ? palette.WindowBackground : palette.Surface;
                item.ForeColor = (item is ToolStripLabel lbl && (lbl.Text?.Contains("Component-Level") ?? false)
                        ? palette.TextMuted
                        : palette.TextPrimary);

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

        void ApplyWorkspaceStripTheme()
        {
            if (statusStrip == null) return;
            var palette = CurrentPalette;
            statusStrip.BackColor = palette.Surface;
            statusStrip.ForeColor = palette.TextPrimary;
        }

        // New event handler for titleMenuHost Paint to ensure themed border
        void TitleMenuHost_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Control control) return;
            var palette = CurrentPalette;
            using var pen = new Pen(palette.Border, 1f);
            var rect = new Rectangle(0, 0, Math.Max(0, control.Width - 1), Math.Max(0, control.Height - 1));
            e.Graphics.DrawRectangle(pen, rect);
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
                error += RandomSigned() * 0.03f * sensorNoiseBias;

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
            float responseDelaySec = Math.Max(0.001f, featureProfile.EscResponseDelayMs / 1000f);
            float responseAlpha = dt / (responseDelaySec + dt);
            escDelayedThrottle += (throttle - escDelayedThrottle) * responseAlpha;
            float commandThrottle = Math.Clamp(escDelayedThrottle, 0f, 1f);
            float propellerThrustScale = GetPropellerThrustScale();

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
                    float kvSource = motorAsset?.KV > 0 ? motorAsset.KV : featureProfile.MotorKvOverride;
                    float kvScale = Math.Clamp(kvSource / 1750f, 0.6f, 1.45f);
                    float rpm = commandThrottle * maxRpm * (0.82f + kvScale * 0.18f);
                    float torqueFactor = EvaluateCurve(motorAsset?.TorqueCurve, commandThrottle, 1f);
                    float efficiencyFactor = EvaluateCurve(motorAsset?.EfficiencyMap, commandThrottle, 0.83f) * featureProfile.EfficiencyBias;
                    float perMotorThrust = ThrustFromRPM(rpm) * propellerThrustScale * torqueFactor * featureProfile.TorqueFactor;
                    totalThrustN += perMotorThrust;
                    float limitedCurrent = commandThrottle * maxCurrent / Math.Clamp(efficiencyFactor, 0.42f, 1.3f);
                    totalCurrentA += Math.Min(limitedCurrent, featureProfile.EscCurrentLimitA);
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
                else if (p.Type == PartType.ESC)
                {
                    var escAsset = AssetLibrary.Get(p.AssetId) as ESCAsset;
                    totalMassKg += escAsset?.MassKg > 0 ? escAsset.MassKg : PhysicsDatabase.EscMass();
                }
                else if (p.Type == PartType.FlightController)
                {
                    var fcAsset = AssetLibrary.Get(p.AssetId) as FlightControllerAsset;
                    totalMassKg += fcAsset?.MassKg > 0 ? fcAsset.MassKg : PhysicsDatabase.FcMass();
                }
                else if (p.Type == PartType.Propeller)
                {
                    var propAsset = AssetLibrary.Get(p.AssetId) as PropellerAsset;
                    totalMassKg += propAsset?.MassKg > 0 ? propAsset.MassKg : PhysicsDatabase.PropellerMass();
                }
                else if (p.Type == PartType.Camera)
                {
                    var camAsset = AssetLibrary.Get(p.AssetId) as CameraAsset;
                    totalMassKg += camAsset?.MassKg > 0 ? camAsset.MassKg : PhysicsDatabase.CameraMass();
                }
                else if (p.Type == PartType.Receiver)
                {
                    var rxAsset = AssetLibrary.Get(p.AssetId) as ReceiverAsset;
                    totalMassKg += rxAsset?.MassKg > 0 ? rxAsset.MassKg : PhysicsDatabase.ReceiverMass();
                }
                else if (p.Type == PartType.GPS)
                {
                    var gpsAsset = AssetLibrary.Get(p.AssetId) as GpsAsset;
                    totalMassKg += gpsAsset?.MassKg > 0 ? gpsAsset.MassKg : PhysicsDatabase.GpsMass();
                }
                else if (p.Type == PartType.VTX)
                {
                    var vtxAsset = AssetLibrary.Get(p.AssetId) as VtxAsset;
                    totalMassKg += vtxAsset?.MassKg > 0 ? vtxAsset.MassKg : PhysicsDatabase.VtxMass();
                }
                else if (p.Type == PartType.Antenna)
                {
                    var antAsset = AssetLibrary.Get(p.AssetId) as AntennaAsset;
                    totalMassKg += antAsset?.MassKg > 0 ? antAsset.MassKg : PhysicsDatabase.AntennaMass();
                }
                else if (p.Type == PartType.Buzzer)
                {
                    var buzzerAsset = AssetLibrary.Get(p.AssetId) as BuzzerAsset;
                    totalMassKg += buzzerAsset?.MassKg > 0 ? buzzerAsset.MassKg : PhysicsDatabase.BuzzerMass();
                }
                else if (p.Type == PartType.LED)
                {
                    var ledAsset = AssetLibrary.Get(p.AssetId) as LedAsset;
                    totalMassKg += ledAsset?.MassKg > 0 ? ledAsset.MassKg : PhysicsDatabase.LedMass();
                }
                else if (p.Type == PartType.CustomComponent)
                {
                    var customAsset = AssetLibrary.Get(p.AssetId) as CustomComponentAsset;
                    if (customAsset != null)
                    {
                        totalMassKg += customAsset.MassKg;
                        if (customAsset.PowerDrawA > 0f)
                            totalCurrentA += customAsset.PowerDrawA;
                    }
                }
            }

            float profileCgOffsetCm = MathF.Sqrt(
                featureProfile.CgOffsetXcm * featureProfile.CgOffsetXcm +
                featureProfile.CgOffsetYcm * featureProfile.CgOffsetYcm);
            frameCgOffsetCm = Math.Max(frameCgOffsetCm, profileCgOffsetCm);
            frameDensity = Math.Max(0.3f, featureProfile.MaterialDensity);
            batteryMaxDischargeC = Math.Max(5f, featureProfile.BatteryCRating);
            float weightBiasFactor = Math.Abs(featureProfile.WeightBiasPct) * 0.01f;

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
            if (simulationMode == SimulationMode.Swarm)
            {
                float swarmEffect = Math.Clamp(1f + (featureProfile.SwarmSize - 4) * 0.01f, 0.9f, 1.1f);
                modeThrustMultiplier *= swarmEffect;
            }

            float effectiveThrust = totalThrustN * modeThrustMultiplier;
            effectiveThrust *= Math.Clamp(1f - yawImbalancePct * 0.0025f, 0.70f, 1f);

            if (faultInjection.MotorFailure || simulationMode == SimulationMode.EmergencyFailure)
                effectiveThrust *= 0.72f;

            if (obstacleAvoidanceEnabled && simulationMode == SimulationMode.AutonomousMission)
                totalCurrentA *= 1.05f;

            if (faultInjection.GpsDrop && simulationMode == SimulationMode.AutonomousMission)
            {
                targetAltitude += RandomSigned() * 0.08f;
                targetAltitude = Math.Clamp(targetAltitude, 0.3f, 3.0f);
            }

            float windPenalty = Math.Clamp(1f - environmentModel.WindSpeedMps * 0.02f, 0.65f, 1f);
            float turbulenceNoise = RandomSigned() * environmentModel.TurbulenceStrength * 0.06f;
            float aerodynamicPenalty = Math.Clamp(
                1f - environmentModel.DragCoefficient * (0.60f + featureProfile.PropBladeCount * 0.08f),
                0.72f,
                1.04f);
            float frameArmPenalty = Math.Clamp(1f - Math.Abs(featureProfile.ArmLengthMm - 150f) * 0.0008f, 0.82f, 1f);
            effectiveThrust *= Math.Clamp(windPenalty + turbulenceNoise, 0.55f, 1.08f) * aerodynamicPenalty * frameArmPenalty;

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
                BatteryChemistry.SolidState => 0.010f,
                _ => 0.020f
            };
            chemistrySagCoeff *= featureProfile.BatterySagBias;

            float escThermalLimit = Math.Max(55f, featureProfile.EscThermalLimitC);
            bool escOverThermal = escTempC > escThermalLimit;
            if (faultInjection.EscThermalCutback || escOverThermal)
            {
                float derate = Math.Clamp(1f - ((escTempC - escThermalLimit) / 80f), 0.45f, 1f);
                totalCurrentA *= derate;
                effectiveThrust *= Math.Clamp(derate + 0.08f, 0.55f, 1f);
            }

            if (faultInjection.SensorNoise)
                altitude += RandomSigned() * 0.005f * sensorNoiseBias;

            // ===== BATTERY MODEL =====
            batteryRemainingAh -= (totalCurrentA * dt) / 3600f;
            batteryRemainingAh = Math.Clamp(batteryRemainingAh, 0, Math.Max(0.1f, batteryCapacityAh));

            float stateOfCharge = batteryCapacityAh > 0.01f ? batteryRemainingAh / batteryCapacityAh : 0f;
            float sag = chemistrySagCoeff * totalCurrentA * (1f + (1f - stateOfCharge) * 0.45f);
            float maxDischargeA = batteryCapacityAh * Math.Max(1f, batteryMaxDischargeC);
            if (maxDischargeA > 0f && totalCurrentA > maxDischargeA)
                sag += (totalCurrentA - maxDischargeA) * 0.015f;
            batteryVoltage = Math.Max(0, batteryVoltageNominal - sag);

            motorTempC += (totalCurrentA * 0.12f + commandThrottle * 8f) * dt;
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
                Math.Abs(featureProfile.ArmLengthMm - 150f) * 0.03f +
                environmentModel.WindSpeedMps * 1.8f, 0f, 180f);

            stabilityMarginPct = Math.Clamp(
                100f -
                environmentModel.WindSpeedMps * 4.8f -
                environmentModel.TurbulenceStrength * 46f -
                payloadOffsetCm * 2.2f -
                frameCgOffsetCm * 1.7f -
                weightBiasFactor * 45f -
                yawImbalancePct * 0.55f -
                (sensorProfile == SensorProfile.Degraded ? 12f : 0f) -
                (faultInjection.GpsDrop ? 10f : 0f), 4f, 100f);

            escFailureRiskPct = Math.Clamp(
                Math.Max(0f, escTempC - escThermalLimit) * 1.35f +
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
            undoStack.Clear();
            redoStack.Clear();

            SyncFeatureProfileFromBuild();
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
            undoStack.Clear();
            redoStack.Clear();
            SyncFeatureProfileFromBuild();
            ResetPhysicsState();
            RefreshTree();
            UpdateStatusBar();
            viewport.Invalidate();
        }

        Project CloneProject(Project source)
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<Project>(json) ?? new Project { Name = source.Name };
        }

        Project? CaptureUndoSnapshot()
        {
            if (project == null) return null;
            return CloneProject(project);
        }

        void CommitUndoSnapshot(Project? snapshot)
        {
            if (snapshot == null) return;
            undoStack.Push(snapshot);
            redoStack.Clear();
        }

        void Undo()
        {
            if (project == null || undoStack.Count == 0) return;
            redoStack.Push(CloneProject(project));
            project = undoStack.Pop();
            selected = null;
            OnProjectStructureChanged();
        }

        void Redo()
        {
            if (project == null || redoStack.Count == 0) return;
            undoStack.Push(CloneProject(project));
            project = redoStack.Pop();
            selected = null;
            OnProjectStructureChanged();
        }

        // ================= CORE =================
        void AddPart(string cat, string name, int x, int y)
        {
            var pt = new PointF(x, y);
            var snapshot = CaptureUndoSnapshot();
            bool added = false;

            if (cat == "Motors")
            {
                added = AddMotor(pt, name);
            }
            else if (cat == "Batteries")
            {
                added = AddBattery(pt, name);
            }
            else if (cat == "ESC")
            {
                added = AddEsc(pt, name);
            }
            else if (cat == "Props")
            {
                added = AddPropeller(pt, name);
            }
            else if (cat == "FC")
            {
                added = AddFlightController(pt, name);
            }
            else if (cat == "Cameras")
            {
                added = AddCamera(pt, name);
            }
            else if (cat == "Receivers")
            {
                added = AddReceiver(pt, name);
            }
            else if (cat == "GPS")
            {
                added = AddGps(pt, name);
            }
            else if (cat == "VTX")
            {
                added = AddVtx(pt, name);
            }
            else if (cat == "Antennas")
            {
                added = AddAntenna(pt, name);
            }
            else if (cat == "Buzzers")
            {
                added = AddBuzzer(pt, name);
            }
            else if (cat == "LEDs")
            {
                added = AddLed(pt, name);
            }
            else if (IsCustomCategory(cat))
            {
                added = AddCustomComponent(pt, name);
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
            {
                CommitUndoSnapshot(snapshot);
                OnProjectStructureChanged();
            }
        }

        void CopySelectedPart()
        {
            if (selected == null) return;
            clipboardPart = new PlacedInstance
            {
                AssetId = selected.AssetId,
                Type = selected.Type,
                Position = selected.Position,
                MountIndex = selected.MountIndex
            };
        }

        void PasteClipboardPart()
        {
            if (clipboardPart == null || project == null) return;
            var snapshot = CaptureUndoSnapshot();
            var p = new PlacedInstance
            {
                AssetId = clipboardPart.AssetId,
                Type = clipboardPart.Type,
                Position = new PointF(clipboardPart.Position.X + 10, clipboardPart.Position.Y + 10),
                MountIndex = clipboardPart.MountIndex
            };
            project.Instances.Add(p);
            CommitUndoSnapshot(snapshot);
            OnProjectStructureChanged();
        }

        void ToggleViewport3D()
        {
            viewportIs3D = !viewportIs3D;
            viewport.Invalidate();
        }

        void RefreshTree()
        {
            projectTree.BeginUpdate();
            projectTree.Nodes.Clear();
            if (project == null)
            {
                projectTree.EndUpdate();
                return;
            }

            TreeNode CreateGroupNode(string title)
            {
                return new TreeNode(title)
                {
                    NodeFont = new Font(Font.FontFamily, 9f, FontStyle.Bold, GraphicsUnit.Point)
                };
            }

            void AddComponentNode(TreeNode group, string name, PlacedInstance? instance, string specs, string weight, string warnings)
            {
                var node = new TreeNode(name);
                if (instance != null) node.Tag = instance;
                node.Nodes.Add(new TreeNode($"Specs: {specs}"));
                node.Nodes.Add(new TreeNode($"Weight: {weight}"));
                node.Nodes.Add(new TreeNode($"Warnings: {warnings}"));
                group.Nodes.Add(node);
            }

            string WeightText(float kg)
            {
                return kg > 0f ? $"{kg * 1000f:0} g" : "--";
            }

            var frame = project.Instances.FirstOrDefault(p => p.Type == PartType.Frame);
            var frameAsset = frame != null ? AssetLibrary.Get(frame.AssetId) as FrameAsset : null;

            if (frame != null)
            {
                var frameGroup = CreateGroupNode("Frame");
                string specs = frameAsset != null
                    ? $"{frameAsset.WheelbaseMm:0}mm WB | Arm {GetCurrentArmLengthMm(frameAsset):0}mm | Body {Math.Max(20f, frameAsset.BodySizeMm):0}mm"
                    : "--";
                AddComponentNode(frameGroup, frameAsset?.Name ?? "Frame", frame, specs, WeightText(PhysicsDatabase.FrameMass()), "--");
                projectTree.Nodes.Add(frameGroup);
            }

            var motors = project.Instances.Where(p => p.Type == PartType.Motor).ToList();
            if (motors.Count > 0)
            {
                var motorGroup = CreateGroupNode("Motors");
                foreach (var motor in motors)
                {
                    var motorAsset = AssetLibrary.Get(motor.AssetId) as MotorAsset;
                    var motorName = motorAsset?.Name ?? motor.AssetId;
                    string specs = motorAsset != null ? $"{motorAsset.KV:0}KV" : "--";
                    float weight = motorAsset?.MassKg > 0 ? motorAsset.MassKg : PhysicsDatabase.MotorMass(motorName);
                    AddComponentNode(motorGroup, motorName, motor, specs, WeightText(weight), "--");
                }
                projectTree.Nodes.Add(motorGroup);
            }

            var escs = project.Instances.Where(p => p.Type == PartType.ESC).ToList();
            if (escs.Count > 0)
            {
                var escGroup = CreateGroupNode(escLayout == EscLayout.FourInOne ? "ESC (4-in-1)" : "ESCs");
                foreach (var esc in escs)
                {
                    var escAsset = AssetLibrary.Get(esc.AssetId) as ESCAsset;
                    var escName = escAsset?.Name ?? esc.AssetId;
                    string specs = escAsset != null
                        ? $"{escAsset.ContinuousCurrent:0}A | {escAsset.VoltageRating}"
                        : "--";
                    float weight = escAsset?.MassKg > 0 ? escAsset.MassKg : PhysicsDatabase.EscMass();
                    AddComponentNode(escGroup, escName, esc, specs, WeightText(weight), "--");
                }
                projectTree.Nodes.Add(escGroup);
            }

            var batteryInst = project.Instances.FirstOrDefault(p => p.Type == PartType.Battery);
            if (batteryInst != null)
            {
                var batteryGroup = CreateGroupNode("Battery");
                var batteryAsset = AssetLibrary.Get(batteryInst.AssetId) as BatteryAsset;
                string specs = "--";
                if (batteryAsset != null)
                {
                    int cells = batteryAsset.Cells > 0 ? batteryAsset.Cells : (int)Math.Round(batteryAsset.VoltageNominal / 3.7f);
                    specs = $"{batteryAsset.VoltageNominal:0.0}V {cells}S";
                }
                float weight = batteryAsset?.MassKg > 0 ? batteryAsset.MassKg : PhysicsDatabase.BatteryMass();
                AddComponentNode(batteryGroup, batteryAsset?.Name ?? "Battery", batteryInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(batteryGroup);
            }

            var fcInst = project.Instances.FirstOrDefault(p => p.Type == PartType.FlightController);
            if (fcInst != null)
            {
                var fcGroup = CreateGroupNode("Flight Controller");
                var fcAsset = AssetLibrary.Get(fcInst.AssetId) as FlightControllerAsset;
                string specs = fcAsset != null ? $"{fcAsset.MCU} | {fcAsset.MountSizeMm:0.0}mm" : "--";
                float weight = fcAsset?.MassKg > 0 ? fcAsset.MassKg : PhysicsDatabase.FcMass();
                AddComponentNode(fcGroup, fcAsset?.Name ?? "FC", fcInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(fcGroup);
            }

            var props = project.Instances.Where(p => p.Type == PartType.Propeller).ToList();
            if (props.Count > 0)
            {
                var propGroup = CreateGroupNode("Propellers");
                foreach (var prop in props)
                {
                    var propAsset = AssetLibrary.Get(prop.AssetId) as PropellerAsset;
                    string specs = propAsset != null ? $"{propAsset.DiameterInch:0.0}x{propAsset.Pitch:0.0}" : "--";
                    float weight = propAsset?.MassKg > 0 ? propAsset.MassKg : PhysicsDatabase.PropellerMass();
                    AddComponentNode(propGroup, propAsset?.Name ?? "Prop", prop, specs, WeightText(weight), "--");
                }
                projectTree.Nodes.Add(propGroup);
            }

            var camInst = project.Instances.FirstOrDefault(p => p.Type == PartType.Camera);
            if (camInst != null)
            {
                var camGroup = CreateGroupNode("Camera");
                var camAsset = AssetLibrary.Get(camInst.AssetId) as CameraAsset;
                string specs = camAsset != null ? $"{camAsset.SystemType} | {camAsset.FormFactor}" : "--";
                float weight = camAsset?.MassKg > 0 ? camAsset.MassKg : PhysicsDatabase.CameraMass();
                AddComponentNode(camGroup, camAsset?.Name ?? "Camera", camInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(camGroup);
            }

            var rxInst = project.Instances.FirstOrDefault(p => p.Type == PartType.Receiver);
            if (rxInst != null)
            {
                var rxGroup = CreateGroupNode("Receiver");
                var rxAsset = AssetLibrary.Get(rxInst.AssetId) as ReceiverAsset;
                string specs = rxAsset != null ? $"{rxAsset.Protocol}" : "--";
                float weight = rxAsset?.MassKg > 0 ? rxAsset.MassKg : PhysicsDatabase.ReceiverMass();
                AddComponentNode(rxGroup, rxAsset?.Name ?? "Receiver", rxInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(rxGroup);
            }

            var gpsInst = project.Instances.FirstOrDefault(p => p.Type == PartType.GPS);
            if (gpsInst != null)
            {
                var gpsGroup = CreateGroupNode("GPS");
                var gpsAsset = AssetLibrary.Get(gpsInst.AssetId) as GpsAsset;
                string specs = gpsAsset != null ? $"{gpsAsset.UpdateRateHz:0}Hz" : "--";
                float weight = gpsAsset?.MassKg > 0 ? gpsAsset.MassKg : PhysicsDatabase.GpsMass();
                AddComponentNode(gpsGroup, gpsAsset?.Name ?? "GPS", gpsInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(gpsGroup);
            }

            var vtxInst = project.Instances.FirstOrDefault(p => p.Type == PartType.VTX);
            if (vtxInst != null)
            {
                var vtxGroup = CreateGroupNode("VTX");
                var vtxAsset = AssetLibrary.Get(vtxInst.AssetId) as VtxAsset;
                string specs = vtxAsset != null ? $"{vtxAsset.MaxPowerMw}mW" : "--";
                float weight = vtxAsset?.MassKg > 0 ? vtxAsset.MassKg : PhysicsDatabase.VtxMass();
                AddComponentNode(vtxGroup, vtxAsset?.Name ?? "VTX", vtxInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(vtxGroup);
            }

            var antInst = project.Instances.FirstOrDefault(p => p.Type == PartType.Antenna);
            if (antInst != null)
            {
                var antGroup = CreateGroupNode("Antenna");
                var antAsset = AssetLibrary.Get(antInst.AssetId) as AntennaAsset;
                string specs = antAsset != null ? $"{antAsset.GainDbi:0.0} dBi" : "--";
                float weight = antAsset?.MassKg > 0 ? antAsset.MassKg : PhysicsDatabase.AntennaMass();
                AddComponentNode(antGroup, antAsset?.Name ?? "Antenna", antInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(antGroup);
            }

            var buzzerInst = project.Instances.FirstOrDefault(p => p.Type == PartType.Buzzer);
            if (buzzerInst != null)
            {
                var buzzerGroup = CreateGroupNode("Buzzer");
                var buzzerAsset = AssetLibrary.Get(buzzerInst.AssetId) as BuzzerAsset;
                string specs = buzzerAsset != null ? $"{buzzerAsset.LoudnessDb:0}dB" : "--";
                float weight = buzzerAsset?.MassKg > 0 ? buzzerAsset.MassKg : PhysicsDatabase.BuzzerMass();
                AddComponentNode(buzzerGroup, buzzerAsset?.Name ?? "Buzzer", buzzerInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(buzzerGroup);
            }

            var ledInst = project.Instances.FirstOrDefault(p => p.Type == PartType.LED);
            if (ledInst != null)
            {
                var ledGroup = CreateGroupNode("LED");
                var ledAsset = AssetLibrary.Get(ledInst.AssetId) as LedAsset;
                string specs = ledAsset != null ? $"{ledAsset.LedCount} LED" : "--";
                float weight = ledAsset?.MassKg > 0 ? ledAsset.MassKg : PhysicsDatabase.LedMass();
                AddComponentNode(ledGroup, ledAsset?.Name ?? "LED", ledInst, specs, WeightText(weight), "--");
                projectTree.Nodes.Add(ledGroup);
            }

            var customInsts = project.Instances.Where(p => p.Type == PartType.CustomComponent).ToList();
            if (customInsts.Count > 0)
            {
                var groups = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
                foreach (var customInst in customInsts)
                {
                    var customAsset = AssetLibrary.Get(customInst.AssetId) as CustomComponentAsset;
                    string groupName = customAsset?.Category ?? "Custom Components";
                    if (!groups.TryGetValue(groupName, out var group))
                    {
                        group = CreateGroupNode(groupName);
                        groups[groupName] = group;
                    }

                    string specs = customAsset != null
                        ? $"{customAsset.WidthMm:0}x{customAsset.HeightMm:0}mm | {customAsset.SignalType}"
                        : "--";
                    float weight = customAsset?.MassKg > 0 ? customAsset.MassKg : 0f;
                    AddComponentNode(group, customAsset?.Name ?? "Custom Component", customInst, specs, WeightText(weight), "--");
                }

                foreach (var kvp in groups.OrderBy(k => k.Key))
                    projectTree.Nodes.Add(kvp.Value);
            }

            if (payloadType != PayloadType.None && payloadMassKg > 0f)
            {
                var payloadGroup = CreateGroupNode("Payload");
                AddComponentNode(payloadGroup, PayloadDisplay(payloadType), null, "--", WeightText(payloadMassKg), "--");
                projectTree.Nodes.Add(payloadGroup);
            }

            projectTree.EndUpdate();
            UpdateFrameTuningUi();
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
            
            var state = g.Save();
            g.TranslateTransform(viewOffset.X, viewOffset.Y);
            g.ScaleTransform(zoomFactor, zoomFactor);

            if (viewportIs3D)
                DrawGrid3D(g);
            else
                DrawGrid(g);

            if (project != null)
            {
                if (viewportIs3D)
                    DrawScene3D(g);
                else
                    DrawScene2D(g);
            }

            g.Restore(state);

            DrawPendingAddHint(g);
            DrawViewportOverlays(g);
            DrawViewportBorder(g);
        }

        void DrawScene2D(Graphics g)
        {
            var frame = GetFrame();
            if (frame != null)
                DrawFrame(g, frame);

            if (frame != null)
            {
                DrawMissionWaypoints(g, frame);
                DrawPayloadOverlay(g, frame);
            }

            foreach (var p in project!.Instances)
            {
                if (p.Type == PartType.Motor)
                    DrawMotor(g, p);
                if (p.Type == PartType.Battery)
                    DrawBattery(g, p);
                if (p.Type == PartType.ESC)
                    DrawEsc(g, p);
                if (p.Type == PartType.Propeller)
                    DrawPropeller(g, p);
                if (p.Type == PartType.FlightController)
                    DrawFlightController(g, p);
                if (p.Type == PartType.Camera)
                    DrawCamera(g, p);
                if (p.Type == PartType.Receiver)
                    DrawReceiver(g, p);
                if (p.Type == PartType.GPS)
                    DrawGps(g, p);
                if (p.Type == PartType.VTX)
                    DrawVtx(g, p);
                if (p.Type == PartType.Antenna)
                    DrawAntenna(g, p);
                if (p.Type == PartType.Buzzer)
                    DrawBuzzer(g, p);
                if (p.Type == PartType.LED)
                    DrawLed(g, p);
                if (p.Type == PartType.CustomComponent)
                    DrawCustomComponent(g, p);
            }

            if (frame != null)
                DrawCgOverlay(g, frame);
        }
    
    void DrawGrid(Graphics g)
    {
        var palette = CurrentPalette;
        float gridSize = 50.0f * zoomFactor;
        if (gridSize < 20) gridSize *= 5;
    
        int halfWidth = (int)(viewport.Width / (2 * zoomFactor));
        int halfHeight = (int)(viewport.Height / (2 * zoomFactor));
        
        int startX = (int)(-viewOffset.X / zoomFactor - halfWidth);
        int startY = (int)(-viewOffset.Y / zoomFactor - halfHeight);
        int endX = (int)((viewport.Width - viewOffset.X) / zoomFactor + halfWidth);
        int endY = (int)((viewport.Height - viewOffset.Y) / zoomFactor + halfHeight);
    
        startX = (int)(Math.Floor(startX / gridSize) * gridSize);
        startY = (int)(Math.Floor(startY / gridSize) * gridSize);
    
        using var majorPen = new Pen(Color.FromArgb(50, palette.Border), 2.0f / zoomFactor);
        using var minorPen = new Pen(Color.FromArgb(30, palette.Border), 1.0f / zoomFactor);
    
        for (float x = startX; x < endX; x += gridSize)
        {
            g.DrawLine(Math.Abs(x) < 0.01 ? majorPen : minorPen, x, startY, x, endY);
        }
        for (float y = startY; y < endY; y += gridSize)
        {
            g.DrawLine(Math.Abs(y) < 0.01 ? majorPen : minorPen, startX, y, endX, y);
        }
    }

    void DrawGrid3D(Graphics g)
    {
        var palette = CurrentPalette;
        float gridSize = 50f;

        var topLeft = ScreenToWorld(new Point(0, 0));
        var bottomRight = ScreenToWorld(new Point(viewport.Width, viewport.Height));
        float minX = Math.Min(topLeft.X, bottomRight.X) - 200f;
        float maxX = Math.Max(topLeft.X, bottomRight.X) + 200f;
        float minY = Math.Min(topLeft.Y, bottomRight.Y) - 200f;
        float maxY = Math.Max(topLeft.Y, bottomRight.Y) + 200f;

        minX = (float)Math.Floor(minX / gridSize) * gridSize;
        minY = (float)Math.Floor(minY / gridSize) * gridSize;

        using var majorPen = new Pen(Color.FromArgb(50, palette.Border), 2.0f / zoomFactor);
        using var minorPen = new Pen(Color.FromArgb(30, palette.Border), 1.0f / zoomFactor);

        for (float x = minX; x <= maxX; x += gridSize)
        {
            var a = IsoProject(new PointF(x, minY), 0f);
            var b = IsoProject(new PointF(x, maxY), 0f);
            g.DrawLine(Math.Abs(x) < 0.01 ? majorPen : minorPen, a, b);
        }

        for (float y = minY; y <= maxY; y += gridSize)
        {
            var a = IsoProject(new PointF(minX, y), 0f);
            var b = IsoProject(new PointF(maxX, y), 0f);
            g.DrawLine(Math.Abs(y) < 0.01 ? majorPen : minorPen, a, b);
        }
    }

    PointF ApplyViewRotation(PointF point, float degrees)
    {
        float rad = degrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        return new PointF(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
    }

    PointF IsoProject(PointF point, float z)
    {
        var rotated = ApplyViewRotation(point, viewRotation);
        float isoX = 0.866f;
        float isoY = 0.5f;
        return new PointF(
            (rotated.X - rotated.Y) * isoX,
            (rotated.X + rotated.Y) * isoY - z
        );
    }

    void DrawScene3D(Graphics g)
    {
        var frame = GetFrame();
        if (frame != null)
            DrawFrame3D(g, frame);

        var parts = project!.Instances
            .Where(p => p.Type != PartType.Frame)
            .OrderBy(p =>
            {
                var pos = GetPartWorldPosition(p);
                var iso = IsoProject(pos, 0f);
                return iso.Y;
            })
            .ToList();

        foreach (var p in parts)
        {
            if (p.Type == PartType.Motor) DrawMotor3D(g, p);
            else if (p.Type == PartType.Propeller) DrawPropeller3D(g, p);
            else if (p.Type == PartType.Battery) DrawBattery3D(g, p);
            else if (p.Type == PartType.ESC) DrawEsc3D(g, p);
            else if (p.Type == PartType.FlightController) DrawFlightController3D(g, p);
            else if (p.Type == PartType.Camera) DrawCamera3D(g, p);
            else if (p.Type == PartType.Receiver) DrawReceiver3D(g, p);
            else if (p.Type == PartType.GPS) DrawGps3D(g, p);
            else if (p.Type == PartType.VTX) DrawVtx3D(g, p);
            else if (p.Type == PartType.Antenna) DrawAntenna3D(g, p);
            else if (p.Type == PartType.Buzzer) DrawBuzzer3D(g, p);
            else if (p.Type == PartType.LED) DrawLed3D(g, p);
            else if (p.Type == PartType.CustomComponent) DrawCustomComponent3D(g, p);
        }

        if (frame != null)
            DrawCgOverlay3D(g, frame);
    }

    void DrawFrame3D(Graphics g, PlacedInstance frame)
    {
        var palette = CurrentPalette;
        var frameAsset = GetFrameAsset();
        var mounts = GetMotorMounts(frameAsset);
        if (mounts.Count < 3) return;

        var center = frame.Position;
        var polygon = mounts
            .Select(m => new PointF(center.X + m.Mount.Position.X, center.Y + m.Mount.Position.Y))
            .OrderBy(p => MathF.Atan2(p.Y - center.Y, p.X - center.X))
            .ToArray();

        DrawExtrudedPolygonIso(
            g,
            polygon,
            6f,
            Color.FromArgb(150, palette.SurfaceAlt),
            Color.FromArgb(120, palette.Border),
            palette.Border
        );
    }

    void DrawExtrudedRectIso(Graphics g, RectangleF rect, float height, Color topColor, Color sideColor, Color edgeColor)
    {
        var points = new[]
        {
            new PointF(rect.Left, rect.Top),
            new PointF(rect.Right, rect.Top),
            new PointF(rect.Right, rect.Bottom),
            new PointF(rect.Left, rect.Bottom)
        };
        DrawExtrudedPolygonIso(g, points, height, topColor, sideColor, edgeColor);
    }

    void DrawExtrudedPolygonIso(Graphics g, PointF[] polygon, float height, Color topColor, Color sideColor, Color edgeColor)
    {
        var bottom = polygon.Select(p => IsoProject(p, 0f)).ToArray();
        var top = polygon.Select(p => IsoProject(p, height)).ToArray();

        var faces = new List<(PointF[] Points, float Depth)>();
        for (int i = 0; i < polygon.Length; i++)
        {
            int next = (i + 1) % polygon.Length;
            var face = new[] { bottom[i], bottom[next], top[next], top[i] };
            float depth = face.Average(p => p.Y);
            faces.Add((face, depth));
        }

        foreach (var face in faces.OrderBy(f => f.Depth))
        {
            using var brush = new SolidBrush(Color.FromArgb(150, sideColor));
            g.FillPolygon(brush, face.Points);
            using var pen = new Pen(Color.FromArgb(160, edgeColor), 1f);
            g.DrawPolygon(pen, face.Points);
        }

        using (var topBrush = new SolidBrush(topColor))
            g.FillPolygon(topBrush, top);
        using (var topPen = new Pen(edgeColor, 1f))
            g.DrawPolygon(topPen, top);
    }

    void DrawExtrudedCircleIso(Graphics g, PointF center, float radius, float height, Color topColor, Color sideColor, Color edgeColor)
    {
        const int segments = 20;
        var bottom = new PointF[segments];
        var top = new PointF[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * MathF.PI * 2f / segments;
            var p = new PointF(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);
            bottom[i] = IsoProject(p, 0f);
            top[i] = IsoProject(p, height);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            var face = new[] { bottom[i], bottom[next], top[next], top[i] };
            using var brush = new SolidBrush(Color.FromArgb(120, sideColor));
            g.FillPolygon(brush, face);
            using var pen = new Pen(Color.FromArgb(140, edgeColor), 1f);
            g.DrawPolygon(pen, face);
        }

        using (var topBrush = new SolidBrush(topColor))
            g.FillPolygon(topBrush, top);
        using (var topPen = new Pen(edgeColor, 1f))
            g.DrawPolygon(topPen, top);
    }
    
    void DrawFrame(Graphics g, PlacedInstance frame)
    {
        var palette = CurrentPalette;    var frameAsset = GetFrameAsset();
    var mounts = GetMotorMounts(frameAsset);
    var center = frame.Position;
    float armThickness = frameAsset?.ArmThicknessMm > 0 ? frameAsset.ArmThicknessMm : 4f;
    float armPixels = Math.Clamp(armThickness * 0.6f, 3f, 10f);

    using var armPen = new Pen(palette.Accent, armPixels)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round
    };
    float shadowAlpha = 55f;
    foreach (var (mount, _) in mounts)
    {
        var world = FrameToWorld(mount.Position);
        DrawShadowLine(g, center, world, ViewportDepth, armPixels, (int)shadowAlpha);
        g.DrawLine(armPen, center, world);
    }

    var ordered = mounts
        .Select(m => FrameToWorld(m.Mount.Position))
        .OrderBy(p => MathF.Atan2(p.Y - center.Y, p.X - center.X))
        .ToList();
    if (ordered.Count >= 3)
    {
        using var outlinePen = new Pen(Color.FromArgb(160, palette.Border), 1.2f);
        g.DrawPolygon(outlinePen, ordered.ToArray());
    }

    var style = frameAsset?.Style ?? "X";
    bool wingStyle = style.Equals("FixedWing", StringComparison.OrdinalIgnoreCase)
        || style.Equals("VTOL", StringComparison.OrdinalIgnoreCase)
        || style.Equals("TiltRotor", StringComparison.OrdinalIgnoreCase);
    if (wingStyle)
    {
        float wingSpan = Math.Max(120f, mounts.Count > 0 ? mounts.Max(m => MathF.Abs(m.Mount.Position.X)) * 2.1f : 220f);
        float wingChord = Math.Max(18f, wingSpan * 0.08f);
        var wingRect = new RectangleF(center.X - wingSpan / 2f, center.Y - wingChord / 2f, wingSpan, wingChord);
        using var wingPath = BuildRoundedPath(wingRect, 8f);
        DrawShadowPath(g, wingPath, ViewportDepth, 40);
        using var wingBrush = new SolidBrush(Color.FromArgb(120, palette.SurfaceAlt));
        using var wingPen = new Pen(Color.FromArgb(150, palette.Border), 1.1f);
        g.FillPath(wingBrush, wingPath);
        g.DrawPath(wingPen, wingPath);
    }

    float bodySize = frameAsset?.BodySizeMm > 0 ? frameAsset.BodySizeMm : 48f;
    float bodyW = bodySize;
    float bodyH = Math.Max(28f, bodySize * 0.7f);
    var plate = new RectangleF(center.X - bodyW / 2f, center.Y - bodyH / 2f, bodyW, bodyH);
    using var platePath = BuildRoundedPath(plate, 10f);
    DrawShadowPath(g, platePath, ViewportDepth, 45);
    using var plateBrush = new SolidBrush(Color.FromArgb(140, palette.SurfaceAlt));
    using var platePen = new Pen(palette.Border, 1.2f);
    g.FillPath(plateBrush, platePath);
    g.DrawPath(platePen, platePath);

    // Draw direction indicator
    using var directionBrush = new SolidBrush(palette.Accent);
    var arrowPoints = new PointF[]
    {
        new PointF(0, -15),
        new PointF(-7, 10),
        new PointF(7, 10)
    };

    var transform = new Matrix();
    transform.Rotate(viewRotation);
    transform.Translate(center.X, center.Y, MatrixOrder.Append);
    transform.TransformPoints(arrowPoints);
    g.FillPolygon(directionBrush, arrowPoints);

    var fcMount = frameAsset?.Mounts.FirstOrDefault(m => m.Type == MountType.FlightController);
    if (fcMount != null && fcMount.Size.Width > 1f)
    {
        var world = FrameToWorld(fcMount.Position);
        var fcRect = new RectangleF(
            world.X - fcMount.Size.Width / 2f,
            world.Y - fcMount.Size.Height / 2f,
            fcMount.Size.Width,
            fcMount.Size.Height);
        using var fcPen = new Pen(Color.FromArgb(120, palette.Border), 1f) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(fcPen, fcRect.X, fcRect.Y, fcRect.Width, fcRect.Height);
    }

    if (frameStressPct > 95f)
    {
        float alpha = Math.Clamp((frameStressPct - 90f) * 2f, 40f, 180f);
        using var stressPen = new Pen(Color.FromArgb((int)alpha, 208, 92, 64), 2.4f);
        float radius = mounts.Count > 0 ? mounts.Max(m => Distance(center, FrameToWorld(m.Mount.Position))) : 140f;
        g.DrawEllipse(
            stressPen,
            center.X - radius - 6f,
            center.Y - radius - 6f,
            radius * 2f + 12f,
            radius * 2f + 12f);
    }

    // determine nearest mount when placing motors
    int nearest = -1;
    if (pendingAddMode == PartType.Motor)
        nearest = FindNearestMount(mousePos);

    foreach (var (mount, index) in mounts)
    {
        var world = FrameToWorld(mount.Position);

        using var mountBrush = new SolidBrush(palette.SurfaceAlt);
        using var mountPen = new Pen(palette.Border, 1.4f);
        g.FillEllipse(mountBrush, world.X - 10, world.Y - 10, 20, 20);
        g.DrawEllipse(mountPen, world.X - 10, world.Y - 10, 20, 20);

        if (frameAsset?.Ducted == true)
        {
            float ductRadius = Math.Max(18f, mount.Size.Width * 0.9f);
            using var ductPen = new Pen(Color.FromArgb(170, palette.Accent), 2.2f);
            g.DrawEllipse(ductPen, world.X - ductRadius, world.Y - ductRadius, ductRadius * 2f, ductRadius * 2f);
        }

        // highlight candidate
        if (index == nearest)
        {
            using var highlightPen = new Pen(palette.Accent, 2f);
            g.DrawEllipse(highlightPen, world.X - 14, world.Y - 14, 28, 28);
        }
    }

    var batteryMount = GetBatteryMount(frameAsset);
    var worldBay = RectangleF.Empty;
    if (batteryMount != null)
    {
        var mount = batteryMount.Value.Mount;
        var worldCenter = FrameToWorld(mount.Position);
        worldBay = new RectangleF(
            worldCenter.X - mount.Size.Width / 2f,
            worldCenter.Y - mount.Size.Height / 2f,
            mount.Size.Width,
            mount.Size.Height);
    }

    if (!worldBay.IsEmpty)
    {
        using var bayPath = BuildRoundedPath(worldBay, 6f);
        using var pen = new Pen(palette.Warning, 2f);
        g.DrawPath(pen, bayPath);
    }

    if (pendingAddMode == PartType.Battery)
    {
        if (!worldBay.IsEmpty && worldBay.Contains(mousePos))
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
    var frame = GetFrame();
    if (frame == null) return;
    if (!TryGetMotorMountPosition(motor.MountIndex, out var mount)) return;
    var palette = CurrentPalette;

    var pos = new PointF(
        frame.Position.X + mount.X,
        frame.Position.Y + mount.Y
    );

    var outer = new RectangleF(pos.X - 13, pos.Y - 13, 26, 26);
    var inner = new RectangleF(pos.X - 9, pos.Y - 9, 18, 18);

    DrawShadowEllipse(g, outer, ViewportDepth, 55);
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

    DrawShadowRect(g, rect, 10f, ViewportDepth, 55);
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

void DrawEsc(Graphics g, PlacedInstance esc)
{
    var frame = GetFrame();
    if (frame == null) return;
    if (!TryGetEscMountPosition(esc.MountIndex, out var mount)) return;

    var palette = CurrentPalette;
    var pos = new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
    MountPoint? escMount = null;
    var escMounts = GetEscMounts(GetFrameAsset());
    for (int i = 0; i < escMounts.Count; i++)
    {
        if (escMounts[i].Index == esc.MountIndex)
        {
            escMount = escMounts[i].Mount;
            break;
        }
    }
    float w = (escMount != null && escMount.Size.Width > 1f) ? escMount.Size.Width : 32f;
    float h = (escMount != null && escMount.Size.Height > 1f) ? escMount.Size.Height : 20f;
    var rect = new RectangleF(pos.X - w / 2f, pos.Y - h / 2f, w, h);
    DrawShadowRect(g, rect, 6f, ViewportDepth, 50);
    using var bodyPath = BuildRoundedPath(rect, 6f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(190, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);
}

void DrawPropeller(Graphics g, PlacedInstance prop)
{
    var frame = GetFrame();
    if (frame == null) return;
    if (!TryGetMotorMountPosition(prop.MountIndex, out var mount)) return;

    var palette = CurrentPalette;
    var pos = new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
    using var ringPen = new Pen(Color.FromArgb(150, palette.TextMuted), 1.4f);
    g.DrawEllipse(ringPen, pos.X - 18, pos.Y - 18, 36, 36);
    var propAsset = AssetLibrary.Get(prop.AssetId) as PropellerAsset;
    int blades = propAsset?.BladeCount > 0 ? propAsset.BladeCount : 2;
    DrawPropellerBlades(g, pos, blades, 16f, Color.FromArgb(150, palette.TextMuted));
    using var hubBrush = new SolidBrush(Color.FromArgb(180, palette.SurfaceAlt));
    g.FillEllipse(hubBrush, pos.X - 4, pos.Y - 4, 8, 8);
}

void DrawPropellerBlades(Graphics g, PointF center, int blades, float radius, Color color)
{
    if (blades <= 0) return;
    float spread = MathF.PI / (blades * 1.6f);
    float rotation = viewRotation * MathF.PI / 180f;
    using var brush = new SolidBrush(color);

    for (int i = 0; i < blades; i++)
    {
        float angle = rotation + i * MathF.PI * 2f / blades;
        var tip1 = new PointF(center.X + MathF.Cos(angle - spread) * radius, center.Y + MathF.Sin(angle - spread) * radius);
        var tip2 = new PointF(center.X + MathF.Cos(angle + spread) * radius, center.Y + MathF.Sin(angle + spread) * radius);
        var root = new PointF(center.X + MathF.Cos(angle) * radius * 0.35f, center.Y + MathF.Sin(angle) * radius * 0.35f);
        g.FillPolygon(brush, new[] { root, tip1, tip2 });
    }
}

void DrawFlightController(Graphics g, PlacedInstance fc)
{
    var palette = CurrentPalette;
    float size = 32f;
    var frameAsset = GetFrameAsset();
    if (frameAsset != null)
    {
        var mount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.FlightController);
        if (mount != null && mount.Size.Width > 1f)
            size = Math.Min(mount.Size.Width, mount.Size.Height);
    }
    var rect = new RectangleF(fc.Position.X - size / 2f, fc.Position.Y - size / 2f, size, size);
    DrawShadowRect(g, rect, 6f, ViewportDepth, 50);
    using var bodyPath = BuildRoundedPath(rect, 6f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(200, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);

    using var chipBrush = new SolidBrush(Color.FromArgb(180, palette.TextMuted));
    float chip = Math.Max(10f, size * 0.35f);
    g.FillRectangle(chipBrush, rect.X + (rect.Width - chip) / 2f, rect.Y + (rect.Height - chip) / 2f, chip, chip);
}

void DrawCamera(Graphics g, PlacedInstance cam)
{
    var palette = CurrentPalette;
    var rect = new RectangleF(cam.Position.X - 14, cam.Position.Y - 10, 28, 20);
    DrawShadowRect(g, rect, 5f, ViewportDepth, 50);
    using var bodyPath = BuildRoundedPath(rect, 5f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(200, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);

    using var lensBrush = new SolidBrush(Color.FromArgb(180, palette.TextMuted));
    g.FillEllipse(lensBrush, rect.Right - 10, rect.Y + 6, 6, 6);
}

void DrawReceiver(Graphics g, PlacedInstance rx)
{
    var palette = CurrentPalette;
    var rect = new RectangleF(rx.Position.X - 12, rx.Position.Y - 8, 24, 16);
    DrawShadowRect(g, rect, 4f, ViewportDepth, 50);
    using var bodyPath = BuildRoundedPath(rect, 4f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(200, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);
    using var dot = new SolidBrush(Color.FromArgb(200, palette.TextMuted));
    g.FillEllipse(dot, rect.X + 4, rect.Y + 4, 4, 4);
}

void DrawGps(Graphics g, PlacedInstance gps)
{
    var palette = CurrentPalette;
    var rect = new RectangleF(gps.Position.X - 12, gps.Position.Y - 12, 24, 24);
    DrawShadowRect(g, rect, 6f, ViewportDepth, 50);
    using var bodyPath = BuildRoundedPath(rect, 6f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(200, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);
    using var dot = new SolidBrush(Color.FromArgb(200, palette.TextMuted));
    g.FillEllipse(dot, rect.X + 9, rect.Y + 9, 6, 6);
}

void DrawVtx(Graphics g, PlacedInstance vtx)
{
    var palette = CurrentPalette;
    var rect = new RectangleF(vtx.Position.X - 13, vtx.Position.Y - 9, 26, 18);
    DrawShadowRect(g, rect, 4f, ViewportDepth, 50);
    using var bodyPath = BuildRoundedPath(rect, 4f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(200, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);
    using var linePen = new Pen(Color.FromArgb(170, palette.TextMuted), 1f);
    g.DrawLine(linePen, rect.X + 4, rect.Y + 6, rect.Right - 4, rect.Y + 6);
}

void DrawAntenna(Graphics g, PlacedInstance antenna)
{
    var palette = CurrentPalette;
    DrawShadowLine(g, new PointF(antenna.Position.X, antenna.Position.Y - 8), new PointF(antenna.Position.X, antenna.Position.Y + 8), ViewportDepth, 1.6f, 40);
    DrawShadowEllipse(g, new RectangleF(antenna.Position.X - 3, antenna.Position.Y - 10, 6, 6), ViewportDepth, 40);
    using var pen = new Pen(Color.FromArgb(200, palette.TextMuted), 1.6f);
    g.DrawLine(pen, antenna.Position.X, antenna.Position.Y - 8, antenna.Position.X, antenna.Position.Y + 8);
    g.DrawEllipse(pen, antenna.Position.X - 3, antenna.Position.Y - 10, 6, 6);
}

void DrawBuzzer(Graphics g, PlacedInstance buzzer)
{
    var palette = CurrentPalette;
    var rect = new RectangleF(buzzer.Position.X - 9, buzzer.Position.Y - 6, 18, 12);
    DrawShadowRect(g, rect, 3f, ViewportDepth, 45);
    using var bodyPath = BuildRoundedPath(rect, 3f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(210, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);
    using var dot = new SolidBrush(Color.FromArgb(180, palette.TextMuted));
    g.FillEllipse(dot, rect.Right - 6, rect.Y + 3, 3, 3);
}

void DrawLed(Graphics g, PlacedInstance led)
{
    var palette = CurrentPalette;
    var rect = new RectangleF(led.Position.X - 12, led.Position.Y - 4, 24, 8);
    DrawShadowRect(g, rect, 3f, ViewportDepth, 45);
    using var bodyPath = BuildRoundedPath(rect, 3f);
    using var bodyBrush = new SolidBrush(Color.FromArgb(190, palette.SurfaceAlt));
    using var bodyPen = new Pen(palette.Border, 1.1f);
    g.FillPath(bodyBrush, bodyPath);
    g.DrawPath(bodyPen, bodyPath);
    using var glow = new SolidBrush(Color.FromArgb(200, 72, 140, 255));
    g.FillEllipse(glow, rect.X + 4, rect.Y + 2, 4, 4);
}

void DrawCustomComponent(Graphics g, PlacedInstance customInst)
{
    var palette = CurrentPalette;
    var asset = AssetLibrary.Get(customInst.AssetId) as CustomComponentAsset;

    float widthMm = asset?.WidthMm > 0 ? asset.WidthMm : 26f;
    float heightMm = asset?.HeightMm > 0 ? asset.HeightMm : 18f;
    float mmToPx = 0.6f;
    float width = Math.Clamp(widthMm * mmToPx, 14f, 140f);
    float height = Math.Clamp(heightMm * mmToPx, 10f, 120f);

    var rect = new RectangleF(
        customInst.Position.X - width / 2f,
        customInst.Position.Y - height / 2f,
        width,
        height);

    var imagePath = asset != null ? ResolveAssetImagePath(asset) : null;
    var image = !string.IsNullOrWhiteSpace(imagePath) ? GetCachedCustomImage(imagePath) : null;

    var state = g.Save();
    float rotation = asset?.RotationDeg ?? 0f;
    if (Math.Abs(rotation) > 0.1f)
    {
        g.TranslateTransform(customInst.Position.X, customInst.Position.Y);
        g.RotateTransform(rotation);
        g.TranslateTransform(-customInst.Position.X, -customInst.Position.Y);
    }

    if (image != null)
    {
        DrawShadowRect(g, rect, 6f, ViewportDepth, 55);
        g.DrawImage(image, rect);
        using var imgBorder = new Pen(palette.Border, 1f);
        g.DrawRectangle(imgBorder, rect.X, rect.Y, rect.Width, rect.Height);
    }
    else
    {
        var fillColor = asset?.FillColor ?? Color.FromArgb(200, palette.Accent);
        var strokeColor = asset?.StrokeColor ?? palette.Border;
        float opacity = asset?.Opacity ?? 1f;
        int fillAlpha = (int)Math.Clamp(fillColor.A * opacity, 0, 255);
        int strokeAlpha = (int)Math.Clamp(strokeColor.A * opacity, 0, 255);
        using var fill = new SolidBrush(Color.FromArgb(fillAlpha, fillColor));
        using var stroke = new Pen(Color.FromArgb(strokeAlpha, strokeColor), asset?.StrokeWidth > 0 ? asset.StrokeWidth : 1.2f);

        var shape = asset?.Shape ?? CustomShape.RoundedRect;
        using var path = BuildCustomShapePath(shape, rect, asset?.CornerRadius ?? 6);
        DrawShadowPath(g, path, ViewportDepth, 55);
        g.FillPath(fill, path);
        g.DrawPath(stroke, path);
    }

    if (selected == customInst)
    {
        using var selectedPen = new Pen(palette.Accent, 2f);
        g.DrawRectangle(selectedPen, rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8);
    }

    g.Restore(state);
}

RectangleF RectFromCenter(PointF center, float width, float height)
{
    return new RectangleF(center.X - width / 2f, center.Y - height / 2f, width, height);
}

void DrawMotor3D(Graphics g, PlacedInstance motor)
{
    var frame = GetFrame();
    if (frame == null) return;
    if (!TryGetMotorMountPosition(motor.MountIndex, out var mount)) return;
    var pos = new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
    var palette = CurrentPalette;
    DrawExtrudedCircleIso(g, pos, 13f, 6f, Color.FromArgb(210, palette.Accent), Color.FromArgb(120, palette.Border), palette.Border);
}

void DrawPropeller3D(Graphics g, PlacedInstance prop)
{
    var frame = GetFrame();
    if (frame == null) return;
    if (!TryGetMotorMountPosition(prop.MountIndex, out var mount)) return;
    var pos = new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
    var palette = CurrentPalette;
    DrawExtrudedCircleIso(g, pos, 18f, 2.5f, Color.FromArgb(90, palette.SurfaceAlt), Color.FromArgb(80, palette.Border), palette.Border);

    var propAsset = AssetLibrary.Get(prop.AssetId) as PropellerAsset;
    int blades = propAsset?.BladeCount > 0 ? propAsset.BladeCount : 2;
    DrawPropellerBladesIso(g, pos, blades, 16f, 3f, Color.FromArgb(160, palette.TextMuted));
}

void DrawBattery3D(Graphics g, PlacedInstance battery)
{
    var palette = CurrentPalette;
    var rect = RectFromCenter(battery.Position, 68f, 36f);
    DrawExtrudedRectIso(g, rect, 10f, Color.FromArgb(200, 90, 132, 255), Color.FromArgb(120, palette.Border), palette.Border);
}

void DrawEsc3D(Graphics g, PlacedInstance esc)
{
    var frame = GetFrame();
    if (frame == null) return;
    if (!TryGetEscMountPosition(esc.MountIndex, out var mount)) return;
    var pos = new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
    MountPoint? escMount = null;
    var escMounts = GetEscMounts(GetFrameAsset());
    for (int i = 0; i < escMounts.Count; i++)
    {
        if (escMounts[i].Index == esc.MountIndex)
        {
            escMount = escMounts[i].Mount;
            break;
        }
    }
    float w = (escMount != null && escMount.Size.Width > 1f) ? escMount.Size.Width : 32f;
    float h = (escMount != null && escMount.Size.Height > 1f) ? escMount.Size.Height : 20f;
    var rect = RectFromCenter(pos, w, h);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 5f, Color.FromArgb(180, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawFlightController3D(Graphics g, PlacedInstance fc)
{
    var frameAsset = GetFrameAsset();
    float size = 32f;
    if (frameAsset != null)
    {
        var mount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.FlightController);
        if (mount != null && mount.Size.Width > 1f)
            size = Math.Min(mount.Size.Width, mount.Size.Height);
    }
    var rect = RectFromCenter(fc.Position, size, size);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 6f, Color.FromArgb(190, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawCamera3D(Graphics g, PlacedInstance cam)
{
    var rect = RectFromCenter(cam.Position, 28f, 20f);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 10f, Color.FromArgb(190, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawReceiver3D(Graphics g, PlacedInstance rx)
{
    var rect = RectFromCenter(rx.Position, 24f, 16f);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 5f, Color.FromArgb(190, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawGps3D(Graphics g, PlacedInstance gps)
{
    var rect = RectFromCenter(gps.Position, 24f, 24f);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 6f, Color.FromArgb(190, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawVtx3D(Graphics g, PlacedInstance vtx)
{
    var rect = RectFromCenter(vtx.Position, 26f, 18f);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 5f, Color.FromArgb(190, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawAntenna3D(Graphics g, PlacedInstance antenna)
{
    var palette = CurrentPalette;
    var basePos = IsoProject(new PointF(antenna.Position.X, antenna.Position.Y), 0f);
    var tipPos = IsoProject(new PointF(antenna.Position.X, antenna.Position.Y), 14f);
    using var pen = new Pen(Color.FromArgb(180, palette.TextMuted), 1.5f);
    g.DrawLine(pen, basePos, tipPos);
}

void DrawBuzzer3D(Graphics g, PlacedInstance buzzer)
{
    var rect = RectFromCenter(buzzer.Position, 18f, 12f);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 5f, Color.FromArgb(190, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawLed3D(Graphics g, PlacedInstance led)
{
    var rect = RectFromCenter(led.Position, 24f, 8f);
    var palette = CurrentPalette;
    DrawExtrudedRectIso(g, rect, 4f, Color.FromArgb(170, palette.SurfaceAlt), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawCustomComponent3D(Graphics g, PlacedInstance customInst)
{
    var asset = AssetLibrary.Get(customInst.AssetId) as CustomComponentAsset;
    float widthMm = asset?.WidthMm > 0 ? asset.WidthMm : 26f;
    float heightMm = asset?.HeightMm > 0 ? asset.HeightMm : 18f;
    float depthMm = asset?.DepthMm > 0 ? asset.DepthMm : 8f;
    float mmToPx = 0.6f;
    float width = Math.Clamp(widthMm * mmToPx, 14f, 140f);
    float height = Math.Clamp(heightMm * mmToPx, 10f, 120f);
    float depth = Math.Clamp(depthMm * mmToPx, 2f, 40f);
    var rect = RectFromCenter(customInst.Position, width, height);
    var palette = CurrentPalette;
    var top = asset?.FillColor ?? palette.AccentSoft;
    DrawExtrudedRectIso(g, rect, depth, Color.FromArgb(200, top), Color.FromArgb(110, palette.Border), palette.Border);
}

void DrawCgOverlay3D(Graphics g, PlacedInstance frame)
{
    var palette = CurrentPalette;
    float cgOffsetXcm = featureProfile.CgOffsetXcm + payloadOffsetCm;
    float cgOffsetYcm = featureProfile.CgOffsetYcm;
    const float cmToUnits = 2.4f;

    var center = frame.Position;
    var cgPos = new PointF(center.X + cgOffsetXcm * cmToUnits, center.Y + cgOffsetYcm * cmToUnits);

    var centerIso = IsoProject(center, 10f);
    var cgIso = IsoProject(cgPos, 10f);

    using var linePen = new Pen(Color.FromArgb(140, palette.Accent), 1.2f) { DashStyle = DashStyle.Dash };
    g.DrawLine(linePen, centerIso, cgIso);
    using var centerBrush = new SolidBrush(Color.FromArgb(220, palette.Accent));
    using var cgBrush = new SolidBrush(Color.FromArgb(220, palette.Success));
    g.FillEllipse(centerBrush, centerIso.X - 3, centerIso.Y - 3, 6, 6);
    g.FillEllipse(cgBrush, cgIso.X - 3, cgIso.Y - 3, 6, 6);
}

void DrawPropellerBladesIso(Graphics g, PointF center, int blades, float radius, float z, Color color)
{
    if (blades <= 0) return;
    float spread = MathF.PI / (blades * 1.6f);
    float rotation = viewRotation * MathF.PI / 180f;
    using var brush = new SolidBrush(color);

    for (int i = 0; i < blades; i++)
    {
        float angle = rotation + i * MathF.PI * 2f / blades;
        var tip1 = new PointF(center.X + MathF.Cos(angle - spread) * radius, center.Y + MathF.Sin(angle - spread) * radius);
        var tip2 = new PointF(center.X + MathF.Cos(angle + spread) * radius, center.Y + MathF.Sin(angle + spread) * radius);
        var root = new PointF(center.X + MathF.Cos(angle) * radius * 0.35f, center.Y + MathF.Sin(angle) * radius * 0.35f);
        var pts = new[] { IsoProject(root, z), IsoProject(tip1, z), IsoProject(tip2, z) };
        g.FillPolygon(brush, pts);
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

void DrawCgOverlay(Graphics g, PlacedInstance frame)
{
    var palette = CurrentPalette;
    var frameAsset = GetFrameAsset();
    float cgOffsetXcm = featureProfile.CgOffsetXcm + payloadOffsetCm;
    float cgOffsetYcm = featureProfile.CgOffsetYcm;
    const float cmToUnits = 2.4f;

    var center = frame.Position;
    var cgPos = new PointF(center.X + cgOffsetXcm * cmToUnits, center.Y + cgOffsetYcm * cmToUnits);

    var mounts = GetMotorMounts(frameAsset);
    float maxRadius = 0f;
    for (int i = 0; i < mounts.Count; i++)
    {
        var pos = mounts[i].Mount.Position;
        float dist = MathF.Sqrt(pos.X * pos.X + pos.Y * pos.Y);
        if (dist > maxRadius) maxRadius = dist;
    }
    if (maxRadius <= 0f)
        maxRadius = frameAsset?.BodySizeMm > 0 ? frameAsset.BodySizeMm * 0.6f : 80f;

    float offsetDist = Distance(center, cgPos);
    float offsetPct = maxRadius > 0f ? (offsetDist / maxRadius) * 100f : 0f;
    bool imbalance = offsetPct > 5f;

    using var linePen = new Pen(imbalance ? Color.FromArgb(196, 64, 64) : Color.FromArgb(160, palette.Accent), 1.4f)
    {
        DashStyle = DashStyle.Dash
    };
    if (offsetDist > 0.5f)
        g.DrawLine(linePen, center, cgPos);

    float dotSize = 6f;
    using var centerBrush = new SolidBrush(Color.FromArgb(220, palette.Accent));
    using var cgBrush = new SolidBrush(imbalance ? Color.FromArgb(220, 208, 92, 64) : Color.FromArgb(220, palette.Success));
    g.FillEllipse(centerBrush, center.X - dotSize / 2f, center.Y - dotSize / 2f, dotSize, dotSize);
    g.FillEllipse(cgBrush, cgPos.X - dotSize / 2f, cgPos.Y - dotSize / 2f, dotSize, dotSize);

    using var textBrush = new SolidBrush(palette.TextPrimary);
    using var labelFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold, GraphicsUnit.Point);
    g.DrawString("Center", labelFont, textBrush, center.X + 6f, center.Y + 4f);
    g.DrawString("CG", labelFont, textBrush, cgPos.X + 6f, cgPos.Y + 4f);

    if (imbalance)
    {
        using var warnBrush = new SolidBrush(Color.FromArgb(196, 64, 64));
        g.DrawString("CG imbalance detected", labelFont, warnBrush, cgPos.X + 10f, cgPos.Y - 16f);
    }
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

void DrawViewportOverlays(Graphics g)
{
    var palette = CurrentPalette;
    using var hudFont = new Font(Font.FontFamily, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
    using var textBrush = new SolidBrush(palette.HudText);
    using var borderPen = new Pen(palette.Border, 1f);
    using var panelBrush = new SolidBrush(Color.FromArgb(210, palette.HudBackground));

    string buildName = project?.Name ?? "--";
    string massText = totalMassKg > 0.01f ? $"{totalMassKg:0.00} kg" : "--";
    string statusText = "No Build";

    if (project != null)
    {
        int frames = project.Instances.Count(i => i.Type == PartType.Frame);
        int motors = project.Instances.Count(i => i.Type == PartType.Motor);
        int escs = project.Instances.Count(i => i.Type == PartType.ESC);
        int rxs = project.Instances.Count(i => i.Type == PartType.Receiver);
        int vtxs = project.Instances.Count(i => i.Type == PartType.VTX);
        bool hasBattery = project.Instances.Any(i => i.Type == PartType.Battery);
        var frameAsset = GetFrameAsset();
        int expectedMotors = frameAsset?.ArmCount > 0 ? frameAsset.ArmCount : 4;
        int expectedEscs = escLayout == EscLayout.FourInOne ? (motors > 0 ? 1 : 0) : expectedMotors;
        bool escOk = expectedEscs == 0 || escs >= expectedEscs;
        statusText = (frames > 0 && motors >= expectedMotors && hasBattery && escOk && rxs > 0 && vtxs > 0) ? "Ready" : "Incomplete";
    }

    var leftLines = new[]
    {
        $"Build Name: {buildName}",
        $"Mass: {massText}",
        $"Status: {statusText}"
    };

    float lineHeight = hudFont.Height + 2f;
    float leftWidth = leftLines.Max(l => g.MeasureString(l, hudFont).Width);
    var leftRect = new RectangleF(12f, 12f, leftWidth + 16f, leftLines.Length * lineHeight + 10f);

    using (var leftPath = BuildRoundedPath(leftRect, 10f))
    {
        g.FillPath(panelBrush, leftPath);
        g.DrawPath(borderPen, leftPath);
    }

    float lx = leftRect.X + 8f;
    float ly = leftRect.Y + 6f;
    foreach (var line in leftLines)
    {
        g.DrawString(line, hudFont, textBrush, lx, ly);
        ly += lineHeight;
    }

    var toggles = new[]
    {
        "Solid View",
        "Wireframe",
        "Aero Overlay",
        "Mass Distribution",
        "Stress Map"
    };
    int activeToggle = 0;

    float toggleWidth = toggles.Max(t => g.MeasureString(t, hudFont).Width) + 28f;
    float toggleHeight = hudFont.Height + 6f;
    float panelWidth = toggleWidth + 12f;
    float panelHeight = toggles.Length * toggleHeight + 8f;
    float rightX = Math.Max(12f, viewport.Width - panelWidth - 12f);
    float rightY = 12f;
    var rightRect = new RectangleF(rightX, rightY, panelWidth, panelHeight);

    using (var rightPath = BuildRoundedPath(rightRect, 10f))
    {
        g.FillPath(panelBrush, rightPath);
        g.DrawPath(borderPen, rightPath);
    }

    for (int i = 0; i < toggles.Length; i++)
    {
        float rowY = rightRect.Y + 4f + i * toggleHeight;
        var box = new RectangleF(rightRect.X + 8f, rowY + 4f, 10f, 10f);
        using var boxPen = new Pen(palette.Border, 1f);
        g.DrawRectangle(boxPen, box.X, box.Y, box.Width, box.Height);

        if (i == activeToggle)
        {
            using var fill = new SolidBrush(palette.Accent);
            g.FillRectangle(fill, box.X + 2f, box.Y + 2f, box.Width - 4f, box.Height - 4f);
        }

        g.DrawString(toggles[i], hudFont, textBrush, box.Right + 6f, rowY + 2f);
    }
}

void DrawViewportBorder(Graphics g)
{
    var palette = CurrentPalette;
    using var borderPen = new Pen(Color.FromArgb(120, palette.Border));
    g.DrawRectangle(borderPen, 0, 0, Math.Max(0, viewport.Width - 1), Math.Max(0, viewport.Height - 1));
}

void DrawPendingAddHint(Graphics g)
{
    if (pendingAddMode == null) return;
    var palette = CurrentPalette;
    string hint = pendingAddMode switch
    {
        PartType.Motor => pendingAddName ?? "Motor",
        PartType.Battery => pendingAddName ?? "Battery",
        PartType.ESC => pendingAddName ?? "ESC",
        PartType.FlightController => pendingAddName ?? "Flight Controller",
        PartType.Propeller => pendingAddName ?? "Propeller",
        PartType.Camera => pendingAddName ?? "Camera",
        PartType.Receiver => pendingAddName ?? "Receiver",
        PartType.GPS => pendingAddName ?? "GPS",
        PartType.VTX => pendingAddName ?? "VTX",
        PartType.Antenna => pendingAddName ?? "Antenna",
        PartType.Buzzer => pendingAddName ?? "Buzzer",
        PartType.LED => pendingAddName ?? "LED",
        PartType.CustomComponent => pendingAddName ?? "Custom Component",
        _ => pendingAddName ?? "Part"
    };
    var txt = $"Click to place {hint}";
    var size = g.MeasureString(txt, Font);
    var pos = new PointF(lastMouseScreen.X + 12, lastMouseScreen.Y + 12);
    var hintRect = new RectangleF(pos.X - 8, pos.Y - 4, size.Width + 16, size.Height + 8);
    using var hintPath = BuildRoundedPath(hintRect, 8f);
    using var b = new SolidBrush(Color.FromArgb(224, palette.Surface));
    using var p = new Pen(palette.Border);
    using var t = new SolidBrush(palette.TextPrimary);
    g.FillPath(b, hintPath);
    g.DrawPath(p, hintPath);
    g.DrawString(txt, Font, t, pos);
}

void DrawShadowPath(Graphics g, GraphicsPath path, float depth, int alpha)
{
    using var shadowPath = (GraphicsPath)path.Clone();
    using var transform = new Matrix();
    transform.Translate(depth, depth);
    shadowPath.Transform(transform);
    using var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
    g.FillPath(brush, shadowPath);
}

void DrawShadowRect(Graphics g, RectangleF rect, float radius, float depth, int alpha)
{
    var shadowRect = new RectangleF(rect.X + depth, rect.Y + depth, rect.Width, rect.Height);
    using var path = BuildRoundedPath(shadowRect, radius);
    using var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
    g.FillPath(brush, path);
}

void DrawShadowEllipse(Graphics g, RectangleF rect, float depth, int alpha)
{
    var shadowRect = new RectangleF(rect.X + depth, rect.Y + depth, rect.Width, rect.Height);
    using var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
    g.FillEllipse(brush, shadowRect);
}

void DrawShadowLine(Graphics g, PointF a, PointF b, float depth, float width, int alpha)
{
    using var pen = new Pen(Color.FromArgb(alpha, 0, 0, 0), width);
    g.DrawLine(pen, a.X + depth, a.Y + depth, b.X + depth, b.Y + depth);
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

        GraphicsPath BuildCustomShapePath(CustomShape shape, RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            switch (shape)
            {
                case CustomShape.Circle:
                {
                    float size = Math.Min(rect.Width, rect.Height);
                    var circle = new RectangleF(
                        rect.X + (rect.Width - size) / 2f,
                        rect.Y + (rect.Height - size) / 2f,
                        size,
                        size);
                    path.AddEllipse(circle);
                    break;
                }
                case CustomShape.Triangle:
                {
                    var p1 = new PointF(rect.X + rect.Width / 2f, rect.Y);
                    var p2 = new PointF(rect.Right, rect.Bottom);
                    var p3 = new PointF(rect.X, rect.Bottom);
                    path.AddPolygon(new[] { p1, p2, p3 });
                    break;
                }
                case CustomShape.Capsule:
                {
                    float w = rect.Width;
                    float h = rect.Height;
                    if (w >= h)
                    {
                        float r = h / 2f;
                        var left = new RectangleF(rect.X, rect.Y, h, h);
                        var right = new RectangleF(rect.Right - h, rect.Y, h, h);
                        path.AddArc(left, 90, 180);
                        path.AddArc(right, 270, 180);
                        path.CloseFigure();
                    }
                    else
                    {
                        float r = w / 2f;
                        var top = new RectangleF(rect.X, rect.Y, w, w);
                        var bottom = new RectangleF(rect.X, rect.Bottom - w, w, w);
                        path.AddArc(top, 180, 180);
                        path.AddArc(bottom, 0, 180);
                        path.CloseFigure();
                    }
                    break;
                }
                case CustomShape.RoundedRect:
                    return BuildRoundedPath(rect, Math.Max(1f, radius));
                default:
                    path.AddRectangle(rect);
                    break;
            }
            return path;
        }

        string? ResolveAssetImagePath(Asset asset)
        {
            if (asset is not CustomComponentAsset custom || string.IsNullOrWhiteSpace(custom.ImagePath))
                return null;

            var path = custom.ImagePath.Trim();
            if (Path.IsPathRooted(path))
                return path;

            if (AssetLibrary.Paths.TryGetValue(asset.Id, out var assetPath))
            {
                var dir = Path.GetDirectoryName(assetPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    return Path.Combine(dir, path);
            }

            if (!string.IsNullOrWhiteSpace(AssetLibrary.CatalogBaseDir))
            {
                var candidate = Path.Combine(AssetLibrary.CatalogBaseDir, path);
                if (File.Exists(candidate))
                    return candidate;
            }

            return path;
        }

        Image? GetCachedCustomImage(string path)
        {
            if (customImageCache.TryGetValue(path, out var cached))
                return cached;

            if (!File.Exists(path))
                return null;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                ms.Position = 0;
                using var temp = Image.FromStream(ms);
                var img = new Bitmap(temp);
                customImageCache[path] = img;
                return img;
            }
            catch
            {
                return null;
            }
        }



        void BuildLibrary() => BuildLibrary(string.Empty);

        void BuildLibrary(string filter)
        {
            libraryTree.Nodes.Clear();

            // built-in assets
            AssetLibrary.LoadDefaults();
            // user assets on disk
            AssetLibrary.LoadAll(AssetLibrary.UserAssetRoot);

            var orderedCategories = new (string Key, string Display)[]
            {
                ("Frames", "Frames"),
                ("Motors", "Motors"),
                ("ESC", "ESCs"),
                ("Batteries", "Batteries"),
                ("FC", "Controllers"),
                ("Props", "Props"),
                ("Cameras", "Cameras"),
                ("Receivers", "Receivers"),
                ("GPS", "GPS"),
                ("VTX", "VTX"),
                ("Antennas", "Antennas"),
                ("Buzzers", "Buzzers"),
                ("LEDs", "LEDs"),
                ("Payload", "Payload"),
                ("Landing Gear", "Landing Gear"),
                ("Aerodynamic Add-ons", "Aero Add-ons"),
                ("Telemetry", "Telemetry"),
                ("Power Distribution", "Power Distribution"),
                ("Safety", "Safety"),
                ("Advanced", "Advanced / Research"),
                ("Industrial", "Industrial"),
                ("Custom", "Custom Components")
            };

            bool hasFilter = !string.IsNullOrWhiteSpace(filter);
            foreach (var (key, display) in orderedCategories)
            {
                var parent = new TreeNode(display) { Tag = key };
                foreach (var a in AssetLibrary.GetByCategory(key))
                {
                    if (hasFilter && !a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var node = new TreeNode(a.Name) { Tag = a };
                    parent.Nodes.Add(node);
                }
                if (parent.Nodes.Count > 0)
                    libraryTree.Nodes.Add(parent);
            }

            libraryTree.ExpandAll();
            ApplyTheme();
        }

        static bool IsCustomCategory(string category)
            => CustomCategories.Contains(category);
        void OnProjectStructureChanged()
{
    SyncFeatureProfileFromBuild();
    ResetPhysicsState();
    dirty = true;
    RefreshTree();
    viewport.Invalidate();
    UpdateStatusBar();
}

        void DeleteSelectedPart()
        {
            if (project == null || selected == null) return;

            if (selected.Type == PartType.Frame)
            {
                MessageBox.Show("Cannot delete the frame.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var snapshot = CaptureUndoSnapshot();
            project.Instances.Remove(selected);
            selected = null;
            CommitUndoSnapshot(snapshot);
            OnProjectStructureChanged();
        }

        PlacedInstance? HitTestPart(PointF worldPos)
        {
            if (project == null) return null;
            for (int i = project.Instances.Count - 1; i >= 0; i--)
            {
                var p = project.Instances[i];
                if (p.Type == PartType.Frame) continue;
                var partWorldPos = GetPartWorldPosition(p);
                float radius = GetPartHitRadius(p);
                if (Distance(partWorldPos, worldPos) <= radius)
                    return p;
            }

            if (HitTestFrame(worldPos))
                return GetFrame();

            return null;
        }

        float GetPartHitRadius(PlacedInstance p)
        {
            if (p.Type == PartType.Battery) return 40f;
            if (p.Type == PartType.Motor || p.Type == PartType.Propeller) return 22f;
            if (p.Type == PartType.ESC) return 20f;
            if (p.Type == PartType.FlightController) return 20f;
            if (p.Type == PartType.Camera) return 16f;
            if (p.Type == PartType.Receiver) return 14f;
            if (p.Type == PartType.GPS) return 16f;
            if (p.Type == PartType.VTX) return 16f;
            if (p.Type == PartType.Antenna) return 12f;
            if (p.Type == PartType.Buzzer) return 12f;
            if (p.Type == PartType.LED) return 12f;
            if (p.Type == PartType.CustomComponent)
            {
                var custom = AssetLibrary.Get(p.AssetId) as CustomComponentAsset;
                if (custom != null)
                    return Math.Max(custom.WidthMm, custom.HeightMm) * 0.3f;
            }
            return 18f;
        }

        void ShowViewportContextMenu(Point location)
        {
            if (viewportContextMenu == null)
            {
                viewportContextMenu = new ContextMenuStrip
                {
                    RenderMode = ToolStripRenderMode.Professional,
                    Renderer = toolStripRenderer
                };

                var infoItem = new ToolStripMenuItem("Info", null, (_,__) =>
                {
                    if (selected != null)
                        MessageBox.Show(GetPlacedPartInfo(selected), "Part Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
                var deleteItem = new ToolStripMenuItem("Delete", null, (_,__) => DeleteSelectedPart());
                var duplicateItem = new ToolStripMenuItem("Duplicate", null, (_,__) => DuplicateSelectedPart());
                var copyItem = new ToolStripMenuItem("Copy", null, (_,__) => CopySelectedPart());
                var pasteItem = new ToolStripMenuItem("Paste", null, (_,__) => PasteClipboardPart());
                var toggle3dItem = new ToolStripMenuItem("Toggle 3D View", null, (_,__) => ToggleViewport3D());

                viewportContextMenu.Items.AddRange(new ToolStripItem[]
                {
                    infoItem,
                    deleteItem,
                    duplicateItem,
                    new ToolStripSeparator(),
                    copyItem,
                    pasteItem,
                    new ToolStripSeparator(),
                    toggle3dItem
                });

                viewportContextMenu.Opening += (_, __) =>
                {
                    bool hasSelected = selected != null;
                    infoItem.Enabled = hasSelected;
                    deleteItem.Enabled = hasSelected && selected?.Type != PartType.Frame;
                    duplicateItem.Enabled = hasSelected;
                    copyItem.Enabled = hasSelected;
                    pasteItem.Enabled = clipboardPart != null;
                    toggle3dItem.Checked = viewportIs3D;
                };
            }

            ApplyContextMenuTheme(viewportContextMenu);
            viewportContextMenu.Show(viewport, location);
        }

        void DuplicateSelectedPart()
        {
            if (project == null || selected == null) return;

            if (selected.Type == PartType.Frame)
            {
                MessageBox.Show("Frame duplication is not supported.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (selected.Type == PartType.Battery && project.Instances.Any(i => i.Type == PartType.Battery))
            {
                MessageBox.Show("Only one battery can be attached to the frame.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if ((selected.Type == PartType.FlightController && project.Instances.Any(i => i.Type == PartType.FlightController)) ||
                (selected.Type == PartType.Camera && project.Instances.Any(i => i.Type == PartType.Camera)) ||
                (selected.Type == PartType.Receiver && project.Instances.Any(i => i.Type == PartType.Receiver)) ||
                (selected.Type == PartType.GPS && project.Instances.Any(i => i.Type == PartType.GPS)) ||
                (selected.Type == PartType.VTX && project.Instances.Any(i => i.Type == PartType.VTX)) ||
                (selected.Type == PartType.Antenna && project.Instances.Any(i => i.Type == PartType.Antenna)) ||
                (selected.Type == PartType.Buzzer && project.Instances.Any(i => i.Type == PartType.Buzzer)) ||
                (selected.Type == PartType.LED && project.Instances.Any(i => i.Type == PartType.LED)))
            {
                MessageBox.Show("Only one of this component can be attached to the frame.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var snapshot = CaptureUndoSnapshot();
            var clone = new PlacedInstance
            {
                AssetId = selected.AssetId,
                Type = selected.Type,
                Position = new PointF(selected.Position.X + 14, selected.Position.Y + 14),
                MountIndex = selected.MountIndex
            };

            if (clone.Type == PartType.Motor)
            {
                var mounts = GetMotorMounts(GetFrameAsset());
                int nextMount = Enumerable.Range(0, mounts.Count)
                    .FirstOrDefault(i => !project.Instances.Any(p => p.Type == PartType.Motor && p.MountIndex == i), -1);

                if (nextMount < 0)
                {
                    MessageBox.Show("No free motor mount available.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                clone.MountIndex = nextMount;
            }
            else if (clone.Type == PartType.ESC)
            {
                var mounts = GetEscMounts(GetFrameAsset());
                int nextMount = Enumerable.Range(0, mounts.Count)
                    .FirstOrDefault(i => !project.Instances.Any(p => p.Type == PartType.ESC && p.MountIndex == i), -1);
                if (nextMount < 0)
                {
                    MessageBox.Show("No free ESC mount available.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                clone.MountIndex = nextMount;
            }
            else if (clone.Type == PartType.Propeller)
            {
                var mounts = GetMotorMounts(GetFrameAsset());
                int nextMount = Enumerable.Range(0, mounts.Count)
                    .FirstOrDefault(i => !project.Instances.Any(p => p.Type == PartType.Propeller && p.MountIndex == i), -1);
                if (nextMount < 0)
                {
                    MessageBox.Show("No free propeller mount available.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                clone.MountIndex = nextMount;
            }

            project.Instances.Add(clone);
            selected = clone;
            CommitUndoSnapshot(snapshot);
            OnProjectStructureChanged();
        }

        void ClearFaultInjection()
        {
            faultInjection.MotorFailure = false;
            faultInjection.SensorNoise = false;
            faultInjection.GpsDrop = false;
            faultInjection.EscThermalCutback = false;
            UpdateStatusBar();
            viewport.Invalidate();
        }

        void ApplySimulationAutomation(string mode)
        {
            bool autoMode = mode.Equals("Testing", StringComparison.OrdinalIgnoreCase) ||
                            mode.Equals("Simulation", StringComparison.OrdinalIgnoreCase);

            autoSimEnabled = true;
            deterministicSim = autoMode;
            autoSimUseParts = true;

            if (autoMode)
            {
                SyncFeatureProfileFromBuild();
                ResetPhysicsState();
                UpdateStatusBar();
                viewport.Invalidate();
            }
        }

        void AdvanceSimulation()
        {
            if (project == null) return;
            if (!simClock.IsRunning) simClock.Start();
            UpdatePhysics(SimStepSeconds);
        }

        float RandomSigned()
        {
            return deterministicSim ? 0f : ((float)random.NextDouble() * 2f - 1f);
        }

        void SyncFeatureProfileFromBuild()
        {
            if (!autoSimUseParts || project == null) return;

            var frameInst = project.Instances.FirstOrDefault(p => p.Type == PartType.Frame);
            var frameAsset = frameInst != null ? AssetLibrary.Get(frameInst.AssetId) as FrameAsset : null;
            if (frameAsset != null)
            {
                featureProfile.ArmLengthMm = GetCurrentArmLengthMm(frameAsset);
                featureProfile.MaterialDensity = frameAsset.MaterialDensity;
                featureProfile.CgOffsetXcm = frameAsset.CgOffsetXcm;
                featureProfile.CgOffsetYcm = frameAsset.CgOffsetYcm;
            }

            var motorAssets = project.Instances.Where(p => p.Type == PartType.Motor)
                .Select(p => AssetLibrary.Get(p.AssetId) as MotorAsset)
                .Where(a => a != null)
                .ToList();
            if (motorAssets.Count > 0)
            {
                featureProfile.MotorKvOverride = motorAssets.Average(m => m!.KV > 0 ? m.KV : 1850f);
            }

            var escAssets = project.Instances.Where(p => p.Type == PartType.ESC)
                .Select(p => AssetLibrary.Get(p.AssetId) as ESCAsset)
                .Where(a => a != null)
                .ToList();
            if (escAssets.Count > 0)
            {
                featureProfile.EscCurrentLimitA = escAssets.Average(e => e!.ContinuousCurrent > 0 ? e.ContinuousCurrent : 45f);
                featureProfile.EscResponseDelayMs = escAssets.Average(e => e!.ResponseDelayMs > 0 ? e.ResponseDelayMs : 8f);
                featureProfile.EscThermalLimitC = escAssets.Average(e => e!.ThermalLimitC > 0 ? e.ThermalLimitC : 95f);
            }

            var propAssets = project.Instances.Where(p => p.Type == PartType.Propeller)
                .Select(p => AssetLibrary.Get(p.AssetId) as PropellerAsset)
                .Where(a => a != null)
                .ToList();
            if (propAssets.Count > 0)
            {
                featureProfile.PropDiameterInch = propAssets.Average(p => p!.DiameterInch > 0 ? p.DiameterInch : 5f);
                featureProfile.PropPitchInch = propAssets.Average(p => p!.Pitch > 0 ? p.Pitch : 4.3f);
                featureProfile.PropBladeCount = (int)Math.Round(propAssets.Average(p => p!.BladeCount > 0 ? p.BladeCount : 3f));
            }

            var batteryInst = project.Instances.FirstOrDefault(p => p.Type == PartType.Battery);
            var batteryAsset = batteryInst != null ? AssetLibrary.Get(batteryInst.AssetId) as BatteryAsset : null;
            if (batteryAsset != null)
            {
                featureProfile.BatteryCRating = batteryAsset.MaxDischargeC > 0 ? batteryAsset.MaxDischargeC : featureProfile.BatteryCRating;
            }
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

            foreach (var img in customImageCache.Values)
                img.Dispose();
            customImageCache.Clear();

            base.OnFormClosing(e);
        }

        void UpdateTitle()
        {
            Text = $"SILVU VIEWFINDER — {project?.Name}{(dirty ? " *" : "")}";
        }

        static string SimulationModeDisplayName(SimulationMode mode) => mode switch
        {
            SimulationMode.ManualFpv => "Manual",
            SimulationMode.AutonomousMission => "Autonomous",
            SimulationMode.EmergencyFailure => "Emergency",
            SimulationMode.Swarm => "Swarm",
            SimulationMode.VtolHybrid => "VTOL",
            SimulationMode.HeavyLift => "Heavy Lift",
            _ => mode.ToString()
        };

        static string WorkspaceDisplayName(Workspace workspace) => workspace switch
        {
            Workspace.Assemble => "Assemble",
            _ => workspace.ToString()
        };

        void UpdateStatusBar()
        {
            if (workspaceStatus == null) return;

            if (project == null)
            {
                workspaceStatus.Text = $"Workspace: {WorkspaceDisplayName(currentWorkspace)}";
                modeStatus.Text = " | Mode: Manual";
                firmwareStatus.Text = " | Firmware: Betaflight";
                sensorsStatus.Text = " | Sensors: Nominal";
                errorsStatus.Text = "Errors: 0";
                simReadyStatus.Text = " | Simulation Ready: No";
                errorsStatus.ForeColor = CurrentPalette.Success;

                if (twrValueLabel != null) twrValueLabel.Text = "--";
                if (hoverValueLabel != null) hoverValueLabel.Text = "--";
                if (flightValueLabel != null) flightValueLabel.Text = "--";
                if (thrustRequiredValueLabel != null) thrustRequiredValueLabel.Text = "--";
                if (maxThrustValueLabel != null) maxThrustValueLabel.Text = "--";
                if (thrustMarginValueLabel != null) thrustMarginValueLabel.Text = "--";
                if (sagValueLabel != null) sagValueLabel.Text = "--";
                if (tempValueLabel != null) tempValueLabel.Text = "--";
                if (motorTempLimitLabel != null) motorTempLimitLabel.Text = "--";
                if (batteryMaxCurrentLabel != null) batteryMaxCurrentLabel.Text = "--";
                if (powerHoverCurrentLabel != null) powerHoverCurrentLabel.Text = "--";
                if (peakCurrentLabel != null) peakCurrentLabel.Text = "--";
                if (escRatingLabel != null) escRatingLabel.Text = "--";
                if (cgValueLabel != null) cgValueLabel.Text = "--";
                if (rollInertiaValueLabel != null) rollInertiaValueLabel.Text = "--";
                if (pitchInertiaValueLabel != null) pitchInertiaValueLabel.Text = "--";
                if (yawInertiaValueLabel != null) yawInertiaValueLabel.Text = "--";
                if (yawValueLabel != null) yawValueLabel.Text = "--";
                if (payloadMaxLabel != null) payloadMaxLabel.Text = "--";
                if (payloadCurrentLabel != null) payloadCurrentLabel.Text = "--";
                if (payloadRemainingLabel != null) payloadRemainingLabel.Text = "--";
                if (massFrameLabel != null) massFrameLabel.Text = "--";
                if (massMotorsLabel != null) massMotorsLabel.Text = "--";
                if (massEscLabel != null) massEscLabel.Text = "--";
                if (massBatteryLabel != null) massBatteryLabel.Text = "--";
                if (massPayloadLabel != null) massPayloadLabel.Text = "--";
                if (missingValueLabel != null) missingValueLabel.Text = "--";
                if (overcurrentValueLabel != null) overcurrentValueLabel.Text = "--";
                if (overheatValueLabel != null) overheatValueLabel.Text = "--";
                if (structuralValueLabel != null) structuralValueLabel.Text = "--";
                if (allUpWeightLabel != null) allUpWeightLabel.Text = "All-Up Weight: --";
                if (totalThrustLabel != null) totalThrustLabel.Text = "Total Thrust: --";
                if (hoverCurrentLabel != null) hoverCurrentLabel.Text = "Hover Current: --";
                if (efficiencyIndexLabel != null) efficiencyIndexLabel.Text = "Power-to-Weight Efficiency Score: --";
                if (stabilityIndexLabel != null) stabilityIndexLabel.Text = "Stability Index: --";
                if (cgRowPanel != null) cgRowPanel.Visible = false;
                if (rollInertiaRowPanel != null) rollInertiaRowPanel.Visible = false;
                if (pitchInertiaRowPanel != null) pitchInertiaRowPanel.Visible = false;
                if (yawInertiaRowPanel != null) yawInertiaRowPanel.Visible = false;
                if (yawStabilityRowPanel != null) yawStabilityRowPanel.Visible = false;
                if (stabilityHeaderLabel != null) stabilityHeaderLabel.Visible = false;
                UpdateFeatureSetPanel();
                return;
            }

            int frames = project.Instances.Count(i => i.Type == PartType.Frame);
            int motors = project.Instances.Count(i => i.Type == PartType.Motor);
            int escs = project.Instances.Count(i => i.Type == PartType.ESC);
            int props = project.Instances.Count(i => i.Type == PartType.Propeller);
            int fcs = project.Instances.Count(i => i.Type == PartType.FlightController);
            int cams = project.Instances.Count(i => i.Type == PartType.Camera);
            int rxs = project.Instances.Count(i => i.Type == PartType.Receiver);
            int gps = project.Instances.Count(i => i.Type == PartType.GPS);
            int vtxs = project.Instances.Count(i => i.Type == PartType.VTX);
            int ants = project.Instances.Count(i => i.Type == PartType.Antenna);
            int buzzers = project.Instances.Count(i => i.Type == PartType.Buzzer);
            int leds = project.Instances.Count(i => i.Type == PartType.LED);
            var batteryInst = project.Instances.FirstOrDefault(i => i.Type == PartType.Battery);
            float payloadMass = payloadMassKg;
            var frameAsset = GetFrameAsset();
            int expectedMotors = frameAsset?.ArmCount > 0 ? frameAsset.ArmCount : 4;
            int expectedEscs = escLayout == EscLayout.FourInOne ? (motors > 0 ? 1 : 0) : expectedMotors;

            int errors = 0;
            if (frames == 0) errors++;
            if (motors < expectedMotors) errors++;
            if (batteryInst == null) errors++;
            if (expectedEscs > 0 && escs < expectedEscs) errors++;
            if (rxs == 0) errors++;
            if (vtxs == 0) errors++;

            float estMass = 0f;
            float estThrust = 0f;
            float estCurrentAtHover = 0f;
            float estPeakCurrent = 0f;
            float peakMotorCurrent = 0f;
            float massFrame = 0f;
            float massMotors = 0f;
            float massEsc = 0f;
            float massBattery = 0f;
            float massPayload = payloadMassKg;
            foreach (var p in project.Instances)
            {
                if (p.Type == PartType.Motor)
                {
                    var motorAsset = AssetLibrary.Get(p.AssetId) as MotorAsset;
                    var motorName = motorAsset?.Name ?? p.AssetId;
                    float motorMass = motorAsset?.MassKg > 0 ? motorAsset.MassKg : PhysicsDatabase.MotorMass(motorName);
                    estMass += motorMass;
                    massMotors += motorMass;
                    float maxThrust = motorAsset?.MaxThrust > 0 ? motorAsset.MaxThrust : PhysicsDatabase.GetMaxThrust(motorName);
                    estThrust += maxThrust;
                    float maxCurrent = motorAsset?.MaxCurrent > 0 ? motorAsset.MaxCurrent : PhysicsDatabase.MaxCurrent(motorName);
                    float hoverCurrent = PhysicsDatabase.GetCurrentDraw(motorName);
                    estCurrentAtHover += hoverCurrent > 0 ? hoverCurrent : maxCurrent * 0.45f;
                    estPeakCurrent += maxCurrent;
                    peakMotorCurrent = Math.Max(peakMotorCurrent, maxCurrent);
                }
                else if (p.Type == PartType.Frame)
                {
                    var frameAssetForMass = AssetLibrary.Get(p.AssetId) as FrameAsset;
                    float frameMass = frameAssetForMass?.MassKg > 0 ? frameAssetForMass.MassKg : PhysicsDatabase.FrameMass();
                    estMass += frameMass;
                    massFrame += frameMass;
                }
                else if (p.Type == PartType.Battery)
                {
                    var bat = AssetLibrary.Get(p.AssetId) as BatteryAsset;
                    float batteryMass = bat?.MassKg > 0 ? bat.MassKg : PhysicsDatabase.BatteryMass();
                    estMass += batteryMass;
                    massBattery += batteryMass;
                }
                else if (p.Type == PartType.ESC)
                {
                    var escAsset = AssetLibrary.Get(p.AssetId) as ESCAsset;
                    float escMass = escAsset?.MassKg > 0 ? escAsset.MassKg : PhysicsDatabase.EscMass();
                    estMass += escMass;
                    massEsc += escMass;
                }
                else if (p.Type == PartType.FlightController)
                {
                    var fcAsset = AssetLibrary.Get(p.AssetId) as FlightControllerAsset;
                    estMass += fcAsset?.MassKg > 0 ? fcAsset.MassKg : PhysicsDatabase.FcMass();
                }
                else if (p.Type == PartType.Propeller)
                {
                    var propAsset = AssetLibrary.Get(p.AssetId) as PropellerAsset;
                    estMass += propAsset?.MassKg > 0 ? propAsset.MassKg : PhysicsDatabase.PropellerMass();
                }
                else if (p.Type == PartType.Camera)
                {
                    var camAsset = AssetLibrary.Get(p.AssetId) as CameraAsset;
                    estMass += camAsset?.MassKg > 0 ? camAsset.MassKg : PhysicsDatabase.CameraMass();
                }
                else if (p.Type == PartType.Receiver)
                {
                    var rxAsset = AssetLibrary.Get(p.AssetId) as ReceiverAsset;
                    estMass += rxAsset?.MassKg > 0 ? rxAsset.MassKg : PhysicsDatabase.ReceiverMass();
                }
                else if (p.Type == PartType.GPS)
                {
                    var gpsAsset = AssetLibrary.Get(p.AssetId) as GpsAsset;
                    estMass += gpsAsset?.MassKg > 0 ? gpsAsset.MassKg : PhysicsDatabase.GpsMass();
                }
                else if (p.Type == PartType.VTX)
                {
                    var vtxAsset = AssetLibrary.Get(p.AssetId) as VtxAsset;
                    estMass += vtxAsset?.MassKg > 0 ? vtxAsset.MassKg : PhysicsDatabase.VtxMass();
                }
                else if (p.Type == PartType.Antenna)
                {
                    var antAsset = AssetLibrary.Get(p.AssetId) as AntennaAsset;
                    estMass += antAsset?.MassKg > 0 ? antAsset.MassKg : PhysicsDatabase.AntennaMass();
                }
                else if (p.Type == PartType.Buzzer)
                {
                    var buzzerAsset = AssetLibrary.Get(p.AssetId) as BuzzerAsset;
                    estMass += buzzerAsset?.MassKg > 0 ? buzzerAsset.MassKg : PhysicsDatabase.BuzzerMass();
                }
                else if (p.Type == PartType.LED)
                {
                    var ledAsset = AssetLibrary.Get(p.AssetId) as LedAsset;
                    estMass += ledAsset?.MassKg > 0 ? ledAsset.MassKg : PhysicsDatabase.LedMass();
                }
                else if (p.Type == PartType.CustomComponent)
                {
                    var customAsset = AssetLibrary.Get(p.AssetId) as CustomComponentAsset;
                    if (customAsset != null)
                    {
                        estMass += customAsset.MassKg;
                        estCurrentAtHover += Math.Max(0f, customAsset.PowerDrawA);
                    }
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
            float hoverThrustRequired = liveMass > 0 ? liveMass * GRAVITY : 0f;
            float maxThrustAvailable = estThrust > 0.1f ? estThrust : liveThrust;
            float thrustMarginPct = maxThrustAvailable > 0f
                ? MathF.Max(0f, (maxThrustAvailable - hoverThrustRequired) / maxThrustAvailable) * 100f
                : 0f;

            var batteryAsset = batteryInst != null ? AssetLibrary.Get(batteryInst.AssetId) as BatteryAsset : null;
            float batteryMaxContinuousA = 0f;
            if (batteryAsset != null)
            {
                float capAh = batteryAsset.CapacityAh > 0 ? batteryAsset.CapacityAh : batteryCapacityAh;
                float cRating = batteryAsset.MaxDischargeC > 0 ? batteryAsset.MaxDischargeC : featureProfile.BatteryCRating;
                batteryMaxContinuousA = capAh * Math.Max(1f, cRating);
            }

            float escRatingA = 0f;
            var escRatings = project.Instances
                .Where(i => i.Type == PartType.ESC)
                .Select(i => AssetLibrary.Get(i.AssetId) as ESCAsset)
                .Where(e => e != null)
                .Select(e => e!.ContinuousCurrent)
                .Where(v => v > 0f)
                .ToList();
            if (escRatings.Count > 0)
                escRatingA = escRatings.Min();
            else if (featureProfile.EscCurrentLimitA > 0f)
                escRatingA = featureProfile.EscCurrentLimitA;

            bool escOverLimit = escRatingA > 0f && peakMotorCurrent > 0f && escRatingA < peakMotorCurrent;

            float maxSafeMassKg = maxThrustAvailable > 0f ? (maxThrustAvailable * 0.65f) / GRAVITY : 0f;
            float baseMassKg = Math.Max(0f, liveMass - payloadMass);
            float maxPayloadSafeKg = Math.Max(0f, maxSafeMassKg - baseMassKg);
            float remainingPayloadKg = Math.Max(0f, maxPayloadSafeKg - payloadMass);

            float efficiencyScore = 0f;
            if (twr > 0.01f && hoverPct > 0.01f)
            {
                float twrScore = Math.Clamp((twr / 2.5f) * 60f, 0f, 60f);
                float headroomScore = Math.Clamp((100f - hoverPct) / 35f, 0f, 1f) * 40f;
                efficiencyScore = Math.Clamp(twrScore + headroomScore, 0f, 100f);
            }

            workspaceStatus.Text = $"Workspace: {WorkspaceDisplayName(currentWorkspace)}";
            modeStatus.Text = $" | Mode: {SimulationModeDisplayName(simulationMode)}";
            firmwareStatus.Text = $" | Firmware: {firmwareProfile}";
            sensorsStatus.Text = $" | Sensors: {sensorProfile}";
            errorsStatus.Text = $"Errors: {errors}";
            simReadyStatus.Text = $" | Simulation Ready: {(errors == 0 ? "Yes" : "No")}";
            errorsStatus.ForeColor = errors == 0 ? CurrentPalette.Success : CurrentPalette.Warning;

            if (twrValueLabel != null) twrValueLabel.Text = twr > 0 ? $"{twr:0.0}:1" : "--";
            if (hoverValueLabel != null)
            {
                string hoverText = hoverPct > 0 ? $"{hoverPct:0}% (Rec < 65%)" : "--";
                if (hoverPct > 75f)
                    hoverText += " — Insufficient power headroom";
                hoverValueLabel.Text = hoverText;
            }
            if (flightValueLabel != null) flightValueLabel.Text = flightMins > 0 ? $"{flightMins:0.0} min" : "--";
            if (thrustRequiredValueLabel != null) thrustRequiredValueLabel.Text = hoverThrustRequired > 0 ? $"{hoverThrustRequired:0.0} N" : "--";
            if (maxThrustValueLabel != null) maxThrustValueLabel.Text = maxThrustAvailable > 0 ? $"{maxThrustAvailable:0.0} N" : "--";
            if (thrustMarginValueLabel != null) thrustMarginValueLabel.Text = maxThrustAvailable > 0 ? $"{thrustMarginPct:0}%" : "--";
            if (sagValueLabel != null) sagValueLabel.Text = batteryInst != null ? $"{sagVolts:0.0} V" : "--";
            if (tempValueLabel != null)
                tempValueLabel.Text = motors > 0 ? $"{motorTempC:0} C" : "--";
            if (motorTempLimitLabel != null)
                motorTempLimitLabel.Text = motors > 0 ? "120 C" : "--";

            if (twrValueLabel != null)
                twrValueLabel.ForeColor = twr > 0f
                    ? (twr >= 2.0f ? CurrentPalette.Success : CurrentPalette.Warning)
                    : CurrentPalette.TextPrimary;
            if (hoverValueLabel != null)
            {
                if (hoverPct <= 0f)
                {
                    hoverValueLabel.ForeColor = CurrentPalette.TextPrimary;
                }
                else if (hoverPct > 75f)
                    hoverValueLabel.ForeColor = Color.FromArgb(196, 64, 64);
                else
                    hoverValueLabel.ForeColor = hoverPct <= 65f ? CurrentPalette.Success : CurrentPalette.Warning;
            }
            if (thrustMarginValueLabel != null)
            {
                if (maxThrustAvailable <= 0f)
                    thrustMarginValueLabel.ForeColor = CurrentPalette.TextPrimary;
                else if (thrustMarginPct >= 40f)
                    thrustMarginValueLabel.ForeColor = CurrentPalette.Success;
                else if (thrustMarginPct >= 20f)
                    thrustMarginValueLabel.ForeColor = CurrentPalette.Warning;
                else
                    thrustMarginValueLabel.ForeColor = Color.FromArgb(196, 64, 64);
            }
            if (sagValueLabel != null)
                sagValueLabel.ForeColor = batteryInst == null ? CurrentPalette.TextPrimary
                    : (sagVolts <= 2.5f ? CurrentPalette.TextPrimary : CurrentPalette.Warning);
            if (tempValueLabel != null)
                tempValueLabel.ForeColor = motors == 0 ? CurrentPalette.TextPrimary
                    : (motorTempC > 95f ? CurrentPalette.Warning : CurrentPalette.TextPrimary);

            float cgOffset = 0f;
            if (Math.Abs(featureProfile.CgOffsetXcm) > 0.01f || Math.Abs(featureProfile.CgOffsetYcm) > 0.01f)
                cgOffset = MathF.Sqrt(featureProfile.CgOffsetXcm * featureProfile.CgOffsetXcm + featureProfile.CgOffsetYcm * featureProfile.CgOffsetYcm);
            if (Math.Abs(payloadOffsetCm) > cgOffset) cgOffset = Math.Abs(payloadOffsetCm);

            bool hasFrame = frames > 0 && frameAsset != null;
            var motorMounts = hasFrame ? GetMotorMounts(frameAsset) : new List<(MountPoint Mount, int Index)>();
            float maxRadius = 0f;
            for (int i = 0; i < motorMounts.Count; i++)
            {
                var pos = motorMounts[i].Mount.Position;
                float dist = MathF.Sqrt(pos.X * pos.X + pos.Y * pos.Y);
                if (dist > maxRadius) maxRadius = dist;
            }
            float bodySizeMm = hasFrame && frameAsset?.BodySizeMm > 0 ? frameAsset.BodySizeMm : 0f;

            string InertiaBand(float value, float low, float high)
            {
                if (value <= 0f) return "--";
                if (value < low) return "Low";
                if (value < high) return "Medium";
                return "High";
            }

            string rollInertia = hasFrame ? InertiaBand(maxRadius, 45f, 85f) : "--";
            string pitchInertia = hasFrame ? InertiaBand(maxRadius, 45f, 85f) : "--";
            string yawInertia = hasFrame
                ? (bodySizeMm > 0f ? InertiaBand(bodySizeMm, 40f, 80f) : InertiaBand(maxRadius * 0.8f, 45f, 85f))
                : "--";

            bool showCg = hasFrame && cgOffset > 0.01f;
            bool showRoll = rollInertia != "--";
            bool showPitch = pitchInertia != "--";
            bool showYawInertia = yawInertia != "--";
            bool showYawStability = hasFrame && yawImbalancePct > 0.01f;

            if (cgValueLabel != null) cgValueLabel.Text = showCg ? $"{cgOffset:0.0} cm" : "--";
            if (rollInertiaValueLabel != null) rollInertiaValueLabel.Text = rollInertia;
            if (pitchInertiaValueLabel != null) pitchInertiaValueLabel.Text = pitchInertia;
            if (yawInertiaValueLabel != null) yawInertiaValueLabel.Text = yawInertia;
            if (yawValueLabel != null) yawValueLabel.Text = showYawStability ? $"{Math.Max(0f, 100f - yawImbalancePct):0}%" : "--";

            if (cgRowPanel != null) cgRowPanel.Visible = showCg;
            if (rollInertiaRowPanel != null) rollInertiaRowPanel.Visible = showRoll;
            if (pitchInertiaRowPanel != null) pitchInertiaRowPanel.Visible = showPitch;
            if (yawInertiaRowPanel != null) yawInertiaRowPanel.Visible = showYawInertia;
            if (yawStabilityRowPanel != null) yawStabilityRowPanel.Visible = showYawStability;
            if (stabilityHeaderLabel != null)
                stabilityHeaderLabel.Visible = showCg || showRoll || showPitch || showYawInertia || showYawStability;

            if (batteryMaxCurrentLabel != null)
                batteryMaxCurrentLabel.Text = batteryMaxContinuousA > 0 ? $"{batteryMaxContinuousA:0} A" : "--";
            if (powerHoverCurrentLabel != null)
                powerHoverCurrentLabel.Text = currentForEstimate > 0.1f ? $"{currentForEstimate:0.0} A" : "--";
            if (peakCurrentLabel != null)
                peakCurrentLabel.Text = estPeakCurrent > 0.1f ? $"{estPeakCurrent:0.0} A" : "--";
            if (escRatingLabel != null)
                escRatingLabel.Text = escRatingA > 0 ? $"{escRatingA:0} A per motor{(escOverLimit ? " (Overlimit)" : "")}" : "--";

            if (escRatingLabel != null)
                escRatingLabel.ForeColor = escOverLimit ? Color.FromArgb(196, 64, 64) : CurrentPalette.TextPrimary;

            if (payloadMaxLabel != null)
                payloadMaxLabel.Text = maxThrustAvailable > 0 ? $"{maxPayloadSafeKg * 1000f:0} g" : "--";
            if (payloadCurrentLabel != null)
                payloadCurrentLabel.Text = $"{payloadMass * 1000f:0} g";
            if (payloadRemainingLabel != null)
                payloadRemainingLabel.Text = maxThrustAvailable > 0 ? $"{remainingPayloadKg * 1000f:0} g" : "--";

            if (massFrameLabel != null) massFrameLabel.Text = $"{massFrame * 1000f:0} g";
            if (massMotorsLabel != null) massMotorsLabel.Text = $"{massMotors * 1000f:0} g";
            if (massEscLabel != null) massEscLabel.Text = $"{massEsc * 1000f:0} g";
            if (massBatteryLabel != null) massBatteryLabel.Text = $"{massBattery * 1000f:0} g";
            if (massPayloadLabel != null) massPayloadLabel.Text = $"{payloadMass * 1000f:0} g";

            var missing = new List<string>();
            if (frames == 0) missing.Add("Frame");
            if (motors < expectedMotors) missing.Add("Motors");
            if (batteryInst == null) missing.Add("Battery");
            if (expectedEscs > 0 && escs < expectedEscs) missing.Add("ESCs");
            if (props < motors && motors > 0) missing.Add("Props");
            if (fcs == 0) missing.Add("FC");
            if (cams == 0) missing.Add("Camera");
            if (rxs == 0) missing.Add("Receiver");
            if (vtxs == 0) missing.Add("VTX");
            if (missingValueLabel != null) missingValueLabel.Text = missing.Count == 0 ? "None" : string.Join(", ", missing);

            if (overcurrentValueLabel != null)
            {
                if (escOverLimit)
                {
                    overcurrentValueLabel.Text = "ESC Overlimit";
                }
                else if (currentForEstimate <= 0.1f)
                {
                    overcurrentValueLabel.Text = "--";
                }
                else if (currentForEstimate > 80f)
                {
                    overcurrentValueLabel.Text = "High";
                }
                else if (currentForEstimate > 60f)
                {
                    overcurrentValueLabel.Text = "Moderate";
                }
                else
                {
                    overcurrentValueLabel.Text = "Low";
                }
            }

            if (overheatValueLabel != null)
            {
                if (motors == 0)
                {
                    overheatValueLabel.Text = "--";
                }
                else
                {
                    const float motorLimitC = 120f;
                    float tempSlopeCps = 0f;
                    if (telemetry.Count >= 12)
                    {
                        var window = telemetry.Skip(Math.Max(0, telemetry.Count - 60)).ToList();
                        var first = window.First();
                        var last = window.Last();
                        float dt = (float)(last.TimeSec - first.TimeSec);
                        if (dt > 0.1f)
                            tempSlopeCps = (last.MotorTempC - first.MotorTempC) / dt;
                    }

                    if (motorTempC >= motorLimitC)
                    {
                        overheatValueLabel.Text = "Critical";
                    }
                    else if (tempSlopeCps > 0.01f)
                    {
                        float minutesToLimit = (motorLimitC - motorTempC) / tempSlopeCps / 60f;
                        if (minutesToLimit < 30f)
                            overheatValueLabel.Text = $"High (Est {minutesToLimit:0.0} min)";
                        else
                            overheatValueLabel.Text = (motorTempC > 95f || escTempC > 85f) ? "High" : "Normal";
                    }
                    else
                    {
                        overheatValueLabel.Text = (motorTempC > 95f || escTempC > 85f) ? "High" : "Normal";
                    }
                }
            }

            if (structuralValueLabel != null)
            {
                if (frameStressPct <= 0f) structuralValueLabel.Text = "--";
                else if (frameStressPct > 100f) structuralValueLabel.Text = "High";
                else if (frameStressPct > 70f) structuralValueLabel.Text = "Moderate";
                else structuralValueLabel.Text = "Low";
            }

            if (allUpWeightLabel != null) allUpWeightLabel.Text = liveMass > 0 ? $"All-Up Weight: {liveMass * 1000f:0} g" : "All-Up Weight: --";
            if (totalThrustLabel != null) totalThrustLabel.Text = liveThrust > 0 ? $"Total Thrust: {liveThrust:0.0} N" : "Total Thrust: --";
            if (hoverCurrentLabel != null) hoverCurrentLabel.Text = currentForEstimate > 0.1f ? $"Hover Current: {currentForEstimate:0.0} A" : "Hover Current: --";
            if (efficiencyIndexLabel != null)
            {
                if (motors == 0 || efficiencyScore <= 0f)
                    efficiencyIndexLabel.Text = "Power-to-Weight Efficiency Score: --";
                else
                    efficiencyIndexLabel.Text = $"Power-to-Weight Efficiency Score: {efficiencyScore:0} / 100";
            }
            if (stabilityIndexLabel != null)
            {
                if (frames == 0 || motors == 0) stabilityIndexLabel.Text = "Stability Index: --";
                else if (stabilityMarginPct > 0.1f) stabilityIndexLabel.Text = $"Stability Index: {stabilityMarginPct:0}%";
                else stabilityIndexLabel.Text = "Stability Index: --";
            }

            UpdateFeatureSetPanel();
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
                "Cameras" => new CameraAsset { Name = "New Camera", Category = category },
                "GPS" => new GpsAsset { Name = "New GPS", Category = category },
                "VTX" => new VtxAsset { Name = "New VTX", Category = category },
                "Antennas" => new AntennaAsset { Name = "New Antenna", Category = category },
                "Buzzers" => new BuzzerAsset { Name = "New Buzzer", Category = category },
                "LEDs" => new LedAsset { Name = "New LED", Category = category },
                "Custom" => new CustomComponentAsset { Name = "New Custom Component", Category = category },
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

            public override Color MenuStripGradientBegin => Palette.WindowBackground;
            public override Color MenuStripGradientEnd => Palette.WindowBackground;
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
            Battery,
            ESC,
            FlightController,
            Propeller,
            Camera,
            Receiver,
            GPS,
            VTX,
            Antenna,
            Buzzer,
            LED,
            CustomComponent
        }

        enum EscLayout
        {
            FourInOne,
            Arms
        }

        enum Workspace
        {
            Assemble
        }
        enum RibbonIcon
        {
            Create,
            Modify,
            Move,
            Rotate,
            Analyze,
            Mass,
            Inertia,
            Thermal
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
    Propeller,
    Antenna,
    Buzzer,
    LED
}

        enum CustomShape
        {
            Rectangle,
            RoundedRect,
            Circle,
            Triangle,
            Capsule
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
    public string? Label;           // e.g. "ESC1", "FC Stack"
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

        [TypeConverter(typeof(ExpandableObjectConverter))]
        class CustomProperty
        {
            public string Name { get; set; } = "Property";
            public string Value { get; set; } = "";
            public string Unit { get; set; } = "";

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Unit) ? $"{Name}: {Value}" : $"{Name}: {Value} {Unit}";
            }
        }

        class CustomComponentAsset : Asset
        {
            public override MountType RequiredMount => Mount;

            public MountType Mount { get; set; } = MountType.None;
            public float WidthMm { get; set; } = 28f;
            public float HeightMm { get; set; } = 18f;
            public float DepthMm { get; set; } = 8f;
            public float PowerDrawA { get; set; }
            public float VoltageMin { get; set; } = 5f;
            public float VoltageMax { get; set; } = 20f;
            public string SignalType { get; set; } = "UART";
            public string Connector { get; set; } = "JST-SH";

            public CustomShape Shape { get; set; } = CustomShape.RoundedRect;
            public float RotationDeg { get; set; }
            public int CornerRadius { get; set; } = 6;

            [Browsable(false)]
            public int FillColorArgb { get; set; } = Color.FromArgb(200, 90, 132, 255).ToArgb();
            [Browsable(false)]
            public int StrokeColorArgb { get; set; } = Color.FromArgb(200, 26, 36, 54).ToArgb();
            public float StrokeWidth { get; set; } = 1.2f;
            public float Opacity { get; set; } = 1.0f;

            public string ImagePath { get; set; } = "";

            [Editor(typeof(CollectionEditor), typeof(UITypeEditor))]
            public BindingList<CustomProperty> Properties { get; set; } = new();

            [JsonIgnore]
            public Color FillColor
            {
                get => Color.FromArgb(FillColorArgb);
                set => FillColorArgb = value.ToArgb();
            }

            [JsonIgnore]
            public Color StrokeColor
            {
                get => Color.FromArgb(StrokeColorArgb);
                set => StrokeColorArgb = value.ToArgb();
            }
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
            public bool IsFourInOne { get; set; }
            public string VoltageRating { get; set; } = "3S-6S";
        }

        class FlightControllerAsset : Asset
        {
            public override MountType RequiredMount => MountType.FlightController;
            public string MCU { get; set; } = "";
            public int UARTCount { get; set; }
            public bool HasOSD { get; set; }
            public bool HasBlackbox { get; set; }
            public float GyroUpdateRate { get; set; }
            public float MountSizeMm { get; set; } = 30.5f;
            public string Gyro { get; set; } = "MPU6000";
            public bool HasBarometer { get; set; }
        }

        class ReceiverAsset : Asset
        {
            public override MountType RequiredMount => MountType.Receiver;
            public string Protocol { get; set; } = ""; // SBUS, CRSF, ELRS
            public float FrequencyGHz { get; set; }
            public bool Telemetry { get; set; }
        }

        class GpsAsset : Asset
        {
            public override MountType RequiredMount => MountType.GPS;
            public float UpdateRateHz { get; set; } = 10f;
            public float AccuracyM { get; set; } = 1.5f;
            public bool HasCompass { get; set; } = true;
        }

        class VtxAsset : Asset
        {
            public override MountType RequiredMount => MountType.VTX;
            public int MaxPowerMw { get; set; } = 800;
            public int ChannelCount { get; set; } = 40;
            public bool HasPitMode { get; set; } = true;
        }

        class AntennaAsset : Asset
        {
            public override MountType RequiredMount => MountType.Antenna;
            public float GainDbi { get; set; } = 2.2f;
            public string Polarization { get; set; } = "RHCP";
        }

        class BuzzerAsset : Asset
        {
            public override MountType RequiredMount => MountType.Buzzer;
            public float LoudnessDb { get; set; } = 85f;
            public float VoltageMin { get; set; } = 5f;
        }

        class LedAsset : Asset
        {
            public override MountType RequiredMount => MountType.LED;
            public int LedCount { get; set; } = 8;
            public string Color { get; set; } = "RGB";
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

        class CameraAsset : Asset
        {
            public override MountType RequiredMount => MountType.Camera;
            public string Resolution { get; set; } = "4K";
            public float FovDeg { get; set; } = 155f;
            public bool Stabilization { get; set; } = true;
            public string FormFactor { get; set; } = "Micro";
            public string SystemType { get; set; } = "Analog";
        }

        class FrameAsset : Asset
        {
            public float WheelbaseMm { get; set; }
            public int ArmCount { get; set; }
            public float ArmThicknessMm { get; set; }
            public float ArmLengthMm { get; set; }
            public float BodySizeMm { get; set; } = 48f;
            public float MaterialDensity { get; set; } = 1.6f;
            public float CgOffsetXcm { get; set; }
            public float CgOffsetYcm { get; set; }
            public FrameDefinition? Geometry { get; set; }
            public List<MountPoint> Mounts { get; set; } = new();
            public string Style { get; set; } = "X";
            public bool Ducted { get; set; }
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
                    "Cameras" => JsonSerializer.Deserialize<CameraAsset>(json)!,
                    "GPS" => JsonSerializer.Deserialize<GpsAsset>(json)!,
                    "VTX" => JsonSerializer.Deserialize<VtxAsset>(json)!,
                    "Antennas" => JsonSerializer.Deserialize<AntennaAsset>(json)!,
                    "Buzzers" => JsonSerializer.Deserialize<BuzzerAsset>(json)!,
                    "LEDs" => JsonSerializer.Deserialize<LedAsset>(json)!,
                    "Custom" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Payload" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Landing Gear" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Aerodynamic Add-ons" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Telemetry" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Power Distribution" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Safety" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Advanced" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    "Industrial" => JsonSerializer.Deserialize<CustomComponentAsset>(json)!,
                    _ => throw new Exception("Unknown asset type")
                };
            }
        }

        static class AssetLibrary
        {
            public static readonly Dictionary<string, Asset> Assets = new();
            public static readonly Dictionary<string, string> Paths = new();
            public static string? CatalogBaseDir { get; private set; }

            public static readonly string UserAssetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SilvuViewfinder", "Assets");

            public static void LoadDefaults()
            {
                // keep a small set of built-in assets for a nice initial experience
                Assets.Clear();
                Paths.Clear();
                CatalogBaseDir = null;

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

                var esc35 = new ESCAsset
                {
                    Name = "35A ESC",
                    Category = "ESC",
                    ContinuousCurrent = 35f,
                    BurstCurrent = 45f,
                    MassKg = 0.012f,
                    Description = "35A 4-in-1 ESC"
                };
                Assets[esc35.Id] = esc35;

                var esc45 = new ESCAsset
                {
                    Name = "45A ESC",
                    Category = "ESC",
                    ContinuousCurrent = 45f,
                    BurstCurrent = 55f,
                    MassKg = 0.014f,
                    Description = "45A 4-in-1 ESC"
                };
                Assets[esc45.Id] = esc45;

                var fc = new FlightControllerAsset
                {
                    Name = "F7 FC",
                    Category = "FC",
                    MCU = "STM32F7",
                    UARTCount = 6,
                    HasOSD = true,
                    HasBlackbox = true,
                    GyroUpdateRate = 8.0f,
                    MassKg = 0.012f,
                    Description = "F7 flight controller"
                };
                Assets[fc.Id] = fc;

                var prop = new PropellerAsset
                {
                    Name = "5x4.3x3",
                    Category = "Props",
                    DiameterInch = 5.0f,
                    Pitch = 4.3f,
                    BladeCount = 3,
                    AerodynamicDragCoeff = 0.08f,
                    MassKg = 0.003f,
                    Description = "5 inch tri-blade prop"
                };
                Assets[prop.Id] = prop;

                var prop514 = new PropellerAsset
                {
                    Name = "5.1x4.8x3",
                    Category = "Props",
                    DiameterInch = 5.1f,
                    Pitch = 4.8f,
                    BladeCount = 3,
                    AerodynamicDragCoeff = 0.085f,
                    MassKg = 0.0032f,
                    Description = "Aggressive 5.1 inch prop"
                };
                Assets[prop514.Id] = prop514;

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

                var dji = new CameraAsset
                {
                    Name = "DJI Action",
                    Category = "Cameras",
                    Resolution = "4K",
                    FovDeg = 155f,
                    Stabilization = true,
                    MassKg = 0.063f,
                    Description = "Compact action camera"
                };
                Assets[dji.Id] = dji;

                var gopro = new CameraAsset
                {
                    Name = "GoPro Hero",
                    Category = "Cameras",
                    Resolution = "5.3K",
                    FovDeg = 155f,
                    Stabilization = true,
                    MassKg = 0.154f,
                    Description = "GoPro style action camera"
                };
                Assets[gopro.Id] = gopro;

                var elrs = new ReceiverAsset
                {
                    Name = "ELRS Nano",
                    Category = "Receivers",
                    Protocol = "ELRS",
                    FrequencyGHz = 2.4f,
                    Telemetry = true,
                    MassKg = 0.004f,
                    Description = "ExpressLRS nano receiver"
                };
                Assets[elrs.Id] = elrs;

                var crossfire = new ReceiverAsset
                {
                    Name = "Crossfire Micro",
                    Category = "Receivers",
                    Protocol = "CRSF",
                    FrequencyGHz = 0.915f,
                    Telemetry = true,
                    MassKg = 0.006f,
                    Description = "Long range CRSF receiver"
                };
                Assets[crossfire.Id] = crossfire;

                var gpsM8 = new GpsAsset
                {
                    Name = "M8N GPS",
                    Category = "GPS",
                    UpdateRateHz = 10f,
                    AccuracyM = 1.5f,
                    HasCompass = true,
                    MassKg = 0.012f,
                    Description = "UBlox M8N GPS"
                };
                Assets[gpsM8.Id] = gpsM8;

                var gpsM10 = new GpsAsset
                {
                    Name = "M10 GPS",
                    Category = "GPS",
                    UpdateRateHz = 18f,
                    AccuracyM = 0.9f,
                    HasCompass = true,
                    MassKg = 0.014f,
                    Description = "UBlox M10 high precision GPS"
                };
                Assets[gpsM10.Id] = gpsM10;

                var vtxUnify = new VtxAsset
                {
                    Name = "Unify Pro32",
                    Category = "VTX",
                    MaxPowerMw = 1000,
                    ChannelCount = 40,
                    HasPitMode = true,
                    MassKg = 0.007f,
                    Description = "TBS Unify Pro32 VTX"
                };
                Assets[vtxUnify.Id] = vtxUnify;

                var vtxRush = new VtxAsset
                {
                    Name = "Rush Tank",
                    Category = "VTX",
                    MaxPowerMw = 800,
                    ChannelCount = 40,
                    HasPitMode = true,
                    MassKg = 0.009f,
                    Description = "Rush Tank VTX"
                };
                Assets[vtxRush.Id] = vtxRush;

                var antennaLollipop = new AntennaAsset
                {
                    Name = "Lollipop RHCP",
                    Category = "Antennas",
                    GainDbi = 2.2f,
                    Polarization = "RHCP",
                    MassKg = 0.004f,
                    Description = "Omni FPV antenna"
                };
                Assets[antennaLollipop.Id] = antennaLollipop;

                var antennaStubby = new AntennaAsset
                {
                    Name = "Stubby RHCP",
                    Category = "Antennas",
                    GainDbi = 1.6f,
                    Polarization = "RHCP",
                    MassKg = 0.003f,
                    Description = "Compact stubby antenna"
                };
                Assets[antennaStubby.Id] = antennaStubby;

                var buzzer = new BuzzerAsset
                {
                    Name = "5V Buzzer",
                    Category = "Buzzers",
                    LoudnessDb = 88f,
                    VoltageMin = 5f,
                    MassKg = 0.004f,
                    Description = "Active 5V buzzer"
                };
                Assets[buzzer.Id] = buzzer;

                var buzzerSelf = new BuzzerAsset
                {
                    Name = "Self-Powered Buzzer",
                    Category = "Buzzers",
                    LoudnessDb = 95f,
                    VoltageMin = 3.7f,
                    MassKg = 0.006f,
                    Description = "Battery-backed buzzer"
                };
                Assets[buzzerSelf.Id] = buzzerSelf;

                var ledStrip = new LedAsset
                {
                    Name = "RGB LED Strip",
                    Category = "LEDs",
                    LedCount = 12,
                    Color = "RGB",
                    MassKg = 0.005f,
                    Description = "Flexible LED strip"
                };
                Assets[ledStrip.Id] = ledStrip;

                var ledSingle = new LedAsset
                {
                    Name = "Single LED",
                    Category = "LEDs",
                    LedCount = 1,
                    Color = "White",
                    MassKg = 0.001f,
                    Description = "Status indicator LED"
                };
                Assets[ledSingle.Id] = ledSingle;

                var customModule = new CustomComponentAsset
                {
                    Name = "Telemetry Radio",
                    Category = "Custom",
                    WidthMm = 22f,
                    HeightMm = 14f,
                    DepthMm = 4f,
                    MassKg = 0.006f,
                    PowerDrawA = 0.25f,
                    VoltageMin = 5f,
                    VoltageMax = 16f,
                    SignalType = "UART",
                    Connector = "GH-4",
                    Shape = CustomShape.RoundedRect,
                    CornerRadius = 6,
                    Description = "Custom long-range telemetry module"
                };
                customModule.Properties.Add(new CustomProperty { Name = "Range", Value = "30", Unit = "km" });
                customModule.Properties.Add(new CustomProperty { Name = "Protocol", Value = "CRSF" });
                Assets[customModule.Id] = customModule;

                FrameAsset CreateFrame(string name, FrameDefinition def, int armCount, int wheelbaseMm, float massKg, float armThicknessMm, string description)
                {
                    float armLen = 0f;
                    if (def.MotorMounts.Length > 0)
                        armLen = def.MotorMounts.Max(p => MathF.Sqrt(p.X * p.X + p.Y * p.Y));

                    var frame = new FrameAsset
                    {
                        Name = name,
                        Category = "Frames",
                        Geometry = def,
                        ArmCount = armCount,
                        ArmThicknessMm = armThicknessMm,
                        ArmLengthMm = armLen,
                        BodySizeMm = Math.Clamp(armLen * 0.35f, 40f, 70f),
                        WheelbaseMm = wheelbaseMm,
                        MassKg = massKg,
                        Description = description
                    };

                    float fcSize = Math.Clamp(frame.BodySizeMm * 0.65f, 28f, 56f);
                    float rxOffset = Math.Clamp(frame.BodySizeMm * 0.9f, 36f, 70f);
                    float camOffset = Math.Clamp(frame.BodySizeMm * 1.5f, 60f, 105f);
                    float vtxOffset = -Math.Clamp(frame.BodySizeMm * 0.9f, 40f, 80f);
                    float gpsOffset = -Math.Clamp(frame.BodySizeMm * 1.4f, 65f, 110f);
                    float antennaOffset = -Math.Clamp(frame.BodySizeMm * 1.8f, 85f, 130f);
                    float buzzerOffset = -Math.Clamp(frame.BodySizeMm * 1.1f, 50f, 95f);
                    float ledOffset = Math.Clamp(frame.BodySizeMm * 1.4f, 60f, 110f);
                    float rxSize = Math.Clamp(fcSize * 0.55f, 18f, 34f);
                    float camW = Math.Clamp(fcSize * 0.9f, 24f, 48f);
                    float camH = Math.Clamp(fcSize * 0.6f, 18f, 34f);

                    for (int i = 0; i < def.MotorMounts.Length; i++)
                    {
                        frame.Mounts.Add(new MountPoint
                        {
                            Type = MountType.Motor,
                            Position = def.MotorMounts[i],
                            Size = new SizeF(20, 20),
                            Label = $"M{i + 1}"
                        });

                        frame.Mounts.Add(new MountPoint
                        {
                            Type = MountType.ESC,
                            Position = new PointF(def.MotorMounts[i].X * 0.7f, def.MotorMounts[i].Y * 0.7f),
                            Size = new SizeF(24, 36),
                            Label = $"ESC{i + 1}"
                        });
                    }

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.FlightController,
                        Position = new PointF(0, 0),
                        Size = new SizeF(fcSize, fcSize),
                        Label = "FC Stack"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.Receiver,
                        Position = new PointF(0, rxOffset),
                        Size = new SizeF(rxSize, rxSize),
                        Label = "RX"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.VTX,
                        Position = new PointF(0, vtxOffset),
                        Size = new SizeF(26, 20),
                        Label = "VTX"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.GPS,
                        Position = new PointF(0, gpsOffset),
                        Size = new SizeF(24, 24),
                        Label = "GPS"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.Antenna,
                        Position = new PointF(0, antennaOffset),
                        Size = new SizeF(10, 24),
                        Label = "Antenna"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.Buzzer,
                        Position = new PointF(0, buzzerOffset),
                        Size = new SizeF(18, 12),
                        Label = "Buzzer"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.LED,
                        Position = new PointF(0, ledOffset),
                        Size = new SizeF(24, 8),
                        Label = "LED"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.Camera,
                        Position = new PointF(0, camOffset),
                        Size = new SizeF(camW, camH),
                        Label = "Camera"
                    });

                    var bay = def.BatteryBay;
                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.Battery,
                        Position = new PointF(bay.X + bay.Width / 2f, bay.Y + bay.Height / 2f),
                        Size = new SizeF(bay.Width, bay.Height),
                        Label = "Battery Tray"
                    });

                    return frame;
                }

                var frames = new[]
                {
                    CreateFrame("X Frame", FrameDB.XFrame, 4, 300, 0.15f, 4.0f, "Standard 5-inch X frame"),
                    CreateFrame("Plus Frame", FrameDB.PlusFrame, 4, 320, 0.16f, 4.2f, "Plus layout frame"),
                    CreateFrame("H Frame", FrameDB.HFrame, 4, 340, 0.17f, 4.5f, "Wide H layout frame"),
                    CreateFrame("Y Frame", FrameDB.YFrame, 3, 280, 0.14f, 4.0f, "Tri-arm Y frame"),
                    CreateFrame("Custom Frame", FrameDB.CustomFrame, 4, 320, 0.16f, 4.0f, "Custom layout frame")
                };

                foreach (var frame in frames)
                    Assets[frame.Id] = frame;

                LoadCatalog();
            }

            static void LoadCatalog()
            {
                var path = ResolveCatalogPath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return;

                try
                {
                    CatalogBaseDir = Path.GetDirectoryName(path);
                    var json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new JsonStringEnumConverter());

                    var catalog = JsonSerializer.Deserialize<CatalogRoot>(json, options);
                    if (catalog == null) return;

                    foreach (var frame in catalog.Frames)
                    {
                        var asset = CreateFrameFromCatalog(frame);
                        AddAssetIfMissing(asset);
                    }

                    foreach (var motor in catalog.Motors)
                    {
                        var asset = CreateMotorFromCatalog(motor);
                        AddAssetIfMissing(asset);
                    }

                    foreach (var prop in catalog.Props)
                    {
                        var asset = new PropellerAsset
                        {
                            Name = prop.Name,
                            Category = "Props",
                            DiameterInch = prop.DiameterInch,
                            Pitch = prop.Pitch,
                            BladeCount = prop.BladeCount,
                            AerodynamicDragCoeff = prop.AerodynamicDragCoeff > 0 ? prop.AerodynamicDragCoeff : 0.08f,
                            MassKg = prop.MassKg > 0 ? prop.MassKg : Math.Clamp(prop.DiameterInch * 0.0003f, 0.0008f, 0.01f),
                            Description = prop.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var esc in catalog.Escs)
                    {
                        var asset = new ESCAsset
                        {
                            Name = esc.Name,
                            Category = "ESC",
                            ContinuousCurrent = esc.ContinuousCurrent,
                            BurstCurrent = esc.BurstCurrent,
                            MassKg = esc.MassKg,
                            IsFourInOne = esc.IsFourInOne,
                            VoltageRating = esc.VoltageRating ?? "3S-6S",
                            Description = esc.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var battery in catalog.Batteries)
                    {
                        var asset = new BatteryAsset
                        {
                            Name = battery.Name,
                            Category = "Batteries",
                            Cells = battery.Cells,
                            VoltageNominal = battery.VoltageNominal,
                            CapacityAh = battery.CapacityAh,
                            MaxDischargeC = battery.MaxDischargeC,
                            Chemistry = battery.Chemistry,
                            MassKg = battery.MassKg,
                            Description = battery.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var fc in catalog.FlightControllers)
                    {
                        var asset = new FlightControllerAsset
                        {
                            Name = fc.Name,
                            Category = "FC",
                            MCU = fc.Mcu,
                            UARTCount = fc.UartCount,
                            HasOSD = fc.HasOsd,
                            HasBlackbox = fc.HasBlackbox,
                            GyroUpdateRate = fc.GyroUpdateRate,
                            MountSizeMm = fc.MountSizeMm,
                            Gyro = fc.Gyro ?? "MPU6000",
                            HasBarometer = fc.HasBarometer,
                            MassKg = fc.MassKg,
                            Description = fc.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var rx in catalog.Receivers)
                    {
                        var asset = new ReceiverAsset
                        {
                            Name = rx.Name,
                            Category = "Receivers",
                            Protocol = rx.Protocol,
                            FrequencyGHz = rx.FrequencyGhz,
                            Telemetry = rx.Telemetry,
                            MassKg = rx.MassKg,
                            Description = rx.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var cam in catalog.Cameras)
                    {
                        var asset = new CameraAsset
                        {
                            Name = cam.Name,
                            Category = "Cameras",
                            Resolution = cam.Resolution,
                            FovDeg = cam.FovDeg,
                            Stabilization = cam.Stabilization,
                            FormFactor = cam.FormFactor ?? "Micro",
                            SystemType = cam.SystemType ?? "Analog",
                            MassKg = cam.MassKg,
                            Description = cam.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var gps in catalog.GpsModules)
                    {
                        var asset = new GpsAsset
                        {
                            Name = gps.Name,
                            Category = "GPS",
                            UpdateRateHz = gps.UpdateRateHz,
                            AccuracyM = gps.AccuracyM,
                            HasCompass = gps.HasCompass,
                            MassKg = gps.MassKg,
                            Description = gps.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var vtx in catalog.Vtx)
                    {
                        var asset = new VtxAsset
                        {
                            Name = vtx.Name,
                            Category = "VTX",
                            MaxPowerMw = vtx.MaxPowerMw,
                            ChannelCount = vtx.ChannelCount,
                            HasPitMode = vtx.HasPitMode,
                            MassKg = vtx.MassKg,
                            Description = vtx.Description ?? ""
                        };
                        AddAssetIfMissing(asset);
                    }

                    foreach (var custom in catalog.Custom)
                    {
                        var asset = CreateCustomFromCatalog(custom);
                        AddAssetIfMissing(asset);
                    }
                }
                catch
                {
                    // ignore catalog load failures
                }
            }

            static string? ResolveCatalogPath()
            {
                var candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "catalog.json"),
                    Path.Combine(Environment.CurrentDirectory, "catalog.json")
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                        return path;
                }

                return null;
            }

            static void AddAssetIfMissing(Asset asset)
            {
                if (Assets.Values.Any(a => a.Category == asset.Category && a.Name.Equals(asset.Name, StringComparison.OrdinalIgnoreCase)))
                    return;

                Assets[asset.Id] = asset;
            }

            static FrameAsset CreateFrameFromCatalog(CatalogFrame frame)
            {
                var def = BuildFrameDefinition(frame.Style ?? "X", frame.ArmCount > 0 ? frame.ArmCount : 4, frame.WheelbaseMm);
                float massKg = frame.MassKg > 0 ? frame.MassKg : EstimateFrameMass(frame.WheelbaseMm, frame.ArmThicknessMm);
                float bodySize = frame.BodySizeMm > 0 ? frame.BodySizeMm : EstimateBodySize(frame.WheelbaseMm);
                var asset = BuildFrame(frame.Name, def, frame.ArmCount, frame.WheelbaseMm, massKg, frame.ArmThicknessMm, frame.Description ?? "");
                asset.BodySizeMm = bodySize;
                asset.Style = frame.Style ?? "X";
                asset.Ducted = frame.Ducted;
                return asset;
            }

            static MotorAsset CreateMotorFromCatalog(CatalogMotor motor)
            {
                var (d, h) = ParseStator(motor.Stator);
                float volume = MathF.PI * (d / 2f) * (d / 2f) * h;
                float massKg = motor.MassKg > 0 ? motor.MassKg : Math.Clamp(volume * 0.000012f, 0.002f, 0.25f);
                float maxCurrent = motor.MaxCurrentA > 0 ? motor.MaxCurrentA : Math.Clamp(d * h * 0.25f, 4f, 220f);
                float maxThrust = motor.MaxThrustN > 0 ? motor.MaxThrustN : Math.Clamp(d * h * 0.1f, 1.5f, 90f);

                var (vmin, vmax) = EstimateMotorVoltage(motor.Kv);
                return new MotorAsset
                {
                    Name = motor.Name,
                    Category = "Motors",
                    KV = motor.Kv,
                    MaxRPM = motor.MaxRpm > 0 ? motor.MaxRpm : motor.Kv * 16f,
                    MaxCurrent = maxCurrent,
                    MaxThrust = maxThrust,
                    MassKg = massKg,
                    VoltageMin = motor.VoltageMin > 0 ? motor.VoltageMin : vmin,
                    VoltageMax = motor.VoltageMax > 0 ? motor.VoltageMax : vmax,
                    Description = motor.Description ?? ""
                };
            }

            static CustomComponentAsset CreateCustomFromCatalog(CatalogCustom custom)
            {
                var asset = new CustomComponentAsset
                {
                    Name = custom.Name,
                    Category = string.IsNullOrWhiteSpace(custom.Category) ? "Custom" : custom.Category!,
                    WidthMm = custom.WidthMm,
                    HeightMm = custom.HeightMm,
                    DepthMm = custom.DepthMm,
                    MassKg = custom.MassKg,
                    PowerDrawA = custom.PowerDrawA,
                    VoltageMin = custom.VoltageMin,
                    VoltageMax = custom.VoltageMax,
                    SignalType = custom.SignalType ?? "",
                    Connector = custom.Connector ?? "",
                    Shape = ParseEnum(custom.Shape, CustomShape.RoundedRect),
                    RotationDeg = custom.RotationDeg,
                    CornerRadius = custom.CornerRadius > 0 ? custom.CornerRadius : 6,
                    Opacity = custom.Opacity > 0 ? custom.Opacity : 1f,
                    ImagePath = custom.ImagePath ?? "",
                    Description = custom.Description ?? ""
                };

                asset.Mount = ParseEnum(custom.Mount, MountType.None);

                if (!string.IsNullOrWhiteSpace(custom.FillColor))
                    asset.FillColor = ParseColor(custom.FillColor);
                if (!string.IsNullOrWhiteSpace(custom.StrokeColor))
                    asset.StrokeColor = ParseColor(custom.StrokeColor);

                if (custom.Properties != null)
                {
                    foreach (var prop in custom.Properties)
                    {
                        asset.Properties.Add(new CustomProperty
                        {
                            Name = prop.Name ?? "Property",
                            Value = prop.Value ?? "",
                            Unit = prop.Unit ?? ""
                        });
                    }
                }

                return asset;
            }

            static FrameAsset BuildFrame(string name, FrameDefinition def, int armCount, int wheelbaseMm, float massKg, float armThicknessMm, string description)
            {
                float armLen = 0f;
                if (def.MotorMounts.Length > 0)
                    armLen = def.MotorMounts.Max(p => MathF.Sqrt(p.X * p.X + p.Y * p.Y));

                var frame = new FrameAsset
                {
                    Name = name,
                    Category = "Frames",
                    Geometry = def,
                    ArmCount = armCount,
                    ArmThicknessMm = armThicknessMm,
                    ArmLengthMm = armLen,
                    BodySizeMm = Math.Clamp(armLen * 0.35f, 40f, 70f),
                    WheelbaseMm = wheelbaseMm,
                    MassKg = massKg,
                    Description = description
                };

                float fcSize = Math.Clamp(frame.BodySizeMm * 0.65f, 28f, 56f);
                float rxOffset = Math.Clamp(frame.BodySizeMm * 0.9f, 36f, 70f);
                float camOffset = Math.Clamp(frame.BodySizeMm * 1.5f, 60f, 105f);
                float vtxOffset = -Math.Clamp(frame.BodySizeMm * 0.9f, 40f, 80f);
                float gpsOffset = -Math.Clamp(frame.BodySizeMm * 1.4f, 65f, 110f);
                float antennaOffset = -Math.Clamp(frame.BodySizeMm * 1.8f, 85f, 130f);
                float buzzerOffset = -Math.Clamp(frame.BodySizeMm * 1.1f, 50f, 95f);
                float ledOffset = Math.Clamp(frame.BodySizeMm * 1.4f, 60f, 110f);
                float rxSize = Math.Clamp(fcSize * 0.55f, 18f, 34f);
                float camW = Math.Clamp(fcSize * 0.9f, 24f, 48f);
                float camH = Math.Clamp(fcSize * 0.6f, 18f, 34f);

                for (int i = 0; i < def.MotorMounts.Length; i++)
                {
                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.Motor,
                        Position = def.MotorMounts[i],
                        Size = new SizeF(20, 20),
                        Label = $"M{i + 1}"
                    });

                    frame.Mounts.Add(new MountPoint
                    {
                        Type = MountType.ESC,
                        Position = new PointF(def.MotorMounts[i].X * 0.7f, def.MotorMounts[i].Y * 0.7f),
                        Size = new SizeF(24, 36),
                        Label = $"ESC{i + 1}"
                    });
                }

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.FlightController,
                    Position = new PointF(0, 0),
                    Size = new SizeF(fcSize, fcSize),
                    Label = "FC Stack"
                });

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.Receiver,
                    Position = new PointF(0, rxOffset),
                    Size = new SizeF(rxSize, rxSize),
                    Label = "RX"
                });

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.VTX,
                    Position = new PointF(0, vtxOffset),
                    Size = new SizeF(26, 20),
                    Label = "VTX"
                });

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.GPS,
                    Position = new PointF(0, gpsOffset),
                    Size = new SizeF(24, 24),
                    Label = "GPS"
                });

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.Antenna,
                    Position = new PointF(0, antennaOffset),
                    Size = new SizeF(10, 24),
                    Label = "Antenna"
                });

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.Buzzer,
                    Position = new PointF(0, buzzerOffset),
                    Size = new SizeF(18, 12),
                    Label = "Buzzer"
                });

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.LED,
                    Position = new PointF(0, ledOffset),
                    Size = new SizeF(24, 8),
                    Label = "LED"
                });

                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.Camera,
                    Position = new PointF(0, camOffset),
                    Size = new SizeF(camW, camH),
                    Label = "Camera"
                });

                var bay = def.BatteryBay;
                frame.Mounts.Add(new MountPoint
                {
                    Type = MountType.Battery,
                    Position = new PointF(bay.X + bay.Width / 2f, bay.Y + bay.Height / 2f),
                    Size = new SizeF(bay.Width, bay.Height),
                    Label = "Battery Tray"
                });

                return frame;
            }

            static FrameDefinition BuildFrameDefinition(string style, int armCount, int wheelbaseMm)
            {
                float r = wheelbaseMm / 2f;
                var mounts = new List<PointF>();
                string normalized = style.Trim();

                if (normalized.Equals("Y6", StringComparison.OrdinalIgnoreCase))
                {
                    float armR = r * 0.7f;
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (float)(Math.PI * 2 * i / 3.0);
                        var basePos = new PointF(MathF.Cos(angle) * armR, MathF.Sin(angle) * armR);
                        mounts.Add(basePos);
                        mounts.Add(new PointF(basePos.X * 0.92f, basePos.Y * 0.92f));
                    }
                }
                else if (armCount == 6 || normalized.Equals("Hexacopter", StringComparison.OrdinalIgnoreCase))
                {
                    float armR = r * 0.75f;
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = (float)(Math.PI * 2 * i / 6.0);
                        mounts.Add(new PointF(MathF.Cos(angle) * armR, MathF.Sin(angle) * armR));
                    }
                }
                else if (armCount == 8 || normalized.Equals("Octocopter", StringComparison.OrdinalIgnoreCase))
                {
                    float armR = r * 0.78f;
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = (float)(Math.PI * 2 * i / 8.0);
                        mounts.Add(new PointF(MathF.Cos(angle) * armR, MathF.Sin(angle) * armR));
                    }
                }
                else if (normalized.Equals("H", StringComparison.OrdinalIgnoreCase))
                {
                    float x = r * 0.85f;
                    float y = r * 0.45f;
                    mounts.Add(new PointF(-x, -y));
                    mounts.Add(new PointF(x, -y));
                    mounts.Add(new PointF(-x, y));
                    mounts.Add(new PointF(x, y));
                }
                else if (normalized.Equals("Deadcat", StringComparison.OrdinalIgnoreCase))
                {
                    float x = r * 0.8f;
                    mounts.Add(new PointF(-x, -r * 0.55f));
                    mounts.Add(new PointF(x, -r * 0.55f));
                    mounts.Add(new PointF(-x, r * 0.95f));
                    mounts.Add(new PointF(x, r * 0.95f));
                }
                else if (normalized.Equals("StretchX", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Stretch X", StringComparison.OrdinalIgnoreCase))
                {
                    float x = r * 0.75f;
                    float y = r * 1.05f;
                    mounts.Add(new PointF(-x, -y));
                    mounts.Add(new PointF(x, -y));
                    mounts.Add(new PointF(-x, y));
                    mounts.Add(new PointF(x, y));
                }
                else if (normalized.Equals("FixedWing", StringComparison.OrdinalIgnoreCase))
                {
                    float x = r * 1.1f;
                    mounts.Add(new PointF(-x, 0));
                    mounts.Add(new PointF(x, 0));
                }
                else
                {
                    float armR = r / MathF.Sqrt(2f);
                    mounts.Add(new PointF(-armR, -armR));
                    mounts.Add(new PointF(armR, -armR));
                    mounts.Add(new PointF(-armR, armR));
                    mounts.Add(new PointF(armR, armR));
                }

                var def = new FrameDefinition
                {
                    MotorMounts = mounts.ToArray(),
                    Size = new SizeF(wheelbaseMm, wheelbaseMm),
                    BatteryBay = new RectangleF(-r * 0.25f, -r * 0.12f, r * 0.5f, r * 0.24f)
                };
                return def;
            }

            static float EstimateFrameMass(int wheelbaseMm, float armThicknessMm)
            {
                float baseMass = wheelbaseMm * 0.0007f;
                float thicknessBoost = Math.Clamp(armThicknessMm / 4f, 0.7f, 2.0f);
                return Math.Clamp(baseMass * thicknessBoost, 0.02f, 1.2f);
            }

            static float EstimateBodySize(int wheelbaseMm)
            {
                return Math.Clamp(wheelbaseMm * 0.22f, 35f, 140f);
            }

            static (float Diameter, float Height) ParseStator(string stator)
            {
                var s = stator.Trim();
                if (s.Length < 4)
                    return (22f, 7f);

                string dStr = s.Substring(0, 2);
                string hStr = s.Substring(2);
                if (!float.TryParse(dStr, out var d))
                    d = 22f;
                if (!float.TryParse(hStr, out var h))
                    h = 7f;
                return (d, h);
            }

            static (float MinV, float MaxV) EstimateMotorVoltage(int kv)
            {
                if (kv >= 3500) return (7.4f, 14.8f);
                if (kv >= 2200) return (11.1f, 14.8f);
                if (kv >= 1500) return (14.8f, 22.2f);
                if (kv >= 1000) return (22.2f, 25.2f);
                return (22.2f, 44.4f);
            }

            static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
            {
                if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, true, out var parsed))
                    return parsed;
                return fallback;
            }

            static Color ParseColor(string value)
            {
                if (value.StartsWith("#", StringComparison.Ordinal))
                    value = value[1..];
                if (value.Length == 6)
                {
                    int r = Convert.ToInt32(value.Substring(0, 2), 16);
                    int g = Convert.ToInt32(value.Substring(2, 2), 16);
                    int b = Convert.ToInt32(value.Substring(4, 2), 16);
                    return Color.FromArgb(255, r, g, b);
                }
                if (value.Length == 8)
                {
                    int a = Convert.ToInt32(value.Substring(0, 2), 16);
                    int r = Convert.ToInt32(value.Substring(2, 2), 16);
                    int g = Convert.ToInt32(value.Substring(4, 2), 16);
                    int b = Convert.ToInt32(value.Substring(6, 2), 16);
                    return Color.FromArgb(a, r, g, b);
                }
                return Color.FromArgb(200, 90, 132, 255);
            }

            sealed class CatalogRoot
            {
                public List<CatalogFrame> Frames { get; set; } = new();
                public List<CatalogMotor> Motors { get; set; } = new();
                public List<CatalogProp> Props { get; set; } = new();
                public List<CatalogEsc> Escs { get; set; } = new();
                public List<CatalogBattery> Batteries { get; set; } = new();
                public List<CatalogFlightController> FlightControllers { get; set; } = new();
                public List<CatalogReceiver> Receivers { get; set; } = new();
                public List<CatalogCamera> Cameras { get; set; } = new();
                public List<CatalogGps> GpsModules { get; set; } = new();
                public List<CatalogVtx> Vtx { get; set; } = new();
                public List<CatalogCustom> Custom { get; set; } = new();
            }

            sealed class CatalogFrame
            {
                public string Name { get; set; } = "";
                public string? Style { get; set; }
                public int WheelbaseMm { get; set; }
                public int ArmCount { get; set; } = 4;
                public float ArmThicknessMm { get; set; } = 4f;
                public float BodySizeMm { get; set; }
                public float MassKg { get; set; }
                public bool Ducted { get; set; }
                public string? Description { get; set; }
            }

            sealed class CatalogMotor
            {
                public string Name { get; set; } = "";
                public string Stator { get; set; } = "2207";
                public int Kv { get; set; }
                public float MaxCurrentA { get; set; }
                public float MaxThrustN { get; set; }
                public float MaxRpm { get; set; }
                public float MassKg { get; set; }
                public float VoltageMin { get; set; }
                public float VoltageMax { get; set; }
                public string? Description { get; set; }
            }

            sealed class CatalogProp
            {
                public string Name { get; set; } = "";
                public float DiameterInch { get; set; }
                public float Pitch { get; set; }
                public int BladeCount { get; set; } = 2;
                public float MassKg { get; set; }
                public float AerodynamicDragCoeff { get; set; }
                public string? Description { get; set; }
            }

            sealed class CatalogEsc
            {
                public string Name { get; set; } = "";
                public float ContinuousCurrent { get; set; }
                public float BurstCurrent { get; set; }
                public bool IsFourInOne { get; set; }
                public string? VoltageRating { get; set; }
                public float MassKg { get; set; }
                public string? Description { get; set; }
            }

            sealed class CatalogBattery
            {
                public string Name { get; set; } = "";
                public int Cells { get; set; }
                public float VoltageNominal { get; set; }
                public float CapacityAh { get; set; }
                public float MaxDischargeC { get; set; }
                public BatteryChemistry Chemistry { get; set; } = BatteryChemistry.LiPo;
                public float MassKg { get; set; }
                public string? Description { get; set; }
            }

            sealed class CatalogFlightController
            {
                public string Name { get; set; } = "";
                public string Mcu { get; set; } = "F7";
                public int UartCount { get; set; } = 6;
                public bool HasOsd { get; set; }
                public bool HasBlackbox { get; set; }
                public float GyroUpdateRate { get; set; } = 8f;
                public float MountSizeMm { get; set; } = 30.5f;
                public string? Gyro { get; set; }
                public bool HasBarometer { get; set; }
                public float MassKg { get; set; } = 0.012f;
                public string? Description { get; set; }
            }

            sealed class CatalogReceiver
            {
                public string Name { get; set; } = "";
                public string Protocol { get; set; } = "ELRS";
                public float FrequencyGhz { get; set; } = 2.4f;
                public bool Telemetry { get; set; } = true;
                public float MassKg { get; set; } = 0.004f;
                public string? Description { get; set; }
            }

            sealed class CatalogCamera
            {
                public string Name { get; set; } = "";
                public string Resolution { get; set; } = "1080p";
                public float FovDeg { get; set; } = 155f;
                public bool Stabilization { get; set; }
                public string? FormFactor { get; set; }
                public string? SystemType { get; set; }
                public float MassKg { get; set; } = 0.02f;
                public string? Description { get; set; }
            }

            sealed class CatalogGps
            {
                public string Name { get; set; } = "";
                public float UpdateRateHz { get; set; } = 10f;
                public float AccuracyM { get; set; } = 1.5f;
                public bool HasCompass { get; set; } = true;
                public float MassKg { get; set; } = 0.012f;
                public string? Description { get; set; }
            }

            sealed class CatalogVtx
            {
                public string Name { get; set; } = "";
                public int MaxPowerMw { get; set; } = 800;
                public int ChannelCount { get; set; } = 40;
                public bool HasPitMode { get; set; } = true;
                public float MassKg { get; set; } = 0.008f;
                public string? Description { get; set; }
            }

            sealed class CatalogCustom
            {
                public string Name { get; set; } = "";
                public string? Category { get; set; }
                public float WidthMm { get; set; } = 24f;
                public float HeightMm { get; set; } = 16f;
                public float DepthMm { get; set; } = 6f;
                public float MassKg { get; set; } = 0.01f;
                public float PowerDrawA { get; set; }
                public float VoltageMin { get; set; } = 5f;
                public float VoltageMax { get; set; } = 20f;
                public string? SignalType { get; set; }
                public string? Connector { get; set; }
                public string? Shape { get; set; }
                public string? Mount { get; set; }
                public int CornerRadius { get; set; } = 6;
                public float RotationDeg { get; set; }
                public float Opacity { get; set; } = 1f;
                public string? FillColor { get; set; }
                public string? StrokeColor { get; set; }
                public string? ImagePath { get; set; }
                public string? Description { get; set; }
                public List<CustomProperty>? Properties { get; set; }
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

                if (asset is CustomComponentAsset custom && !string.IsNullOrWhiteSpace(custom.ImagePath))
                {
                    string sourcePath = custom.ImagePath;
                    if (!Path.IsPathRooted(sourcePath) && Paths.TryGetValue(asset.Id, out var currentAssetPath))
                    {
                        var currentDir = Path.GetDirectoryName(currentAssetPath);
                        if (!string.IsNullOrWhiteSpace(currentDir))
                            sourcePath = Path.Combine(currentDir, sourcePath);
                    }

                    if (File.Exists(sourcePath))
                    {
                        string imgDir = Path.Combine(dir, "images");
                        Directory.CreateDirectory(imgDir);
                        string ext = Path.GetExtension(sourcePath);
                        string imgName = SanitizeFileName(custom.Name) + ext;
                        string imgPath = Path.Combine(imgDir, imgName);
                        int suffix = 1;
                        while (File.Exists(imgPath))
                        {
                            imgPath = Path.Combine(imgDir, $"{SanitizeFileName(custom.Name)}_{suffix}{ext}");
                            suffix++;
                        }
                        File.Copy(sourcePath, imgPath, overwrite: true);
                        custom.ImagePath = Path.Combine("images", Path.GetFileName(imgPath));
                    }
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

                if (asset is CustomComponentAsset custom)
                {
                    var previewHost = new Panel { Dock = DockStyle.Top, Height = 170, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8) };
                    var previewCanvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
                    var imageButtons = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Bottom,
                        Height = 32,
                        FlowDirection = FlowDirection.LeftToRight,
                        WrapContents = false
                    };

                    var btnPickImage = new Button { Text = "Pick Image", Width = 100, Height = 26 };
                    var btnClearImage = new Button { Text = "Clear Image", Width = 100, Height = 26 };
                    StyleButton(btnPickImage, false);
                    StyleButton(btnClearImage, false);

                    btnPickImage.Click += (_, __) =>
                    {
                        using var ofd = new OpenFileDialog
                        {
                            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
                        };
                        if (ofd.ShowDialog(this) == DialogResult.OK)
                        {
                            custom.ImagePath = ofd.FileName;
                            previewCanvas.Invalidate();
                        }
                    };
                    btnClearImage.Click += (_, __) =>
                    {
                        custom.ImagePath = "";
                        previewCanvas.Invalidate();
                    };

                    imageButtons.Controls.Add(btnPickImage);
                    imageButtons.Controls.Add(btnClearImage);

                    previewCanvas.Paint += (_, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        var rect = previewCanvas.ClientRectangle;
                        rect.Inflate(-16, -16);

                        if (!string.IsNullOrWhiteSpace(custom.ImagePath))
                        {
                            var resolved = ResolveImagePath(custom, custom.ImagePath);
                            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                            {
                                using var fs = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                using var ms = new MemoryStream();
                                fs.CopyTo(ms);
                                ms.Position = 0;
                                using var img = Image.FromStream(ms);
                                e.Graphics.DrawImage(img, rect);
                                using var border = new Pen(Color.FromArgb(140, 120, 130, 140), 1f);
                                e.Graphics.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                                return;
                            }
                        }

                        using var shapePath = BuildPreviewShape(custom.Shape, rect, custom.CornerRadius);
                        var fillColor = Color.FromArgb(custom.FillColorArgb);
                        var strokeColor = Color.FromArgb(custom.StrokeColorArgb);
                        int fillAlpha = (int)Math.Clamp(fillColor.A * custom.Opacity, 0, 255);
                        int strokeAlpha = (int)Math.Clamp(strokeColor.A * custom.Opacity, 0, 255);
                        using var fill = new SolidBrush(Color.FromArgb(fillAlpha, fillColor));
                        using var stroke = new Pen(Color.FromArgb(strokeAlpha, strokeColor), Math.Max(1f, custom.StrokeWidth));
                        e.Graphics.FillPath(fill, shapePath);
                        e.Graphics.DrawPath(stroke, shapePath);
                    };

                    grid.PropertyValueChanged += (_, __) => previewCanvas.Invalidate();

                    previewHost.Controls.Add(previewCanvas);
                    previewHost.Controls.Add(imageButtons);
                    Controls.Add(previewHost);
                    Controls.SetChildIndex(previewHost, 0);
                }
            }

            string ResolveImagePath(CustomComponentAsset custom, string path)
            {
                if (Path.IsPathRooted(path)) return path;
                if (AssetLibrary.Paths.TryGetValue(custom.Id, out var assetPath))
                {
                    var dir = Path.GetDirectoryName(assetPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                        return Path.Combine(dir, path);
                }
                return path;
            }

            GraphicsPath BuildPreviewShape(CustomShape shape, Rectangle rect, float radius)
            {
                var r = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
                var path = new GraphicsPath();
                switch (shape)
                {
                    case CustomShape.Circle:
                        float size = Math.Min(r.Width, r.Height);
                        var circle = new RectangleF(r.X + (r.Width - size) / 2f, r.Y + (r.Height - size) / 2f, size, size);
                        path.AddEllipse(circle);
                        break;
                    case CustomShape.Triangle:
                        var p1 = new PointF(r.X + r.Width / 2f, r.Y);
                        var p2 = new PointF(r.Right, r.Bottom);
                        var p3 = new PointF(r.X, r.Bottom);
                        path.AddPolygon(new[] { p1, p2, p3 });
                        break;
                    case CustomShape.Capsule:
                        if (r.Width >= r.Height)
                        {
                            var left = new RectangleF(r.X, r.Y, r.Height, r.Height);
                            var right = new RectangleF(r.Right - r.Height, r.Y, r.Height, r.Height);
                            path.AddArc(left, 90, 180);
                            path.AddArc(right, 270, 180);
                            path.CloseFigure();
                        }
                        else
                        {
                            var top = new RectangleF(r.X, r.Y, r.Width, r.Width);
                            var bottom = new RectangleF(r.X, r.Bottom - r.Width, r.Width, r.Width);
                            path.AddArc(top, 180, 180);
                            path.AddArc(bottom, 0, 180);
                            path.CloseFigure();
                        }
                        break;
                    case CustomShape.RoundedRect:
                        return BuildRoundedPreview(r, Math.Max(1f, radius));
                    default:
                        path.AddRectangle(r);
                        break;
                }
                return path;
            }

            GraphicsPath BuildRoundedPreview(RectangleF rect, float radius)
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

            public static FrameDefinition PlusFrame = new FrameDefinition
            {
                Size = new SizeF(320, 320),
                MotorMounts = new[]
                {
                    new PointF(0, -140),
                    new PointF(140, 0),
                    new PointF(0, 140),
                    new PointF(-140, 0),
                },
                BatteryBay = new RectangleF(-45, -22, 90, 44)
            };

            public static FrameDefinition HFrame = new FrameDefinition
            {
                Size = new SizeF(340, 220),
                MotorMounts = new[]
                {
                    new PointF(-150, -90),
                    new PointF(150, -90),
                    new PointF(150, 90),
                    new PointF(-150, 90),
                },
                BatteryBay = new RectangleF(-50, -25, 100, 50)
            };

            public static FrameDefinition YFrame = new FrameDefinition
            {
                Size = new SizeF(280, 280),
                MotorMounts = new[]
                {
                    new PointF(0, -140),
                    new PointF(-120, 90),
                    new PointF(120, 90),
                },
                BatteryBay = new RectangleF(-40, 0, 80, 40)
            };

            public static FrameDefinition CustomFrame = new FrameDefinition
            {
                Size = new SizeF(320, 300),
                MotorMounts = new[]
                {
                    new PointF(-140, -110),
                    new PointF(140, -110),
                    new PointF(140, 110),
                    new PointF(-140, 110),
                },
                BatteryBay = new RectangleF(-45, -20, 90, 40)
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
            public static float EscMass() => 0.012f;
            public static float FcMass() => 0.012f;
            public static float CameraMass() => 0.06f;
            public static float PropellerMass() => 0.003f;
            public static float ReceiverMass() => 0.005f;
            public static float GpsMass() => 0.012f;
            public static float VtxMass() => 0.008f;
            public static float AntennaMass() => 0.003f;
            public static float BuzzerMass() => 0.004f;
            public static float LedMass() => 0.003f;

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
            return project != null && project.Instances.Exists(p => p.Type == PartType.Frame);
        }

        PlacedInstance? GetFrame()
        {
            return project?.Instances.Find(p => p.Type == PartType.Frame);
        }

        float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        bool HitTestFrame(PointF point)
        {
            var frame = GetFrame();
            if (frame == null) return false;
            var mounts = GetMotorMounts(GetFrameAsset());
            float radius = mounts.Count > 0
                ? mounts.Max(m => Distance(frame.Position, FrameToWorld(m.Mount.Position)))
                : 140f;
            return Distance(frame.Position, point) <= radius + 12f;
        }

        float GetCurrentArmLengthMm(FrameAsset frameAsset)
        {
            if (frameAsset.ArmLengthMm > 0.1f) return frameAsset.ArmLengthMm;
            var mounts = GetMotorMounts(frameAsset);
            if (mounts.Count == 0) return 150f;
            var center = new PointF(0, 0);
            return mounts.Max(m => Distance(center, m.Mount.Position));
        }

        void ApplyArmLength(FrameAsset frameAsset, float newLengthMm)
        {
            if (newLengthMm <= 10f) return;
            float current = GetCurrentArmLengthMm(frameAsset);
            if (current <= 0.1f) return;
            float scale = newLengthMm / current;

            for (int i = 0; i < frameAsset.Mounts.Count; i++)
            {
                var m = frameAsset.Mounts[i];
                if (m.Type == MountType.Motor || m.Type == MountType.ESC)
                {
                    m.Position = new PointF(m.Position.X * scale, m.Position.Y * scale);
                }
            }

            frameAsset.ArmLengthMm = newLengthMm;
            frameAsset.WheelbaseMm = (float)Math.Round(newLengthMm * 2f);
            if (autoSimUseParts)
                featureProfile.ArmLengthMm = newLengthMm;
        }

        void ApplyBodySize(FrameAsset frameAsset, float newBodyMm)
        {
            if (newBodyMm <= 10f) return;
            newBodyMm = Math.Clamp(newBodyMm, 24f, 200f);
            frameAsset.BodySizeMm = newBodyMm;

            float fcSize = Math.Clamp(newBodyMm * 0.65f, 28f, 56f);
            float receiverOffset = Math.Clamp(newBodyMm * 0.9f, 36f, 70f);
            float cameraOffset = Math.Clamp(newBodyMm * 1.5f, 60f, 105f);
            float vtxOffset = -Math.Clamp(newBodyMm * 0.9f, 40f, 80f);
            float gpsOffset = -Math.Clamp(newBodyMm * 1.4f, 65f, 110f);
            float antennaOffset = -Math.Clamp(newBodyMm * 1.8f, 85f, 130f);
            float buzzerOffset = -Math.Clamp(newBodyMm * 1.1f, 50f, 95f);
            float ledOffset = Math.Clamp(newBodyMm * 1.4f, 60f, 110f);

            for (int i = 0; i < frameAsset.Mounts.Count; i++)
            {
                var m = frameAsset.Mounts[i];
                if (m.Type == MountType.FlightController)
                {
                    m.Position = new PointF(0, 0);
                    m.Size = new SizeF(fcSize, fcSize);
                }
                else if (m.Type == MountType.Receiver)
                {
                    m.Position = new PointF(0, receiverOffset);
                    float rxSize = Math.Clamp(fcSize * 0.55f, 18f, 34f);
                    m.Size = new SizeF(rxSize, rxSize);
                }
                else if (m.Type == MountType.VTX)
                {
                    m.Position = new PointF(0, vtxOffset);
                    m.Size = new SizeF(26, 20);
                }
                else if (m.Type == MountType.GPS)
                {
                    m.Position = new PointF(0, gpsOffset);
                    m.Size = new SizeF(24, 24);
                }
                else if (m.Type == MountType.Antenna)
                {
                    m.Position = new PointF(0, antennaOffset);
                    m.Size = new SizeF(10, 24);
                }
                else if (m.Type == MountType.Buzzer)
                {
                    m.Position = new PointF(0, buzzerOffset);
                    m.Size = new SizeF(18, 12);
                }
                else if (m.Type == MountType.LED)
                {
                    m.Position = new PointF(0, ledOffset);
                    m.Size = new SizeF(24, 8);
                }
                else if (m.Type == MountType.Camera)
                {
                    m.Position = new PointF(0, cameraOffset);
                    float camW = Math.Clamp(fcSize * 0.9f, 24f, 48f);
                    float camH = Math.Clamp(fcSize * 0.6f, 18f, 34f);
                    m.Size = new SizeF(camW, camH);
                }
            }

            var framePlaced = GetFrame();
            if (project != null && framePlaced != null)
            {
                var fcMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.FlightController);
                if (fcMount != null)
                {
                    foreach (var fc in project.Instances.Where(p => p.Type == PartType.FlightController))
                        fc.Position = FrameToWorld(fcMount.Position);
                }

                var camMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.Camera);
                if (camMount != null)
                {
                    foreach (var cam in project.Instances.Where(p => p.Type == PartType.Camera))
                        cam.Position = FrameToWorld(camMount.Position);
                }

                UpdateMountedSingles(frameAsset);
            }
        }

        void UpdateMountedSingles(FrameAsset frameAsset)
        {
            if (project == null) return;

            var rxMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.Receiver);
            if (rxMount != null)
            {
                foreach (var rx in project.Instances.Where(p => p.Type == PartType.Receiver))
                    rx.Position = FrameToWorld(rxMount.Position);
            }

            var vtxMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.VTX);
            if (vtxMount != null)
            {
                foreach (var vtx in project.Instances.Where(p => p.Type == PartType.VTX))
                    vtx.Position = FrameToWorld(vtxMount.Position);
            }

            var gpsMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.GPS);
            if (gpsMount != null)
            {
                foreach (var gps in project.Instances.Where(p => p.Type == PartType.GPS))
                    gps.Position = FrameToWorld(gpsMount.Position);
            }

            var antennaMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.Antenna);
            if (antennaMount != null)
            {
                foreach (var ant in project.Instances.Where(p => p.Type == PartType.Antenna))
                    ant.Position = FrameToWorld(antennaMount.Position);
            }

            var buzzerMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.Buzzer);
            if (buzzerMount != null)
            {
                foreach (var buzzer in project.Instances.Where(p => p.Type == PartType.Buzzer))
                    buzzer.Position = FrameToWorld(buzzerMount.Position);
            }

            var ledMount = frameAsset.Mounts.FirstOrDefault(m => m.Type == MountType.LED);
            if (ledMount != null)
            {
                foreach (var led in project.Instances.Where(p => p.Type == PartType.LED))
                    led.Position = FrameToWorld(ledMount.Position);
            }
        }

        void ApplyEscLayout(EscLayout layout)
        {
            escLayout = layout;

            if (project == null)
            {
                viewport.Invalidate();
                UpdateStatusBar();
                return;
            }

            var escs = project.Instances.Where(p => p.Type == PartType.ESC).ToList();
            if (layout == EscLayout.FourInOne)
            {
                if (escs.Count > 0)
                {
                    var keep = escs[0];
                    keep.MountIndex = 0;
                    for (int i = escs.Count - 1; i >= 1; i--)
                        project.Instances.Remove(escs[i]);
                }
            }
            else
            {
                var mounts = GetEscMounts(GetFrameAsset());
                int limit = mounts.Count;
                if (limit > 0)
                {
                    int assign = Math.Min(escs.Count, limit);
                    for (int i = 0; i < assign; i++)
                        escs[i].MountIndex = i;
                    for (int i = escs.Count - 1; i >= limit; i--)
                        project.Instances.Remove(escs[i]);
                }
            }

            OnProjectStructureChanged();
        }

        void ConfigureUnitRange(NumericUpDown numeric, decimal minMm, decimal maxMm, string unit)
        {
            if (numeric == null) return;
            decimal min = minMm;
            decimal max = maxMm;
            switch (unit)
            {
                case "cm":
                    min = minMm / 10m;
                    max = maxMm / 10m;
                    break;
                case "in":
                    min = minMm / 25.4m;
                    max = maxMm / 25.4m;
                    break;
            }
            numeric.Minimum = Math.Min(min, max);
            numeric.Maximum = Math.Max(min, max);
        }

        void UpdateFrameTuningUi()
        {
            if (frameArmLengthInput == null || frameArmUnitSelector == null)
                return;

            suppressFrameUiEvents = true;
            try
            {
                var frameAssetNow = GetFrameAsset();
                bool hasFrame = frameAssetNow != null;

                frameArmLengthInput.Enabled = hasFrame;
                frameArmUnitSelector.Enabled = hasFrame;
                if (frameBodySizeInput != null) frameBodySizeInput.Enabled = hasFrame;
                if (frameBodyUnitSelector != null) frameBodyUnitSelector.Enabled = hasFrame;
                if (escLayoutSelector != null) escLayoutSelector.Enabled = hasFrame;

                if (!hasFrame || frameAssetNow == null) return;

                ConfigureUnitRange(frameArmLengthInput, 20m, 1000m, frameArmUnitSelector.SelectedItem?.ToString() ?? "mm");
                float armMm = frameAssetNow.ArmLengthMm > 0 ? frameAssetNow.ArmLengthMm : GetCurrentArmLengthMm(frameAssetNow);
                decimal armValue = ArmLengthFromMm(armMm, frameArmUnitSelector.SelectedItem?.ToString() ?? "mm");
                if (frameArmLengthInput.Value != armValue)
                    frameArmLengthInput.Value = armValue;

                if (frameBodySizeInput != null && frameBodyUnitSelector != null)
                {
                    ConfigureUnitRange(frameBodySizeInput, 20m, 250m, frameBodyUnitSelector.SelectedItem?.ToString() ?? "mm");
                    float bodyMm = frameAssetNow.BodySizeMm > 0 ? frameAssetNow.BodySizeMm : 48f;
                    decimal bodyValue = ArmLengthFromMm(bodyMm, frameBodyUnitSelector.SelectedItem?.ToString() ?? "mm");
                    if (frameBodySizeInput.Value != bodyValue)
                        frameBodySizeInput.Value = bodyValue;
                }

                if (escLayoutSelector != null)
                {
                    int desired = escLayout == EscLayout.FourInOne ? 0 : 1;
                    if (escLayoutSelector.SelectedIndex != desired)
                        escLayoutSelector.SelectedIndex = desired;
                }
            }
            finally
            {
                suppressFrameUiEvents = false;
            }
        }

        float ArmLengthToMm(decimal value, string unit)
        {
            float v = (float)value;
            return unit switch
            {
                "cm" => v * 10f,
                "in" => v * 25.4f,
                _ => v
            };
        }

        decimal ArmLengthFromMm(float mm, string unit)
        {
            return unit switch
            {
                "cm" => (decimal)(mm / 10f),
                "in" => (decimal)(mm / 25.4f),
                _ => (decimal)mm
            };
        }

        string GetPartInfo(string category, string name)
        {
            var asset = AssetLibrary.GetByCategory(category).FirstOrDefault(a => a.Name == name);
            if (asset is CustomComponentAsset customAsset)
                return BuildCustomComponentInfo(customAsset);

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
                           $"Chemistry: {battery.Chemistry}\nC Rating: {battery.MaxDischargeC:0}C\nMass: {battery.MassKg:0.###} kg";
                }
                return $"{name}\nMass: {PhysicsDatabase.BatteryMass()} kg";
            }
            else if (category == "Frames")
            {
                if (name == "X Frame")
                    return $"{name}\nSize: {FrameDB.XFrame.Size.Width} x {FrameDB.XFrame.Size.Height}";
                return $"{name}\nFrame";
            }
            else if (category == "ESC")
            {
                var esc = AssetLibrary.FindByName(name) as ESCAsset;
                if (esc != null)
                    return $"{name}\nCurrent: {esc.ContinuousCurrent:0}A\nBurst: {esc.BurstCurrent:0}A\nVoltage: {esc.VoltageRating}\nMass: {esc.MassKg:0.###} kg";
                return $"{name}\nMass: {PhysicsDatabase.EscMass()} kg";
            }
            else if (category == "FC")
            {
                var fc = AssetLibrary.FindByName(name) as FlightControllerAsset;
                if (fc != null)
                    return $"{name}\nMCU: {fc.MCU}\nMount: {fc.MountSizeMm:0.0}mm\nGyro: {fc.Gyro}\nUARTs: {fc.UARTCount}\nMass: {fc.MassKg:0.###} kg";
                return $"{name}\nMass: {PhysicsDatabase.FcMass()} kg";
            }
            else if (category == "Props")
            {
                var prop = AssetLibrary.FindByName(name) as PropellerAsset;
                if (prop != null)
                    return $"{name}\nDiameter: {prop.DiameterInch:0.0}in\nPitch: {prop.Pitch:0.0}in\nBlades: {prop.BladeCount}";
                return name;
            }
            else if (category == "Cameras")
            {
                var cam = AssetLibrary.FindByName(name) as CameraAsset;
                if (cam != null)
                    return $"{name}\nType: {cam.SystemType}\nForm: {cam.FormFactor}\nResolution: {cam.Resolution}\nFOV: {cam.FovDeg:0}°\nMass: {cam.MassKg:0.###} kg";
                return name;
            }
            else if (category == "Receivers")
            {
                var rx = AssetLibrary.FindByName(name) as ReceiverAsset;
                if (rx != null)
                    return $"{name}\nProtocol: {rx.Protocol}\nFreq: {rx.FrequencyGHz:0.00} GHz\nMass: {rx.MassKg:0.###} kg";
                return name;
            }
            else if (category == "GPS")
            {
                var gps = AssetLibrary.FindByName(name) as GpsAsset;
                if (gps != null)
                    return $"{name}\nRate: {gps.UpdateRateHz:0} Hz\nAccuracy: {gps.AccuracyM:0.0} m\nMass: {gps.MassKg:0.###} kg";
                return name;
            }
            else if (category == "VTX")
            {
                var vtx = AssetLibrary.FindByName(name) as VtxAsset;
                if (vtx != null)
                    return $"{name}\nPower: {vtx.MaxPowerMw} mW\nChannels: {vtx.ChannelCount}\nMass: {vtx.MassKg:0.###} kg";
                return name;
            }
            else if (category == "Antennas")
            {
                var ant = AssetLibrary.FindByName(name) as AntennaAsset;
                if (ant != null)
                    return $"{name}\nGain: {ant.GainDbi:0.0} dBi\nPol: {ant.Polarization}\nMass: {ant.MassKg:0.###} kg";
                return name;
            }
            else if (category == "Buzzers")
            {
                var buzzer = AssetLibrary.FindByName(name) as BuzzerAsset;
                if (buzzer != null)
                    return $"{name}\nLoudness: {buzzer.LoudnessDb:0} dB\nMass: {buzzer.MassKg:0.###} kg";
                return name;
            }
            else if (category == "LEDs")
            {
                var led = AssetLibrary.FindByName(name) as LedAsset;
                if (led != null)
                    return $"{name}\nLEDs: {led.LedCount}\nColor: {led.Color}\nMass: {led.MassKg:0.###} kg";
                return name;
            }
            else if (category == "Custom")
            {
                var custom = AssetLibrary.FindByName(name) as CustomComponentAsset;
                if (custom != null)
                    return BuildCustomComponentInfo(custom);
                return name;
            }
            return name;
        }

        string BuildCustomComponentInfo(CustomComponentAsset custom)
        {
            var sb = new StringBuilder();
            sb.AppendLine(custom.Name);
            sb.AppendLine($"Size: {custom.WidthMm:0} x {custom.HeightMm:0} x {custom.DepthMm:0} mm");
            if (custom.PowerDrawA > 0f)
                sb.AppendLine($"Power Draw: {custom.PowerDrawA:0.##} A");
            if (custom.VoltageMin > 0f || custom.VoltageMax > 0f)
                sb.AppendLine($"Voltage: {custom.VoltageMin:0.0} - {custom.VoltageMax:0.0} V");
            if (custom.Mount != MountType.None)
                sb.AppendLine($"Mount: {custom.Mount}");
            if (!string.IsNullOrWhiteSpace(custom.SignalType))
                sb.AppendLine($"Signal: {custom.SignalType}");
            if (!string.IsNullOrWhiteSpace(custom.Connector))
                sb.AppendLine($"Connector: {custom.Connector}");
            if (custom.Properties.Count > 0)
            {
                int shown = 0;
                foreach (var prop in custom.Properties)
                {
                    if (shown >= 3) break;
                    var unit = string.IsNullOrWhiteSpace(prop.Unit) ? "" : $" {prop.Unit}";
                    sb.AppendLine($"{prop.Name}: {prop.Value}{unit}");
                    shown++;
                }
            }
            return sb.ToString().TrimEnd();
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
                    sb.AppendLine($"C Rating: {battery.MaxDischargeC:0}C");
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
            else if (p.Type == PartType.ESC && asset is ESCAsset esc)
            {
                sb.AppendLine($"Current: {esc.ContinuousCurrent:0}A");
                sb.AppendLine($"Burst: {esc.BurstCurrent:0}A");
                sb.AppendLine($"Voltage: {esc.VoltageRating}");
                sb.AppendLine($"Mass: {esc.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.FlightController && asset is FlightControllerAsset fc)
            {
                sb.AppendLine($"MCU: {fc.MCU}");
                sb.AppendLine($"Mount: {fc.MountSizeMm:0.0}mm");
                sb.AppendLine($"Gyro: {fc.Gyro}");
                sb.AppendLine($"UARTs: {fc.UARTCount}");
                sb.AppendLine($"Mass: {fc.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.Propeller && asset is PropellerAsset prop)
            {
                sb.AppendLine($"Prop: {prop.DiameterInch:0.0}x{prop.Pitch:0.0} {prop.BladeCount}B");
            }
            else if (p.Type == PartType.Camera && asset is CameraAsset cam)
            {
                sb.AppendLine($"Type: {cam.SystemType}");
                sb.AppendLine($"Form: {cam.FormFactor}");
                sb.AppendLine($"Resolution: {cam.Resolution}");
                sb.AppendLine($"FOV: {cam.FovDeg:0}°");
                sb.AppendLine($"Mass: {cam.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.Receiver && asset is ReceiverAsset rx)
            {
                sb.AppendLine($"Protocol: {rx.Protocol}");
                sb.AppendLine($"Freq: {rx.FrequencyGHz:0.00} GHz");
                sb.AppendLine($"Mass: {rx.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.GPS && asset is GpsAsset gps)
            {
                sb.AppendLine($"Rate: {gps.UpdateRateHz:0} Hz");
                sb.AppendLine($"Accuracy: {gps.AccuracyM:0.0} m");
                sb.AppendLine($"Mass: {gps.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.VTX && asset is VtxAsset vtx)
            {
                sb.AppendLine($"Power: {vtx.MaxPowerMw} mW");
                sb.AppendLine($"Channels: {vtx.ChannelCount}");
                sb.AppendLine($"Mass: {vtx.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.Antenna && asset is AntennaAsset ant)
            {
                sb.AppendLine($"Gain: {ant.GainDbi:0.0} dBi");
                sb.AppendLine($"Pol: {ant.Polarization}");
                sb.AppendLine($"Mass: {ant.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.Buzzer && asset is BuzzerAsset buzzer)
            {
                sb.AppendLine($"Loudness: {buzzer.LoudnessDb:0} dB");
                sb.AppendLine($"Mass: {buzzer.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.LED && asset is LedAsset led)
            {
                sb.AppendLine($"LEDs: {led.LedCount}");
                sb.AppendLine($"Color: {led.Color}");
                sb.AppendLine($"Mass: {led.MassKg:0.###} kg");
            }
            else if (p.Type == PartType.CustomComponent && asset is CustomComponentAsset custom)
            {
                sb.AppendLine($"Size: {custom.WidthMm:0} x {custom.HeightMm:0} x {custom.DepthMm:0} mm");
                if (custom.PowerDrawA > 0f)
                    sb.AppendLine($"Power Draw: {custom.PowerDrawA:0.##} A");
                if (custom.VoltageMin > 0f || custom.VoltageMax > 0f)
                    sb.AppendLine($"Voltage: {custom.VoltageMin:0.0} - {custom.VoltageMax:0.0} V");
                if (custom.Mount != MountType.None)
                    sb.AppendLine($"Mount: {custom.Mount}");
                if (!string.IsNullOrWhiteSpace(custom.SignalType))
                    sb.AppendLine($"Signal: {custom.SignalType}");
                if (!string.IsNullOrWhiteSpace(custom.Connector))
                    sb.AppendLine($"Connector: {custom.Connector}");
                if (custom.Properties.Count > 0)
                {
                    foreach (var customProp in custom.Properties)
                    {
                        var unit = string.IsNullOrWhiteSpace(customProp.Unit) ? "" : $" {customProp.Unit}";
                        sb.AppendLine($"{customProp.Name}: {customProp.Value}{unit}");
                    }
                }
            }
            sb.AppendLine($"Position: {p.Position.X:0},{p.Position.Y:0}");
            return sb.ToString();
        }

        int FindNearestMount(PointF mousePos)
        {
            var frame = GetFrame();
            if (frame == null) return -1;
            var mounts = GetMotorMounts(GetFrameAsset());

            int nearest = -1;
            float best = 9999f;
            foreach (var (mount, index) in mounts)
            {
                var world = FrameToWorld(mount.Position);
                float d = Distance(mousePos, world);
                if (d < 25f && d < best)
                {
                    best = d;
                    nearest = index;
                }
            }
            return nearest;
        }

        int FindNearestEscMount(PointF mousePos)
        {
            var frame = GetFrame();
            if (frame == null) return -1;
            var mounts = GetEscMounts(GetFrameAsset());

            int nearest = -1;
            float best = 9999f;
            foreach (var (mount, index) in mounts)
            {
                var world = FrameToWorld(mount.Position);
                float d = Distance(mousePos, world);
                float radius = Math.Max(22f, Math.Max(mount.Size.Width, mount.Size.Height) * 0.6f);
                if (d < radius && d < best)
                {
                    best = d;
                    nearest = index;
                }
            }
            return nearest;
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

        bool AddPropeller(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            int mount = FindNearestMount(mousePos);
            if (mount == -1) return false;
            if (!project!.Instances.Any(p => p.Type == PartType.Motor && p.MountIndex == mount))
                return false;
            if (project.Instances.Any(p => p.Type == PartType.Propeller && p.MountIndex == mount))
                return false;

            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Propeller,
                MountIndex = mount
            });
            return true;
        }

        bool AddEsc(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            int mount = escLayout == EscLayout.FourInOne ? 0 : FindNearestEscMount(mousePos);
            if (mount == -1) return false;
            if (project!.Instances.Any(p => p.Type == PartType.ESC && p.MountIndex == mount))
                return false;

            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.ESC,
                MountIndex = mount
            });
            return true;
        }

        bool AddFlightController(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.FlightController))
                return false;

            var mount = GetFrameAsset()?.Mounts.FirstOrDefault(m => m.Type == MountType.FlightController);
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.FlightController,
                Position = pos
            });
            return true;
        }

        bool AddCamera(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.Camera))
                return false;

            var mount = GetCameraMount(GetFrameAsset());
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Camera,
                Position = pos
            });
            return true;
        }

        bool AddReceiver(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.Receiver))
                return false;

            var mount = GetFrameAsset()?.Mounts.FirstOrDefault(m => m.Type == MountType.Receiver);
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Receiver,
                Position = pos
            });
            return true;
        }

        bool AddGps(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.GPS))
                return false;

            var mount = GetFrameAsset()?.Mounts.FirstOrDefault(m => m.Type == MountType.GPS);
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.GPS,
                Position = pos
            });
            return true;
        }

        bool AddVtx(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.VTX))
                return false;

            var mount = GetFrameAsset()?.Mounts.FirstOrDefault(m => m.Type == MountType.VTX);
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.VTX,
                Position = pos
            });
            return true;
        }

        bool AddAntenna(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.Antenna))
                return false;

            var mount = GetFrameAsset()?.Mounts.FirstOrDefault(m => m.Type == MountType.Antenna);
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Antenna,
                Position = pos
            });
            return true;
        }

        bool AddBuzzer(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.Buzzer))
                return false;

            var mount = GetFrameAsset()?.Mounts.FirstOrDefault(m => m.Type == MountType.Buzzer);
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Buzzer,
                Position = pos
            });
            return true;
        }

        bool AddLed(PointF mousePos, string name)
        {
            if (!HasFrame()) return false;
            if (project!.Instances.Any(p => p.Type == PartType.LED))
                return false;

            var mount = GetFrameAsset()?.Mounts.FirstOrDefault(m => m.Type == MountType.LED);
            var pos = mount != null ? FrameToWorld(mount.Position) : mousePos;
            project.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.LED,
                Position = pos
            });
            return true;
        }

        bool AddCustomComponent(PointF mousePos, string name, string? assetId = null)
        {
            if (project == null) return false;
            if (!HasFrame()) return false;
            CustomComponentAsset? asset = null;
            if (!string.IsNullOrWhiteSpace(assetId))
                asset = AssetLibrary.Get(assetId) as CustomComponentAsset;
            asset ??= AssetLibrary.FindByName(name) as CustomComponentAsset;
            if (asset == null) return false;

            if (asset.Mount != MountType.None)
            {
                if (TryPlaceAsset(asset, mousePos))
                    return true;
                return false;
            }

            project.Instances.Add(new PlacedInstance
            {
                AssetId = asset.Id,
                Type = PartType.CustomComponent,
                Position = mousePos
            });
            return true;
        }
bool TryPlaceAsset(Asset asset, PointF mouse)
{
    var frame = GetFrameAsset();
    if (frame == null) return false;

    if (asset is not CustomComponentAsset && asset.RequiredMount == MountType.Propeller)
    {
        int mount = FindNearestMount(mouse);
        if (mount == -1) return false;
        return AddPropeller(mouse, asset.Name);
    }

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

        List<(MountPoint Mount, int Index)> GetMotorMounts(FrameAsset? frameAsset)
        {
            var mounts = new List<(MountPoint, int)>();
            if (frameAsset != null && frameAsset.Mounts.Count > 0)
            {
                int idx = 0;
                for (int i = 0; i < frameAsset.Mounts.Count; i++)
                {
                    var m = frameAsset.Mounts[i];
                    if (m.Type == MountType.Motor)
                    {
                        mounts.Add((m, idx));
                        idx++;
                    }
                }
            }

            if (mounts.Count == 0)
            {
                for (int i = 0; i < FrameDB.XFrame.MotorMounts.Length; i++)
                {
                    mounts.Add((new MountPoint
                    {
                        Type = MountType.Motor,
                        Position = FrameDB.XFrame.MotorMounts[i],
                        Size = new SizeF(20, 20),
                        Label = $"M{i + 1}"
                    }, i));
                }
            }

            return mounts;
        }

        List<(MountPoint Mount, int Index)> GetEscMounts(FrameAsset? frameAsset)
        {
            if (escLayout == EscLayout.FourInOne)
            {
                float bodySize = frameAsset?.BodySizeMm > 0 ? frameAsset.BodySizeMm : 48f;
                float escSize = Math.Clamp(bodySize * 0.7f, 28f, 54f);
                return new List<(MountPoint, int)>
                {
                    (new MountPoint
                    {
                        Type = MountType.ESC,
                        Position = new PointF(0, 0),
                        Size = new SizeF(escSize, escSize),
                        Label = "ESC 4-in-1"
                    }, 0)
                };
            }

            var mounts = new List<(MountPoint, int)>();
            if (frameAsset != null && frameAsset.Mounts.Count > 0)
            {
                int idx = 0;
                for (int i = 0; i < frameAsset.Mounts.Count; i++)
                {
                    var m = frameAsset.Mounts[i];
                    if (m.Type == MountType.ESC)
                    {
                        mounts.Add((m, idx));
                        idx++;
                    }
                }
            }

            if (mounts.Count == 0)
            {
                var motorMounts = GetMotorMounts(frameAsset);
                for (int i = 0; i < motorMounts.Count; i++)
                {
                    var motor = motorMounts[i].Mount.Position;
                    mounts.Add((new MountPoint
                    {
                        Type = MountType.ESC,
                        Position = new PointF(motor.X * 0.7f, motor.Y * 0.7f),
                        Size = new SizeF(24, 36),
                        Label = $"ESC{i + 1}"
                    }, i));
                }
            }

            return mounts;
        }

        MountPoint? GetCameraMount(FrameAsset? frameAsset)
        {
            if (frameAsset != null && frameAsset.Mounts.Count > 0)
            {
                foreach (var m in frameAsset.Mounts)
                {
                    if (m.Type == MountType.Camera)
                        return m;
                }
            }
            return new MountPoint
            {
                Type = MountType.Camera,
                Position = new PointF(0, 70),
                Size = new SizeF(28, 20),
                Label = "Camera"
            };
        }

        (MountPoint Mount, int Index)? GetBatteryMount(FrameAsset? frameAsset)
        {
            if (frameAsset != null && frameAsset.Mounts.Count > 0)
            {
                for (int i = 0; i < frameAsset.Mounts.Count; i++)
                {
                    var m = frameAsset.Mounts[i];
                    if (m.Type == MountType.Battery)
                        return (m, i);
                }
            }

            var bay = FrameDB.XFrame.BatteryBay;
            var center = new PointF(bay.X + bay.Width / 2f, bay.Y + bay.Height / 2f);
            return (new MountPoint
            {
                Type = MountType.Battery,
                Position = center,
                Size = new SizeF(bay.Width, bay.Height),
                Label = "Battery"
            }, -1);
        }

        bool TryGetMotorMountPosition(int mountIndex, out PointF relPos)
        {
            var mounts = GetMotorMounts(GetFrameAsset());
            if (mountIndex >= 0 && mountIndex < mounts.Count)
            {
                relPos = mounts[mountIndex].Mount.Position;
                return true;
            }
            relPos = new PointF();
            return false;
        }

        bool TryGetEscMountPosition(int mountIndex, out PointF relPos)
        {
            var mounts = GetEscMounts(GetFrameAsset());
            if (mountIndex >= 0 && mountIndex < mounts.Count)
            {
                relPos = mounts[mountIndex].Mount.Position;
                return true;
            }
            relPos = new PointF();
            return false;
        }

        PointF FrameToWorld(PointF relPos)
        {
            var frame = GetFrame();
            if (frame == null) return relPos;
            return new PointF(frame.Position.X + relPos.X, frame.Position.Y + relPos.Y);
        }

        int FindMountIndexByPosition(List<(MountPoint Mount, int Index)> mounts, PointF pos)
        {
            for (int i = 0; i < mounts.Count; i++)
            {
                if (Distance(mounts[i].Mount.Position, pos) < 0.5f)
                    return mounts[i].Index;
            }
            return -1;
        }

        void PlaceInstance(Asset asset, MountPoint mount)
        {
            if (project == null) return;
            var framePlaced = GetFrame();
            if (framePlaced == null) return;
            var frameAsset = AssetLibrary.Get(framePlaced.AssetId) as FrameAsset;
            if (frameAsset == null) return;
            int mountIndex = frameAsset.Mounts.IndexOf(mount);

            if (asset is CustomComponentAsset)
            {
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.CustomComponent,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.Motor)
            {
                int motorIndex = FindMountIndexByPosition(GetMotorMounts(frameAsset), mount.Position);
                if (motorIndex < 0) return;
                if (project.Instances.Any(p => p.Type == PartType.Motor && p.MountIndex == motorIndex)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Motor,
                    MountIndex = motorIndex
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
            else if (asset.RequiredMount == MountType.ESC)
            {
                int escIndex = FindMountIndexByPosition(GetEscMounts(frameAsset), mount.Position);
                if (escIndex < 0) return;
                if (project.Instances.Any(p => p.Type == PartType.ESC && p.MountIndex == escIndex)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.ESC,
                    MountIndex = escIndex
                });
            }
            else if (asset.RequiredMount == MountType.Propeller)
            {
                int motorIndex = FindMountIndexByPosition(GetMotorMounts(frameAsset), mount.Position);
                if (motorIndex < 0) return;
                if (project.Instances.Any(p => p.Type == PartType.Propeller && p.MountIndex == motorIndex)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Propeller,
                    MountIndex = motorIndex
                });
            }
            else if (asset.RequiredMount == MountType.FlightController)
            {
                if (project.Instances.Any(p => p.Type == PartType.FlightController)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.FlightController,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.Camera)
            {
                if (project.Instances.Any(p => p.Type == PartType.Camera)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Camera,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.Receiver)
            {
                if (project.Instances.Any(p => p.Type == PartType.Receiver)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Receiver,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.GPS)
            {
                if (project.Instances.Any(p => p.Type == PartType.GPS)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.GPS,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.VTX)
            {
                if (project.Instances.Any(p => p.Type == PartType.VTX)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.VTX,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.Antenna)
            {
                if (project.Instances.Any(p => p.Type == PartType.Antenna)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Antenna,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.Buzzer)
            {
                if (project.Instances.Any(p => p.Type == PartType.Buzzer)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.Buzzer,
                    Position = FrameToWorld(mount.Position)
                });
            }
            else if (asset.RequiredMount == MountType.LED)
            {
                if (project.Instances.Any(p => p.Type == PartType.LED)) return;
                project.Instances.Add(new PlacedInstance
                {
                    AssetId = asset.Id,
                    Type = PartType.LED,
                    Position = FrameToWorld(mount.Position)
                });
            }
            OnProjectStructureChanged();
        }

        

        bool AddBattery(PointF mousePos, string name)
        {
            var frame = GetFrame();
            if (frame == null) return false;
            var batteryMount = GetBatteryMount(GetFrameAsset());
            if (batteryMount != null)
            {
                var mount = batteryMount.Value.Mount;
                var worldCenter = FrameToWorld(mount.Position);
                var worldBay = new RectangleF(
                    worldCenter.X - mount.Size.Width / 2f,
                    worldCenter.Y - mount.Size.Height / 2f,
                    mount.Size.Width,
                    mount.Size.Height
                );
                if (!worldBay.Contains(mousePos)) return false;
            }

            // Prevent multiple batteries on the same frame
            if (project!.Instances.Any(p => p.Type == PartType.Battery))
                return false;

            var placePos = batteryMount != null ? FrameToWorld(batteryMount.Value.Mount.Position) : frame.Position;

            project!.Instances.Add(new PlacedInstance
            {
                AssetId = AssetLibrary.FindByName(name)?.Id ?? name,
                Type = PartType.Battery,
                Position = placePos
            });

            return true;
        }



        PointF GetPartWorldPosition(PlacedInstance p)
        {
            if (p.Type == PartType.Motor)
            {
                var frame = GetFrame();
                if (frame == null) return p.Position;
                if (!TryGetMotorMountPosition(p.MountIndex, out var mount))
                    return p.Position;
                return new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
            }
            if (p.Type == PartType.ESC)
            {
                var frame = GetFrame();
                if (frame == null) return p.Position;
                if (!TryGetEscMountPosition(p.MountIndex, out var mount))
                    return p.Position;
                return new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
            }
            if (p.Type == PartType.Propeller)
            {
                var frame = GetFrame();
                if (frame == null) return p.Position;
                if (!TryGetMotorMountPosition(p.MountIndex, out var mount))
                    return p.Position;
                return new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
            }

            return p.Position;
        }

        PointF ScreenToWorld(Point screenPos)
        {
            return ScreenToWorld(screenPos, zoomFactor);
        }

        PointF ScreenToWorld(Point screenPos, float zoom)
        {
            if (!viewportIs3D)
            {
                return new PointF(
                    (screenPos.X - viewOffset.X) / zoom,
                    (screenPos.Y - viewOffset.Y) / zoom
                );
            }

            float localX = (screenPos.X - viewOffset.X) / zoom;
            float localY = (screenPos.Y - viewOffset.Y) / zoom;
            float ix = 0.866f;
            float iy = 0.5f;
            float x = (localX / ix + localY / iy) * 0.5f;
            float y = (localY / iy - localX / ix) * 0.5f;
            var rotated = new PointF(x, y);
            return ApplyViewRotation(rotated, -viewRotation);
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
            escDelayedThrottle = 0f;
            telemetry.Clear();
            crashCount = 0;
            lastCrashSummary = "No crash events";
            lastCrashTimeSec = -1;
            simClock.Restart();
        }

    }
}
