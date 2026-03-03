#pragma once

#include <string>
#include <vector>

enum class ComponentType { 
    Frame, 
    Motor, 
    ESC, 
    Propeller,
    Battery, 
    FlightController,
    Payload,
    Camera,
    VTX,
    Receiver,
    GPS,
    Internal
};

struct Vector3D {
    float x, y, z;
    Vector3D(float x_ = 0, float y_ = 0, float z_ = 0) : x(x_), y(y_), z(z_) {}
};

struct InertiaTensor {
    float Ixx, Iyy, Izz;
    InertiaTensor(float ixx = 0, float iyy = 0, float izz = 0) : Ixx(ixx), Iyy(iyy), Izz(izz) {}
};

// Returns a human label for a component type
inline const char* componentTypeName(ComponentType t) {
    switch (t) {
        case ComponentType::Frame: return "Frame";
        case ComponentType::Motor: return "Motor";
        case ComponentType::ESC: return "ESC";
        case ComponentType::Propeller: return "Propeller";
        case ComponentType::Battery: return "Battery";
        case ComponentType::FlightController: return "Flight Controller";
        case ComponentType::Payload: return "Payload";
        case ComponentType::Camera: return "Camera";
        case ComponentType::VTX: return "VTX";
        case ComponentType::Receiver: return "Receiver";
        case ComponentType::GPS: return "GPS";
        default: return "Other";
    }
}

class DroneComponent {
public:
    DroneComponent(std::string id, std::string name, ComponentType type, float massGrams)
        : m_id(id), m_name(name), m_type(type), m_massGrams(massGrams), m_offset(0,0,0) {}
    
    virtual ~DroneComponent() = default;

    std::string getId() const { return m_id; }
    std::string getName() const { return m_name; }
    ComponentType getType() const { return m_type; }
    float getMassGraph() const { return m_massGrams; }

    void setName(const std::string &n) { m_name = n; }
    void setMass(float m) { m_massGrams = m; }
    
    Vector3D m_offset;

protected:
    std::string m_id;
    std::string m_name;
    ComponentType m_type;
    float m_massGrams;
};

// ── Motor ──
class MotorComponent : public DroneComponent {
public:
    MotorComponent(std::string id, std::string name, float mass, float kv, float maxThrust)
        : DroneComponent(id, name, ComponentType::Motor, mass), m_kv(kv), m_maxThrust(maxThrust) {}

    float getKV() const { return m_kv; }
    float getMaxThrust() const { return m_maxThrust; }
    void setKV(float v) { m_kv = v; }
    void setMaxThrust(float v) { m_maxThrust = v; }

private:
    float m_kv;
    float m_maxThrust;
};

// ── Battery ──
class BatteryComponent : public DroneComponent {
public:
    BatteryComponent(std::string id, std::string name, float mass, int cells, float capacity, float maxDraw)
        : DroneComponent(id, name, ComponentType::Battery, mass), m_cells(cells), m_capacity(capacity), m_maxDraw(maxDraw) {}

    int getCells() const { return m_cells; }
    float getCapacity() const { return m_capacity; }
    float getMaxDraw() const { return m_maxDraw; }
    float getVoltage() const { return m_cells * 3.7f; }

private:
    int m_cells;
    float m_capacity;
    float m_maxDraw;
};

// ── ESC ──
class ESCComponent : public DroneComponent {
public:
    ESCComponent(std::string id, std::string name, float mass, float maxAmps, int protocol)
        : DroneComponent(id, name, ComponentType::ESC, mass), m_maxAmps(maxAmps), m_protocol(protocol) {}

    float getMaxAmps() const { return m_maxAmps; }
    int getProtocol() const { return m_protocol; }
    // protocol: 0=PWM, 1=DShot150, 2=DShot300, 3=DShot600, 4=DShot1200

private:
    float m_maxAmps;
    int m_protocol;
};

// ── Propeller ──
class PropellerComponent : public DroneComponent {
public:
    PropellerComponent(std::string id, std::string name, float mass, float diameterInch, float pitchInch, int blades)
        : DroneComponent(id, name, ComponentType::Propeller, mass), m_diameter(diameterInch), m_pitch(pitchInch), m_blades(blades) {}

    float getDiameter() const { return m_diameter; }
    float getPitch() const { return m_pitch; }
    int getBlades() const { return m_blades; }

private:
    float m_diameter;
    float m_pitch;
    int m_blades;
};
