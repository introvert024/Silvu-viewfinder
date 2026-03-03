#include "ConfigPanel.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QSlider>
#include <QLineEdit>
#include <QGroupBox>
#include <QPushButton>
#include <QTabBar>
#include <QSplitter>
#include <QFrame>
#include <QProgressBar>

static QWidget* makePIDSlider(const QString &axis, const QString &label, int value, const QString &accentColor = "#e61414")
{
    auto *w = new QWidget();
    auto *layout = new QVBoxLayout(w);
    layout->setContentsMargins(0, 0, 0, 8);
    layout->setSpacing(4);

    // Label row
    auto *labelRow = new QHBoxLayout();
    auto *name = new QLabel(label);
    name->setStyleSheet("font-size: 11px; font-family: Consolas, monospace; color: #94a3b8; font-weight: bold; text-transform: uppercase;");
    auto *val = new QLabel(QString::number(value));
    val->setStyleSheet(QString("font-size: 11px; font-family: Consolas, monospace; color: %1; font-weight: bold;").arg(accentColor));
    labelRow->addWidget(name);
    labelRow->addStretch();
    labelRow->addWidget(val);
    layout->addLayout(labelRow);

    // Slider + input
    auto *sliderRow = new QHBoxLayout();
    auto *slider = new QSlider(Qt::Horizontal);
    slider->setRange(0, 100);
    slider->setValue(value);
    slider->setStyleSheet(QString(
        "QSlider::groove:horizontal { height: 6px; background: #1e2d33; border-radius: 3px; }"
        "QSlider::handle:horizontal { width: 14px; height: 14px; margin: -4px 0; border-radius: 7px; background: %1; }"
        "QSlider::sub-page:horizontal { background: %1; border-radius: 3px; }"
    ).arg(accentColor));

    auto *input = new QLineEdit(QString::number(value));
    input->setFixedWidth(48);
    input->setAlignment(Qt::AlignCenter);
    input->setStyleSheet("font-size: 11px; padding: 4px;");

    // Connect slider to value label and input
    QObject::connect(slider, &QSlider::valueChanged, [val, input](int v) {
        val->setText(QString::number(v));
        input->setText(QString::number(v));
    });

    sliderRow->addWidget(slider, 1);
    sliderRow->addWidget(input);
    layout->addLayout(sliderRow);

    return w;
}

ConfigPanel::ConfigPanel(QWidget *parent)
    : QWidget(parent)
{
    auto *mainLayout = new QHBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(0);

    // Left sidebar nav
    auto *sidebar = new QWidget();
    sidebar->setFixedWidth(220);
    sidebar->setStyleSheet("background: #162228; border-right: 1px solid #1e2d33;");
    auto *sideLayout = new QVBoxLayout(sidebar);
    sideLayout->setContentsMargins(12, 16, 12, 16);

    auto *modeLabel = new QLabel("ENGINEERING MODE");
    modeLabel->setStyleSheet("font-size: 10px; font-weight: bold; color: #64748b; letter-spacing: 2px; padding: 8px;");
    sideLayout->addWidget(modeLabel);

    QStringList navItems = {"Build", "Config", "Simulation", "Debug", "Software", "Testing", "Protocols", "Telemetry"};
    for (int i = 0; i < navItems.size(); ++i) {
        auto *btn = new QPushButton(navItems[i]);
        btn->setStyleSheet(i == 1
            ? "QPushButton { text-align: left; padding: 8px 12px; border-radius: 6px; background: rgba(19,182,236,0.1); color: #e61414; font-size: 13px; font-weight: 600; border: none; }"
            : "QPushButton { text-align: left; padding: 8px 12px; border-radius: 6px; background: transparent; color: #94a3b8; font-size: 13px; font-weight: 500; border: none; }"
              "QPushButton:hover { background: #1e2d33; }"
        );
        sideLayout->addWidget(btn);
    }
    sideLayout->addStretch();
    mainLayout->addWidget(sidebar);

    // Main content
    auto *content = new QWidget();
    auto *contentLayout = new QVBoxLayout(content);
    contentLayout->setContentsMargins(0, 0, 0, 0);
    contentLayout->setSpacing(0);

    // Sub-tabs bar
    auto *tabBar = new QWidget();
    tabBar->setStyleSheet("border-bottom: 1px solid #1e2d33;");
    auto *tabLayout = new QHBoxLayout(tabBar);
    tabLayout->setContentsMargins(24, 0, 24, 0);

    QStringList tabs = {"PID Tuning", "Filters", "Rates & Expo"};
    for (int i = 0; i < tabs.size(); ++i) {
        auto *btn = new QPushButton(tabs[i]);
        btn->setStyleSheet(i == 0
            ? "QPushButton { border: none; border-bottom: 2px solid #e61414; color: #e61414; padding: 12px 8px; font-size: 11px; font-weight: bold; text-transform: uppercase; letter-spacing: 1px; background: transparent; }"
            : "QPushButton { border: none; border-bottom: 2px solid transparent; color: #64748b; padding: 12px 8px; font-size: 11px; font-weight: bold; text-transform: uppercase; letter-spacing: 1px; background: transparent; }"
              "QPushButton:hover { color: #cbd5e1; }"
        );
        tabLayout->addWidget(btn);
    }
    tabLayout->addStretch();
    contentLayout->addWidget(tabBar);

    // PID + Graph area split
    auto *bodyLayout = new QHBoxLayout();
    bodyLayout->setContentsMargins(24, 24, 24, 24);
    bodyLayout->setSpacing(24);

    // Left: PID sliders
    auto *pidColumn = new QWidget();
    auto *pidLayout = new QVBoxLayout(pidColumn);
    pidLayout->setSpacing(20);

    // Roll axis
    auto *rollGroup = new QGroupBox("Roll Axis");
    auto *rollLayout = new QVBoxLayout(rollGroup);
    auto *rollMaster = new QLabel("Master Multiplier: 1.0x");
    rollMaster->setStyleSheet("font-size: 9px; color: #64748b; font-family: Consolas, monospace; background: #1e2d33; padding: 3px 8px; border-radius: 3px;");
    rollLayout->addWidget(rollMaster);
    rollLayout->addWidget(makePIDSlider("Roll", "Proportional (P)", 45));
    rollLayout->addWidget(makePIDSlider("Roll", "Integral (I)", 85));
    rollLayout->addWidget(makePIDSlider("Roll", "Derivative (D)", 32));
    pidLayout->addWidget(rollGroup);

    // Pitch axis
    auto *pitchGroup = new QGroupBox("Pitch Axis");
    auto *pitchLayout = new QVBoxLayout(pitchGroup);
    pitchLayout->addWidget(makePIDSlider("Pitch", "P / I / D Combined", 48));
    pidLayout->addWidget(pitchGroup);

    // Yaw axis
    auto *yawGroup = new QGroupBox("Yaw Axis");
    auto *yawLayout = new QVBoxLayout(yawGroup);
    yawLayout->addWidget(makePIDSlider("Yaw", "P / I / D combined", 70));
    pidLayout->addWidget(yawGroup);

    pidLayout->addStretch();
    bodyLayout->addWidget(pidColumn, 7);

    // Right: Graph area + stability score
    auto *graphColumn = new QWidget();
    auto *graphLayout = new QVBoxLayout(graphColumn);
    graphLayout->setSpacing(16);

    // Oscilloscope placeholder
    auto *oscBox = new QWidget();
    oscBox->setFixedHeight(250);
    oscBox->setStyleSheet("background: #080d10; border: 1px solid #1e2d33; border-radius: 8px;");
    auto *oscLayout = new QVBoxLayout(oscBox);
    auto *oscHeader = new QHBoxLayout();
    auto *oscLabel = new QLabel("■ Live Response Graph");
    oscLabel->setStyleSheet("font-size: 10px; font-weight: bold; color: #64748b; letter-spacing: 1px;");
    auto *oscLegend = new QLabel("━ SETPOINT  ╶╶ ACTUAL");
    oscLegend->setStyleSheet("font-size: 9px; font-family: Consolas, monospace; color: #64748b;");
    oscHeader->addWidget(oscLabel);
    oscHeader->addStretch();
    oscHeader->addWidget(oscLegend);
    oscLayout->addLayout(oscHeader);
    
    auto *graphPlaceholder = new QLabel("Graph renders here via OpenGL subwidget");
    graphPlaceholder->setAlignment(Qt::AlignCenter);
    graphPlaceholder->setStyleSheet("color: #1e2d33; font-style: italic; font-size: 12px;");
    oscLayout->addWidget(graphPlaceholder, 1);
    graphLayout->addWidget(oscBox);

    // Rates curve placeholder
    auto *ratesBox = new QGroupBox("Rates & Expo Curve");
    auto *ratesInner = new QVBoxLayout(ratesBox);
    auto *curvePlaceholder = new QWidget();
    curvePlaceholder->setFixedHeight(120);
    curvePlaceholder->setStyleSheet("background: rgba(16,29,34,0.5); border: 1px solid #1e2d33; border-radius: 4px;");
    ratesInner->addWidget(curvePlaceholder);

    auto *ratesGrid = new QHBoxLayout();
    auto makeRateBox = [](const QString &label, const QString &val) {
        auto *w = new QWidget();
        w->setStyleSheet("background: rgba(16,29,34,0.8); border: 1px solid #1e2d33; border-radius: 4px; padding: 6px;");
        auto *l = new QVBoxLayout(w);
        l->setContentsMargins(8, 4, 8, 4);
        auto *lbl = new QLabel(label);
        lbl->setStyleSheet("font-size: 9px; color: #64748b; text-transform: uppercase;");
        auto *v = new QLabel(val);
        v->setStyleSheet("font-size: 11px; font-weight: bold; font-family: Consolas, monospace; color: #e2e8f0;");
        l->addWidget(lbl);
        l->addWidget(v);
        return w;
    };
    ratesGrid->addWidget(makeRateBox("Rate", "0.75"));
    ratesGrid->addWidget(makeRateBox("Super", "0.68"));
    ratesGrid->addWidget(makeRateBox("Expo", "0.12"));
    ratesInner->addLayout(ratesGrid);
    graphLayout->addWidget(ratesBox);

    // Stability Impact Score
    auto *scoreBox = new QWidget();
    scoreBox->setStyleSheet("background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 #162731, stop:1 #101d22); border: 1px solid rgba(19,182,236,0.3); border-radius: 8px; padding: 16px;");
    auto *scoreLayout = new QVBoxLayout(scoreBox);
    auto *scoreTitle = new QLabel("STABILITY IMPACT SCORE");
    scoreTitle->setStyleSheet("font-size: 11px; font-weight: bold; color: #e61414; letter-spacing: 2px;");
    scoreLayout->addWidget(scoreTitle);

    auto *scoreRow = new QHBoxLayout();
    auto *scoreNum = new QLabel("8.4");
    scoreNum->setStyleSheet("font-size: 42px; font-weight: bold; color: #ffffff; letter-spacing: -2px;");
    auto *scoreBar = new QProgressBar();
    scoreBar->setValue(84);
    scoreBar->setTextVisible(false);
    scoreBar->setFixedHeight(8);
    scoreBar->setStyleSheet("QProgressBar { background: #1e2d33; border-radius: 4px; } QProgressBar::chunk { background: #e61414; border-radius: 4px; }");
    scoreRow->addWidget(scoreNum);
    scoreRow->addWidget(scoreBar, 1);
    scoreLayout->addLayout(scoreRow);

    auto *scoreDesc = new QLabel("Based on current PID values, the flight controller predicts\nhigh responsiveness with minimal oscillation.");
    scoreDesc->setWordWrap(true);
    scoreDesc->setStyleSheet("font-size: 11px; color: #cbd5e1; line-height: 1.5;");
    scoreLayout->addWidget(scoreDesc);

    auto *applyBtn = new QPushButton("Apply Recommended Tune");
    applyBtn->setObjectName("primaryBtn");
    auto *exportBtn = new QPushButton("Export CLI Config");
    scoreLayout->addWidget(applyBtn);
    scoreLayout->addWidget(exportBtn);
    graphLayout->addWidget(scoreBox);

    graphLayout->addStretch();
    bodyLayout->addWidget(graphColumn, 5);

    contentLayout->addLayout(bodyLayout, 1);
    mainLayout->addWidget(content, 1);
}
