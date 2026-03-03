#include "MainWindow.h"
#include "ViewportWidget.h"
#include "panels/BuildPanel.h"
#include "panels/DiagPanel.h"
#include "panels/ConfigPanel.h"
#include "panels/ProtocolPanel.h"
#include "panels/TelemetryBar.h"
#include "panels/ToolRibbon.h"

#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QSplitter>
#include <QAction>
#include <QToolBar>

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
{
    // Allow full vertical and horizontal resize
    setMinimumSize(800, 500);
    setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);

    createMenuBar();
    createTabs();
    createBuildMode();
    createStatusBar();

    connect(m_tabBar, &QTabWidget::currentChanged, this, &MainWindow::onTabChanged);

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
    auto *centralWidget = new QWidget(this);
    auto *layout = new QVBoxLayout(centralWidget);
    layout->setContentsMargins(0, 0, 0, 0);
    layout->setSpacing(0);

    m_tabBar = new QTabWidget(this);
    m_tabBar->setTabPosition(QTabWidget::North);
    m_tabBar->setDocumentMode(true);

    m_buildPage = new QWidget();
    m_configPage = new ConfigPanel();

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
    m_tabBar->addTab(makeStub("Testing"), "TESTING");
    m_tabBar->addTab(makeStub("Telemetry"), "TELEMETRY");

    setCentralWidget(m_tabBar);
}

void MainWindow::createBuildMode()
{
    // Outer vertical: Ribbon on top, then content below
    auto *outerLayout = new QVBoxLayout(m_buildPage);
    outerLayout->setContentsMargins(0, 0, 0, 0);
    outerLayout->setSpacing(0);
    m_buildPage->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);

    // Tool Ribbon — full width across entire build page
    m_toolRibbon = new ToolRibbon(&m_assembly, m_buildPage);
    m_toolRibbon->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
    outerLayout->addWidget(m_toolRibbon, 0);

    // Content area: left panel + center + right panel
    auto *contentWidget = new QWidget(m_buildPage);
    contentWidget->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
    auto *contentLayout = new QHBoxLayout(contentWidget);
    contentLayout->setContentsMargins(0, 0, 0, 0);
    contentLayout->setSpacing(0);

    // Left: Build Panel
    m_buildPanel = new BuildPanel(&m_assembly, contentWidget);
    m_buildPanel->setFixedWidth(300);
    m_buildPanel->setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Expanding);

    // Center: Viewport + Telemetry
    auto *centerWidget = new QWidget(contentWidget);
    centerWidget->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
    auto *centerLayout = new QVBoxLayout(centerWidget);
    centerLayout->setContentsMargins(0, 0, 0, 0);
    centerLayout->setSpacing(0);

    m_viewport = new ViewportWidget(centerWidget);
    m_viewport->setAssembly(&m_assembly);
    m_viewport->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
    m_viewport->setMinimumSize(200, 150);

    m_toolRibbon->setViewport(m_viewport);

    m_telemetryBar = new TelemetryBar(&m_assembly, centerWidget);
    m_telemetryBar->setFixedHeight(120);
    m_telemetryBar->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);

    centerLayout->addWidget(m_viewport, 1);
    centerLayout->addWidget(m_telemetryBar, 0);

    // Right: Diagnostics Panel
    m_diagPanel = new DiagPanel(&m_assembly, contentWidget);
    m_diagPanel->setFixedWidth(300);
    m_diagPanel->setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Expanding);

    contentLayout->addWidget(m_buildPanel, 0);
    contentLayout->addWidget(centerWidget, 1);
    contentLayout->addWidget(m_diagPanel, 0);

    outerLayout->addWidget(contentWidget, 1);

    // Connect signals
    connect(m_buildPanel, &BuildPanel::assemblyChanged, this, &MainWindow::onAssemblyChanged);
    connect(m_viewport, &ViewportWidget::componentDropped, this, &MainWindow::onAssemblyChanged);
    connect(m_toolRibbon, &ToolRibbon::assemblyChanged, this, &MainWindow::onAssemblyChanged);
}

void MainWindow::onAssemblyChanged()
{
    m_buildPanel->refreshUI();
    m_diagPanel->refreshUI();
    m_telemetryBar->refreshUI();
    m_viewport->refreshView();
    if (m_toolRibbon) m_toolRibbon->refreshState();
}

void MainWindow::createStatusBar()
{
    auto *status = statusBar();

    auto *linkLabel = new QLabel("● FC DISCONNECTED");
    linkLabel->setStyleSheet("color: #64748b; padding: 0 12px;");

    auto *cpuLabel = new QLabel("CPU LOAD: --");
    cpuLabel->setStyleSheet("padding: 0 12px;");

    auto *battLabel = new QLabel("BATT: --");
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
}
