using System;
using System.Collections.Generic;
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

        MenuStrip menu;
        TreeView projectTree, libraryTree;
        PictureBox viewport;

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

                if (e.Node.Text == "X Frame")
                {
                    project.PlacedParts.Clear();

                    project.PlacedParts.Add(new PlacedPart
                    {
                        Type = PartType.Frame,
                        Name = "X Frame",
                        Position = new PointF(viewport.Width / 2, viewport.Height / 2)
                    });

                    RefreshTree();
                    viewport.Invalidate();
                    return;
                }

                if (e.Node.Parent?.Text == "Motors")
                {
                    // next click in viewport places motor
                    pendingAddMode = PartType.Motor;
                    return;
                }

                if (e.Node.Parent?.Text == "Batteries")
                {
                    pendingAddMode = PartType.Battery;
                    return;
                }
            };

            BuildLibrary();

            left.Controls.Add(libraryTree);
            left.Controls.Add(projectTree);

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

                    RefreshTree();
                    viewport.Invalidate();
                    return;
                }

                if (dragging.Category == "Motors")
                    AddMotor(pt, dragging.Name);
                else if (dragging.Category == "Batteries")
                    AddBattery(pt, dragging.Name);
            };

            center.Controls.Add(viewport);

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
                    viewport.Invalidate();
                    return;
                }

                // Left-click: if user selected a part in library, place it now
                if (e.Button == MouseButtons.Left && project != null && pendingAddMode != null)
                {
                    if (pendingAddMode == PartType.Motor)
                    {
                        AddMotor(e.Location);
                        pendingAddMode = null;
                        RefreshTree();
                        viewport.Invalidate();
                        return;
                    }
                    else if (pendingAddMode == PartType.Battery)
                    {
                        AddBattery(e.Location);
                        pendingAddMode = null;
                        RefreshTree();
                        viewport.Invalidate();
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
            if (cat == "Motors") AddMotor(pt, name);
            else if (cat == "Batteries") AddBattery(pt, name);
            else if (name == "X Frame")
            {
                project!.PlacedParts.Clear();
                project.PlacedParts.Add(new PlacedPart { Type = PartType.Frame, Name = "X Frame", Position = pt });
                dirty = true;
                RefreshTree();
                viewport.Invalidate();
            }
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

    foreach (var m in def.MotorMounts)
    {
        g.FillEllipse(Brushes.DarkGray,
            frame.Position.X + m.X - 10,
            frame.Position.Y + m.Y - 10,
            20, 20);
    }

    var bay = def.BatteryBay;
    g.DrawRectangle(Pens.Orange,
        frame.Position.X + bay.X,
        frame.Position.Y + bay.Y,
        bay.Width,
        bay.Height);
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
            var motors = new TreeNode("Motors");
            motors.Nodes.Add("2207 1750KV");
            motors.Nodes.Add("2306 1950KV");

            var batteries = new TreeNode("Batteries");
            batteries.Nodes.Add("4S LiPo");
            batteries.Nodes.Add("6S LiPo");

            var frames = new TreeNode("Frames");
            frames.Nodes.Add("X Frame");
            frames.Nodes.Add("Deadcat");

            libraryTree.Nodes.Add(motors);
            libraryTree.Nodes.Add(batteries);
            libraryTree.Nodes.Add(frames);
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
            public PointF[] MotorMounts;   // relative positions
            public RectangleF BatteryBay;
            public SizeF Size;
        }

        class PlacedPart
        {
            public PartType Type;
            public string Name;

            // world position
            public PointF Position;

            // attachment
            public int AttachedMountIndex = -1; // for motors
        }

        class LibraryPart
        {
            public string Category { get; set; } = "";
            public string Name { get; set; } = "";
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

        void AddMotor(PointF mousePos, string name)
        {
            if (!HasFrame()) return;

            int mount = FindNearestMount(mousePos);
            if (mount == -1) return;

            project!.PlacedParts.Add(new PlacedPart
            {
                Type = PartType.Motor,
                Name = name,
                AttachedMountIndex = mount
            });

            dirty = true;
            viewport.Invalidate();
        }

        void AddBattery(PointF mousePos, string name)
        {
            var frame = GetFrame();
            if (frame == null) return;

            var bay = FrameDB.XFrame.BatteryBay;
            var worldBay = new RectangleF(
                frame.Position.X + bay.X,
                frame.Position.Y + bay.Y,
                bay.Width,
                bay.Height
            );

            if (!worldBay.Contains(mousePos)) return;

            project!.PlacedParts.Add(new PlacedPart
            {
                Type = PartType.Battery,
                Name = name,
                Position = frame.Position
            });

            dirty = true;
            viewport.Invalidate();
        }

        // Convenience overloads for click-based placement
        void AddMotor(Point mousePos)
        {
            AddMotor(new PointF(mousePos.X, mousePos.Y), "Motor");
        }

        void AddBattery(Point mousePos)
        {
            AddBattery(new PointF(mousePos.X, mousePos.Y), "Battery");
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

    }
}
