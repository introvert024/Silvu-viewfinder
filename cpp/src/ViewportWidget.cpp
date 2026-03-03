#include "ViewportWidget.h"
#include "data/DroneAssembly.h"
#include "data/DroneComponent.h"
#include "data/ComponentRegistry.h"
#define _USE_MATH_DEFINES
#include <QOpenGLFunctions>
#include <QtMath>
#include <QPainter>
#include <QMimeData>
#include <QHBoxLayout>

#ifdef _WIN32
#include <windows.h>
#endif
#include <GL/gl.h>

// -- Helpers --
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

// ============================================================
//  Constructor
// ============================================================
ViewportWidget::ViewportWidget(QWidget *parent)
    : QOpenGLWidget(parent)
{
    setMinimumSize(400, 300);
    setFocusPolicy(Qt::StrongFocus);
    setAcceptDrops(true); // Enable drag-and-drop

    m_rotateTimer = new QTimer(this);
    connect(m_rotateTimer, &QTimer::timeout, this, [this]() {
        if (m_autoRotate && !m_orthographic) {
            m_rotY += 0.5f;
            update();
        }
    });
    m_rotateTimer->start(33); // ~30fps for auto-rotate, saves CPU/GPU

    createHUDOverlay();
}

// ============================================================
//  HUD Overlay — small pill labels, no huge boxes
// ============================================================
void ViewportWidget::createHUDOverlay()
{
    auto *hudLayout = new QVBoxLayout(this);
    hudLayout->setContentsMargins(12, 12, 12, 12);

    // Top bar: status pills + buttons
    auto *topBarLayout = new QHBoxLayout();
    topBarLayout->setSpacing(6);

    auto makePill = [this](const QString &text) {
        auto *l = new QLabel(text, this);
        l->setFixedHeight(24);
        l->setSizePolicy(QSizePolicy::Maximum, QSizePolicy::Fixed);
        l->setAttribute(Qt::WA_TransparentForMouseEvents);
        l->setStyleSheet(
            "background: rgba(15,22,25,0.85); color: #94a3b8; font-size: 9px;"
            "font-weight: bold; padding: 3px 10px; border: 1px solid #1e2d33;"
            "border-radius: 4px; letter-spacing: 1px;"
        );
        return l;
    };

    topBarLayout->addWidget(makePill("● PHYSICS ACTIVE"));
    topBarLayout->addWidget(makePill("LOD 0"));

    m_statusLabel = makePill("EMPTY");
    topBarLayout->addWidget(m_statusLabel);

    topBarLayout->addStretch();

    // View mode buttons
    m_rotateBtn = new QToolButton(this);
    m_rotateBtn->setText("↻");
    m_rotateBtn->setToolTip("Auto Rotate");
    m_rotateBtn->setCheckable(true);
    m_rotateBtn->setChecked(true);
    m_rotateBtn->setFixedSize(32, 32);
    m_rotateBtn->setStyleSheet(
        "QToolButton { background: rgba(15,22,25,0.85); color: #94a3b8; border: 1px solid #1e2d33;"
        "border-radius: 4px; font-size: 14px; }"
        "QToolButton:checked { color: #e61414; border-color: #e61414; }"
    );
    connect(m_rotateBtn, &QToolButton::toggled, this, [this](bool checked) {
        m_autoRotate = checked;
    });

    m_viewBtn = new QToolButton(this);
    m_viewBtn->setText("3D");
    m_viewBtn->setToolTip("Toggle 2D/3D");
    m_viewBtn->setCheckable(true);
    m_viewBtn->setFixedSize(32, 32);
    m_viewBtn->setStyleSheet(
        "QToolButton { background: rgba(15,22,25,0.85); color: #94a3b8; border: 1px solid #1e2d33;"
        "border-radius: 4px; font-size: 11px; font-weight: bold; }"
        "QToolButton:checked { color: #e61414; border-color: #e61414; }"
    );
    connect(m_viewBtn, &QToolButton::toggled, this, [this](bool checked) {
        m_orthographic = checked;
        m_viewBtn->setText(checked ? "2D" : "3D");
        if (checked) {
            // Switch to 2D: save current 3D angles, snap to top-down
            m_saved3D_rotX = m_rotX;
            m_saved3D_rotY = m_rotY;
            m_rotX = 90.0f; // look straight down
            m_rotY = 0.0f;
            m_autoRotate = false;
            m_rotateBtn->setChecked(false);
        } else {
            // Switch back to 3D: restore saved angles
            m_rotX = m_saved3D_rotX;
            m_rotY = m_saved3D_rotY;
        }
        update();
    });

    topBarLayout->addWidget(m_rotateBtn);
    topBarLayout->addWidget(m_viewBtn);

    hudLayout->addLayout(topBarLayout);
    hudLayout->addStretch();

    // Bottom: axis legend
    auto *axisRow = new QHBoxLayout();
    axisRow->setSpacing(12);

    auto makeAxisPill = [this](const QString &text, const QString &color) {
        auto *l = new QLabel(text, this);
        l->setFixedHeight(16);
        l->setSizePolicy(QSizePolicy::Maximum, QSizePolicy::Fixed);
        l->setAttribute(Qt::WA_TransparentForMouseEvents);
        l->setStyleSheet(QString("color: %1; font-size: 9px; font-weight: bold; letter-spacing: 1px; background: transparent;").arg(color));
        return l;
    };

    axisRow->addWidget(makeAxisPill("━ X", "#f04242"));
    axisRow->addWidget(makeAxisPill("━ Y", "#10b981"));
    axisRow->addWidget(makeAxisPill("━ Z", "#94a3b8"));
    axisRow->addStretch();

    hudLayout->addLayout(axisRow);
}

// ============================================================
//  OpenGL Setup
// ============================================================
void ViewportWidget::initializeGL()
{
    initializeOpenGLFunctions();
    glClearColor(0.063f, 0.114f, 0.133f, 1.0f);
    glEnable(GL_DEPTH_TEST);
    glEnable(GL_LINE_SMOOTH);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
}

void ViewportWidget::resizeGL(int w, int h)
{
    glViewport(0, 0, w, h);
}

// ============================================================
//  Main Render
// ============================================================
void ViewportWidget::paintGL()
{
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

    glMatrixMode(GL_PROJECTION);
    glLoadIdentity();
    float aspect = float(width()) / float(height());
    if (m_orthographic) {
        // True top-down orthographic
        float s = m_zoom * 0.5f;
        glOrtho(-s * aspect, s * aspect, -s, s, -50.0, 50.0);
    } else {
        myPerspective(50.0, aspect, 0.1, 100.0);
    }

    glMatrixMode(GL_MODELVIEW);
    glLoadIdentity();

    if (m_orthographic) {
        // 2D: top-down, no translation on Z needed for ortho
        glRotatef(90.0f, 1, 0, 0); // look straight down
    } else {
        glTranslatef(0, 0, -m_zoom);
        glRotatef(m_rotX, 1, 0, 0);
        glRotatef(m_rotY, 0, 1, 0);
    }

    drawGrid();

    bool hasFrame = m_assembly && m_assembly->getFrame();

    // Update status label
    if (m_statusLabel) {
        if (m_isDragging) {
            m_statusLabel->setText("DROP HERE");
            m_statusLabel->setStyleSheet(
                "background: rgba(230,20,20,0.2); color: #e61414; font-size: 9px;"
                "font-weight: bold; padding: 3px 10px; border: 1px solid #e61414;"
                "border-radius: 4px; letter-spacing: 1px;"
            );
        } else if (hasFrame) {
            int compCount = 0;
            for (const auto &n : m_assembly->getSnapNodes())
                if (n.attachedComponent) compCount++;
            m_statusLabel->setText(QString("COMPONENTS: %1").arg(compCount + 1)); // +1 for frame
            m_statusLabel->setStyleSheet(
                "background: rgba(15,22,25,0.85); color: #10b981; font-size: 9px;"
                "font-weight: bold; padding: 3px 10px; border: 1px solid #1e2d33;"
                "border-radius: 4px; letter-spacing: 1px;"
            );
        } else {
            m_statusLabel->setText("EMPTY");
            m_statusLabel->setStyleSheet(
                "background: rgba(15,22,25,0.85); color: #94a3b8; font-size: 9px;"
                "font-weight: bold; padding: 3px 10px; border: 1px solid #1e2d33;"
                "border-radius: 4px; letter-spacing: 1px;"
            );
        }
    }

    if (hasFrame) {
        drawDroneFrame();
        drawSnapIndicators();
        auto cg = m_assembly->getCenterOfGravity();
        drawCGMarker(cg.x, cg.y, cg.z);
    }

    // Draw drag-drop indicator border
    if (m_isDragging) {
        // Draw a pulsing border quad at edges
        glMatrixMode(GL_PROJECTION);
        glPushMatrix();
        glLoadIdentity();
        glOrtho(0, width(), height(), 0, -1, 1);
        glMatrixMode(GL_MODELVIEW);
        glPushMatrix();
        glLoadIdentity();
        glDisable(GL_DEPTH_TEST);

        glColor4f(0.902f, 0.078f, 0.078f, 0.4f);
        glLineWidth(3.0f);
        float bw = 4.0f;
        glBegin(GL_LINE_LOOP);
        glVertex2f(bw, bw);
        glVertex2f(width() - bw, bw);
        glVertex2f(width() - bw, height() - bw);
        glVertex2f(bw, height() - bw);
        glEnd();

        glEnable(GL_DEPTH_TEST);
        glMatrixMode(GL_PROJECTION);
        glPopMatrix();
        glMatrixMode(GL_MODELVIEW);
        glPopMatrix();
    }
}

// ============================================================
//  Grid
// ============================================================
void ViewportWidget::drawGrid()
{
    if (!m_showGrid) return;

    float gridSize = 7.5f;
    int gridLines = 15;
    float step = gridSize * 2.0f / gridLines;

    glLineWidth(1.0f);

    for (int i = 0; i <= gridLines; ++i) {
        float pos = -gridSize + i * step;
        if (i == gridLines / 2) {
            glColor4f(0.94f, 0.26f, 0.26f, 0.4f);
        } else {
            glColor4f(0.118f, 0.176f, 0.200f, 0.4f);
        }
        glBegin(GL_LINES);
        glVertex3f(pos, -1.0f, -gridSize);
        glVertex3f(pos, -1.0f, gridSize);
        glVertex3f(-gridSize, -1.0f, pos);
        glVertex3f(gridSize, -1.0f, pos);
        glEnd();
    }
}

// ============================================================
//  Drone Frame Rendering
// ============================================================
void ViewportWidget::drawDroneFrame()
{
    if (!m_assembly || !m_assembly->getFrame()) return;

    float armLen = 2.75f;
    float armW = 0.25f;
    float armH = 0.075f;

    // Arms — draw as 3D boxes
    auto drawArm = [&](float x1, float z1, float x2, float z2) {
        float dx = x2 - x1, dz = z2 - z1;
        float len = sqrtf(dx*dx + dz*dz);
        if (len < 0.001f) return;
        float nx = -dz / len * armW, nz = dx / len * armW;

        glColor4f(0.16f, 0.22f, 0.26f, 0.95f);
        glBegin(GL_QUADS);
        // Top
        glVertex3f(x1 + nx, armH, z1 + nz); glVertex3f(x2 + nx, armH, z2 + nz);
        glVertex3f(x2 - nx, armH, z2 - nz); glVertex3f(x1 - nx, armH, z1 - nz);
        // Bottom
        glVertex3f(x1 + nx, -armH, z1 + nz); glVertex3f(x1 - nx, -armH, z1 - nz);
        glVertex3f(x2 - nx, -armH, z2 - nz); glVertex3f(x2 + nx, -armH, z2 + nz);
        // Side 1
        glVertex3f(x1 + nx, -armH, z1 + nz); glVertex3f(x2 + nx, -armH, z2 + nz);
        glVertex3f(x2 + nx, armH, z2 + nz);  glVertex3f(x1 + nx, armH, z1 + nz);
        // Side 2
        glVertex3f(x1 - nx, -armH, z1 - nz); glVertex3f(x1 - nx, armH, z1 - nz);
        glVertex3f(x2 - nx, armH, z2 - nz);  glVertex3f(x2 - nx, -armH, z2 - nz);
        glEnd();

        // Edge highlight
        glColor4f(0.22f, 0.30f, 0.35f, 0.6f);
        glLineWidth(1.0f);
        glBegin(GL_LINE_LOOP);
        glVertex3f(x1 + nx, armH, z1 + nz); glVertex3f(x2 + nx, armH, z2 + nz);
        glVertex3f(x2 - nx, armH, z2 - nz); glVertex3f(x1 - nx, armH, z1 - nz);
        glEnd();
    };

    // 4-arm X configuration (diagonal arms)
    float d = armLen * 0.707f; // cos(45)
    drawArm(-d, -d, d, d);    // diagonal 1
    drawArm(-d, d, d, -d);    // diagonal 2

    // Center hub
    float hubRadius = 0.65f;
    int segments = 32;
    glColor4f(0.12f, 0.16f, 0.19f, 1.0f);
    glBegin(GL_TRIANGLE_FAN);
    glVertex3f(0, 0.1f, 0);
    for (int i = 0; i <= segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(hubRadius * cosf(angle), 0.1f, hubRadius * sinf(angle));
    }
    glEnd();
    // Hub ring
    glColor4f(0.25f, 0.32f, 0.38f, 0.6f);
    glLineWidth(1.5f);
    glBegin(GL_LINE_LOOP);
    for (int i = 0; i < segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(hubRadius * cosf(angle), 0.11f, hubRadius * sinf(angle));
    }
    glEnd();

    // Draw motor pods at arm tips
    const auto &nodes = m_assembly->getSnapNodes();
    struct PodPos { float x, y, z; };
    PodPos motorPositions[] = {
        { d, 0.2f,  d},   // FR
        {-d, 0.2f,  d},   // FL
        {-d, 0.2f, -d},   // BL
        { d, 0.2f, -d}    // BR
    };

    int motorIdx = 0;
    for (const auto &node : nodes) {
        if (node.acceptedType == ComponentType::Motor && motorIdx < 4) {
            bool hasMotor = node.attachedComponent != nullptr;
            drawMotorPod(motorPositions[motorIdx].x, motorPositions[motorIdx].y, motorPositions[motorIdx].z, hasMotor);
            motorIdx++;
        }
    }
    for (; motorIdx < 4; motorIdx++) {
        drawMotorPod(motorPositions[motorIdx].x, motorPositions[motorIdx].y, motorPositions[motorIdx].z, false);
    }

    // Draw battery if attached
    for (const auto &node : nodes) {
        if (node.acceptedType == ComponentType::Battery && node.attachedComponent) {
            drawBatteryBox(0, -0.15f, 0);
        }
    }
}

// ============================================================
//  Motor Pod — much more visible
// ============================================================
void ViewportWidget::drawMotorPod(float x, float y, float z, bool hasMotor)
{
    float podR = 0.45f;
    int segments = 24;

    if (hasMotor) {
        // Solid motor housing
        glColor4f(0.35f, 0.42f, 0.48f, 1.0f);
        glBegin(GL_TRIANGLE_FAN);
        glVertex3f(x, y + 0.05f, z);
        for (int i = 0; i <= segments; ++i) {
            float angle = 2.0f * M_PI * i / segments;
            glVertex3f(x + podR * cosf(angle), y + 0.05f, z + podR * sinf(angle));
        }
        glEnd();

        // Motor bell (smaller circle on top)
        float bellR = 0.2f;
        glColor4f(0.45f, 0.52f, 0.58f, 1.0f);
        glBegin(GL_TRIANGLE_FAN);
        glVertex3f(x, y + 0.12f, z);
        for (int i = 0; i <= segments; ++i) {
            float angle = 2.0f * M_PI * i / segments;
            glVertex3f(x + bellR * cosf(angle), y + 0.12f, z + bellR * sinf(angle));
        }
        glEnd();

        // Ring highlight
        glColor4f(0.902f, 0.078f, 0.078f, 0.7f);
        glLineWidth(2.0f);
        glBegin(GL_LINE_LOOP);
        for (int i = 0; i < segments; ++i) {
            float angle = 2.0f * M_PI * i / segments;
            glVertex3f(x + podR * cosf(angle), y + 0.06f, z + podR * sinf(angle));
        }
        glEnd();

        // Thrust arrow
        glColor4f(0.902f, 0.078f, 0.078f, 0.85f);
        glLineWidth(2.5f);
        glBegin(GL_LINES);
        glVertex3f(x, y + 0.15f, z);
        glVertex3f(x, y + 1.1f, z);
        glEnd();
        // Arrow head
        glBegin(GL_TRIANGLES);
        glVertex3f(x, y + 1.3f, z);
        glVertex3f(x - 0.12f, y + 1.0f, z);
        glVertex3f(x + 0.12f, y + 1.0f, z);
        // Side view head
        glVertex3f(x, y + 1.3f, z);
        glVertex3f(x, y + 1.0f, z - 0.12f);
        glVertex3f(x, y + 1.0f, z + 0.12f);
        glEnd();
    } else {
        // Empty mount — dashed ring only
        glColor4f(0.25f, 0.32f, 0.38f, 0.3f);
        glLineWidth(1.5f);
        glBegin(GL_LINES);
        for (int i = 0; i < segments; i += 2) {
            float a1 = 2.0f * M_PI * i / segments;
            float a2 = 2.0f * M_PI * (i + 1) / segments;
            glVertex3f(x + podR * cosf(a1), y, z + podR * sinf(a1));
            glVertex3f(x + podR * cosf(a2), y, z + podR * sinf(a2));
        }
        glEnd();

        // Small plus marker in center
        glColor4f(0.25f, 0.32f, 0.38f, 0.4f);
        float sz = 0.1f;
        glBegin(GL_LINES);
        glVertex3f(x - sz, y, z); glVertex3f(x + sz, y, z);
        glVertex3f(x, y, z - sz); glVertex3f(x, y, z + sz);
        glEnd();
    }
}

// ============================================================
//  Battery Box
// ============================================================
void ViewportWidget::drawBatteryBox(float x, float y, float z)
{
    float bw = 0.4f, bh = 0.15f, bd = 0.8f;

    glColor4f(0.15f, 0.55f, 0.15f, 0.8f); // green tint for battery
    glBegin(GL_QUADS);
    // Top
    glVertex3f(x-bw, y+bh, z-bd); glVertex3f(x+bw, y+bh, z-bd);
    glVertex3f(x+bw, y+bh, z+bd); glVertex3f(x-bw, y+bh, z+bd);
    // Bottom
    glVertex3f(x-bw, y-bh, z-bd); glVertex3f(x-bw, y-bh, z+bd);
    glVertex3f(x+bw, y-bh, z+bd); glVertex3f(x+bw, y-bh, z-bd);
    // Front
    glVertex3f(x-bw, y-bh, z+bd); glVertex3f(x+bw, y-bh, z+bd);
    glVertex3f(x+bw, y+bh, z+bd); glVertex3f(x-bw, y+bh, z+bd);
    // Back
    glVertex3f(x-bw, y-bh, z-bd); glVertex3f(x-bw, y+bh, z-bd);
    glVertex3f(x+bw, y+bh, z-bd); glVertex3f(x+bw, y-bh, z-bd);
    // Left
    glVertex3f(x-bw, y-bh, z-bd); glVertex3f(x-bw, y-bh, z+bd);
    glVertex3f(x-bw, y+bh, z+bd); glVertex3f(x-bw, y+bh, z-bd);
    // Right
    glVertex3f(x+bw, y-bh, z-bd); glVertex3f(x+bw, y+bh, z-bd);
    glVertex3f(x+bw, y+bh, z+bd); glVertex3f(x+bw, y-bh, z+bd);
    glEnd();

    // Wire outline
    glColor4f(0.2f, 0.7f, 0.2f, 0.6f);
    glLineWidth(1.5f);
    glBegin(GL_LINE_LOOP);
    glVertex3f(x-bw, y+bh+0.01f, z-bd); glVertex3f(x+bw, y+bh+0.01f, z-bd);
    glVertex3f(x+bw, y+bh+0.01f, z+bd); glVertex3f(x-bw, y+bh+0.01f, z+bd);
    glEnd();
}

// ============================================================
//  CG Marker
// ============================================================
void ViewportWidget::drawCGMarker(float cgX, float cgY, float cgZ)
{
    float r = 0.08f;
    int segments = 16;

    glColor4f(0.902f, 0.078f, 0.078f, 0.9f);
    glBegin(GL_TRIANGLE_FAN);
    glVertex3f(cgX, 0.25f, cgZ);
    for (int i = 0; i <= segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(cgX + r * cosf(angle), 0.25f, cgZ + r * sinf(angle));
    }
    glEnd();

    glColor4f(0.902f, 0.078f, 0.078f, 0.25f);
    glLineWidth(1.5f);
    float outerR = 0.35f;
    glBegin(GL_LINE_LOOP);
    for (int i = 0; i < segments; ++i) {
        float angle = 2.0f * M_PI * i / segments;
        glVertex3f(cgX + outerR * cosf(angle), 0.25f, cgZ + outerR * sinf(angle));
    }
    glEnd();

    // Cross-hairs
    glColor4f(0.902f, 0.078f, 0.078f, 0.2f);
    glBegin(GL_LINES);
    glVertex3f(cgX - 0.5f, 0.25f, cgZ); glVertex3f(cgX + 0.5f, 0.25f, cgZ);
    glVertex3f(cgX, 0.25f, cgZ - 0.5f); glVertex3f(cgX, 0.25f, cgZ + 0.5f);
    glEnd();
}

// ============================================================
//  Snap node indicators (show open slots)
// ============================================================
void ViewportWidget::drawSnapIndicators()
{
    if (!m_assembly) return;
    const auto &nodes = m_assembly->getSnapNodes();
    for (const auto &node : nodes) {
        if (!node.attachedComponent) {
            // Show a small pulsing indicator at the snap position
            float x = node.localPosition.x;
            float z = node.localPosition.z;
            float y = 0.2f;

            // Small rotating diamond
            glColor4f(0.35f, 0.65f, 0.9f, 0.4f);
            glLineWidth(1.0f);
            float sz = 0.15f;
            glBegin(GL_LINE_LOOP);
            glVertex3f(x, y, z - sz);
            glVertex3f(x + sz, y, z);
            glVertex3f(x, y, z + sz);
            glVertex3f(x - sz, y, z);
            glEnd();
        }
    }
}

// ============================================================
//  Mouse Events
// ============================================================
void ViewportWidget::mousePressEvent(QMouseEvent *event)
{
    m_lastMousePos = event->pos();
    if (event->button() == Qt::LeftButton && !m_orthographic) {
        m_autoRotate = false;
        m_rotateBtn->setChecked(false);
    }
}

void ViewportWidget::mouseMoveEvent(QMouseEvent *event)
{
    int dx = event->pos().x() - m_lastMousePos.x();
    int dy = event->pos().y() - m_lastMousePos.y();

    if (event->buttons() & Qt::LeftButton) {
        if (m_orthographic) {
            // In 2D mode, don't allow rotation — could add pan here later
        } else {
            m_rotY += dx * 0.5f;
            m_rotX += dy * 0.5f;
            m_rotX = qBound(-89.0f, m_rotX, 89.0f);
        }
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

// ============================================================
//  Drag-and-Drop from BuildPanel
// ============================================================
void ViewportWidget::dragEnterEvent(QDragEnterEvent *event)
{
    if (event->mimeData()->hasText()) {
        event->acceptProposedAction();
        m_isDragging = true;
        update();
    }
}

void ViewportWidget::dragMoveEvent(QDragMoveEvent *event)
{
    event->acceptProposedAction();
}

void ViewportWidget::dropEvent(QDropEvent *event)
{
    m_isDragging = false;
    if (!m_assembly) return;

    QString compId = event->mimeData()->text();
    auto comp = ComponentRegistry::getInstance().getComponent(compId.toStdString());
    if (!comp) return;

    if (comp->getType() == ComponentType::Frame) {
        m_assembly->setFrame(comp);
        emit componentDropped();
        update();
        return;
    }

    const auto &nodes = m_assembly->getSnapNodes();
    for (const auto &node : nodes) {
        if (node.acceptedType == comp->getType() && !node.attachedComponent) {
            m_assembly->attachComponent(node.id, comp);
            emit componentDropped();
            update();
            return;
        }
    }
    update();
}

// ============================================================
//  Right-Click Context Menu
// ============================================================
void ViewportWidget::contextMenuEvent(QContextMenuEvent *event)
{
    QMenu menu(this);
    menu.setStyleSheet(
        "QMenu { background: #0f1619; border: 1px solid #1e2d33; padding: 4px; }"
        "QMenu::item { color: #cbd5e1; padding: 6px 20px; font-size: 11px; }"
        "QMenu::item:selected { background: rgba(230,20,20,0.2); color: #e61414; }"
        "QMenu::separator { height: 1px; background: #1e2d33; margin: 4px 8px; }"
    );

    menu.addAction("Reset Camera", this, [this]() {
        m_rotX = 30.0f; m_rotY = 45.0f; m_zoom = 8.0f;
        m_autoRotate = true;
        m_rotateBtn->setChecked(true);
        if (m_orthographic) { m_orthographic = false; m_viewBtn->setChecked(false); }
        update();
    });

    menu.addAction(m_showGrid ? "Hide Grid" : "Show Grid", this, [this]() {
        m_showGrid = !m_showGrid;
        update();
    });

    menu.addAction(m_orthographic ? "Switch to 3D" : "Switch to 2D", this, [this]() {
        m_viewBtn->setChecked(!m_orthographic);
    });

    menu.addAction(m_autoRotate ? "Stop Rotation" : "Auto Rotate", this, [this]() {
        m_autoRotate = !m_autoRotate;
        m_rotateBtn->setChecked(m_autoRotate);
    });

    menu.addSeparator();

    // Assembly info
    if (m_assembly && m_assembly->getFrame()) {
        int compCount = 1; // frame
        for (const auto &n : m_assembly->getSnapNodes())
            if (n.attachedComponent) compCount++;
        float mass = m_assembly->getTotalMass();
        float twr = m_assembly->getThrustToWeightRatio();
        menu.addAction(QString("Assembly: %1 parts, %2g, T/W %3:1")
            .arg(compCount).arg(mass, 0, 'f', 0).arg(twr, 0, 'f', 1))->setEnabled(false);
    } else {
        menu.addAction("No assembly loaded")->setEnabled(false);
    }

    menu.exec(event->globalPos());
}
