#pragma once
#include <array>

struct Vec3 { double x, y, z; };

class PhysicsEngine
{
public:
    PhysicsEngine();

    // Core drone parameters
    void setFrameMass(double mass);
    void setMotorMass(double mass, int count);
    void setBatteryMass(double mass);
    void setPayloadMass(double mass);
    void setArmLength(double length);

    // Computed values
    double totalMass() const;
    Vec3 centerOfGravity() const;
    std::array<std::array<double, 3>, 3> inertiaTensor() const;

    // Thrust calculations
    double hoverThrottle() const;       // % throttle needed to hover
    double thrustToWeightRatio() const;
    double maxThrust() const;          // kg

    // Thermal prediction (simplified)
    double escTemperatureC() const;

private:
    double m_frameMass = 0.210;    // kg
    double m_motorMass = 0.020;    // kg per motor
    int    m_motorCount = 8;
    double m_escMass = 0.035;      // kg (total)
    double m_batteryMass = 0.120;  // kg
    double m_payloadMass = 0.057;  // kg
    double m_armLength = 0.275;    // meters (arm half-length)
    double m_maxThrustPerMotor = 6.0;  // kg per motor
    double m_ambientTemp = 25.0;   // °C
};
