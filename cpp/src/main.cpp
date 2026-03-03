#include <QApplication>
#include <QFile>
#include <QFontDatabase>
#include <QPalette>
#include <QStyleFactory>
#include "MainWindow.h"

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    app.setApplicationName("Silvu Viewfinder");
    app.setOrganizationName("Silvu");
    app.setApplicationVersion("1.0.0");

    // Use Fusion style as base (professional, cross-platform)
    app.setStyle(QStyleFactory::create("Fusion"));

    // Set up dark palette
    QPalette darkPalette;
    darkPalette.setColor(QPalette::Window, QColor(16, 29, 34));          // #101d22
    darkPalette.setColor(QPalette::WindowText, QColor(226, 232, 240));   // #e2e8f0
    darkPalette.setColor(QPalette::Base, QColor(10, 15, 18));            // #0a0f12
    darkPalette.setColor(QPalette::AlternateBase, QColor(22, 34, 40));   // #162228
    darkPalette.setColor(QPalette::ToolTipBase, QColor(22, 34, 40));
    darkPalette.setColor(QPalette::ToolTipText, QColor(226, 232, 240));
    darkPalette.setColor(QPalette::Text, QColor(226, 232, 240));
    darkPalette.setColor(QPalette::Button, QColor(30, 45, 51));          // #1e2d33
    darkPalette.setColor(QPalette::ButtonText, QColor(148, 163, 184));   // #94a3b8
    darkPalette.setColor(QPalette::BrightText, QColor(19, 182, 236));    // #e61414 primary
    darkPalette.setColor(QPalette::Link, QColor(19, 182, 236));
    darkPalette.setColor(QPalette::Highlight, QColor(19, 182, 236));
    darkPalette.setColor(QPalette::HighlightedText, QColor(0, 0, 0));
    darkPalette.setColor(QPalette::Disabled, QPalette::Text, QColor(100, 116, 139));
    app.setPalette(darkPalette);

    // Load QSS stylesheet
    QFile styleFile(":/styles/dark_theme.qss");
    if (styleFile.open(QFile::ReadOnly | QFile::Text)) {
        QString style = styleFile.readAll();
        app.setStyleSheet(style);
        styleFile.close();
    }

    MainWindow mainWindow;
    mainWindow.setWindowTitle("SILVU VIEWFINDER");
    mainWindow.resize(1600, 900);
    mainWindow.show();

    return app.exec();
}
