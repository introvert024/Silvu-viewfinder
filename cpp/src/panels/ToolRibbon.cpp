#include "ToolRibbon.h"
#include "../data/DroneAssembly.h"
#include "../data/DroneComponent.h"
#include "../data/ComponentRegistry.h"
#include "../ViewportWidget.h"
#include <QMessageBox>
#include <cmath>

static const char* kMenuStyle =
    "QMenu { background: #0d1317; border: 1px solid #1e2d33; padding: 4px; }"
    "QMenu::item { color: #cbd5e1; padding: 5px 16px; font-size: 10px; }"
    "QMenu::item:selected { background: rgba(230,20,20,0.12); color: #e61414; }"
    "QMenu::item:disabled { color: #334155; }"
    "QMenu::separator { height: 1px; background: #1e2d33; margin: 3px 8px; }";

ToolRibbon::ToolRibbon(DroneAssembly *assembly, QWidget *parent)
    : QWidget(parent), m_assembly(assembly)
{
    setFixedHeight(34);
    setStyleSheet("background: #0b1015; border-bottom: 1px solid #1a2530;");

    auto *layout = new QHBoxLayout(this);
    layout->setContentsMargins(8, 0, 8, 0);
    layout->setSpacing(2);

    // Separator helper
    auto addSep = [&]() {
        auto *s = new QWidget(); s->setFixedSize(1, 18);
        s->setStyleSheet("background: #1a2530;");
        layout->addWidget(s);
    };

    // ── Main Ribbon Buttons ──
    m_insertBtn   = makeDropdownButton("Insert");
    m_modifyBtn   = makeDropdownButton("Modify");
    m_analyzeBtn  = makeDropdownButton("Analyze");
    m_inspectBtn  = makeDropdownButton("Inspect");
    m_validateBtn = makeDropdownButton("Validate");
    m_viewBtn     = makeDropdownButton("View");

    layout->addWidget(m_insertBtn);
    layout->addWidget(m_modifyBtn);
    addSep();
    layout->addWidget(m_analyzeBtn);
    layout->addWidget(m_inspectBtn);
    layout->addWidget(m_validateBtn);
    addSep();
    layout->addWidget(m_viewBtn);
    addSep();

    // ── Snapshot Button ──
    m_snapshotBtn = new QToolButton();
    m_snapshotBtn->setText("Snapshot");
    m_snapshotBtn->setToolTip("Capture build snapshot for revision comparison");
    m_snapshotBtn->setFixedHeight(24);
    m_snapshotBtn->setCursor(Qt::PointingHandCursor);
    m_snapshotBtn->setStyleSheet(
        "QToolButton { background: transparent; color: #64748b; border: 1px solid #1e2d33;"
        "border-radius: 3px; font-size: 9px; font-weight: bold; padding: 0 8px; }"
        "QToolButton:hover { background: rgba(226,232,240,0.06); color: #e2e8f0; }"
    );
    connect(m_snapshotBtn, &QToolButton::clicked, this, [this]() {
        m_statusLabel->setText("Snapshot saved");
        // TODO: persist snapshot data
    });
    layout->addWidget(m_snapshotBtn);

    layout->addStretch();

    // ── Status Label ──
    m_statusLabel = new QLabel("Ready");
    m_statusLabel->setStyleSheet("font-size: 9px; color: #4a5568; font-family: Consolas; padding: 0 6px;");
    layout->addWidget(m_statusLabel);

    // ── Assembly Lock ──
    m_lockBtn = new QToolButton();
    m_lockBtn->setText("Unlocked");
    m_lockBtn->setToolTip("Lock geometry to prevent accidental moves");
    m_lockBtn->setCheckable(true);
    m_lockBtn->setFixedHeight(24);
    m_lockBtn->setCursor(Qt::PointingHandCursor);
    m_lockBtn->setStyleSheet(
        "QToolButton { background: transparent; color: #10b981; border: 1px solid rgba(16,185,129,0.2);"
        "border-radius: 3px; font-size: 9px; font-weight: bold; padding: 0 8px; }"
        "QToolButton:checked { background: rgba(239,68,68,0.1); color: #ef4444; border-color: rgba(239,68,68,0.3); }"
        "QToolButton:hover { background: rgba(255,255,255,0.04); }"
    );
    connect(m_lockBtn, &QToolButton::toggled, this, [this](bool checked) {
        m_locked = checked;
        m_lockBtn->setText(checked ? "Locked" : "Unlocked");
        emit lockToggled(checked);
    });
    layout->addWidget(m_lockBtn);

    // Wire menus
    buildInsertMenu();
    buildModifyMenu();
    buildAnalyzeMenu();
    buildInspectMenu();
    buildValidateMenu();
    buildViewMenu();
}

QPushButton* ToolRibbon::makeDropdownButton(const QString &text)
{
    auto *btn = new QPushButton(text);
    btn->setFixedHeight(24);
    btn->setCursor(Qt::PointingHandCursor);
    btn->setStyleSheet(
        "QPushButton { background: transparent; color: #94a3b8; border: none; border-radius: 3px;"
        "font-size: 10px; font-weight: bold; padding: 0 10px; }"
        "QPushButton:hover { background: rgba(226,232,240,0.06); color: #e2e8f0; }"
        "QPushButton::menu-indicator { width: 8px; image: none; }"
    );
    return btn;
}

// ────────────────────────────────────────────────────────────
//  INSERT
// ────────────────────────────────────────────────────────────
void ToolRibbon::buildInsertMenu()
{
    auto *menu = new QMenu(this);
    menu->setStyleSheet(kMenuStyle);

    auto add = [&](const QString &name, ComponentType type) {
        menu->addAction(name, this, [this, type]() { emit requestAddComponent((int)type); });
    };

    add("Frame",             ComponentType::Frame);
    add("Motor",             ComponentType::Motor);
    add("Propeller",         ComponentType::Propeller);
    add("ESC",               ComponentType::ESC);
    add("Battery",           ComponentType::Battery);
    menu->addSeparator();
    add("Flight Controller", ComponentType::FlightController);
    add("Camera",            ComponentType::Camera);
    add("VTX",               ComponentType::VTX);
    add("Receiver",          ComponentType::Receiver);
    add("GPS",               ComponentType::GPS);
    menu->addSeparator();
    add("Payload",           ComponentType::Payload);

    m_insertBtn->setMenu(menu);
}

// ────────────────────────────────────────────────────────────
//  MODIFY (expanded with constraints + assembly tools)
// ────────────────────────────────────────────────────────────
void ToolRibbon::buildModifyMenu()
{
    auto *menu = new QMenu(this);
    menu->setStyleSheet(kMenuStyle);

    // Component operations
    menu->addAction("Remove Selected")->setEnabled(false);
    menu->addAction("Duplicate")->setEnabled(false);
    menu->addAction("Replace Component")->setEnabled(false);
    menu->addSeparator();

    // Transform
    menu->addAction("Move")->setEnabled(false);
    menu->addAction("Rotate")->setEnabled(false);
    menu->addAction("Mirror")->setEnabled(false);
    menu->addSeparator();

    // Constraints
    menu->addAction("Snap to Mount")->setEnabled(false);
    menu->addAction("Align to Center")->setEnabled(false);
    menu->addSeparator();

    // Assembly tools
    menu->addAction("Auto Arrange Motors")->setEnabled(false);
    menu->addAction("Mirror Assembly")->setEnabled(false);
    menu->addAction("Reset to Default Layout")->setEnabled(false);
    menu->addSeparator();

    menu->addAction("Clear All Components", this, [this]() {
        if (!m_assembly) return;
        m_assembly->setFrame(nullptr);
        m_statusLabel->setText("Cleared");
        emit assemblyChanged();
    });

    m_modifyBtn->setMenu(menu);
}

// ────────────────────────────────────────────────────────────
//  ANALYZE (calculations that highlight issues)
// ────────────────────────────────────────────────────────────
void ToolRibbon::buildAnalyzeMenu()
{
    auto *menu = new QMenu(this);
    menu->setStyleSheet(kMenuStyle);

    menu->addAction("Recalculate All", this, [this]() {
        emit assemblyChanged();
        m_statusLabel->setText("Recalculated");
    });
    menu->addSeparator();

    menu->addAction("Power Analysis", this, [this]() {
        if (!m_assembly) return;
        float totalMass = m_assembly->getTotalMass(), totalThrust = m_assembly->getTotalThrust();
        float bV = 0, bC = 0;
        for (const auto &n : m_assembly->getSnapNodes()) {
            if (n.attachedComponent && n.attachedComponent->getType() == ComponentType::Battery) {
                auto b = std::static_pointer_cast<BatteryComponent>(n.attachedComponent);
                bV = b->getVoltage(); bC = b->getCapacity();
            }
        }
        QString r = "POWER ANALYSIS\n\n";
        if (bV > 0) {
            float hp = totalThrust > 0 ? (totalMass / totalThrust) : 0;
            float ea = hp * 40.0f;
            float ft = bC > 0 ? (bC / 1000.0f / ea) * 60.0f : 0;
            r += QString("Battery: %1V (%2mAh)\nEst. Hover Current: ~%3A\nEst. Flight Time: ~%4 min")
                .arg(bV,0,'f',1).arg(bC,0,'f',0).arg(ea,0,'f',1).arg(ft,0,'f',1);
        } else r += "No battery attached.";
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Power Analysis"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addAction("Thrust Margin Check", this, [this]() {
        if (!m_assembly) return;
        float twr = m_assembly->getThrustToWeightRatio();
        QString r;
        if (twr <= 0) r = "No motors attached.";
        else if (twr >= 3) r = QString("EXCELLENT — T/W %1:1\nFull acrobatic capability.").arg(twr,0,'f',1);
        else if (twr >= 2) r = QString("GOOD — T/W %1:1\nFreestyle/racing suitable.").arg(twr,0,'f',1);
        else if (twr >= 1.5) r = QString("MARGINAL — T/W %1:1\nGentle flying only.").arg(twr,0,'f',1);
        else r = QString("CRITICAL — T/W %1:1\nMay not sustain flight!").arg(twr,0,'f',1);
        m_statusLabel->setText(twr >= 2 ? "Thrust OK" : "Thrust LOW");
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Thrust Margin"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addAction("Payload Capacity", this, [this]() {
        if (!m_assembly) return;
        float mass = m_assembly->getTotalMass();
        float thrust = m_assembly->getTotalThrust();
        float twr = m_assembly->getThrustToWeightRatio();
        QString r;
        if (twr > 0) {
            float maxPayload = thrust - mass;
            float safePayload = thrust * 0.5f - mass; // 2:1 T/W with payload
            r = QString("PAYLOAD CAPACITY\n\nAbsolute Max: %1g\nSafe (T/W > 2): %2g\nCurrent Mass: %3g\nTotal Thrust: %4g")
                .arg(maxPayload > 0 ? maxPayload : 0, 0, 'f', 0)
                .arg(safePayload > 0 ? safePayload : 0, 0, 'f', 0)
                .arg(mass, 0, 'f', 1).arg(thrust, 0, 'f', 0);
        } else {
            r = "No thrust data available.";
        }
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Payload"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addSeparator();
    menu->addAction("Thermal Estimate")->setEnabled(false);
    menu->addAction("Structural Load Check")->setEnabled(false);

    m_analyzeBtn->setMenu(menu);
}

// ────────────────────────────────────────────────────────────
//  INSPECT (read detailed properties — no calculations)
// ────────────────────────────────────────────────────────────
void ToolRibbon::buildInspectMenu()
{
    auto *menu = new QMenu(this);
    menu->setStyleSheet(kMenuStyle);

    menu->addAction("Mass Properties", this, [this]() {
        if (!m_assembly || m_assembly->getTotalMass() == 0) { m_statusLabel->setText("Nothing to inspect"); return; }
        float frameMass = 0, motorMass = 0, battMass = 0, otherMass = 0;
        auto frame = m_assembly->getFrame();
        if (frame) frameMass = frame->getMassGraph();
        for (const auto &n : m_assembly->getSnapNodes()) {
            if (!n.attachedComponent) continue;
            switch (n.attachedComponent->getType()) {
                case ComponentType::Motor: motorMass += n.attachedComponent->getMassGraph(); break;
                case ComponentType::Battery: battMass += n.attachedComponent->getMassGraph(); break;
                default: otherMass += n.attachedComponent->getMassGraph(); break;
            }
        }
        float total = m_assembly->getTotalMass();
        QString r = QString("MASS PROPERTIES\n\nFrame: %1g (%2%)\nMotors: %3g (%4%)\nBattery: %5g (%6%)\nOther: %7g (%8%)\n\nTotal: %9g")
            .arg(frameMass,0,'f',1).arg(total>0?frameMass/total*100:0,0,'f',0)
            .arg(motorMass,0,'f',1).arg(total>0?motorMass/total*100:0,0,'f',0)
            .arg(battMass,0,'f',1).arg(total>0?battMass/total*100:0,0,'f',0)
            .arg(otherMass,0,'f',1).arg(total>0?otherMass/total*100:0,0,'f',0)
            .arg(total,0,'f',1);
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Mass Properties"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addAction("CG Coordinates", this, [this]() {
        if (!m_assembly) return;
        auto cg = m_assembly->getCenterOfGravity();
        float offset = sqrtf(cg.x*cg.x + cg.z*cg.z);
        QString r = QString("CENTER OF GRAVITY\n\nX: %1 mm\nY: %2 mm\nZ: %3 mm\n\nOffset from center: %4 mm\n%5")
            .arg(cg.x*100,0,'f',2).arg(cg.y*100,0,'f',2).arg(cg.z*100,0,'f',2)
            .arg(offset*100,0,'f',2)
            .arg(offset < 0.1f ? "CG is centered." : offset < 0.3f ? "Slight offset — acceptable." : "Significant offset — rebalance.");
        QMessageBox msg(parentWidget()); msg.setWindowTitle("CG Coordinates"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addAction("Inertia Tensor", this, [this]() {
        if (!m_assembly) return;
        auto it = m_assembly->getInertiaTensor();
        QString r = QString("INERTIA TENSOR (g*mm^2)\n\nIxx: %1\nIyy: %2\nIzz: %3\n\nHigher values = more resistance to rotation on that axis.")
            .arg(it.Ixx, 0, 'f', 1).arg(it.Iyy, 0, 'f', 1).arg(it.Izz, 0, 'f', 1);
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Inertia Tensor"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addSeparator();

    menu->addAction("Motor Current Estimate", this, [this]() {
        if (!m_assembly) return;
        int motorCount = 0;
        float totalKV = 0;
        for (const auto &n : m_assembly->getSnapNodes()) {
            if (n.attachedComponent && n.attachedComponent->getType() == ComponentType::Motor) {
                motorCount++;
                auto m = std::static_pointer_cast<MotorComponent>(n.attachedComponent);
                totalKV += m->getKV();
            }
        }
        QString r;
        if (motorCount == 0) r = "No motors attached.";
        else {
            float avgKV = totalKV / motorCount;
            float estCurrentPerMotor = avgKV * 0.015f; // rough estimate
            r = QString("MOTOR CURRENT ESTIMATE\n\nMotors: %1\nAvg KV: %2\nEst. Current/Motor: ~%3A\nEst. Total Current: ~%4A\n\nNote: Actual varies with prop load.")
                .arg(motorCount).arg(avgKV,0,'f',0).arg(estCurrentPerMotor,0,'f',1).arg(estCurrentPerMotor*motorCount,0,'f',1);
        }
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Motor Current"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addAction("ESC Load")->setEnabled(false);
    menu->addAction("Battery Sag Estimate")->setEnabled(false);

    m_inspectBtn->setMenu(menu);
}

// ────────────────────────────────────────────────────────────
//  VALIDATE
// ────────────────────────────────────────────────────────────
void ToolRibbon::buildValidateMenu()
{
    auto *menu = new QMenu(this);
    menu->setStyleSheet(kMenuStyle);

    menu->addAction("Validate Build", this, [this]() {
        if (!m_assembly || !m_assembly->getFrame()) { m_statusLabel->setText("No frame"); return; }
        QStringList issues;
        int mc = 0, emptyMotor = 0;
        bool hasBatt = false, hasESC = false;
        for (const auto &n : m_assembly->getSnapNodes()) {
            if (n.acceptedType == ComponentType::Motor) { if (n.attachedComponent) mc++; else emptyMotor++; }
            if (n.attachedComponent) {
                if (n.attachedComponent->getType() == ComponentType::Battery) hasBatt = true;
                if (n.attachedComponent->getType() == ComponentType::ESC) hasESC = true;
            }
        }
        if (emptyMotor > 0) issues << QString("%1 empty motor slot(s)").arg(emptyMotor);
        if (!hasBatt) issues << "No battery attached";
        if (!hasESC) issues << "No ESC attached";
        float twr = m_assembly->getThrustToWeightRatio();
        if (twr > 0 && twr < 1.0f) issues << "CRITICAL: T/W ratio below 1";

        auto cg = m_assembly->getCenterOfGravity();
        float co = sqrtf(cg.x*cg.x + cg.z*cg.z);
        if (co > 0.5f) issues << "CG significantly off-center";

        QString r;
        if (issues.isEmpty()) { r = "BUILD VALIDATION PASSED\n\nAll systems nominal."; m_statusLabel->setText("VALID"); }
        else { r = "BUILD ISSUES\n\n"; for (auto &i : issues) r += QString("  X  %1\n").arg(i); m_statusLabel->setText(QString("%1 issue(s)").arg(issues.size())); }
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Build Validation"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addAction("Compatibility Check", this, [this]() {
        if (!m_assembly) return;
        QStringList w;
        int mc = 0;
        for (const auto &n : m_assembly->getSnapNodes())
            if (n.attachedComponent && n.attachedComponent->getType() == ComponentType::Motor) mc++;
        if (mc > 0 && mc < 4) w << QString("Only %1 motors (quad needs 4)").arg(mc);

        bool hasBatt = false;
        for (const auto &n : m_assembly->getSnapNodes())
            if (n.attachedComponent && n.attachedComponent->getType() == ComponentType::Battery) hasBatt = true;
        if (!hasBatt && mc > 0) w << "Motors have no power source";

        float twr = m_assembly->getThrustToWeightRatio();
        if (twr > 0 && twr < 1.5f) w << QString("Low T/W ratio (%1:1)").arg(twr,0,'f',1);
        if (!m_assembly->getFrame()) w << "No frame selected";

        QString r;
        if (w.isEmpty()) { r = "ALL CHECKS PASSED\n\nNo compatibility issues."; m_statusLabel->setText("Compatible"); }
        else { r = "COMPATIBILITY ISSUES\n\n"; for (auto &x : w) r += QString("  !  %1\n").arg(x); m_statusLabel->setText(QString("%1 issue(s)").arg(w.size())); }
        QMessageBox msg(parentWidget()); msg.setWindowTitle("Compatibility"); msg.setText(r);
        msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0;font-family:Consolas;font-size:11px}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });

    menu->addSeparator();
    menu->addAction("Risk Summary")->setEnabled(false);

    m_validateBtn->setMenu(menu);
}

// ────────────────────────────────────────────────────────────
//  VIEW (all visual toggles grouped cleanly)
// ────────────────────────────────────────────────────────────
void ToolRibbon::buildViewMenu()
{
    auto *menu = new QMenu(this);
    menu->setStyleSheet(kMenuStyle);

    // Camera presets
    menu->addAction("Front View",     this, [this]() { if (m_viewport) { m_viewport->setCameraAngle(0, 0); m_statusLabel->setText("Front"); } });
    menu->addAction("Top View",       this, [this]() { if (m_viewport) { m_viewport->setCameraAngle(90, 0); m_statusLabel->setText("Top"); } });
    menu->addAction("Right View",     this, [this]() { if (m_viewport) { m_viewport->setCameraAngle(0, 90); m_statusLabel->setText("Right"); } });
    menu->addAction("Back View",      this, [this]() { if (m_viewport) { m_viewport->setCameraAngle(0, 180); m_statusLabel->setText("Back"); } });
    menu->addAction("Left View",      this, [this]() { if (m_viewport) { m_viewport->setCameraAngle(0, -90); m_statusLabel->setText("Left"); } });
    menu->addAction("Bottom View",    this, [this]() { if (m_viewport) { m_viewport->setCameraAngle(-90, 0); m_statusLabel->setText("Bottom"); } });
    menu->addAction("Isometric View", this, [this]() { if (m_viewport) { m_viewport->setCameraAngle(30, 45); m_statusLabel->setText("Isometric"); } });
    menu->addSeparator();

    // Overlays
    menu->addAction("Toggle CG Marker");
    menu->addAction("Toggle Thrust Vectors");
    menu->addAction("Toggle Motor Direction");
    menu->addAction("Toggle Mass Heatmap")->setEnabled(false);
    menu->addAction("Toggle Stress Overlay")->setEnabled(false);
    menu->addSeparator();

    // Render mode
    menu->addAction("Wireframe Mode")->setEnabled(false);
    menu->addAction("Solid Mode")->setEnabled(false);
    menu->addSeparator();

    menu->addAction("Toggle Grid", this, [this]() { if (m_viewport) m_viewport->refreshView(); });
    menu->addAction("Reset Camera", this, [this]() { if (m_viewport) m_viewport->setCameraAngle(30, 45); });

    m_viewBtn->setMenu(menu);
}

void ToolRibbon::refreshState()
{
    bool hasFrame = m_assembly && m_assembly->getFrame();
    int cc = 0;
    if (hasFrame) { cc = 1; for (const auto &n : m_assembly->getSnapNodes()) if (n.attachedComponent) cc++; }
    m_statusLabel->setText(hasFrame ? QString("%1 parts").arg(cc) : "Use Insert to begin");
}
