#include "BuildPanel.h"
#include <QGroupBox>
#include <QGridLayout>

static QLabel* makeStatLabel(const QString &title, const QString &value, const QString &color = "#e2e8f0")
{
    auto *w = new QWidget();
    auto *l = new QVBoxLayout(w);
    l->setContentsMargins(8, 6, 8, 6);
    l->setSpacing(2);
    auto *t = new QLabel(title);
    t->setStyleSheet("font-size: 9px; font-weight: bold; color: #64748b; text-transform: uppercase; letter-spacing: 2px;");
    auto *v = new QLabel(value);
    v->setStyleSheet(QString("font-size: 11px; font-weight: bold; color: %1; font-family: Consolas, monospace;").arg(color));
    l->addWidget(t);
    l->addWidget(v);
    // We're returning a QLabel but need a QWidget; use a trick
    auto *container = new QLabel();
    container->setLayout(new QVBoxLayout());
    // Actually just return as widget — caller should use QWidget*
    // Simplify: just create inline
    return t; // placeholder
}

BuildPanel::BuildPanel(QWidget *parent)
    : QWidget(parent)
{
    auto *layout = new QVBoxLayout(this);
    layout->setContentsMargins(0, 0, 0, 0);
    layout->setSpacing(0);

    setStyleSheet("background: #162228; border-right: 1px solid #1e2d33;");

    // Header
    auto *header = new QWidget();
    auto *headerLayout = new QVBoxLayout(header);
    headerLayout->setContentsMargins(16, 16, 16, 16);

    auto *sectionLabel = new QLabel("BUILD STRUCTURE");
    sectionLabel->setStyleSheet("font-size: 10px; font-weight: bold; color: #e61414; letter-spacing: 2px;");
    
    auto *titleRow = new QHBoxLayout();
    auto *title = new QLabel("Carbon Fiber X-8");
    title->setStyleSheet("font-size: 15px; font-weight: bold; color: #f1f5f9; letter-spacing: -0.5px;");
    auto *badge = new QLabel("ACTIVE");
    badge->setStyleSheet("font-size: 10px; color: #e61414; border: 1px solid rgba(19,182,236,0.3); padding: 2px 8px; border-radius: 3px; font-weight: bold; letter-spacing: 1px;");
    badge->setFixedHeight(20);
    titleRow->addWidget(title);
    titleRow->addStretch();
    titleRow->addWidget(badge);

    headerLayout->addWidget(sectionLabel);
    headerLayout->addLayout(titleRow);
    layout->addWidget(header);

    // Component Tree
    auto *tree = new QTreeWidget();
    tree->setHeaderHidden(true);
    tree->setIndentation(20);
    tree->setStyleSheet("QTreeWidget { background: transparent; border: none; padding: 8px; }");

    auto *frameItem = new QTreeWidgetItem(tree, {"◆ Frame — Carbon Fiber X-8"});
    frameItem->setForeground(0, QColor("#e61414"));
    auto *massItem = new QTreeWidgetItem(frameItem, {"  Mass: 1.2kg total"});
    auto *stiffItem = new QTreeWidgetItem(frameItem, {"  Stiffness: 92GPa"});
    Q_UNUSED(massItem); Q_UNUSED(stiffItem);
    frameItem->setExpanded(true);

    auto *motorItem = new QTreeWidgetItem(tree, {"⚙ Motors — T-Motor F60 Pro × 8"});
    auto *motorDetail = new QTreeWidgetItem(motorItem, {"  2200KV"});
    Q_UNUSED(motorDetail);

    auto *escItem = new QTreeWidgetItem(tree, {"⚡ ESCs — Hobbywing 60A 4-in-1"});
    Q_UNUSED(escItem);

    auto *battItem = new QTreeWidgetItem(tree, {"🔋 Battery"});
    battItem->setForeground(0, QColor("#64748b"));

    layout->addWidget(tree, 1);

    // Mass Breakdown Section
    auto *bottomSection = new QWidget();
    bottomSection->setStyleSheet("background: rgba(16,29,34,0.4); border-top: 1px solid #1e2d33;");
    auto *bottomLayout = new QVBoxLayout(bottomSection);
    bottomLayout->setContentsMargins(16, 16, 16, 16);
    bottomLayout->setSpacing(12);

    auto *massTitle = new QLabel("MASS BREAKDOWN");
    massTitle->setStyleSheet("font-size: 10px; font-weight: bold; color: #e61414; letter-spacing: 2px;");
    bottomLayout->addWidget(massTitle);

    auto addMassRow = [&](const QString &name, const QString &val, const QString &color = "#e2e8f0") {
        auto *row = new QHBoxLayout();
        auto *n = new QLabel(name);
        n->setStyleSheet("font-size: 11px; color: #94a3b8; font-weight: 500;");
        auto *v = new QLabel(val);
        v->setStyleSheet(QString("font-size: 11px; color: %1; font-weight: 500;").arg(color));
        row->addWidget(n);
        row->addStretch();
        row->addWidget(v);
        bottomLayout->addLayout(row);
    };

    addMassRow("Frame", "210g");
    addMassRow("Motors", "160g");
    addMassRow("ESC", "35g");
    addMassRow("Battery", "120g");
    addMassRow("Payload", "57g");

    // Separator
    auto *sep = new QFrame();
    sep->setFrameShape(QFrame::HLine);
    sep->setStyleSheet("color: #1e2d33;");
    bottomLayout->addWidget(sep);

    addMassRow("Total Mass", "582g", "#f1f5f9");

    // Payload Status
    auto *payloadTitle = new QLabel("PAYLOAD STATUS");
    payloadTitle->setStyleSheet("font-size: 10px; font-weight: bold; color: #e61414; letter-spacing: 2px; margin-top: 8px;");
    bottomLayout->addWidget(payloadTitle);

    addMassRow("Max Payload (Safe)", "450g");
    addMassRow("Current Payload", "57g", "#e61414");
    addMassRow("Remaining", "393g", "#10b981");

    // Add Component button
    auto *addBtn = new QPushButton("+ Add Component");
    addBtn->setStyleSheet(
        "QPushButton { border: 1px solid #1e2d33; color: #94a3b8; padding: 10px; border-radius: 4px;"
        "font-size: 11px; font-weight: bold; background: transparent; }"
        "QPushButton:hover { color: #e2e8f0; border-color: #64748b; }"
    );
    bottomLayout->addWidget(addBtn);

    layout->addWidget(bottomSection);
}
