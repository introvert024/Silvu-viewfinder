#include "DiagPanel.h"
#include "../data/DroneAssembly.h"
#include "../data/DroneComponent.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QProgressBar>
#include <QFrame>
#include <QScrollArea>
#include <cmath>

DiagPanel::DiagPanel(DroneAssembly *assembly, QWidget *parent)
    : QWidget(parent), m_assembly(assembly)
{
    auto *outerLayout = new QVBoxLayout(this);
    outerLayout->setContentsMargins(0, 0, 0, 0);
    outerLayout->setSpacing(0);
    setStyleSheet("background: #162228; border-left: 1px solid #1e2d33;");

    auto *scroll = new QScrollArea();
    scroll->setWidgetResizable(true);
    scroll->setStyleSheet("QScrollArea{border:none;background:transparent}"
        "QScrollBar:vertical{background:#111a1f;width:5px}"
        "QScrollBar::handle:vertical{background:#1e2d33;border-radius:2px;min-height:16px}"
        "QScrollBar::add-line:vertical,QScrollBar::sub-line:vertical{height:0}");

    auto *content = new QWidget();
    auto *layout = new QVBoxLayout(content);
    layout->setContentsMargins(14, 14, 14, 14);
    layout->setSpacing(10);

    // Header
    auto *sectionLabel = new QLabel("DIAGNOSTICS");
    sectionLabel->setStyleSheet("font-size: 9px; font-weight: bold; color: #e61414; letter-spacing: 2px;");
    layout->addWidget(sectionLabel);

    // ═══ MASS BREAKDOWN ═══
    auto *massTitle = new QLabel("MASS BREAKDOWN");
    massTitle->setStyleSheet("font-size: 9px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding-bottom: 4px; border-bottom: 1px solid #1e2d33;");
    layout->addWidget(massTitle);

    auto addMassRow = [&](const QString &name, QLabel **val) {
        auto *row = new QHBoxLayout();
        auto *n = new QLabel(name); n->setStyleSheet("font-size: 10px; color: #94a3b8;");
        *val = new QLabel("0g"); (*val)->setStyleSheet("font-size: 10px; color: #64748b; font-family: Consolas;");
        row->addWidget(n); row->addStretch(); row->addWidget(*val);
        layout->addLayout(row);
    };
    addMassRow("Frame", &m_massFrame);
    addMassRow("Motors", &m_massMotors);
    addMassRow("Battery", &m_massBattery);
    addMassRow("Other", &m_massOther);

    auto *sep1 = new QFrame(); sep1->setFrameShape(QFrame::HLine); sep1->setStyleSheet("color: #1e2d33;");
    layout->addWidget(sep1);

    { auto *row = new QHBoxLayout();
      auto *n = new QLabel("Total"); n->setStyleSheet("font-size: 11px; color: #f1f5f9; font-weight: bold;");
      m_massTotal = new QLabel("0g"); m_massTotal->setStyleSheet("font-size: 12px; color: #f1f5f9; font-weight: bold; font-family: Consolas;");
      row->addWidget(n); row->addStretch(); row->addWidget(m_massTotal); layout->addLayout(row); }

    { auto *row = new QHBoxLayout();
      auto *n = new QLabel("Payload Left"); n->setStyleSheet("font-size: 10px; color: #94a3b8;");
      m_payloadRemaining = new QLabel("--"); m_payloadRemaining->setStyleSheet("font-size: 10px; color: #64748b; font-family: Consolas;");
      row->addWidget(n); row->addStretch(); row->addWidget(m_payloadRemaining); layout->addLayout(row); }

    // ═══ THRUST DYNAMICS ═══
    auto *thrustTitle = new QLabel("THRUST DYNAMICS");
    thrustTitle->setStyleSheet("font-size: 9px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding-bottom: 4px; border-bottom: 1px solid #1e2d33; margin-top: 6px;");
    layout->addWidget(thrustTitle);

    auto addMetric = [&](const QString &name, QLabel **val, const QString &init = "--") {
        auto *row = new QHBoxLayout();
        auto *n = new QLabel(name); n->setStyleSheet("font-size: 10px; color: #94a3b8;");
        *val = new QLabel(init); (*val)->setStyleSheet("font-size: 12px; font-weight: bold; color: #64748b; font-family: Consolas;");
        row->addWidget(n); row->addStretch(); row->addWidget(*val);
        layout->addLayout(row);
    };
    addMetric("Hover Throttle", &m_hoverThrottle);
    addMetric("T/W Ratio", &m_thrustMargin);
    addMetric("Max Thrust", &m_maxThrust, "0g");
    addMetric("Stability", &m_stabIndex);

    // ═══ SYSTEM HEALTH ═══
    auto *healthTitle = new QLabel("SYSTEM HEALTH");
    healthTitle->setStyleSheet("font-size: 9px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding-bottom: 4px; border-bottom: 1px solid #1e2d33; margin-top: 6px;");
    layout->addWidget(healthTitle);

    auto *healthGrid = new QGridLayout();
    auto makeHealthBox = [](const QString &label, const QString &value, const QString &color) {
        auto *box = new QWidget();
        box->setStyleSheet(QString("border: 1px solid %1; background: rgba(%2, 0.05); border-radius: 4px; padding: 6px;")
            .arg(color).arg(color == "#10b981" ? "16,185,129" : "100,116,139"));
        auto *l = new QVBoxLayout(box); l->setAlignment(Qt::AlignCenter);
        auto *lbl = new QLabel(label); lbl->setAlignment(Qt::AlignCenter);
        lbl->setStyleSheet("font-size: 8px; color: #64748b; font-weight: bold; letter-spacing: 1px; border: none;");
        auto *val = new QLabel(value); val->setAlignment(Qt::AlignCenter);
        val->setStyleSheet(QString("font-size: 16px; font-weight: bold; color: %1; font-family: Consolas; border: none;").arg(color));
        l->addWidget(lbl); l->addWidget(val);
        return box;
    };
    healthGrid->addWidget(makeHealthBox("LINK", "--", "#64748b"), 0, 0);
    healthGrid->addWidget(makeHealthBox("VIBES", "--", "#64748b"), 0, 1);
    layout->addLayout(healthGrid);

    // ═══ VALIDATION CONSOLE ═══
    auto *consoleTitle = new QLabel("VALIDATION CONSOLE");
    consoleTitle->setStyleSheet("font-size: 9px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding-bottom: 4px; border-bottom: 1px solid #1e2d33; margin-top: 6px;");
    layout->addWidget(consoleTitle);

    m_console = new QTextEdit();
    m_console->setReadOnly(true);
    m_console->setFixedHeight(110);
    m_console->setStyleSheet("background: #0a0f12; border: 1px solid #1e2d33; border-radius: 4px; font-family: Consolas; font-size: 9px; padding: 6px; color: #94a3b8;");
    m_console->append("<span style='color:#64748b'>[--:--:--] Waiting for components...</span>");
    layout->addWidget(m_console);

    layout->addStretch();
    scroll->setWidget(content);
    outerLayout->addWidget(scroll);
}

void DiagPanel::refreshUI()
{
    if (!m_assembly) return;

    float totalMass = m_assembly->getTotalMass();
    float totalThrust = m_assembly->getTotalThrust();
    float twr = m_assembly->getThrustToWeightRatio();

    // ── Mass breakdown ──
    float frameMass = 0, motorMass = 0, batteryMass = 0, otherMass = 0;
    auto frame = m_assembly->getFrame();
    if (frame) frameMass = frame->getMassGraph();

    for (const auto &n : m_assembly->getSnapNodes()) {
        if (!n.attachedComponent) continue;
        auto c = n.attachedComponent;
        switch (c->getType()) {
            case ComponentType::Motor: motorMass += c->getMassGraph(); break;
            case ComponentType::Battery: batteryMass += c->getMassGraph(); break;
            default: otherMass += c->getMassGraph(); break;
        }
    }

    auto active = "font-size: 10px; color: #e2e8f0; font-family: Consolas;";
    auto dim = "font-size: 10px; color: #64748b; font-family: Consolas;";
    m_massFrame->setText(QString("%1g").arg(frameMass, 0, 'f', 1)); m_massFrame->setStyleSheet(frameMass > 0 ? active : dim);
    m_massMotors->setText(QString("%1g").arg(motorMass, 0, 'f', 1)); m_massMotors->setStyleSheet(motorMass > 0 ? active : dim);
    m_massBattery->setText(QString("%1g").arg(batteryMass, 0, 'f', 1)); m_massBattery->setStyleSheet(batteryMass > 0 ? active : dim);
    m_massOther->setText(QString("%1g").arg(otherMass, 0, 'f', 1)); m_massOther->setStyleSheet(otherMass > 0 ? active : dim);
    m_massTotal->setText(QString("%1g").arg(totalMass, 0, 'f', 1));

    if (twr > 0) {
        float mp = totalThrust - totalMass;
        m_payloadRemaining->setText(QString("%1g").arg(mp > 0 ? mp : 0, 0, 'f', 0));
        m_payloadRemaining->setStyleSheet("font-size: 10px; color: #10b981; font-family: Consolas;");
    } else {
        m_payloadRemaining->setText("--");
        m_payloadRemaining->setStyleSheet(dim);
    }

    // ── Thrust ──
    m_maxThrust->setText(QString("%1g").arg(totalThrust, 0, 'f', 0));
    m_maxThrust->setStyleSheet(totalThrust > 0
        ? "font-size: 12px; font-weight: bold; color: #e2e8f0; font-family: Consolas;"
        : "font-size: 12px; font-weight: bold; color: #64748b; font-family: Consolas;");

    if (twr > 0) {
        QString status = twr >= 2.0f ? "Optimal" : (twr >= 1.0f ? "Marginal" : "CRITICAL");
        QString color = twr >= 2.0f ? "#10b981" : (twr >= 1.0f ? "#f97316" : "#ef4444");
        m_thrustMargin->setText(QString("%1:1 %2").arg(twr, 0, 'f', 1).arg(status));
        m_thrustMargin->setStyleSheet(QString("font-size: 12px; font-weight: bold; color: %1; font-family: Consolas;").arg(color));

        float hoverPct = (1.0f / twr) * 100.0f;
        QString hStatus = hoverPct < 50.0f ? "Optimal" : (hoverPct < 80.0f ? "High" : "DANGER");
        QString hColor = hoverPct < 50.0f ? "#10b981" : (hoverPct < 80.0f ? "#f97316" : "#ef4444");
        m_hoverThrottle->setText(QString("%1% %2").arg(hoverPct, 0, 'f', 0).arg(hStatus));
        m_hoverThrottle->setStyleSheet(QString("font-size: 12px; font-weight: bold; color: %1; font-family: Consolas;").arg(hColor));
    } else {
        m_thrustMargin->setText("--");
        m_thrustMargin->setStyleSheet("font-size: 12px; font-weight: bold; color: #64748b; font-family: Consolas;");
        m_hoverThrottle->setText("--");
        m_hoverThrottle->setStyleSheet("font-size: 12px; font-weight: bold; color: #64748b; font-family: Consolas;");
    }

    // Stability
    auto cg = m_assembly->getCenterOfGravity();
    float cgOffset = sqrtf(cg.x * cg.x + cg.z * cg.z);
    if (totalMass > 0) {
        float score = fmax(0.0f, 10.0f - cgOffset * 5.0f);
        QString color = score >= 7.0f ? "#10b981" : (score >= 4.0f ? "#f97316" : "#ef4444");
        m_stabIndex->setText(QString("%1/10").arg(score, 0, 'f', 1));
        m_stabIndex->setStyleSheet(QString("font-size: 12px; font-weight: bold; color: %1; font-family: Consolas;").arg(color));
    } else {
        m_stabIndex->setText("--");
        m_stabIndex->setStyleSheet("font-size: 12px; font-weight: bold; color: #64748b; font-family: Consolas;");
    }

    // Console
    m_console->clear();
    if (totalMass > 0) {
        m_console->append(QString("<span style='color:#94a3b8'>[SYS] Mass: %1g  Thrust: %2g  T/W: %3:1</span>")
            .arg(totalMass, 0, 'f', 1).arg(totalThrust, 0, 'f', 0).arg(twr, 0, 'f', 2));
        m_console->append(QString("<span style='color:#94a3b8'>[SYS] CG: (%1, %2, %3)</span>")
            .arg(cg.x, 0, 'f', 3).arg(cg.y, 0, 'f', 3).arg(cg.z, 0, 'f', 3));
        if (twr < 1.0f && totalThrust > 0)
            m_console->append("<span style='color:#ef4444'><b>[ERROR] Thrust insufficient!</b></span>");
        if (cgOffset > 0.5f)
            m_console->append("<span style='color:#f97316'><b>[WARN] CG off-center</b></span>");
    } else {
        m_console->append("<span style='color:#64748b'>[--:--:--] Waiting for components...</span>");
    }
}
