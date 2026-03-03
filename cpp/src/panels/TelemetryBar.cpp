#include "TelemetryBar.h"
#include <QHBoxLayout>
#include <QVBoxLayout>
#include <QLabel>
#include <QProgressBar>
#include <QGridLayout>

static QWidget* makeMetricBar(const QString &title, const QString &value, const QString &color, int percent)
{
    auto *w = new QWidget();
    auto *layout = new QVBoxLayout(w);
    layout->setContentsMargins(0, 0, 0, 0);
    layout->setSpacing(4);

    auto *labelRow = new QHBoxLayout();
    auto *name = new QLabel(title);
    name->setStyleSheet("font-size: 11px; font-weight: bold; color: #cbd5e1;");
    auto *val = new QLabel(value);
    val->setStyleSheet(QString("font-size: 11px; font-weight: bold; color: %1; font-family: Consolas, monospace;").arg(color));
    labelRow->addWidget(name);
    labelRow->addStretch();
    labelRow->addWidget(val);
    layout->addLayout(labelRow);

    auto *bar = new QProgressBar();
    bar->setValue(percent);
    bar->setTextVisible(false);
    bar->setFixedHeight(4);
    bar->setStyleSheet(QString(
        "QProgressBar { background: #1e2d33; border: none; border-radius: 2px; }"
        "QProgressBar::chunk { background: %1; border-radius: 2px; }"
    ).arg(color));
    layout->addWidget(bar);

    return w;
}

TelemetryBar::TelemetryBar(QWidget *parent)
    : QWidget(parent)
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

    // Metrics + Inertia row
    auto *bodyRow = new QHBoxLayout();
    bodyRow->setSpacing(32);

    // Metric bars
    auto *metricsWidget = new QWidget();
    auto *metricsLayout = new QHBoxLayout(metricsWidget);
    metricsLayout->setSpacing(24);
    metricsLayout->addWidget(makeMetricBar("Voltage (V)", "24.8V", "#10b981", 85));
    metricsLayout->addWidget(makeMetricBar("Current (A)", "12.4A", "#f97316", 45));
    metricsLayout->addWidget(makeMetricBar("Motor RPM", "12,400", "#f04242", 65));
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

    // 3x3 matrix grid
    auto *grid = new QGridLayout();
    grid->setSpacing(6);

    auto makeCell = [](const QString &val, bool isDiagonal) {
        auto *l = new QLabel(val);
        l->setAlignment(Qt::AlignCenter);
        l->setStyleSheet(QString("font-size: 12px; font-weight: bold; font-family: Consolas, monospace; color: %1;")
            .arg(isDiagonal ? "#e61414" : "#f1f5f9"));
        return l;
    };

    // Row 0
    grid->addWidget(makeCell("0.012", true), 0, 0);
    grid->addWidget(makeCell("0.000", false), 0, 1);
    grid->addWidget(makeCell("0.000", false), 0, 2);
    // Row 1
    grid->addWidget(makeCell("0.000", false), 1, 0);
    grid->addWidget(makeCell("0.015", true), 1, 1);
    grid->addWidget(makeCell("0.000", false), 1, 2);
    // Row 2
    grid->addWidget(makeCell("0.000", false), 2, 0);
    grid->addWidget(makeCell("0.000", false), 2, 1);
    grid->addWidget(makeCell("0.024", true), 2, 2);

    inertiaLayout->addLayout(grid);
    bodyRow->addWidget(inertiaWidget);

    mainLayout->addLayout(bodyRow);
}
