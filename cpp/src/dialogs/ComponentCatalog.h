#pragma once
#include <QDialog>
#include <QTabWidget>
#include <QVBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QScrollArea>
#include <QLineEdit>
#include <QDoubleSpinBox>
#include <QSpinBox>
#include <QComboBox>
#include "../data/ComponentRegistry.h"
#include "../data/DroneAssembly.h"

class ComponentCatalog : public QDialog
{
    Q_OBJECT
public:
    explicit ComponentCatalog(DroneAssembly *assembly, QWidget *parent = nullptr);

signals:
    void componentAdded();

private:
    QWidget* makeComponentRow(std::shared_ptr<DroneComponent> comp);
    QWidget* makeCustomCreator(ComponentType type);
    void addComponentToAssembly(std::shared_ptr<DroneComponent> comp);
    QString typeDisplayName(ComponentType t);

    DroneAssembly *m_assembly;
    int m_customCounter = 0;
};
