#pragma once

#include <string>
#include <vector>

enum class ComponentType { 
    Frame, 
    Motor, 
    ESC, 
    Propeller,
    Battery, 
    Payload,
    Internal
};

struct Vector3D {
    float x, y, z;
    Vector3D(float x_ = 0, float y_ = 0, float z_ = 0) : x(x_), y(y_), z(z_) {}
};

class DroneComponent {
public:
    DroneComponent(std::string id, std::string name, ComponentType type, float massGraph)
        : m_id(id), m_name(name), m_type(type), m_massGraph(massGraph), m_offset(0,0,0) {}
    
    virtual ~DroneComponent() = default;

    std::string getId() const { return m_id; }
    std::string getName() const { return m_name; }
    ComponentType getType() const { return m_type; }

    float getMassGraph() const { return m_massGraph; }
    
    // Abstract physical properties
    Vector3D m_offset; // Relative position offset

protected:
    std::string m_id;
    std::string m_name;
    ComponentType m_type;
    float m_massGraph;
};

class MotorComponent : public DroneComponent {
public:
    MotorComponent(std::string id, std::string name, float massGraph, float kv, float maxThrust)
        : DroneComponent(id, name, ComponentType::Motor, massGraph), m_kv(kv), m_maxThrust(maxThrust) {}

    float getKV() const { return m_kv; }
    float getMaxThrust() const { return m_maxThrust; }

private:
    float m_kv;
    float m_maxThrust; // grams
};

class BatteryComponent : public DroneComponent {
public:
    BatteryComponent(std::string id, std::string name, float massGraph, int cells, float capacity, float maxCurrentDraw)
        : DroneComponent(id, name, ComponentType::Battery, massGraph), m_cells(cells), m_capacity(capacity), m_maxCurrentDraw(maxCurrentDraw) {}

    int getCells() const { return m_cells; }
    float getVoltage() const { return m_cells * 3.7f; }

private:
    int m_cells;
    float m_capacity; // mAh
    float m_maxCurrentDraw; // Amps
};
