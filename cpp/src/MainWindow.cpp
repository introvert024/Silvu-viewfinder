#include "MainWindow.h"
#include "ViewportWidget.h"
#include "panels/BuildPanel.h"
#include "panels/DiagPanel.h"
#include "panels/ConfigPanel.h"
#include "panels/ProtocolPanel.h"
#include "panels/TelemetryBar.h"

#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QSplitter>
#include <QAction>
#include <QToolBar>

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
{
    createMenuBar();
    createTabs();
    createBuildMode();
    createStatusBar();

    // Connect tab switching
    connect(m_tabBar, &QTabWidget::currentChanged, this, &MainWindow::onTabChanged);

    // Default to Build tab
    m_tabBar->setCurrentIndex(0);
    onTabChanged(0);
}

void MainWindow::createMenuBar()
{
    auto *menuBar = this->menuBar();

    auto *fileMenu = menuBar->addMenu("File");
    fileMenu->addAction("New Project", this, [](){}, QKeySequence::New);
    fileMenu->addAction("Open Project...", this, [](){}, QKeySequence::Open);
    fileMenu->addAction("Save", this, [](){}, QKeySequence::Save);
    fileMenu->addAction("Save As...", this, [](){}, QKeySequence::SaveAs);
    fileMenu->addSeparator();
    fileMenu->addAction("Export CAD...");
    fileMenu->addSeparator();
    fileMenu->addAction("Exit", this, &QWidget::close, QKeySequence::Quit);

    auto *editMenu = menuBar->addMenu("Edit");
    editMenu->addAction("Undo", this, [](){}, QKeySequence::Undo);
    editMenu->addAction("Redo", this, [](){}, QKeySequence::Redo);
    editMenu->addSeparator();
    editMenu->addAction("Preferences...");

    auto *viewMenu = menuBar->addMenu("View");
    viewMenu->addAction("Toggle Build Panel");
    viewMenu->addAction("Toggle Diagnostics Panel");
    viewMenu->addAction("Toggle Telemetry Bar");
    viewMenu->addSeparator();
    viewMenu->addAction("Reset Layout");

    menuBar->addMenu("Assets");
    menuBar->addMenu("Settings");
    menuBar->addMenu("Data");
    menuBar->addMenu("Education");
    menuBar->addMenu("Workspace");
}

void MainWindow::createTabs()
{
    // Create tab bar (header) + stacked widget (body)
    auto *centralWidget = new QWidget(this);
    auto *layout = new QVBoxLayout(centralWidget);
    layout->setContentsMargins(0, 0, 0, 0);
    layout->setSpacing(0);

    m_tabBar = new QTabWidget(this);
    m_tabBar->setTabPosition(QTabWidget::North);
    m_tabBar->setDocumentMode(true);

    // Create pages
    m_buildPage = new QWidget();
    m_configPage = new ConfigPanel();
    m_protocolPage = new ProtocolPanel();

    // Stub pages for other tabs
    auto makeStub = [](const QString &name) {
        auto *w = new QWidget();
        auto *l = new QVBoxLayout(w);
        auto *label = new QLabel(name + " — Under Construction");
        label->setAlignment(Qt::AlignCenter);
        label->setStyleSheet("color: #64748b; font-size: 16px; font-weight: bold; letter-spacing: 3px; text-transform: uppercase;");
        l->addWidget(label);
        return w;
    };

    m_tabBar->addTab(m_buildPage, "BUILD");
    m_tabBar->addTab(m_configPage, "CONFIG");
    m_tabBar->addTab(makeStub("Simulation"), "SIMULATION");
    m_tabBar->addTab(makeStub("Debug"), "DEBUG");
    m_tabBar->addTab(makeStub("Software"), "SOFTWARE");
    m_tabBar->addTab(makeStub("Testing"), "TESTING");
    m_tabBar->addTab(m_protocolPage, "PROTOCOLS");
    m_tabBar->addTab(makeStub("Telemetry"), "TELEMETRY");

    setCentralWidget(m_tabBar);
}

void MainWindow::createBuildMode()
{
    // The Build page has a horizontal splitter with left/center/right
    auto *layout = new QHBoxLayout(m_buildPage);
    layout->setContentsMargins(0, 0, 0, 0);
    layout->setSpacing(0);

    // Left dock: Build Panel
    m_buildPanel = new BuildPanel(this);
    m_buildPanel->setFixedWidth(300);

    // Center: Viewport + bottom telemetry bar
    auto *centerWidget = new QWidget();
    auto *centerLayout = new QVBoxLayout(centerWidget);
    centerLayout->setContentsMargins(0, 0, 0, 0);
    centerLayout->setSpacing(0);

    m_viewport = new ViewportWidget(centerWidget);
    m_telemetryBar = new TelemetryBar(centerWidget);
    m_telemetryBar->setFixedHeight(130);

    centerLayout->addWidget(m_viewport, 1);
    centerLayout->addWidget(m_telemetryBar);

    // Right dock: Diagnostics Panel
    m_diagPanel = new DiagPanel(this);
    m_diagPanel->setFixedWidth(300);

    layout->addWidget(m_buildPanel);
    layout->addWidget(centerWidget, 1);
    layout->addWidget(m_diagPanel);
}

void MainWindow::createStatusBar()
{
    auto *status = statusBar();

    auto *linkLabel = new QLabel("● FC CONNECTED: MATEKH743");
    linkLabel->setStyleSheet("color: #22c55e; padding: 0 12px;");

    auto *cpuLabel = new QLabel("CPU LOAD: 14%");
    cpuLabel->setStyleSheet("padding: 0 12px;");

    auto *battLabel = new QLabel("BATT: 16.4V");
    battLabel->setStyleSheet("padding: 0 12px;");

    auto *fwLabel = new QLabel("FIRMWARE: VIEWFINDER v4.2.0");
    fwLabel->setStyleSheet("padding: 0 12px;");

    status->addWidget(linkLabel);
    status->addWidget(cpuLabel);
    status->addWidget(battLabel);
    status->addPermanentWidget(fwLabel);
}

void MainWindow::onTabChanged(int index)
{
    Q_UNUSED(index);
    // Tab switching is handled by QTabWidget automatically
}
