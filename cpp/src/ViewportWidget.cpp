#include "ViewportWidget.h"
#define _USE_MATH_DEFINES
#include <QOpenGLFunctions>
#include <QtMath>
#include <QPainter>
#include <QLabel>

#ifdef _WIN32
#include <windows.h>
#endif
#include <GL/gl.h>

// Manual gluPerspective replacement to avoid linking GLU
static void myPerspective(double fovY, double aspect, double zNear, double zFar)
{
    double f = 1.0 / tan(fovY * M_PI / 360.0);
    double nf = 1.0 / (zNear - zFar);
    double m[16] = {0};
    m[0]  = f / aspect;
    m[5]  = f;
    m[10] = (zFar + zNear) * nf;
    m[11] = -1.0;
    m[14] = 2.0 * zFar * zNear * nf;
    glMultMatrixd(m);
}

ViewportWidget::ViewportWidget(QWidget *parent)
    : QOpenGLWidget(parent)
{
    setMinimumSize(400, 300);
    setFocusPolicy(Qt::StrongFocus);

    // Auto-rotate timer
    m_rotateTimer = new QTimer(this);
    connect(m_rotateTimer, &QTimer::timeout, this, [this]() {
        if (m_autoRotate) {
            m_rotY += 0.3f;
            update();
        }
    });
    m_rotateTimer->start(16); // ~60fps

    createHUDOverlay();
}

void ViewportWidget::createHUDOverlay()
{
    // HUD labels
    auto *hudLayout = new QVBoxLayout(this);
    hudLayout->setContentsMargins(16, 16, 16, 16);

    // Top left: status badges
    auto *topRow = new QHBoxLayout();
    auto *physicsLabel = new QLabel("● PHYSICS ENGINE ACTIVE");
    physicsLabel->setStyleSheet(
        "background: rgba(22,34,40,0.8); color: #cbd5e1; font-size: 10px;"
        "font-weight: bold; padding: 4px 10px; border: 1px solid #1e2d33; border-radius: 4px;"
    );
    auto *lodLabel = new QLabel("MODEL: LOD 0");
    lodLabel->setStyleSheet(
        "background: rgba(22,34,40,0.8); color: #cbd5e1; font-size: 10px;"
        "font-weight: bold; padding: 4px 10px; border: 1px solid #1e2d33; border-radius: 4px;"
    );
    topRow->addWidget(physicsLabel);
    topRow->addWidget(lodLabel);
    topRow->addStretch();

    // Top right: viewport buttons
    auto *btnCol = new QVBoxLayout();
    
    m_rotateBtn = new QToolButton();
    m_rotateBtn->setText("↻");
    m_rotateBtn->setToolTip("Toggle Auto Rotation");
    m_rotateBtn->setCheckable(true);
    m_rotateBtn->setChecked(true);
    m_rotateBtn->setFixedSize(36, 36);
    connect(m_rotateBtn, &QToolButton::toggled, this, [this](bool checked) {
        m_autoRotate = checked;
    });

    m_viewBtn = new QToolButton();
    m_viewBtn->setText("3D");
    m_viewBtn->setToolTip("Toggle 2D/3D View");
    m_viewBtn->setCheckable(true);
    m_viewBtn->setFixedSize(36, 36);
    connect(m_viewBtn, &QToolButton::toggled, this, [this](bool checked) {
        m_orthographic = checked;
        m_viewBtn->setText(checked ? "2D" : "3D");
        update();
    });

    btnCol->addWidget(m_rotateBtn);
    btnCol->addWidget(m_viewBtn);
    btnCol->addStretch();

    auto *topBarLayout = new QHBoxLayout();
    topBarLayout->addLayout(topRow);
    topBarLayout->addLayout(btnCol);

    hudLayout->addLayout(topBarLayout);
    hudLayout->addStretch();

    // Bottom left: axis legend
    auto *axisWidget = new QWidget();
    axisWidget->setStyleSheet("background: transparent;");
    auto *axisLayout = new QVBoxLayout(axisWidget);
    axisLayout->setContentsMargins(0, 0, 0, 0);
    axisLayout->setSpacing(4);

    auto makeAxisLabel = [](const QString &text, const QString &color) {
        auto *l = new QLabel(text);
        l->setStyleSheet(QString("color: %1; font-size: 9px; font-weight: bold; letter-spacing: 2px; background: transparent;").arg(color));
        return l;
    };
    axisLayout->addWidget(makeAxisLabel("━━━  X (ROLL)", "#f04242"));
    axisLayout->addWidget(makeAxisLabel("━━━  Y (PITCH)", "#10b981"));
    axisLayout->addWidget(makeAxisLabel("━━━  Z (YAW)", "#94a3b8"));

    hudLayout->addWidget(axisWidget, 0, Qt::AlignLeft | Qt::AlignBottom);
}

void ViewportWidget::initializeGL()
{
    initializeOpenGLFunctions();
    glClearColor(0.063f, 0.114f, 0.133f, 1.0f); // #101d22
    glEnable(GL_DEPTH_TEST);
    glEnable(GL_LINE_SMOOTH);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
}

void ViewportWidget::resizeGL(int w, int h)
{
    glViewport(0, 0, w, h);
}

void ViewportWidget::paintGL()
{
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

    // Set up projection
    glMatrixMode(GL_PROJECTION);
    glLoadIdentity();
    float aspect = float(width()) / float(height());
    if (m_orthographic) {
        float s = m_zoom * 0.5f;
        glOrtho(-s * aspect, s * aspect, -s, s, 0.1, 100.0);
    } else {
        myPerspective(50.0, aspect, 0.1, 100.0);
    }

    // Set up camera (modelview)
    glMatrixMode(GL_MODELVIEW);
    glLoadIdentity();
    glTranslatef(0, 0, -m_zoom);
    glRotatef(m_rotX, 1, 0, 0);
    glRotatef(m_rotY, 0, 1, 0);

    drawGrid();
    drawDroneFrame();
    drawCGMarker();
}

void ViewportWidget::drawGrid()
{
    float gridSize = 7.5f;
    int gridLines = 15;
    float step = gridSize * 2.0f / gridLines;

    glLineWidth(1.0f);

    for (int i = 0; i <= gridLines; ++i) {
        float pos = -gridSize + i * step;
        
        // Main grid color or accent on center
        if (i == gridLines / 2) {
            glColor4f(0.94f, 0.26f, 0.26f, 0.6f); // #f04242 accent
        } else {
            glColor4f(0.118f, 0.176f, 0.200f, 0.5f); // #1e2d33 subtle
        }
        
        glBegin(GL_LINES);
        glVertex3f(pos, -1.0f, -gridSize);
        glVertex3f(pos, -1.0f, gridSize);
        glVertex3f(-gridSize, -1.0f, pos);
        glVertex3f(gridSize, -1.0f, pos);
        glEnd();
    }
}

void ViewportWidget::drawDroneFrame()
{
    float armLen = 2.75f;
    float armW = 0.25f;
    float armH = 0.075f;

    // Arm 1 (X axis)
    glColor3f(0.129f, 0.188f, 0.220f); // #213038
    glPushMatrix();
    glBegin(GL_QUADS);
    // Top
    glVertex3f(-armLen, armH, -armW);
    glVertex3f(armLen, armH, -armW);
    glVertex3f(armLen, armH, armW);
    glVertex3f(-armLen, armH, armW);
    // Bottom
    glVertex3f(-armLen, -armH, -armW);
    glVertex3f(armLen, -armH, -armW);
    glVertex3f(armLen, -armH, armW);
    glVertex3f(-armLen, -armH, armW);
    // Front
    glVertex3f(-armLen, -armH, armW);
    glVertex3f(armLen, -armH, armW);
    glVertex3f(armLen, armH, armW);
    glVertex3f(-armLen, armH, armW);
    // Back
    glVertex3f(-armLen, -armH, -armW);
    glVertex3f(armLen, -armH, -armW);
    glVertex3f(armLen, armH, -armW);
    glVertex3f(-armLen, armH, -armW);
    // Left
    glVertex3f(-armLen, -armH, -armW);
    glVertex3f(-armLen, -armH, armW);
    glVertex3f(-armLen, armH, armW);
    glVertex3f(-armLen, armH, -armW);
    // Right
    glVertex3f(armLen, -armH, -armW);
    glVertex3f(armLen, -armH, armW);
    glVertex3f(armLen, armH, armW);
    glVertex3f(armLen, armH, -armW);
    glEnd();
    glPopMatrix();

    // Arm 2 (Z axis)
    glBegin(GL_QUADS);
    glVertex3f(-armW, armH, -armLen);
    glVertex3f(armW, armH, -armLen);
    glVertex3f(armW, armH, armLen);
    glVertex3f(-armW, armH, armLen);

    glVertex3f(-armW, -armH, -armLen);
    glVertex3f(armW, -armH, -armLen);
    glVertex3f(armW, -armH, armLen);
    glVertex3f(-armW, -armH, armLen);

    glVertex3f(-armW, -armH, armLen);
    glVertex3f(armW, -armH, armLen);
    glVertex3f(armW, armH, armLen);
    glVertex3f(-armW, armH, armLen);

    glVertex3f(-armW, -armH, -armLen);
    glVertex3f(armW, -armH, -armLen);
    glVertex3f(armW, armH, -armLen);
    glVertex3f(-armW, armH, -armLen);
    glEnd();

    // Center hub (approximated as a flat disc via polygon)
    float hubRadius = 0.7f;
    int segments = 32;
    glColor3f(0.090f, 0.133f, 0.157f); // #172228

    glBegin(GL_TRIANGLE_FAN);
    glVertex3f(0, 0.15f, 0);
    for (int i = 0; i <= segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(hubRadius * cosf(angle), 0.15f, hubRadius * sinf(angle));
    }
    glEnd();

    // Motor pods
    drawMotorPod(-armLen, 0.3f, 0);
    drawMotorPod(armLen, 0.3f, 0);
    drawMotorPod(0, 0.3f, -armLen);
    drawMotorPod(0, 0.3f, armLen);
}

void ViewportWidget::drawMotorPod(float x, float y, float z)
{
    float podR = 0.4f;
    int segments = 20;

    // Motor housing circle
    glColor4f(0.294f, 0.369f, 0.420f, 0.9f); // #4B5E6B
    glBegin(GL_TRIANGLE_FAN);
    glVertex3f(x, y, z);
    for (int i = 0; i <= segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(x + podR * cosf(angle), y, z + podR * sinf(angle));
    }
    glEnd();

    // Ring outline
    glColor4f(0.227f, 0.290f, 0.333f, 0.8f); // #3A4A55
    glLineWidth(1.5f);
    glBegin(GL_LINE_LOOP);
    for (int i = 0; i < segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(x + podR * cosf(angle), y + 0.01f, z + podR * sinf(angle));
    }
    glEnd();

    // Thrust arrow (pointing up)
    glColor4f(0.075f, 0.714f, 0.925f, 0.9f); // #e61414 primary
    glLineWidth(2.5f);
    glBegin(GL_LINES);
    glVertex3f(x, y + 0.1f, z);
    glVertex3f(x, y + 1.0f, z);
    glEnd();
    // Arrow head
    glBegin(GL_TRIANGLES);
    glVertex3f(x, y + 1.2f, z);
    glVertex3f(x - 0.1f, y + 0.9f, z);
    glVertex3f(x + 0.1f, y + 0.9f, z);
    glEnd();
}

void ViewportWidget::drawCGMarker()
{
    // CG dot at origin
    float r = 0.08f;
    int segments = 16;

    // Cyan glow circle
    glColor4f(0.075f, 0.714f, 0.925f, 0.8f); // #e61414
    glBegin(GL_TRIANGLE_FAN);
    glVertex3f(0, 0.3f, 0);
    for (int i = 0; i <= segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(r * cosf(angle), 0.3f, r * sinf(angle));
    }
    glEnd();

    // Larger ring
    glColor4f(0.075f, 0.714f, 0.925f, 0.3f);
    glLineWidth(1.5f);
    glBegin(GL_LINE_LOOP);
    float outerR = 0.3f;
    for (int i = 0; i < segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(outerR * cosf(angle), 0.3f, outerR * sinf(angle));
    }
    glEnd();
}

void ViewportWidget::mousePressEvent(QMouseEvent *event)
{
    m_lastMousePos = event->pos();
    if (event->button() == Qt::LeftButton)
        m_autoRotate = false;
    if (m_rotateBtn)
        m_rotateBtn->setChecked(m_autoRotate);
}

void ViewportWidget::mouseMoveEvent(QMouseEvent *event)
{
    int dx = event->pos().x() - m_lastMousePos.x();
    int dy = event->pos().y() - m_lastMousePos.y();

    if (event->buttons() & Qt::LeftButton) {
        m_rotY += dx * 0.5f;
        m_rotX += dy * 0.5f;
        m_rotX = qBound(-89.0f, m_rotX, 89.0f);
        update();
    }

    m_lastMousePos = event->pos();
}

void ViewportWidget::wheelEvent(QWheelEvent *event)
{
    m_zoom -= event->angleDelta().y() / 120.0f * 0.5f;
    m_zoom = qBound(2.0f, m_zoom, 30.0f);
    update();
}
