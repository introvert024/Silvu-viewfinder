#pragma once
#include <QMainWindow>
#include <QTabWidget>
#include <QStackedWidget>
#include <QMenuBar>
#include <QStatusBar>
#include <QDockWidget>
#include <QLabel>

#include "data/DroneAssembly.h"

class ViewportWidget;
class BuildPanel;
class DiagPanel;
class ConfigPanel;
class ProtocolPanel;
class TelemetryBar;
class ToolRibbon;

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = nullptr);
    ~MainWindow() override = default;

private slots:
    void onTabChanged(int index);
    void onAssemblyChanged();

private:
    void createMenuBar();
    void createTabs();
    void createBuildMode();
    void createStatusBar();

    // Tabs
    QTabWidget *m_tabBar = nullptr;
    QStackedWidget *m_stack = nullptr;

    // Shared assembly
    DroneAssembly m_assembly;

    // Build mode widgets
    QWidget *m_buildPage = nullptr;
    ViewportWidget *m_viewport = nullptr;
    BuildPanel *m_buildPanel = nullptr;
    DiagPanel *m_diagPanel = nullptr;
    TelemetryBar *m_telemetryBar = nullptr;
    ToolRibbon *m_toolRibbon = nullptr;

    // Other mode pages
    ConfigPanel *m_configPage = nullptr;
    ProtocolPanel *m_protocolPage = nullptr;
};
