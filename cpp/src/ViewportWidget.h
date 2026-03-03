#pragma once
#include <QOpenGLWidget>
#include <QOpenGLFunctions>
#include <QMouseEvent>
#include <QDragEnterEvent>
#include <QDropEvent>
#include <QTimer>
#include <QToolButton>
#include <QVBoxLayout>
#include <QLabel>
#include <QMenu>

class DroneAssembly;

class ViewportWidget : public QOpenGLWidget, protected QOpenGLFunctions
{
    Q_OBJECT

public:
    explicit ViewportWidget(QWidget *parent = nullptr);
    void setAssembly(DroneAssembly *assembly) { m_assembly = assembly; }
    void refreshView() { update(); }
    void setCameraAngle(float rx, float ry) { m_rotX = rx; m_rotY = ry; m_autoRotate = false; update(); }

signals:
    void componentDropped();

protected:
    void initializeGL() override;
    void resizeGL(int w, int h) override;
    void paintGL() override;

    void mousePressEvent(QMouseEvent *event) override;
    void mouseMoveEvent(QMouseEvent *event) override;
    void wheelEvent(QWheelEvent *event) override;
    void contextMenuEvent(QContextMenuEvent *event) override;

    void dragEnterEvent(QDragEnterEvent *event) override;
    void dragMoveEvent(QDragMoveEvent *event) override;
    void dropEvent(QDropEvent *event) override;

private:
    void drawGrid();
    void drawDroneFrame();
    void drawMotorPod(float x, float y, float z, bool hasMotor);
    void drawBatteryBox(float x, float y, float z);
    void drawCGMarker(float cgX, float cgY, float cgZ);
    void drawSnapIndicators();
    void createHUDOverlay();

    float m_rotX = 30.0f;
    float m_rotY = 45.0f;
    float m_zoom = 8.0f;
    QPoint m_lastMousePos;

    float m_saved3D_rotX = 30.0f;
    float m_saved3D_rotY = 45.0f;

    bool m_autoRotate = false;
    bool m_orthographic = false;
    bool m_isDragging = false;
    bool m_showGrid = true;
    QTimer *m_rotateTimer = nullptr;

    QToolButton *m_rotateBtn = nullptr;
    QToolButton *m_viewBtn = nullptr;
    QLabel *m_statusLabel = nullptr;

    DroneAssembly *m_assembly = nullptr;
};
