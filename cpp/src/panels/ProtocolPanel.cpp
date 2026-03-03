#include "ProtocolPanel.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QTextEdit>
#include <QLineEdit>
#include <QGridLayout>
#include <QRadioButton>
#include <QGroupBox>
#include <QSplitter>

static QWidget* makeStatCard(const QString &title, const QString &value, const QString &subtitle, const QString &subColor)
{
    auto *card = new QWidget();
    card->setStyleSheet("background: rgba(22,34,40,0.4); border: 1px solid #1e2d33; border-radius: 8px; padding: 16px;");
    auto *layout = new QVBoxLayout(card);
    layout->setSpacing(4);

    auto *t = new QLabel(title);
    t->setStyleSheet("font-size: 11px; font-weight: bold; color: #94a3b8; text-transform: uppercase; letter-spacing: 1px;");
    auto *v = new QLabel(value);
    v->setStyleSheet("font-size: 22px; font-weight: bold; color: #f1f5f9; letter-spacing: -1px;");
    auto *s = new QLabel(subtitle);
    s->setStyleSheet(QString("font-size: 11px; font-weight: 500; color: %1;").arg(subColor));

    layout->addWidget(t);
    layout->addWidget(v);
    layout->addWidget(s);
    return card;
}

ProtocolPanel::ProtocolPanel(QWidget *parent)
    : QWidget(parent)
{
    auto *mainLayout = new QHBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(0);

    // Left sidebar
    auto *sidebar = new QWidget();
    sidebar->setFixedWidth(220);
    sidebar->setStyleSheet("background: rgba(22,34,40,0.5); border-right: 1px solid #1e2d33;");
    auto *sideLayout = new QVBoxLayout(sidebar);
    sideLayout->setContentsMargins(12, 16, 12, 16);
    sideLayout->setSpacing(4);

    QStringList sideItems = {"📡 Radio Status", "⚙ Link Config", "💻 MAVLink Terminal", "🛰 Sat Link", "📊 Diagnostics"};
    for (int i = 0; i < sideItems.size(); ++i) {
        auto *btn = new QPushButton(sideItems[i]);
        btn->setStyleSheet(i == 0
            ? "QPushButton { text-align: left; padding: 10px 12px; border-radius: 6px; background: rgba(19,182,236,0.1); color: #e61414; font-size: 13px; font-weight: 600; border: none; }"
            : "QPushButton { text-align: left; padding: 10px 12px; border-radius: 6px; background: transparent; color: #94a3b8; font-size: 13px; font-weight: 500; border: none; }"
              "QPushButton:hover { background: #1e2d33; }"
        );
        sideLayout->addWidget(btn);
    }

    sideLayout->addStretch();

    // Emergency Kill section
    auto *killBox = new QWidget();
    killBox->setStyleSheet("background: rgba(239,68,68,0.1); border: 1px solid rgba(239,68,68,0.3); border-radius: 8px; padding: 12px;");
    auto *killLayout = new QVBoxLayout(killBox);
    auto *killTitle = new QLabel("⚠ SAFETY PROTOCOLS");
    killTitle->setStyleSheet("font-size: 11px; font-weight: bold; color: #ef4444; letter-spacing: 1px;");
    auto *killBtn = new QPushButton("EMERGENCY KILL");
    killBtn->setStyleSheet(
        "QPushButton { background: #dc2626; color: white; padding: 12px; border-radius: 6px;"
        "font-weight: bold; font-size: 12px; letter-spacing: 2px; border: none; }"
        "QPushButton:hover { background: #b91c1c; }"
    );
    killLayout->addWidget(killTitle);
    killLayout->addWidget(killBtn);
    sideLayout->addWidget(killBox);

    mainLayout->addWidget(sidebar);

    // Main content
    auto *content = new QWidget();
    auto *contentLayout = new QVBoxLayout(content);
    contentLayout->setContentsMargins(24, 16, 24, 16);
    contentLayout->setSpacing(16);

    // Protocol tabs
    auto *tabRow = new QHBoxLayout();
    QStringList protocols = {"ELRS", "Crossfire", "MAVLink", "Satellite"};
    for (int i = 0; i < protocols.size(); ++i) {
        auto *btn = new QPushButton(protocols[i]);
        btn->setStyleSheet(i == 0
            ? "QPushButton { border: none; border-bottom: 2px solid #e61414; color: #e61414; padding: 10px 16px; font-size: 12px; font-weight: bold; background: transparent; }"
            : "QPushButton { border: none; border-bottom: 2px solid transparent; color: #64748b; padding: 10px 16px; font-size: 12px; font-weight: bold; background: transparent; }"
              "QPushButton:hover { color: #e2e8f0; }"
        );
        tabRow->addWidget(btn);
    }
    tabRow->addStretch();
    contentLayout->addLayout(tabRow);

    // Stats grid
    auto *statsGrid = new QHBoxLayout();
    statsGrid->addWidget(makeStatCard("Link Status", "ACTIVE", "▲ 0% Latency Jitter", "#22c55e"));
    statsGrid->addWidget(makeStatCard("Packet Rate", "500Hz", "ExpressLRS F1000", "#e61414"));
    statsGrid->addWidget(makeStatCard("Telemetry Ratio", "1:32", "▼ -5% Packet Loss", "#ef4444"));
    statsGrid->addWidget(makeStatCard("RSSI (dBm)", "-84.2", "LQ: 100%", "#e61414"));
    contentLayout->addLayout(statsGrid);

    // Config + Terminal split
    auto *bodyLayout = new QHBoxLayout();
    bodyLayout->setSpacing(16);

    // Config panel
    auto *configCol = new QWidget();
    configCol->setFixedWidth(320);
    auto *configLayout = new QVBoxLayout(configCol);
    configLayout->setSpacing(8);

    auto *configTitle = new QLabel("LINK CONFIGURATION");
    configTitle->setStyleSheet("font-size: 11px; font-weight: bold; color: #94a3b8; letter-spacing: 2px;");
    configLayout->addWidget(configTitle);

    auto makeConfigRow = [](const QString &title, const QString &desc, const QString &val, bool active) {
        auto *w = new QWidget();
        w->setStyleSheet(active
            ? "background: rgba(19,182,236,0.05); border: 1px solid rgba(19,182,236,0.2); border-radius: 8px; padding: 12px;"
            : "background: rgba(22,34,40,0.4); border: 1px solid #1e2d33; border-radius: 8px; padding: 12px;");
        auto *l = new QHBoxLayout(w);
        auto *text = new QWidget();
        auto *tl = new QVBoxLayout(text);
        tl->setContentsMargins(0,0,0,0);
        auto *t = new QLabel(title);
        t->setStyleSheet(active ? "font-size: 12px; font-weight: bold; color: #ffffff;" : "font-size: 12px; font-weight: bold; color: #cbd5e1;");
        auto *d = new QLabel(desc);
        d->setStyleSheet("font-size: 10px; color: #64748b;");
        tl->addWidget(t);
        tl->addWidget(d);
        l->addWidget(text, 1);
        auto *v = new QLabel(val);
        v->setStyleSheet(active ? "font-size: 12px; font-weight: bold; color: #e61414; font-family: Consolas;" : "font-size: 12px; font-weight: bold; color: #64748b; font-family: Consolas;");
        l->addWidget(v);
        return w;
    };

    configLayout->addWidget(makeConfigRow("Packet Rate", "Global sync frequency", "500Hz", true));
    configLayout->addWidget(makeConfigRow("Telemetry Ratio", "Data downlink overhead", "1:32", false));
    configLayout->addWidget(makeConfigRow("Switch Mode", "Hybrid Wide dynamic", "HYBRID", false));

    // Satellite details
    auto *satBox = new QGroupBox("Satellite Link Details");
    auto *satLayout = new QVBoxLayout(satBox);
    auto addSatRow = [&](const QString &label, const QString &value, const QString &color = "#cbd5e1") {
        auto *row = new QHBoxLayout();
        auto *l = new QLabel(label);
        l->setStyleSheet("font-size: 11px; color: #94a3b8;");
        auto *v = new QLabel(value);
        v->setStyleSheet(QString("font-size: 11px; font-weight: bold; color: %1; font-family: Consolas, monospace;").arg(color));
        row->addWidget(l);
        row->addStretch();
        row->addWidget(v);
        satLayout->addLayout(row);
    };
    addSatRow("Uplink Encryption", "AES-256-GCM", "#22c55e");
    addSatRow("Polarization", "Circular Left");
    addSatRow("Orbital Slot", "112.5°W");
    configLayout->addWidget(satBox);
    configLayout->addStretch();

    bodyLayout->addWidget(configCol);

    // Terminal
    auto *terminalWidget = new QWidget();
    auto *termLayout = new QVBoxLayout(terminalWidget);
    termLayout->setContentsMargins(0, 0, 0, 0);
    termLayout->setSpacing(0);

    auto *termHeader = new QWidget();
    termHeader->setStyleSheet("background: #162228; border: 1px solid #1e2d33; border-bottom: none; border-radius: 8px 8px 0 0; padding: 8px 12px;");
    auto *termHLayout = new QHBoxLayout(termHeader);
    auto *termTitle = new QLabel("💻 MAVLINK MESSAGE INSPECTOR");
    termTitle->setStyleSheet("font-size: 11px; font-weight: bold; color: #94a3b8; letter-spacing: 1px;");
    auto *pauseBtn = new QPushButton("Pause");
    pauseBtn->setStyleSheet("background: rgba(19,182,236,0.2); color: #e61414; font-size: 10px; font-weight: bold; padding: 4px 10px; border-radius: 3px; border: none;");
    auto *clearBtn = new QPushButton("Clear");
    clearBtn->setStyleSheet("background: #1e293b; color: #94a3b8; font-size: 10px; font-weight: bold; padding: 4px 10px; border-radius: 3px; border: none;");
    termHLayout->addWidget(termTitle);
    termHLayout->addStretch();
    termHLayout->addWidget(pauseBtn);
    termHLayout->addWidget(clearBtn);
    termLayout->addWidget(termHeader);

    auto *terminal = new QTextEdit();
    terminal->setReadOnly(true);
    terminal->setStyleSheet("background: #0a0f12; border: 1px solid #1e2d33; border-top: none; border-bottom: none; font-family: Consolas, monospace; font-size: 11px; padding: 12px; color: #64748b; border-radius: 0;");

    terminal->append("<span style='color:#64748b'>[14:22:01]</span> <span style='color:#e61414'>MAV_MSG_ID_GPS_RAW_INT</span>: lat=407127840, lon=-740059410");
    terminal->append("<span style='color:#64748b'>[14:22:01]</span> <span style='color:#e61414'>MAV_MSG_ID_ATTITUDE</span>: roll=0.02, pitch=-0.01, yaw=1.57");
    terminal->append("<span style='color:#64748b'>[14:22:02]</span> <span style='color:#e61414'>MAV_MSG_ID_SYS_STATUS</span>: sensors_enabled=0x000F, load=450");
    terminal->append("<span style='color:#64748b'>[14:22:02]</span> <span style='color:#eab308'>MAV_MSG_ID_STATUSTEXT</span>: \"HEARTBEAT active: Protocol ELRS 3.0\"");
    terminal->append("<span style='color:#64748b'>[14:22:03]</span> <span style='color:#e61414'>MAV_MSG_ID_BATTERY_STATUS</span>: volt=16.8, curr=12.5, remain=88");
    terminal->append("<span style='color:#64748b'>[14:22:04]</span> <span style='color:#ef4444'>MAV_MSG_ID_STATUSTEXT</span>: \"WARNING: High interference on 2.4GHz\"");
    terminal->append("<span style='color:#64748b'>[14:22:05]</span> <span style='color:#e61414'>MAV_MSG_ID_HEARTBEAT</span>: type=2, autopilot=3, base_mode=81");

    termLayout->addWidget(terminal, 1);

    // Command input
    auto *inputRow = new QWidget();
    inputRow->setStyleSheet("background: #162228; border: 1px solid #1e2d33; border-top: none; border-radius: 0 0 8px 8px; padding: 8px;");
    auto *inputLayout = new QHBoxLayout(inputRow);
    auto *prompt = new QLabel(">");
    prompt->setStyleSheet("font-family: Consolas; font-weight: bold; color: #e61414; font-size: 12px;");
    auto *cmdInput = new QLineEdit();
    cmdInput->setPlaceholderText("Enter MAVLink command...");
    cmdInput->setStyleSheet("border: none; background: #101d22; font-family: Consolas; font-size: 11px; padding: 6px;");
    auto *sendBtn = new QPushButton("Send");
    sendBtn->setObjectName("primaryBtn");
    sendBtn->setStyleSheet("background: #e61414; color: #000; font-weight: bold; padding: 8px 20px; border-radius: 6px; font-size: 11px; letter-spacing: 1px; border: none;");
    inputLayout->addWidget(prompt);
    inputLayout->addWidget(cmdInput, 1);
    inputLayout->addWidget(sendBtn);
    termLayout->addWidget(inputRow);

    bodyLayout->addWidget(terminalWidget, 1);

    contentLayout->addLayout(bodyLayout, 1);
    mainLayout->addWidget(content, 1);
}
