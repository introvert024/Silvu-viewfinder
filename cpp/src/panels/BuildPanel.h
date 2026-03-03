#pragma once
#include <QWidget>
#include <QTreeWidget>
#include <QLabel>
#include <QVBoxLayout>
#include <QPushButton>

class BuildPanel : public QWidget
{
    Q_OBJECT
public:
    explicit BuildPanel(QWidget *parent = nullptr);
};
