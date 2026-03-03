#include "PhysicsEngine.h"
#define _USE_MATH_DEFINES
#include <cmath>

PhysicsEngine::PhysicsEngine() = default;

void PhysicsEngine::setFrameMass(double mass) { m_frameMass = mass; }
void PhysicsEngine::setMotorMass(double mass, int count) { m_motorMass = mass; m_motorCount = count; }
void PhysicsEngine::setBatteryMass(double mass) { m_batteryMass = mass; }
void PhysicsEngine::setPayloadMass(double mass) { m_payloadMass = mass; }
void PhysicsEngine::setArmLength(double length) { m_armLength = length; }

double PhysicsEngine::totalMass() const
{
    return m_frameMass + (m_motorMass * m_motorCount) + m_escMass + m_batteryMass + m_payloadMass;
}

Vec3 PhysicsEngine::centerOfGravity() const
{
    // Simplified: assuming symmetric drone, CG is at geometric center
    // In a real app, each component would have a 3D position
    return {0.0, 0.0, 0.0};
}

std::array<std::array<double, 3>, 3> PhysicsEngine::inertiaTensor() const
{
    // Simplified inertia tensor for symmetric X-frame drone
    // I = sum(m_i * r_i^2) for each axis
    double total = totalMass();
    double r = m_armLength; // distance from center to motor

    // Point mass approximation: motors at arm tips
    double Ixx = 0.0, Iyy = 0.0, Izz = 0.0;

    // Frame contributes as a thin rod for each arm
    double frameI = (m_frameMass / 12.0) * pow(2.0 * r, 2);
    Ixx += frameI;
    Iyy += frameI;
    Izz += 2.0 * frameI; // Two perpendicular arms

    // Motors as point masses at arm tips (4 motors on X-frame)
    // Each motor at distance r from center
    for (int i = 0; i < m_motorCount; ++i) {
        double angle = (2.0 * M_PI * i) / m_motorCount;
        double mx = r * cos(angle);
        double mz = r * sin(angle);

        Ixx += m_motorMass * (mz * mz);         // about X axis
        Iyy += m_motorMass * (r * r) * 0.1;     // about Y (vertical) — thin disc
        Izz += m_motorMass * (mx * mx);          // about Z axis
    }

    // Battery and payload at center (minimal contribution)
    // Add small offset for battery height
    Ixx += m_batteryMass * 0.01 * 0.01; // 1cm offset

    return {{
        {Ixx, 0.0, 0.0},
        {0.0, Iyy, 0.0},
        {0.0, 0.0, Izz}
    }};
}

double PhysicsEngine::hoverThrottle() const
{
    // Hover throttle = (total_weight / max_thrust) * 100
    double weight = totalMass() * 9.81; // N
    double maxT = maxThrust() * 9.81;   // N
    if (maxT <= 0) return 100.0;
    return (weight / maxT) * 100.0;
}

double PhysicsEngine::thrustToWeightRatio() const
{
    double weight = totalMass();
    double maxT = maxThrust();
    if (weight <= 0) return 0.0;
    return maxT / weight;
}

double PhysicsEngine::maxThrust() const
{
    return m_maxThrustPerMotor * m_motorCount;
}

double PhysicsEngine::escTemperatureC() const
{
    // Very simplified thermal model
    // Temperature rises with current draw, proportional to throttle
    double throttle = hoverThrottle() / 100.0;
    double heatRise = 60.0 * throttle * throttle; // quadratic power dissipation
    return m_ambientTemp + heatRise;
}
