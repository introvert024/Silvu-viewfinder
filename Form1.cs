using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace SilvuViewfinder
{
    public class Form1 : Form
    {

        bool darkMode = false;
        bool dirty = false;

        DroneProject? project;
        string? projectPath;

        LibraryPart? dragging;
        PlacedPart? selected;
        PartType? pendingAddMode = null;
        string? pendingAddName = null;
        Point mousePos;

        MenuStrip menu = null!;
        TreeView projectTree = null!, libraryTree = null!;
        PictureBox viewport = null!;

        // clipboard for copy/paste of placed parts
        PlacedPart? clipboardPart = null;

        // context menu for the project tree (layers)
        ContextMenuStrip projectContextMenu = null!;

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
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();
        }

        void BuildUI()
        {
            // MENU
            menu = new MenuStrip();
            var proj = new ToolStripMenuItem("Project");
            proj.DropDownItems.Add("New", null, (_,__) => NewProject());
            proj.DropDownItems.Add("Open", null, (_,__) => OpenProject());
            proj.DropDownItems.Add("Save", null, (_,__) => SaveProject());
            proj.DropDownItems.Add("Exit", null, (_,__) => Close());

            var tools = new ToolStripMenuItem("Tools");
            tools.DropDownItems.Add("Toggle Dark Mode", null, (_,__) => ToggleDark());

            menu.Items.Add(proj);
            menu.Items.Add(tools);
            Controls.Add(menu);

            // LAYOUT
            var left = new Panel { Dock = DockStyle.Left, Width = 260 };
            var center = new Panel { Dock = DockStyle.Fill };
            Controls.Add(center);
            Controls.Add(left);

            // PROJECT TREE
            projectTree = new TreeView { Dock = DockStyle.Top, Height = 260 };
            projectTree.AfterSelect += (_, e) =>
            {
                selected = e.Node?.Tag as PlacedPart;
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
                    projectContextMenu = new ContextMenuStrip();

                    var infoItem = new ToolStripMenuItem("Info", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is PlacedPart p)
                            MessageBox.Show(GetPlacedPartInfo(p), "Part Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });

                    var deleteItem = new ToolStripMenuItem("Delete", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is PlacedPart p)
                        {
                            if (p.Type == PartType.Frame)
                            {
                                MessageBox.Show("Cannot delete the frame.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            project.PlacedParts.Remove(p);
                            OnProjectStructureChanged();
                        }
                    });

                    var saveCustomItem = new ToolStripMenuItem("Save as Custom", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is PlacedPart p)
                        {
                            string category = p.Type == PartType.Motor ? "Motors" : p.Type == PartType.Battery ? "Batteries" : "Frames";
                            var parent = libraryTree.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == category);
                            if (parent == null)
                            {
                                parent = new TreeNode(category);
                                libraryTree.Nodes.Add(parent);
                            }
                            string name = p.Name;
                            if (parent.Nodes.Cast<TreeNode>().Any(n => n.Text == name)) name += " (custom)";
                            parent.Nodes.Add(new TreeNode(name));
                        }
                    });

                    var copyItem = new ToolStripMenuItem("Copy", null, (_,__) =>
                    {
                        if (projectTree.SelectedNode?.Tag is PlacedPart p)
                        {
                            clipboardPart = new PlacedPart { Type = p.Type, Name = p.Name, Position = p.Position, AttachedMountIndex = p.AttachedMountIndex };
                        }
                    });

                    var pasteItem = new ToolStripMenuItem("Paste", null, (_,__) =>
                    {
                        if (clipboardPart != null && project != null)
                        {
                            var p = new PlacedPart
                            {
                                Type = clipboardPart.Type,
                                Name = clipboardPart.Name,
                                Position = new PointF(clipboardPart.Position.X + 10, clipboardPart.Position.Y + 10),
                                AttachedMountIndex = clipboardPart.AttachedMountIndex
                            };
                            project.PlacedParts.Add(p);
                            OnProjectStructureChanged();
                        }
                    });

                    projectContextMenu.Items.AddRange(new ToolStripItem[] { infoItem, deleteItem, saveCustomItem, new ToolStripSeparator(), copyItem, pasteItem });

                    projectContextMenu.Opening += (_, __) =>
                    {
                        var node = projectTree.SelectedNode;
                        bool hasPlaced = node?.Tag is PlacedPart;
                        infoItem.Enabled = hasPlaced;
                        deleteItem.Enabled = hasPlaced && (node?.Tag as PlacedPart)?.Type != PartType.Frame;
                        saveCustomItem.Enabled = hasPlaced;
                        copyItem.Enabled = hasPlaced;
                        pasteItem.Enabled = clipboardPart != null;
                    };
                }

                projectContextMenu.Show(projectTree, e.Location);
            };

            // LIBRARY TREE
            libraryTree = new TreeView { Dock = DockStyle.Fill };
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

                if (e.Node?.Text == "X Frame")
                {
                    project.PlacedParts.Clear();

                    project.PlacedParts.Add(new PlacedPart
                    {
                        Type = PartType.Frame,
                        Name = "X Frame",
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

            BuildLibrary();

            var leftSplit = new SplitContainer
{
    Dock = DockStyle.Fill,
    Orientation = Orientation.Horizontal,
    SplitterDistance = 260, // top = layers, bottom = library
    FixedPanel = FixedPanel.Panel1
};

// ===== LAYERS (PROJECT TREE) =====
var layersLabel = new Label
{
    Text = "LAYERS",
    Dock = DockStyle.Top,
    Height = 22,
    TextAlign = ContentAlignment.MiddleLeft,
    Padding = new Padding(6, 0, 0, 0),
    BackColor = Color.FromArgb(30, 30, 30),
    ForeColor = Color.White
};

projectTree.Dock = DockStyle.Fill;

var layersPanel = new Panel { Dock = DockStyle.Fill };
layersPanel.Controls.Add(projectTree);
layersPanel.Controls.Add(layersLabel);

// ===== AVAILABLE PARTS (LIBRARY) =====
var partsLabel = new Label
{
    Text = "AVAILABLE PARTS",
    Dock = DockStyle.Top,
    Height = 22,
    TextAlign = ContentAlignment.MiddleLeft,
    Padding = new Padding(6, 0, 0, 0),
    BackColor = Color.FromArgb(30, 30, 30),
    ForeColor = Color.White
};

libraryTree.Dock = DockStyle.Fill;

var partsPanel = new Panel { Dock = DockStyle.Fill };
partsPanel.Controls.Add(libraryTree);
partsPanel.Controls.Add(partsLabel);

// attach
leftSplit.Panel1.Controls.Add(layersPanel);
leftSplit.Panel2.Controls.Add(partsPanel);

left.Controls.Add(leftSplit);

ToolTip partToolTip = new ToolTip();

            libraryTree.NodeMouseHover += (s, e) =>
            {
                if (e.Node == null || e.Node.Parent == null)
                    return;

                string info = GetPartInfo(e.Node.Parent.Text, e.Node.Text);
                partToolTip.SetToolTip(libraryTree, info);
            };




            // VIEWPORT
            viewport = new PictureBox { Dock = DockStyle.Fill, AllowDrop = true };
            viewport.Paint += DrawViewport;
            viewport.DragEnter += (_, e) => e.Effect = DragDropEffects.Copy;
            viewport.DragDrop += (_, e) =>
            {
                if (dragging == null || project == null) return;
                var p = viewport.PointToClient(new Point(e.X, e.Y));
                var pt = new PointF(p.X, p.Y);

                if (dragging.Name == "X Frame")
                {
                    project.PlacedParts.Clear();

                    project.PlacedParts.Add(new PlacedPart
                    {
                        Type = PartType.Frame,
                        Name = "X Frame",
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

            center.Controls.Add(viewport);

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
                    var toRemove = new List<PlacedPart>();
                    foreach (var p in project.PlacedParts)
                    {
                        if (p.Type == PartType.Frame) continue;
                        var worldPos = GetPartWorldPosition(p);
                        if (Distance(worldPos, e.Location) < 20)
                            toRemove.Add(p);
                    }
                    project.PlacedParts.RemoveAll(p => toRemove.Contains(p));
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
        }

        void UpdatePhysics(float dt)
{
    if (project == null) return;

    totalMassKg = 0;
    totalCurrentA = 0;
    totalThrustN = 0;

    int motorCount = 0;

    // ===== PID ALTITUDE CONTROL =====
    float error = targetAltitude - altitude;
    float throttle = PID(error, dt);
    throttle = Math.Clamp(throttle, 0.0f, 1.0f);

    foreach (var p in project.PlacedParts)
    {
        if (p.Type == PartType.Motor)
        {
            motorCount++;
            totalMassKg += PhysicsDatabase.MotorMass(p.Name);

            float rpm = throttle * PhysicsDatabase.MaxRPM(p.Name);
            totalThrustN += ThrustFromRPM(rpm);
            totalCurrentA += throttle * PhysicsDatabase.MaxCurrent(p.Name);
        }
        else if (p.Type == PartType.Frame)
        {
            totalMassKg += PhysicsDatabase.FrameMass();
        }
        else if (p.Type == PartType.Battery)
        {
            totalMassKg += PhysicsDatabase.BatteryMass();
        }
    }

    if (motorCount == 0) return;

    // ===== VERTICAL DYNAMICS =====
    float weight = totalMassKg * GRAVITY;
    float netForce = totalThrustN - weight;
    float acceleration = netForce / totalMassKg;

    verticalVelocity += acceleration * dt;
    altitude += verticalVelocity * dt;

    if (altitude < 0)
    {
        altitude = 0;
        verticalVelocity = 0;
    }

    // ===== BATTERY MODEL =====
    batteryRemainingAh -= (totalCurrentA * dt) / 3600f;
    batteryRemainingAh = Math.Max(0, batteryRemainingAh);

    float sag = 0.02f * totalCurrentA;
    batteryVoltage = batteryVoltageNominal - sag;
}


        // ================= PROJECT =================
        void NewProject()
        {
            project = new DroneProject { Name = "New Drone Project" };
            projectPath = null;
            dirty = false;

            ResetPhysicsState();

            RefreshTree();
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
            var ofd = new OpenFileDialog { Filter = "SILVU Project|*.svproj" };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            project = JsonSerializer.Deserialize<DroneProject>(File.ReadAllText(ofd.FileName));
            projectPath = ofd.FileName;
            RefreshTree();
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
            else if (name == "X Frame")
            {
                project!.PlacedParts.Clear();
                project.PlacedParts.Add(new PlacedPart { Type = PartType.Frame, Name = "X Frame", Position = pt });
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

            foreach (var p in project.PlacedParts)
            {
                string group = p.Type.ToString();

                if (!map.ContainsKey(group))
                {
                    map[group] = new TreeNode(group);
                    root.Nodes.Add(map[group]);
                }

                map[group].Nodes.Add(new TreeNode(p.Name) { Tag = p });
            }

            root.ExpandAll();
            projectTree.Nodes.Add(root);
            UpdateTitle();
        }

        // ================= VIEWPORT =================
        void DrawViewport(object? s, PaintEventArgs e)
{
    if (project == null) return;
    var g = e.Graphics;
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

    // keep physics ticking
    UpdatePhysics(0.016f);

    var frame = GetFrame();
    if (frame != null)
        DrawFrame(g, frame);

    foreach (var p in project.PlacedParts)
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
        using var b = new SolidBrush(Color.FromArgb(200, Color.Black));
        g.FillRectangle(b, pos.X - 4, pos.Y - 2, size.Width + 8, size.Height + 4);
        g.DrawString(txt, Font, Brushes.Yellow, pos);
    }

    DrawPhysicsHUD(g);
}

void DrawFrame(Graphics g, PlacedPart frame)
{
    var def = FrameDB.XFrame;

    g.DrawEllipse(Pens.White,
        frame.Position.X - def.Size.Width / 2,
        frame.Position.Y - def.Size.Height / 2,
        def.Size.Width,
        def.Size.Height);

    // determine nearest mount when placing motors
    int nearest = -1;
    if (pendingAddMode == PartType.Motor)
        nearest = FindNearestMount(mousePos);

    for (int i = 0; i < def.MotorMounts.Length; i++)
    {
        var m = def.MotorMounts[i];
        var world = new PointF(frame.Position.X + m.X, frame.Position.Y + m.Y);

        // base mount
        g.FillEllipse(Brushes.DarkGray,
            world.X - 10,
            world.Y - 10,
            20, 20);

        // highlight candidate
        if (i == nearest)
        {
            g.DrawEllipse(Pens.Yellow, world.X - 14, world.Y - 14, 28, 28);
        }
    }

    var bay = def.BatteryBay;
    var worldBay = new RectangleF(
        frame.Position.X + bay.X,
        frame.Position.Y + bay.Y,
        bay.Width,
        bay.Height);

    // draw bay, highlight if pending battery
    using (var pen = new Pen(Color.Orange, 2))
        g.DrawRectangle(pen, worldBay.X, worldBay.Y, worldBay.Width, worldBay.Height);

    if (pendingAddMode == PartType.Battery)
    {
        if (worldBay.Contains(mousePos))
        {
            using var brush = new SolidBrush(Color.FromArgb(64, Color.Yellow));
            g.FillRectangle(brush, worldBay);
            g.DrawRectangle(Pens.Yellow, worldBay.X, worldBay.Y, worldBay.Width, worldBay.Height);
        }
    }
}

void DrawMotor(Graphics g, PlacedPart motor)
{
    var frame = GetFrame();
    var mount = FrameDB.XFrame.MotorMounts[motor.AttachedMountIndex];

    var pos = new PointF(
        frame!.Position.X + mount.X,
        frame.Position.Y + mount.Y
    );

    g.FillEllipse(Brushes.Red, pos.X - 12, pos.Y - 12, 24, 24);
}

void DrawBattery(Graphics g, PlacedPart battery)
{
    g.FillRectangle(Brushes.Blue,
        battery.Position.X - 30,
        battery.Position.Y - 15,
        60, 30);
}

void DrawPhysicsHUD(Graphics g)
{
    float y = 10;

    g.DrawString($"Altitude: {altitude:F2} m", Font, Brushes.Lime, 10, y); y += 18;
    g.DrawString($"Mass: {totalMassKg:F2} kg", Font, Brushes.Lime, 10, y); y += 18;
    g.DrawString($"Thrust: {totalThrustN:F1} N", Font, Brushes.Lime, 10, y); y += 18;
    g.DrawString($"Current: {totalCurrentA:F1} A", Font, Brushes.Lime, 10, y); y += 18;
    g.DrawString($"Voltage: {batteryVoltage:F1} V", Font, Brushes.Lime, 10, y); y += 18;

    string hover =
        totalThrustN < totalMassKg * GRAVITY
        ? "❌ NO HOVER"
        : "✅ HOVER OK";

    g.DrawString(hover, Font, Brushes.Yellow, 10, y);
}



        void BuildLibrary()
        {
            libraryTree.Nodes.Clear();

            AssetLibrary.LoadDefaults();

            var groups = AssetLibrary.Assets.GroupBy(a => a.Category).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                var parent = new TreeNode(g.Key);
                foreach (var a in g)
                {
                    var node = new TreeNode(a.Name);
                    parent.Nodes.Add(node);
                }
                libraryTree.Nodes.Add(parent);
            }
        }
        void OnProjectStructureChanged()
{
    ResetPhysicsState();
    dirty = true;
    RefreshTree();
    viewport.Invalidate();
}


        // ================= UX =================
        void ToggleDark()
        {
            darkMode = !darkMode;
            BackColor = darkMode ? Color.FromArgb(15,15,15) : Color.White;
            viewport.BackColor = darkMode ? Color.Black : Color.White;
        }

        void UpdateTitle()
        {
            Text = $"SILVU VIEWFINDER — {project?.Name}{(dirty ? " *" : "")}";
        }

        // ================= DATA =================
        class DroneProject
        {
            public string Name { get; set; } = "";
            public List<PlacedPart> PlacedParts { get; set; } = new();
        }

        enum PartType
        {
            Frame,
            Motor,
            Battery
        }

        class FrameDefinition
        {
            public PointF[] MotorMounts = Array.Empty<PointF>();   // relative positions
            public RectangleF BatteryBay = new RectangleF();
            public SizeF Size = new SizeF();
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

        class LibraryPart
        {
            public string Category { get; set; } = "";
            public string Name { get; set; } = "";
            public string AssetId { get; set; } = ""; // optional bridge to AssetLibrary
        }

        abstract class Asset
        {
            public string Id = Guid.NewGuid().ToString();
            public string Name = "";
            public string Category = "";
            public string Description = "";
            public Image? Icon;
        }

        class MotorAsset : Asset
        {
            public float MaxRPM;
            public float MaxCurrent;
            public float MaxThrust;
            public float Mass;
        }

        class BatteryAsset : Asset
        {
            public float Voltage;
            public float CapacityAh;
            public float Mass;
        }

        class FrameAsset : Asset
        {
            public FrameDefinition? Definition;
            public int MotorCount;
        }

        static class AssetLibrary
        {
            public static List<Asset> Assets = new();
            public static void LoadDefaults()
            {
                Assets.Clear();

                Assets.Add(new MotorAsset
                {
                    Name = "2207 1750KV",
                    Category = "Motors",
                    MaxRPM = 22000f,
                    MaxCurrent = 35f,
                    MaxThrust = 15f,
                    Mass = 0.031f,
                    Description = "5-inch FPV motor"
                });

                Assets.Add(new MotorAsset
                {
                    Name = "2306 1950KV",
                    Category = "Motors",
                    MaxRPM = 24000f,
                    MaxCurrent = 40f,
                    MaxThrust = 17f,
                    Mass = 0.031f,
                    Description = "High rpm motor"
                });

                Assets.Add(new BatteryAsset
                {
                    Name = "4S LiPo",
                    Category = "Batteries",
                    Voltage = 14.8f,
                    CapacityAh = 1.3f,
                    Mass = 0.22f,
                    Description = "4S battery"
                });

                Assets.Add(new BatteryAsset
                {
                    Name = "6S LiPo",
                    Category = "Batteries",
                    Voltage = 22.2f,
                    CapacityAh = 1.3f,
                    Mass = 0.22f,
                    Description = "6S battery"
                });

                Assets.Add(new FrameAsset
                {
                    Name = "X Frame",
                    Category = "Frames",
                    Definition = FrameDB.XFrame,
                    MotorCount = 4,
                    Description = "Standard 5-inch X frame"
                });

                Assets.Add(new FrameAsset
                {
                    Name = "Deadcat",
                    Category = "Frames",
                    Definition = FrameDB.XFrame,
                    MotorCount = 4,
                    Description = "Deadcat variant"
                });
            }

            public static Asset? FindByName(string name)
                => Assets.FirstOrDefault(a => a.Name == name);
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
            return project!.PlacedParts.Exists(p => p.Type == PartType.Frame);
        }

        PlacedPart? GetFrame()
        {
            return project!.PlacedParts.Find(p => p.Type == PartType.Frame);
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
                var voltage = name == "4S LiPo" ? "14.8V" : name == "6S LiPo" ? "22.2V" : "";
                return $"{name}\nVoltage: {voltage}\nMass: {PhysicsDatabase.BatteryMass()} kg";
            }
            else if (category == "Frames")
            {
                if (name == "X Frame")
                    return $"{name}\nSize: {FrameDB.XFrame.Size.Width} x {FrameDB.XFrame.Size.Height}";
                return $"{name}\nFrame";
            }
            return name;
        }

        string GetPlacedPartInfo(PlacedPart p)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{p.Type} - {p.Name}");
            if (p.Type == PartType.Motor)
            {
                sb.AppendLine($"Mount: {p.AttachedMountIndex}");
                sb.AppendLine($"Thrust: {PhysicsDatabase.GetMaxThrust(p.Name)} N");
                sb.AppendLine($"Hover Current: {PhysicsDatabase.GetCurrentDraw(p.Name)} A");
                sb.AppendLine($"Mass: {PhysicsDatabase.MotorMass(p.Name)} kg");
            }
            else if (p.Type == PartType.Battery)
            {
                var voltage = p.Name == "4S LiPo" ? "14.8V" : p.Name == "6S LiPo" ? "22.2V" : "";
                sb.AppendLine($"Voltage: {voltage}");
                sb.AppendLine($"Mass: {PhysicsDatabase.BatteryMass()} kg");
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
            if (project!.PlacedParts.Any(p => p.Type == PartType.Motor && p.AttachedMountIndex == mount))
                return false;

            project!.PlacedParts.Add(new PlacedPart
            {
                Type = PartType.Motor,
                Name = name,
                AttachedMountIndex = mount
            });

            return true;
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
            if (project!.PlacedParts.Any(p => p.Type == PartType.Battery))
                return false;

            project!.PlacedParts.Add(new PlacedPart
            {
                Type = PartType.Battery,
                Name = name,
                Position = frame.Position
            });

            return true;
        }



        PointF GetPartWorldPosition(PlacedPart p)
        {
            if (p.Type == PartType.Motor)
            {
                var frame = GetFrame();
                if (frame == null) return p.Position;
                if (p.AttachedMountIndex < 0 || p.AttachedMountIndex >= FrameDB.XFrame.MotorMounts.Length)
                    return p.Position;

                var mount = FrameDB.XFrame.MotorMounts[p.AttachedMountIndex];
                return new PointF(frame.Position.X + mount.X, frame.Position.Y + mount.Y);
            }

            return p.Position;
        }

        void ResetPhysicsState()
        {
            altitude = 0.0f;
            verticalVelocity = 0.0f;
            pidIntegral = 0.0f;
            lastError = 0.0f;
        }

    }
}
