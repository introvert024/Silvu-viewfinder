#pragma once
#include <QWidget>
#include <QPushButton>
#include <QToolButton>
#include <QHBoxLayout>
#include <QMenu>
#include <QLabel>

class DroneAssembly;
class ViewportWidget;

class ToolRibbon : public QWidget
{
    Q_OBJECT
public:
    explicit ToolRibbon(DroneAssembly *assembly, QWidget *parent = nullptr);
    void setViewport(ViewportWidget *vp) { m_viewport = vp; }
    void refreshState();

signals:
    void assemblyChanged();
    void requestAddComponent(int type);
    void lockToggled(bool locked);

private:
    QPushButton* makeDropdownButton(const QString &text);
    void buildInsertMenu();
    void buildModifyMenu();
    void buildAnalyzeMenu();
    void buildInspectMenu();
    void buildValidateMenu();
    void buildViewMenu();

    DroneAssembly *m_assembly;
    ViewportWidget *m_viewport = nullptr;

    QPushButton *m_insertBtn;
    QPushButton *m_modifyBtn;
    QPushButton *m_analyzeBtn;
    QPushButton *m_inspectBtn;
    QPushButton *m_validateBtn;
    QPushButton *m_viewBtn;
    QToolButton *m_lockBtn;
    QToolButton *m_snapshotBtn;

    QLabel *m_statusLabel;
    bool m_locked = false;
};
