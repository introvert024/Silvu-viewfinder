#pragma once
#include <QMainWindow>
#include <QTabWidget>
#include <QStackedWidget>
#include <QMenuBar>
#include <QStatusBar>
#include <QDockWidget>
#include <QLabel>

class ViewportWidget;
class BuildPanel;
class DiagPanel;
class ConfigPanel;
class ProtocolPanel;
class TelemetryBar;

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = nullptr);
    ~MainWindow() override = default;

private slots:
    void onTabChanged(int index);

private:
    void createMenuBar();
    void createTabs();
    void createBuildMode();
    void createStatusBar();

    // Tabs
    QTabWidget *m_tabBar = nullptr;
    QStackedWidget *m_stack = nullptr;

    // Build mode widgets
    QWidget *m_buildPage = nullptr;
    ViewportWidget *m_viewport = nullptr;
    BuildPanel *m_buildPanel = nullptr;
    DiagPanel *m_diagPanel = nullptr;
    TelemetryBar *m_telemetryBar = nullptr;

    // Other mode pages
    ConfigPanel *m_configPage = nullptr;
    ProtocolPanel *m_protocolPage = nullptr;
};
