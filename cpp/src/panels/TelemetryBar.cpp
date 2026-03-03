#include "TelemetryBar.h"
#include "../data/DroneAssembly.h"
#include "../data/DroneComponent.h"
#include <QHBoxLayout>
#include <QVBoxLayout>
#include <QGridLayout>
#include <QFrame>

TelemetryBar::TelemetryBar(DroneAssembly *assembly, QWidget *parent)
    : QWidget(parent), m_assembly(assembly)
{
    setStyleSheet("background: #162228; border-top: 1px solid #1e2d33;");

    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(20, 12, 20, 12);
    mainLayout->setSpacing(8);

    // Header row
    auto *headerRow = new QHBoxLayout();
    auto *title = new QLabel("● Live Telemetry Array");
    title->setStyleSheet("font-size: 10px; font-weight: bold; color: #e61414; letter-spacing: 2px; text-transform: uppercase;");
    auto *rate = new QLabel("120Hz Refresh");
    rate->setStyleSheet("font-size: 10px; color: #64748b; font-family: Consolas, monospace;");
    headerRow->addWidget(title);
    headerRow->addStretch();
    headerRow->addWidget(rate);
    mainLayout->addLayout(headerRow);

    auto *bodyRow = new QHBoxLayout();
    bodyRow->setSpacing(32);

    // Helper to make metric bar
    auto makeMetricWidget = [&](const QString &label, QLabel **valLabel, QProgressBar **bar, const QString &color) {
        auto *w = new QWidget();
        auto *layout = new QVBoxLayout(w);
        layout->setContentsMargins(0, 0, 0, 0);
        layout->setSpacing(4);

        auto *labelRow = new QHBoxLayout();
        auto *name = new QLabel(label);
        name->setStyleSheet("font-size: 11px; font-weight: bold; color: #cbd5e1;");
        *valLabel = new QLabel("0");
        (*valLabel)->setStyleSheet(QString("font-size: 11px; font-weight: bold; color: %1; font-family: Consolas, monospace;").arg(color));
        labelRow->addWidget(name);
        labelRow->addStretch();
        labelRow->addWidget(*valLabel);
        layout->addLayout(labelRow);

        *bar = new QProgressBar();
        (*bar)->setValue(0);
        (*bar)->setTextVisible(false);
        (*bar)->setFixedHeight(4);
        (*bar)->setStyleSheet(QString(
            "QProgressBar { background: #1e2d33; border: none; border-radius: 2px; }"
            "QProgressBar::chunk { background: %1; border-radius: 2px; }"
        ).arg(color));
        layout->addWidget(*bar);
        return w;
    };

    auto *metricsWidget = new QWidget();
    auto *metricsLayout = new QHBoxLayout(metricsWidget);
    metricsLayout->setSpacing(24);
    metricsLayout->addWidget(makeMetricWidget("Total Mass (g)", &m_massVal, &m_massBar, "#10b981"));
    metricsLayout->addWidget(makeMetricWidget("Total Thrust (g)", &m_thrustVal, &m_thrustBar, "#f97316"));
    metricsLayout->addWidget(makeMetricWidget("Battery (V)", &m_voltageVal, &m_voltageBar, "#e61414"));
    bodyRow->addWidget(metricsWidget, 1);

    // Separator
    auto *sep = new QFrame();
    sep->setFrameShape(QFrame::VLine);
    sep->setStyleSheet("color: #1e2d33;");
    bodyRow->addWidget(sep);

    // Inertia Tensor Matrix
    auto *inertiaWidget = new QWidget();
    inertiaWidget->setFixedWidth(280);
    auto *inertiaLayout = new QVBoxLayout(inertiaWidget);
    inertiaLayout->setContentsMargins(0, 0, 0, 0);
    inertiaLayout->setSpacing(4);

    auto *inertiaHeader = new QHBoxLayout();
    auto *inertiaTitle = new QLabel("Inertia Tensor Indices");
    inertiaTitle->setStyleSheet("font-size: 10px; font-weight: bold; color: #cbd5e1;");
    auto *inertiaUnit = new QLabel("kg·m²");
    inertiaUnit->setStyleSheet("font-size: 9px; font-weight: bold; color: #64748b;");
    inertiaHeader->addWidget(inertiaTitle);
    inertiaHeader->addStretch();
    inertiaHeader->addWidget(inertiaUnit);
    inertiaLayout->addLayout(inertiaHeader);

    auto *grid = new QGridLayout();
    grid->setSpacing(6);

    for (int r = 0; r < 3; r++) {
        for (int c = 0; c < 3; c++) {
            m_inertia[r][c] = new QLabel("0.000");
            m_inertia[r][c]->setAlignment(Qt::AlignCenter);
            bool isDiag = (r == c);
            m_inertia[r][c]->setStyleSheet(QString("font-size: 12px; font-weight: bold; font-family: Consolas, monospace; color: %1;")
                .arg(isDiag ? "#e61414" : "#64748b"));
            grid->addWidget(m_inertia[r][c], r, c);
        }
    }

    inertiaLayout->addLayout(grid);
    bodyRow->addWidget(inertiaWidget);

    mainLayout->addLayout(bodyRow);
}

void TelemetryBar::refreshUI()
{
    if (!m_assembly) return;

    float mass = m_assembly->getTotalMass();
    float thrust = m_assembly->getTotalThrust();

    m_massVal->setText(QString("%1g").arg(mass, 0, 'f', 1));
    m_massBar->setValue(qMin(100, (int)(mass / 10.0f))); // Scale: 1000g = 100%

    m_thrustVal->setText(QString("%1g").arg(thrust, 0, 'f', 0));
    m_thrustBar->setValue(qMin(100, (int)(thrust / 80.0f))); // Scale: 8000g = 100%

    // Check for battery voltage
    float voltage = 0;
    for (const auto &node : m_assembly->getSnapNodes()) {
        if (node.attachedComponent && node.attachedComponent->getType() == ComponentType::Battery) {
            auto batt = std::static_pointer_cast<BatteryComponent>(node.attachedComponent);
            voltage = batt->getVoltage();
        }
    }
    m_voltageVal->setText(voltage > 0 ? QString("%1V").arg(voltage, 0, 'f', 1) : "0V");
    m_voltageBar->setValue(qMin(100, (int)(voltage / 0.25f))); // Scale: 25V = 100%

    // Simple inertia estimation (I = m * r^2 for point masses)
    float Ixx = 0, Iyy = 0, Izz = 0;
    for (const auto &node : m_assembly->getSnapNodes()) {
        if (node.attachedComponent) {
            float m = node.attachedComponent->getMassGraph() / 1000.0f; // grams to kg
            float x = node.localPosition.x;
            float y = node.localPosition.y;
            float z = node.localPosition.z;
            Ixx += m * (y * y + z * z);
            Iyy += m * (x * x + z * z);
            Izz += m * (x * x + y * y);
        }
    }

    m_inertia[0][0]->setText(QString::number(Ixx, 'f', 3));
    m_inertia[1][1]->setText(QString::number(Iyy, 'f', 3));
    m_inertia[2][2]->setText(QString::number(Izz, 'f', 3));
    // Off-diagonal remain 0.000
}
