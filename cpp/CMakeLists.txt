cmake_minimum_required(VERSION 3.20)
project(SilvuViewfinder VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# MSVC: enable M_PI and other math constants
add_definitions(-D_USE_MATH_DEFINES)

# Find Qt6
find_package(Qt6 REQUIRED COMPONENTS Widgets OpenGLWidgets)

# Source files
set(SOURCES
    src/main.cpp
    src/MainWindow.cpp
    src/ViewportWidget.cpp
    src/panels/BuildPanel.cpp
    src/panels/DiagPanel.cpp
    src/panels/ConfigPanel.cpp
    src/panels/ProtocolPanel.cpp
    src/panels/TelemetryBar.cpp
    src/physics/PhysicsEngine.cpp
)

set(HEADERS
    src/MainWindow.h
    src/ViewportWidget.h
    src/panels/BuildPanel.h
    src/panels/DiagPanel.h
    src/panels/ConfigPanel.h
    src/panels/ProtocolPanel.h
    src/panels/TelemetryBar.h
    src/physics/PhysicsEngine.h
)

# Resources
set(RESOURCES resources/resources.qrc)

add_executable(${PROJECT_NAME} WIN32 ${SOURCES} ${HEADERS} ${RESOURCES})

target_include_directories(${PROJECT_NAME} PRIVATE src)

target_link_libraries(${PROJECT_NAME} PRIVATE
    Qt6::Widgets
    Qt6::OpenGLWidgets
    opengl32
)

# Copy the stylesheet next to the exe
add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "${CMAKE_SOURCE_DIR}/resources/styles/dark_theme.qss"
        "$<TARGET_FILE_DIR:${PROJECT_NAME}>/dark_theme.qss"
)
