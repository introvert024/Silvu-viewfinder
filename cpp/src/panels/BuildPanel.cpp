#include "BuildPanel.h"
#include "../data/DroneAssembly.h"
#include "../data/DroneComponent.h"
#include "../data/ComponentRegistry.h"
#include <QHBoxLayout>
#include <QFrame>
#include <QMenu>
#include <QClipboard>
#include <QApplication>
#include <QMessageBox>

BuildPanel::BuildPanel(DroneAssembly *assembly, QWidget *parent)
    : QWidget(parent), m_assembly(assembly)
{
    auto *outer = new QVBoxLayout(this);
    outer->setContentsMargins(0, 0, 0, 0);
    outer->setSpacing(0);
    setStyleSheet("background: #162228; border-right: 1px solid #1e2d33;");

    // ═══ TOP: Project Components ═══
    auto *topWidget = new QWidget();
    auto *topLayout = new QVBoxLayout(topWidget);
    topLayout->setContentsMargins(0, 0, 0, 0);
    topLayout->setSpacing(0);

    auto *header = new QWidget();
    auto *hLayout = new QVBoxLayout(header);
    hLayout->setContentsMargins(14, 12, 14, 8);
    hLayout->setSpacing(4);

    auto *sectionLabel = new QLabel("BUILD STRUCTURE");
    sectionLabel->setStyleSheet("font-size: 9px; font-weight: bold; color: #e61414; letter-spacing: 2px;");

    auto *titleRow = new QHBoxLayout();
    m_titleLabel = new QLabel("No Frame");
    m_titleLabel->setStyleSheet("font-size: 13px; font-weight: bold; color: #64748b;");
    m_badgeLabel = new QLabel("EMPTY");
    m_badgeLabel->setStyleSheet("font-size: 9px; color: #64748b; border: 1px solid #1e2d33; padding: 1px 6px; border-radius: 3px; font-weight: bold;");
    m_badgeLabel->setFixedHeight(18);
    titleRow->addWidget(m_titleLabel);
    titleRow->addStretch();
    titleRow->addWidget(m_badgeLabel);

    hLayout->addWidget(sectionLabel);
    hLayout->addLayout(titleRow);
    topLayout->addWidget(header);

    m_tree = new QTreeWidget();
    m_tree->setHeaderHidden(true);
    m_tree->setIndentation(16);
    m_tree->setStyleSheet(
        "QTreeWidget { background: transparent; border: none; padding: 4px 8px; color: #94a3b8; font-size: 11px; }"
        "QTreeWidget::item { padding: 2px 0; }"
        "QTreeWidget::item:selected { background: rgba(230,20,20,0.15); color: #f1f5f9; }"
    );
    m_tree->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(m_tree, &QTreeWidget::customContextMenuRequested, this, &BuildPanel::onTreeContextMenu);
    topLayout->addWidget(m_tree, 1);

    // ═══ BOTTOM: Component Library ═══
    auto *bottomWidget = new QWidget();
    auto *bottomLayout = new QVBoxLayout(bottomWidget);
    bottomLayout->setContentsMargins(0, 0, 0, 0);
    bottomLayout->setSpacing(0);

    auto *catHeader = new QWidget();
    catHeader->setStyleSheet("border-top: 1px solid #1e2d33;");
    auto *chLayout = new QHBoxLayout(catHeader);
    chLayout->setContentsMargins(14, 8, 14, 6);
    auto *catLabel = new QLabel("COMPONENT LIBRARY");
    catLabel->setStyleSheet("font-size: 9px; font-weight: bold; color: #e61414; letter-spacing: 2px;");
    chLayout->addWidget(catLabel);
    chLayout->addStretch();
    auto *hint = new QLabel("double-click to add");
    hint->setStyleSheet("font-size: 8px; color: #4a5568; font-style: italic;");
    chLayout->addWidget(hint);
    bottomLayout->addWidget(catHeader);

    m_catalogTree = new QTreeWidget();
    m_catalogTree->setHeaderHidden(true);
    m_catalogTree->setIndentation(16);
    m_catalogTree->setAnimated(true);
    m_catalogTree->setStyleSheet(
        "QTreeWidget { background: rgba(10,16,20,0.4); border: none; padding: 2px 4px; color: #cbd5e1; font-size: 10px; }"
        "QTreeWidget::item { padding: 2px 0; }"
        "QTreeWidget::item:selected { background: rgba(230,20,20,0.1); color: #f1f5f9; }"
        "QScrollBar:vertical { background: #111a1f; width: 5px; }"
        "QScrollBar::handle:vertical { background: #1e2d33; border-radius: 2px; min-height: 16px; }"
        "QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical { height: 0; }"
    );
    buildCatalogTree();
    bottomLayout->addWidget(m_catalogTree, 1);

    // Splitter: top (build tree) / bottom (catalog)
    auto *splitter = new QSplitter(Qt::Vertical);
    splitter->setStyleSheet("QSplitter::handle { background: #1e2d33; height: 3px; }");
    splitter->addWidget(topWidget);
    splitter->addWidget(bottomWidget);
    splitter->setSizes({250, 350});
    splitter->setChildrenCollapsible(false);

    outer->addWidget(splitter);
}

// ────────────────────────────────────────────────────────────
//  Catalog tree (always visible in bottom half)
// ────────────────────────────────────────────────────────────
void BuildPanel::buildCatalogTree()
{
    auto &reg = ComponentRegistry::getInstance();
    reg.loadDefaults();

    for (ComponentType type : reg.getAllTypes()) {
        QString typeName = QString::fromUtf8(componentTypeName(type)).toUpper();
        auto *catItem = new QTreeWidgetItem(m_catalogTree, {typeName});
        catItem->setForeground(0, QColor("#e61414"));
        QFont f = catItem->font(0);
        f.setBold(true);
        f.setPointSize(8);
        catItem->setFont(0, f);

        auto comps = reg.getComponentsByType(type);
        for (auto &comp : comps) {
            QString line = QString::fromStdString(comp->getName());

            // Inline specs
            if (comp->getType() == ComponentType::Motor) {
                auto m = std::static_pointer_cast<MotorComponent>(comp);
                line += QString("  [%1KV %2g]").arg((int)m->getKV()).arg(m->getMaxThrust(), 0, 'f', 0);
            } else if (comp->getType() == ComponentType::Battery) {
                auto b = std::static_pointer_cast<BatteryComponent>(comp);
                line += QString("  [%1S %2mAh]").arg(b->getCells()).arg((int)b->getCapacity());
            } else if (comp->getType() == ComponentType::ESC) {
                auto e = std::static_pointer_cast<ESCComponent>(comp);
                line += QString("  [%1A]").arg(e->getMaxAmps(), 0, 'f', 0);
            } else if (comp->getType() == ComponentType::Propeller) {
                auto p = std::static_pointer_cast<PropellerComponent>(comp);
                line += QString("  [%1\"%2bl]").arg(p->getDiameter(), 0, 'f', 0).arg(p->getBlades());
            }
            line += QString("  %1g").arg(comp->getMassGraph(), 0, 'f', 1);

            auto *child = new QTreeWidgetItem(catItem, {line});
            child->setData(0, Qt::UserRole, QString::fromStdString(comp->getId()));
            child->setToolTip(0, QString("Double-click to add \xE2\x80\xA2 %1g").arg(comp->getMassGraph(), 0, 'f', 1));
        }

        // Custom entry
        auto *customChild = new QTreeWidgetItem(catItem, {"+ Custom..."});
        customChild->setForeground(0, QColor("#10b981"));
        customChild->setData(0, Qt::UserRole, QString("CUSTOM_%1").arg((int)type));

        catItem->setExpanded(false);
    }

    // Double-click to add
    connect(m_catalogTree, &QTreeWidget::itemDoubleClicked, this, [this](QTreeWidgetItem *item, int) {
        if (!item || !item->parent()) return;

        QString id = item->data(0, Qt::UserRole).toString();
        if (id.startsWith("CUSTOM_")) {
            int typeInt = id.mid(7).toInt();
            m_customCounter++;
            auto comp = std::make_shared<DroneComponent>(
                "C" + std::to_string(m_customCounter), "Custom Part",
                (ComponentType)typeInt, 10.0f);
            ComponentRegistry::getInstance().addCustom(comp);
            addToAssembly(comp);
            return;
        }

        auto comp = ComponentRegistry::getInstance().getComponent(id.toStdString());
        if (comp) addToAssembly(comp);
    });

    // Right-click on catalog
    m_catalogTree->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(m_catalogTree, &QTreeWidget::customContextMenuRequested, this, [this](const QPoint &pos) {
        auto *item = m_catalogTree->itemAt(pos);
        if (!item || !item->parent()) return;

        QString id = item->data(0, Qt::UserRole).toString();
        if (id.startsWith("CUSTOM_")) return;

        auto comp = ComponentRegistry::getInstance().getComponent(id.toStdString());
        if (!comp) return;

        QMenu menu(this);
        menu.setStyleSheet(
            "QMenu { background: #0f1619; border: 1px solid #1e2d33; padding: 4px; }"
            "QMenu::item { color: #cbd5e1; padding: 6px 20px; font-size: 11px; }"
            "QMenu::item:selected { background: rgba(230,20,20,0.2); color: #e61414; }"
        );
        menu.addAction("Add to Assembly", this, [this, comp]() { addToAssembly(comp); });
        menu.addAction("View Info", this, [this, comp]() {
            QString info = QString("%1\nType: %2\nMass: %3g")
                .arg(QString::fromStdString(comp->getName()))
                .arg(QString::fromUtf8(componentTypeName(comp->getType())))
                .arg(comp->getMassGraph(), 0, 'f', 1);
            QMessageBox msg(this); msg.setWindowTitle("Details"); msg.setText(info);
            msg.setStyleSheet("QMessageBox{background:#0f1619;color:#e2e8f0}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
            msg.exec();
        });
        menu.addAction("Copy Name", [comp]() { QApplication::clipboard()->setText(QString::fromStdString(comp->getName())); });
        menu.exec(m_catalogTree->mapToGlobal(pos));
    });
}

// ────────────────────────────────────────────────────────────
//  Add component to assembly
// ────────────────────────────────────────────────────────────
void BuildPanel::addToAssembly(std::shared_ptr<DroneComponent> comp)
{
    if (comp->getType() == ComponentType::Frame) {
        m_assembly->setFrame(comp);
        refreshUI(); emit assemblyChanged(); return;
    }
    for (const auto &n : m_assembly->getSnapNodes()) {
        if (n.acceptedType == comp->getType() && !n.attachedComponent) {
            m_assembly->attachComponent(n.id, comp);
            refreshUI(); emit assemblyChanged(); return;
        }
    }
    for (const auto &n : m_assembly->getSnapNodes()) {
        if (n.acceptedType == ComponentType::Payload && !n.attachedComponent) {
            m_assembly->attachComponent(n.id, comp);
            refreshUI(); emit assemblyChanged(); return;
        }
    }
    QMessageBox msg(this); msg.setWindowTitle("No Slot");
    msg.setText("No open slot. Add a frame first.");
    msg.setStyleSheet("QMessageBox{background:#0d1317;color:#e2e8f0}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
    msg.exec();
}

// ────────────────────────────────────────────────────────────
//  Refresh build tree (NO auto-expand)
// ────────────────────────────────────────────────────────────
void BuildPanel::refreshUI()
{
    m_tree->clear();
    if (!m_assembly) return;

    auto frame = m_assembly->getFrame();
    if (frame) {
        m_titleLabel->setText(QString::fromStdString(frame->getName()));
        m_titleLabel->setStyleSheet("font-size: 13px; font-weight: bold; color: #f1f5f9;");
        m_badgeLabel->setText("ACTIVE");
        m_badgeLabel->setStyleSheet("font-size: 9px; color: #e61414; border: 1px solid rgba(230,20,20,0.3); padding: 1px 6px; border-radius: 3px; font-weight: bold;");

        auto *fi = new QTreeWidgetItem(m_tree, {QString::fromUtf8("\xe2\x97\x86 Frame \xe2\x80\x94 %1").arg(QString::fromStdString(frame->getName()))});
        fi->setForeground(0, QColor("#e61414"));
        new QTreeWidgetItem(fi, {QString("  Mass: %1g").arg(frame->getMassGraph(), 0, 'f', 1)});
        // NOT expanded by default
    } else {
        m_titleLabel->setText("No Frame");
        m_titleLabel->setStyleSheet("font-size: 13px; font-weight: bold; color: #64748b;");
        m_badgeLabel->setText("EMPTY");
        m_badgeLabel->setStyleSheet("font-size: 9px; color: #64748b; border: 1px solid #1e2d33; padding: 1px 6px; border-radius: 3px; font-weight: bold;");
    }

    for (const auto &node : m_assembly->getSnapNodes()) {
        if (!node.attachedComponent) continue;
        auto comp = node.attachedComponent;
        QString icon;
        switch (comp->getType()) {
            case ComponentType::Motor: icon = QString::fromUtf8("\xe2\x9a\x99"); break;
            case ComponentType::Battery: icon = QString::fromUtf8("\xf0\x9f\x94\x8b"); break;
            default: icon = QString::fromUtf8("\xe2\x97\x8f"); break;
        }
        auto *item = new QTreeWidgetItem(m_tree, {
            QString("%1 %2").arg(icon).arg(QString::fromStdString(comp->getName()))
        });
        new QTreeWidgetItem(item, {QString("  Mass: %1g").arg(comp->getMassGraph(), 0, 'f', 1)});
        new QTreeWidgetItem(item, {QString("  Slot: %1").arg(QString::fromStdString(node.id))});
        if (comp->getType() == ComponentType::Motor) {
            auto m = std::static_pointer_cast<MotorComponent>(comp);
            new QTreeWidgetItem(item, {QString("  KV: %1  Thrust: %2g").arg((int)m->getKV()).arg(m->getMaxThrust(), 0, 'f', 0)});
        } else if (comp->getType() == ComponentType::Battery) {
            auto b = std::static_pointer_cast<BatteryComponent>(comp);
            new QTreeWidgetItem(item, {QString("  %1S  %2V").arg(b->getCells()).arg(b->getVoltage(), 0, 'f', 1)});
        }
        // NOT expanded by default — user can click to expand
    }
}

// ────────────────────────────────────────────────────────────
//  Right-click on build tree
// ────────────────────────────────────────────────────────────
void BuildPanel::onTreeContextMenu(const QPoint &pos)
{
    QMenu menu(this);
    menu.setStyleSheet(
        "QMenu { background: #0f1619; border: 1px solid #1e2d33; padding: 4px; }"
        "QMenu::item { color: #cbd5e1; padding: 6px 20px; font-size: 11px; }"
        "QMenu::item:selected { background: rgba(230,20,20,0.2); color: #e61414; }"
        "QMenu::separator { height: 1px; background: #1e2d33; margin: 4px 8px; }"
    );

    auto *item = m_tree->itemAt(pos);
    if (!item) {
        menu.exec(m_tree->mapToGlobal(pos));
        return;
    }

    QTreeWidgetItem *topItem = item;
    while (topItem->parent()) topItem = topItem->parent();
    int index = m_tree->indexOfTopLevelItem(topItem);

    menu.addAction("View Info", this, [this, topItem]() {
        QString info = topItem->text(0) + "\n";
        for (int i = 0; i < topItem->childCount(); i++)
            info += topItem->child(i)->text(0) + "\n";
        QMessageBox msg(this); msg.setWindowTitle("Info"); msg.setText(info);
        msg.setStyleSheet("QMessageBox{background:#0f1619;color:#e2e8f0}QPushButton{background:#1e2d33;color:#e2e8f0;padding:6px 14px;border-radius:4px}");
        msg.exec();
    });
    menu.addAction("Copy Name", [topItem]() { QApplication::clipboard()->setText(topItem->text(0)); });
    menu.addSeparator();
    menu.addAction("Remove", this, [this, index]() {
        if (!m_assembly) return;
        if (index == 0 && m_assembly->getFrame()) {
            m_assembly->setFrame(nullptr);
        } else {
            int ci = 0;
            for (auto &n : m_assembly->getSnapNodes()) {
                if (n.attachedComponent) {
                    if (ci == index - 1) { m_assembly->detachComponent(n.id); break; }
                    ci++;
                }
            }
        }
        refreshUI(); emit assemblyChanged();
    });
    menu.exec(m_tree->mapToGlobal(pos));
}
