#pragma once
#include <QWidget>
#include <QTreeWidget>
#include <QLabel>
#include <QVBoxLayout>
#include <QPushButton>
#include <QMenu>
#include <QSplitter>

class DroneAssembly;

class BuildPanel : public QWidget
{
    Q_OBJECT
public:
    explicit BuildPanel(DroneAssembly *assembly, QWidget *parent = nullptr);
    void refreshUI();

signals:
    void assemblyChanged();

private slots:
    void onTreeContextMenu(const QPoint &pos);

private:
    void buildCatalogTree();
    void addToAssembly(std::shared_ptr<class DroneComponent> comp);

    DroneAssembly *m_assembly;

    // Top: project components
    QTreeWidget *m_tree;
    QLabel *m_titleLabel;
    QLabel *m_badgeLabel;

    // Bottom: catalog
    QTreeWidget *m_catalogTree;

    int m_customCounter = 0;
};
