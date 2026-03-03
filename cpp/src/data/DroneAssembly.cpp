#include "DroneAssembly.h"

DroneAssembly::DroneAssembly() : m_frame(nullptr) {}

void DroneAssembly::setFrame(std::shared_ptr<DroneComponent> frame) {
    m_frame = frame;
    m_nodes.clear();
    
    if(m_frame) {
        // Motor mounts at 4 corners (quadcopter default)
        m_nodes.push_back(SnapNode("Motor_FL", Vector3D(-1.0, 1.0, 0), ComponentType::Motor));
        m_nodes.push_back(SnapNode("Motor_FR", Vector3D(1.0, 1.0, 0), ComponentType::Motor));
        m_nodes.push_back(SnapNode("Motor_BL", Vector3D(-1.0, -1.0, 0), ComponentType::Motor));
        m_nodes.push_back(SnapNode("Motor_BR", Vector3D(1.0, -1.0, 0), ComponentType::Motor));
        
        // Battery bay
        m_nodes.push_back(SnapNode("Battery_Bay", Vector3D(0, 0, -0.5), ComponentType::Battery));
        
        // ESC bay (4-in-1 or individual)
        m_nodes.push_back(SnapNode("ESC_Stack", Vector3D(0, 0.2, 0), ComponentType::ESC));
        
        // FC / electronics stack
        m_nodes.push_back(SnapNode("FC_Stack", Vector3D(0, 0.3, 0), ComponentType::FlightController));
        
        // Camera mount
        m_nodes.push_back(SnapNode("Camera_Mount", Vector3D(0, 0.1, 0.8), ComponentType::Camera));
        
        // VTX bay
        m_nodes.push_back(SnapNode("VTX_Bay", Vector3D(0, 0.15, -0.3), ComponentType::VTX));
        
        // Receiver slot
        m_nodes.push_back(SnapNode("RX_Slot", Vector3D(0, 0.1, -0.6), ComponentType::Receiver));
        
        // GPS mount (top)
        m_nodes.push_back(SnapNode("GPS_Mast", Vector3D(0, 0.5, 0), ComponentType::GPS));
        
        // Payload bay (for generic items)
        m_nodes.push_back(SnapNode("Payload_Bay", Vector3D(0, -0.2, 0.5), ComponentType::Payload));
    }
}

bool DroneAssembly::attachComponent(const std::string& nodeId, std::shared_ptr<DroneComponent> component) {
    for(auto& node : m_nodes) {
        if(node.id == nodeId && node.acceptedType == component->getType()) {
            node.attachedComponent = component;
            return true;
        }
        // Also allow Payload nodes to accept any generic type
        if(node.id == nodeId && node.acceptedType == ComponentType::Payload) {
            node.attachedComponent = component;
            return true;
        }
    }
    return false;
}

void DroneAssembly::detachComponent(const std::string& nodeId) {
    for(auto& node : m_nodes) {
        if(node.id == nodeId) {
            node.attachedComponent = nullptr;
        }
    }
}

float DroneAssembly::getTotalMass() const {
    float total = 0.0f;
    if(m_frame) total += m_frame->getMassGraph();
    for(const auto& node : m_nodes) {
        if(node.attachedComponent) {
            total += node.attachedComponent->getMassGraph();
        }
    }
    return total;
}

float DroneAssembly::getTotalThrust() const {
    float thrust = 0.0f;
    for(const auto& node : m_nodes) {
        if(node.attachedComponent && node.attachedComponent->getType() == ComponentType::Motor) {
            auto motor = std::static_pointer_cast<MotorComponent>(node.attachedComponent);
            if(motor) thrust += motor->getMaxThrust();
        }
    }
    return thrust;
}

float DroneAssembly::getThrustToWeightRatio() const {
    float mass = getTotalMass();
    if(mass == 0.0f) return 0.0f;
    return getTotalThrust() / mass;
}

float DroneAssembly::getHoverThrottle() const {
    float twr = getThrustToWeightRatio();
    if (twr <= 0.0f) return 0.0f;
    return (1.0f / twr) * 100.0f;
}

Vector3D DroneAssembly::getCenterOfGravity() const {
    float m_total = getTotalMass();
    if(m_total <= 0.0001f) return Vector3D(0,0,0);
    
    float mx = 0, my = 0, mz = 0;
    
    for(const auto& node : m_nodes) {
         if(node.attachedComponent) {
             float compMass = node.attachedComponent->getMassGraph();
             mx += node.localPosition.x * compMass;
             my += node.localPosition.y * compMass;
             mz += node.localPosition.z * compMass;
         }
    }
    return Vector3D(mx / m_total, my / m_total, mz / m_total);
}

InertiaTensor DroneAssembly::getInertiaTensor() const {
    InertiaTensor inertia(0, 0, 0);
    Vector3D cg = getCenterOfGravity();

    // Scale factor to get mm from normalized units (assuming frame radius is ~150mm)
    const float scale = 150.0f;

    for (const auto& node : m_nodes) {
        if (node.attachedComponent) {
            float m = node.attachedComponent->getMassGraph();
            
            // Distance from CG in mm
            float dx = ((node.localPosition.x + node.attachedComponent->m_offset.x) - cg.x) * scale;
            float dy = ((node.localPosition.y + node.attachedComponent->m_offset.y) - cg.y) * scale;
            float dz = ((node.localPosition.z + node.attachedComponent->m_offset.z) - cg.z) * scale;

            // I = sum(m * r^2)
            inertia.Ixx += m * (dy * dy + dz * dz);
            inertia.Iyy += m * (dx * dx + dz * dz);
            inertia.Izz += m * (dx * dx + dy * dy);
        }
    }

    return inertia;
}
