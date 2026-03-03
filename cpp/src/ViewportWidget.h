#pragma once
#include <QOpenGLWidget>
#include <QOpenGLFunctions>
#include <QMouseEvent>
#include <QTimer>
#include <QToolButton>
#include <QVBoxLayout>

class ViewportWidget : public QOpenGLWidget, protected QOpenGLFunctions
{
    Q_OBJECT

public:
    explicit ViewportWidget(QWidget *parent = nullptr);

protected:
    void initializeGL() override;
    void resizeGL(int w, int h) override;
    void paintGL() override;

    void mousePressEvent(QMouseEvent *event) override;
    void mouseMoveEvent(QMouseEvent *event) override;
    void wheelEvent(QWheelEvent *event) override;

private:
    void drawGrid();
    void drawDroneFrame();
    void drawMotorPod(float x, float y, float z);
    void drawCGMarker();
    void createHUDOverlay();

    // Camera controls
    float m_rotX = 30.0f;
    float m_rotY = 45.0f;
    float m_zoom = 8.0f;
    QPoint m_lastMousePos;

    // State
    bool m_autoRotate = true;
    bool m_orthographic = false;
    QTimer *m_rotateTimer = nullptr;

    // HUD buttons
    QToolButton *m_rotateBtn = nullptr;
    QToolButton *m_viewBtn = nullptr;
};
