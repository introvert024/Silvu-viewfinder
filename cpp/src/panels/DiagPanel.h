#pragma once
#include <QWidget>
#include <QLabel>
#include <QTextEdit>

class DroneAssembly;

class DiagPanel : public QWidget
{
    Q_OBJECT
public:
    explicit DiagPanel(DroneAssembly *assembly, QWidget *parent = nullptr);
    void refreshUI();

private:
    DroneAssembly *m_assembly;

    // Mass breakdown (moved from BuildPanel)
    QLabel *m_massFrame;
    QLabel *m_massMotors;
    QLabel *m_massBattery;
    QLabel *m_massOther;
    QLabel *m_massTotal;
    QLabel *m_payloadRemaining;

    // Thrust dynamics
    QLabel *m_hoverThrottle;
    QLabel *m_thrustMargin;
    QLabel *m_maxThrust;
    QLabel *m_stabIndex;

    QTextEdit *m_console;
};
