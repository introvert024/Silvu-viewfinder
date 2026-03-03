#include "ComponentCatalog.h"
#include <QHBoxLayout>
#include <QGridLayout>
#include <QMessageBox>
#include <QFormLayout>
#include <QDialogButtonBox>
#include <QGroupBox>

// ────────────────────────────────────────────────────────────
//  Stylesheet constants
// ────────────────────────────────────────────────────────────
static const char* kDialogStyle = R"(
    QDialog { background: #0d1317; }
    QTabWidget::pane { border: 1px solid #1a2530; background: #111a1f; }
    QTabBar::tab {
        background: #0d1317; color: #4a5568; padding: 6px 12px;
        border: none; border-bottom: 2px solid transparent;
        font-size: 10px; font-weight: bold; letter-spacing: 1px;
    }
    QTabBar::tab:selected { color: #e61414; border-bottom: 2px solid #e61414; }
    QTabBar::tab:hover { color: #cbd5e1; }
    QScrollArea { border: none; background: transparent; }
    QScrollBar:vertical {
        background: #0d1317; width: 6px; border: none;
    }
    QScrollBar::handle:vertical {
        background: #1e2d33; border-radius: 3px; min-height: 20px;
    }
    QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical { height: 0; }
)";

// ────────────────────────────────────────────────────────────
//  Constructor
// ────────────────────────────────────────────────────────────
ComponentCatalog::ComponentCatalog(DroneAssembly *assembly, QWidget *parent)
    : QDialog(parent), m_assembly(assembly)
{
    setWindowTitle("Component Library");
    setMinimumSize(520, 460);
    resize(560, 480);
    setStyleSheet(kDialogStyle);

    auto *main = new QVBoxLayout(this);
    main->setContentsMargins(12, 12, 12, 12);
    main->setSpacing(8);

    // Title
    auto *titleRow = new QHBoxLayout();
    auto *titleLabel = new QLabel("COMPONENT LIBRARY");
    titleLabel->setStyleSheet("font-size: 11px; font-weight: bold; color: #e61414; letter-spacing: 2px;");
    titleRow->addWidget(titleLabel);
    titleRow->addStretch();
    auto *closeBtn = new QPushButton("✕");
    closeBtn->setFixedSize(28, 28);
    closeBtn->setStyleSheet(
        "QPushButton { background: transparent; color: #4a5568; border: 1px solid #1e2d33;"
        "border-radius: 4px; font-size: 14px; font-weight: bold; }"
        "QPushButton:hover { color: #e61414; border-color: #e61414; }"
    );
    connect(closeBtn, &QPushButton::clicked, this, &QDialog::close);
    titleRow->addWidget(closeBtn);
    main->addLayout(titleRow);

    // Tabs for each category
    auto *tabs = new QTabWidget();
    auto &reg = ComponentRegistry::getInstance();
    reg.loadDefaults();

    for (ComponentType type : reg.getAllTypes()) {
        auto *scroll = new QScrollArea();
        scroll->setWidgetResizable(true);

        auto *container = new QWidget();
        container->setStyleSheet("background: transparent;");
        auto *cLayout = new QVBoxLayout(container);
        cLayout->setContentsMargins(6, 6, 6, 6);
        cLayout->setSpacing(4);

        // Component rows
        auto comps = reg.getComponentsByType(type);
        for (auto &comp : comps) {
            cLayout->addWidget(makeComponentRow(comp));
        }

        // Custom component creator at bottom
        cLayout->addWidget(makeCustomCreator(type));
        cLayout->addStretch();

        scroll->setWidget(container);
        tabs->addTab(scroll, typeDisplayName(type).toUpper());
    }

    main->addWidget(tabs, 1);
}

// ────────────────────────────────────────────────────────────
//  Component Row — compact single-line
// ────────────────────────────────────────────────────────────
QWidget* ComponentCatalog::makeComponentRow(std::shared_ptr<DroneComponent> comp)
{
    auto *row = new QWidget();
    row->setFixedHeight(38);
    row->setStyleSheet(
        "QWidget { background: #0f1a1f; border: 1px solid #1a2530; border-radius: 4px; }"
        "QWidget:hover { border-color: rgba(230,20,20,0.5); background: #111f25; }"
    );

    auto *layout = new QHBoxLayout(row);
    layout->setContentsMargins(10, 0, 6, 0);
    layout->setSpacing(8);

    // Name
    auto *nameLabel = new QLabel(QString::fromStdString(comp->getName()));
    nameLabel->setStyleSheet("font-size: 11px; font-weight: bold; color: #e2e8f0; border: none; background: transparent;");
    layout->addWidget(nameLabel, 1);

    // Mass pill
    auto *massPill = new QLabel(QString("%1g").arg(comp->getMassGraph(), 0, 'f', 1));
    massPill->setFixedWidth(52);
    massPill->setAlignment(Qt::AlignCenter);
    massPill->setStyleSheet("font-size: 10px; color: #94a3b8; font-family: Consolas; font-weight: bold; border: none; background: transparent;");
    layout->addWidget(massPill);

    // Type-specific spec
    QString spec;
    if (comp->getType() == ComponentType::Motor) {
        auto m = std::static_pointer_cast<MotorComponent>(comp);
        spec = QString("%1KV  %2g").arg((int)m->getKV()).arg(m->getMaxThrust(), 0, 'f', 0);
    } else if (comp->getType() == ComponentType::Battery) {
        auto b = std::static_pointer_cast<BatteryComponent>(comp);
        spec = QString("%1S %2mAh").arg(b->getCells()).arg((int)b->getCapacity());
    } else if (comp->getType() == ComponentType::ESC) {
        auto e = std::static_pointer_cast<ESCComponent>(comp);
        spec = QString("%1A").arg(e->getMaxAmps(), 0, 'f', 0);
    } else if (comp->getType() == ComponentType::Propeller) {
        auto p = std::static_pointer_cast<PropellerComponent>(comp);
        spec = QString("%1x%2 %3bl").arg(p->getDiameter(), 0, 'f', 0).arg(p->getPitch(), 0, 'f', 1).arg(p->getBlades());
    }

    if (!spec.isEmpty()) {
        auto *specLabel = new QLabel(spec);
        specLabel->setFixedWidth(100);
        specLabel->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
        specLabel->setStyleSheet("font-size: 10px; color: #64748b; font-family: Consolas; border: none; background: transparent;");
        layout->addWidget(specLabel);
    }

    // Add button
    auto *addBtn = new QPushButton("ADD");
    addBtn->setFixedSize(44, 24);
    addBtn->setCursor(Qt::PointingHandCursor);
    addBtn->setStyleSheet(
        "QPushButton { background: rgba(230,20,20,0.1); color: #e61414; border: 1px solid rgba(230,20,20,0.3);"
        "border-radius: 3px; font-size: 9px; font-weight: bold; letter-spacing: 1px; }"
        "QPushButton:hover { background: rgba(230,20,20,0.25); }"
    );
    connect(addBtn, &QPushButton::clicked, this, [this, comp]() {
        addComponentToAssembly(comp);
    });
    layout->addWidget(addBtn);

    return row;
}

// ────────────────────────────────────────────────────────────
//  Custom Component Creator
// ────────────────────────────────────────────────────────────
QWidget* ComponentCatalog::makeCustomCreator(ComponentType type)
{
    auto *box = new QWidget();
    box->setStyleSheet(
        "QWidget { background: #0a1014; border: 1px dashed #1e2d33; border-radius: 4px; }"
    );
    auto *layout = new QVBoxLayout(box);
    layout->setContentsMargins(10, 8, 10, 8);
    layout->setSpacing(6);

    auto *heading = new QLabel("+ CREATE CUSTOM");
    heading->setStyleSheet("font-size: 9px; font-weight: bold; color: #4a5568; letter-spacing: 1px; border: none;");
    layout->addWidget(heading);

    // Form
    auto *formLayout = new QHBoxLayout();
    formLayout->setSpacing(6);

    auto *nameEdit = new QLineEdit();
    nameEdit->setPlaceholderText("Name");
    nameEdit->setFixedHeight(26);
    nameEdit->setStyleSheet(
        "QLineEdit { background: #111a1f; color: #e2e8f0; border: 1px solid #1e2d33;"
        "border-radius: 3px; padding: 0 6px; font-size: 10px; }"
        "QLineEdit:focus { border-color: #e61414; }"
    );
    formLayout->addWidget(nameEdit, 1);

    auto *massSpin = new QDoubleSpinBox();
    massSpin->setRange(0.1, 9999.0);
    massSpin->setDecimals(1);
    massSpin->setSuffix("g");
    massSpin->setValue(10.0);
    massSpin->setFixedHeight(26);
    massSpin->setFixedWidth(80);
    massSpin->setStyleSheet(
        "QDoubleSpinBox { background: #111a1f; color: #e2e8f0; border: 1px solid #1e2d33;"
        "border-radius: 3px; padding: 0 4px; font-size: 10px; }"
    );
    formLayout->addWidget(massSpin);

    // Type-specific spin boxes
    QDoubleSpinBox *kvSpin = nullptr, *thrustSpin = nullptr;
    QSpinBox *cellsSpin = nullptr;
    QDoubleSpinBox *capSpin = nullptr;
    QDoubleSpinBox *ampsSpin = nullptr;
    QDoubleSpinBox *diaSpin = nullptr;

    if (type == ComponentType::Motor) {
        kvSpin = new QDoubleSpinBox();
        kvSpin->setRange(100, 20000); kvSpin->setValue(2200); kvSpin->setDecimals(0);
        kvSpin->setSuffix("KV"); kvSpin->setFixedHeight(26); kvSpin->setFixedWidth(78);
        kvSpin->setStyleSheet(massSpin->styleSheet());
        formLayout->addWidget(kvSpin);

        thrustSpin = new QDoubleSpinBox();
        thrustSpin->setRange(10, 10000); thrustSpin->setValue(1000); thrustSpin->setDecimals(0);
        thrustSpin->setSuffix("g thr"); thrustSpin->setFixedHeight(26); thrustSpin->setFixedWidth(84);
        thrustSpin->setStyleSheet(massSpin->styleSheet());
        formLayout->addWidget(thrustSpin);
    } else if (type == ComponentType::Battery) {
        cellsSpin = new QSpinBox();
        cellsSpin->setRange(1, 14); cellsSpin->setValue(4);
        cellsSpin->setSuffix("S"); cellsSpin->setFixedHeight(26); cellsSpin->setFixedWidth(52);
        cellsSpin->setStyleSheet("QSpinBox { background: #111a1f; color: #e2e8f0; border: 1px solid #1e2d33; border-radius: 3px; padding: 0 4px; font-size: 10px; }");
        formLayout->addWidget(cellsSpin);

        capSpin = new QDoubleSpinBox();
        capSpin->setRange(50, 30000); capSpin->setValue(1300); capSpin->setDecimals(0);
        capSpin->setSuffix("mAh"); capSpin->setFixedHeight(26); capSpin->setFixedWidth(84);
        capSpin->setStyleSheet(massSpin->styleSheet());
        formLayout->addWidget(capSpin);
    } else if (type == ComponentType::ESC) {
        ampsSpin = new QDoubleSpinBox();
        ampsSpin->setRange(1, 200); ampsSpin->setValue(35); ampsSpin->setDecimals(0);
        ampsSpin->setSuffix("A"); ampsSpin->setFixedHeight(26); ampsSpin->setFixedWidth(64);
        ampsSpin->setStyleSheet(massSpin->styleSheet());
        formLayout->addWidget(ampsSpin);
    } else if (type == ComponentType::Propeller) {
        diaSpin = new QDoubleSpinBox();
        diaSpin->setRange(1, 20); diaSpin->setValue(5); diaSpin->setDecimals(1);
        diaSpin->setSuffix("\""); diaSpin->setFixedHeight(26); diaSpin->setFixedWidth(60);
        diaSpin->setStyleSheet(massSpin->styleSheet());
        formLayout->addWidget(diaSpin);
    }

    auto *createBtn = new QPushButton("CREATE");
    createBtn->setFixedSize(56, 26);
    createBtn->setCursor(Qt::PointingHandCursor);
    createBtn->setStyleSheet(
        "QPushButton { background: rgba(16,185,129,0.1); color: #10b981; border: 1px solid rgba(16,185,129,0.3);"
        "border-radius: 3px; font-size: 9px; font-weight: bold; letter-spacing: 1px; }"
        "QPushButton:hover { background: rgba(16,185,129,0.25); }"
    );

    connect(createBtn, &QPushButton::clicked, this, [=]() {
        QString name = nameEdit->text().trimmed();
        if (name.isEmpty()) { nameEdit->setFocus(); return; }

        float mass = (float)massSpin->value();
        m_customCounter++;
        std::string id = "CUSTOM_" + std::to_string(m_customCounter);

        std::shared_ptr<DroneComponent> comp;

        if (type == ComponentType::Motor && kvSpin && thrustSpin) {
            comp = std::make_shared<MotorComponent>(id, name.toStdString(), mass, (float)kvSpin->value(), (float)thrustSpin->value());
        } else if (type == ComponentType::Battery && cellsSpin && capSpin) {
            comp = std::make_shared<BatteryComponent>(id, name.toStdString(), mass, cellsSpin->value(), (float)capSpin->value(), 100.0f);
        } else if (type == ComponentType::ESC && ampsSpin) {
            comp = std::make_shared<ESCComponent>(id, name.toStdString(), mass, (float)ampsSpin->value(), 3);
        } else if (type == ComponentType::Propeller && diaSpin) {
            comp = std::make_shared<PropellerComponent>(id, name.toStdString(), mass, (float)diaSpin->value(), 4.0f, 3);
        } else {
            comp = std::make_shared<DroneComponent>(id, name.toStdString(), type, mass);
        }

        ComponentRegistry::getInstance().addCustom(comp);
        addComponentToAssembly(comp);
        nameEdit->clear();
    });
    formLayout->addWidget(createBtn);

    layout->addLayout(formLayout);
    return box;
}

// ────────────────────────────────────────────────────────────
//  Add to assembly
// ────────────────────────────────────────────────────────────
void ComponentCatalog::addComponentToAssembly(std::shared_ptr<DroneComponent> comp)
{
    if (comp->getType() == ComponentType::Frame) {
        m_assembly->setFrame(comp);
        emit componentAdded();
        return;
    }

    const auto &nodes = m_assembly->getSnapNodes();
    for (const auto &node : nodes) {
        if (node.acceptedType == comp->getType() && !node.attachedComponent) {
            m_assembly->attachComponent(node.id, comp);
            emit componentAdded();
            return;
        }
    }

    // If no matching snap node, for generic components (FC, Camera etc) just treat as payload
    // Try payload slot
    for (const auto &node : nodes) {
        if (node.acceptedType == ComponentType::Payload && !node.attachedComponent) {
            m_assembly->attachComponent(node.id, comp);
            emit componentAdded();
            return;
        }
    }

    QMessageBox msg(this);
    msg.setWindowTitle("No Slot");
    msg.setText("No open slot for this type.\nAdd a frame first to create mount points.");
    msg.setStyleSheet("QMessageBox { background: #0d1317; color: #e2e8f0; } QPushButton { background: #1e2d33; color: #e2e8f0; padding: 6px 18px; border-radius: 4px; }");
    msg.exec();
}

QString ComponentCatalog::typeDisplayName(ComponentType t)
{
    return QString::fromUtf8(componentTypeName(t));
}
