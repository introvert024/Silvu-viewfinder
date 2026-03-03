#pragma once
#include <QWidget>
#include <QLabel>
#include <QProgressBar>

class DroneAssembly;

class TelemetryBar : public QWidget
{
    Q_OBJECT
public:
    explicit TelemetryBar(DroneAssembly *assembly, QWidget *parent = nullptr);
    void refreshUI();

private:
    DroneAssembly *m_assembly;
    QLabel *m_voltageVal;
    QProgressBar *m_voltageBar;
    QLabel *m_thrustVal;
    QProgressBar *m_thrustBar;
    QLabel *m_massVal;
    QProgressBar *m_massBar;
    // Inertia matrix cells
    QLabel *m_inertia[3][3];
};
