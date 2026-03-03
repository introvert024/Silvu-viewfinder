#include "DiagPanel.h"
#include <QVBoxLayout>
#include <QLabel>
#include <QTextEdit>
#include <QGridLayout>
#include <QProgressBar>

DiagPanel::DiagPanel(QWidget *parent)
    : QWidget(parent)
{
    auto *layout = new QVBoxLayout(this);
    layout->setContentsMargins(0, 0, 0, 0);
    layout->setSpacing(0);
    setStyleSheet("background: #162228; border-left: 1px solid #1e2d33;");

    // Header
    auto *header = new QWidget();
    auto *hLayout = new QVBoxLayout(header);
    hLayout->setContentsMargins(16, 16, 16, 16);
    auto *sectionLabel = new QLabel("HEALTH & DIAGNOSTICS");
    sectionLabel->setStyleSheet("font-size: 10px; font-weight: bold; color: #e61414; letter-spacing: 2px;");
    auto *title = new QLabel("Calculated Metrics");
    title->setStyleSheet("font-size: 15px; font-weight: bold; color: #f1f5f9;");
    hLayout->addWidget(sectionLabel);
    hLayout->addWidget(title);
    layout->addWidget(header);

    // Content area with scroll
    auto *content = new QWidget();
    auto *cLayout = new QVBoxLayout(content);
    cLayout->setContentsMargins(16, 0, 16, 16);
    cLayout->setSpacing(16);

    // Thrust Dynamics Section
    auto *thrustTitle = new QLabel("THRUST DYNAMICS");
    thrustTitle->setStyleSheet("font-size: 10px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding-bottom: 6px; border-bottom: 1px solid #1e2d33;");
    cLayout->addWidget(thrustTitle);

    auto addMetric = [&](const QString &name, const QString &val, const QString &color) {
        auto *row = new QHBoxLayout();
        auto *n = new QLabel(name);
        n->setStyleSheet("font-size: 11px; font-weight: bold; color: #94a3b8;");
        auto *v = new QLabel(val);
        v->setStyleSheet(QString("font-size: 13px; font-weight: bold; color: %1; font-family: Consolas, monospace;").arg(color));
        row->addWidget(n);
        row->addStretch();
        row->addWidget(v);
        cLayout->addLayout(row);
    };

    addMetric("Hover Throttle", "32% Optimal", "#10b981");
    addMetric("Thrust Margin", "4:1 Optimal", "#10b981");
    addMetric("Max peak", "48.2kg", "#e2e8f0");
    addMetric("Thermal Prediction", "82°C Warning", "#e61414");

    // Warning box
    auto *warnBox = new QLabel("Possible ESC throttling predicted in high-load maneuvers.");
    warnBox->setWordWrap(true);
    warnBox->setStyleSheet("font-size: 9px; color: rgba(19,182,236,0.8); padding: 8px; background: rgba(240,66,66,0.05); border: 1px solid rgba(19,182,236,0.2); border-radius: 4px;");
    cLayout->addWidget(warnBox);

    // Stability Index
    auto *stabRow = new QHBoxLayout();
    auto *stabLabel = new QLabel("Stability Index");
    stabLabel->setStyleSheet("font-size: 11px; font-weight: bold; color: #94a3b8;");
    auto *stabVal = new QLabel("9.2/10 Stable");
    stabVal->setStyleSheet("font-size: 13px; font-weight: bold; color: #e61414; font-family: Consolas, monospace;");
    stabRow->addWidget(stabLabel);
    stabRow->addStretch();
    stabRow->addWidget(stabVal);
    cLayout->addLayout(stabRow);

    // System Health Section
    auto *healthTitle = new QLabel("SYSTEM HEALTH");
    healthTitle->setStyleSheet("font-size: 10px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding-bottom: 6px; border-bottom: 1px solid #1e2d33; margin-top: 8px;");
    cLayout->addWidget(healthTitle);

    auto *healthGrid = new QGridLayout();
    
    auto makeHealthBox = [](const QString &label, const QString &value, const QString &color) {
        auto *box = new QWidget();
        box->setStyleSheet(QString("border: 1px solid %1; background: rgba(%2, 0.05); border-radius: 4px; padding: 8px;")
            .arg(color).arg(color == "#10b981" ? "16,185,129" : "19,182,236"));
        auto *l = new QVBoxLayout(box);
        l->setAlignment(Qt::AlignCenter);
        auto *lbl = new QLabel(label);
        lbl->setAlignment(Qt::AlignCenter);
        lbl->setStyleSheet("font-size: 9px; color: #64748b; font-weight: bold; letter-spacing: 2px; border: none;");
        auto *val = new QLabel(value);
        val->setAlignment(Qt::AlignCenter);
        val->setStyleSheet(QString("font-size: 18px; font-weight: bold; color: %1; font-family: Consolas, monospace; border: none;").arg(color));
        l->addWidget(lbl);
        l->addWidget(val);
        return box;
    };

    healthGrid->addWidget(makeHealthBox("LINK QUAL", "98%", "#10b981"), 0, 0);
    healthGrid->addWidget(makeHealthBox("VIBES", "LOW", "#e61414"), 0, 1);
    cLayout->addLayout(healthGrid);

    // Validation Console
    auto *consoleTitle = new QLabel("VALIDATION CONSOLE");
    consoleTitle->setStyleSheet("font-size: 10px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding-bottom: 6px; border-bottom: 1px solid #1e2d33; margin-top: 8px;");
    cLayout->addWidget(consoleTitle);

    auto *console = new QTextEdit();
    console->setReadOnly(true);
    console->setFixedHeight(120);
    console->setStyleSheet("background: #0a0f12; border: 1px solid #1e2d33; border-radius: 4px; font-family: Consolas, monospace; font-size: 9px; padding: 8px; color: #94a3b8;");

    console->append("<span style='color:#94a3b8'>[12:44:01] Inertia tensor recalculated</span>");
    console->append("<span style='color:#94a3b8'>[12:44:02] CG offset: +1.2mm</span>");
    console->append("<span style='color:#94a3b8'>[12:44:05] Static thrust testing complete</span>");
    console->append("<span style='color:#e61414'><b>[12:44:10] ERROR: ESC 3 heat soak alert</b></span>");
    console->append("<span style='color:#94a3b8'>[12:44:12] Monitoring thermals...</span>");

    cLayout->addWidget(console);
    cLayout->addStretch();

    layout->addWidget(content, 1);
}
