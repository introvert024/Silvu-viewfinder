#pragma once

#include "DroneComponent.h"
#include <string>
#include <vector>
#include <memory>

class DroneComponent;
struct Vector3D; // Defined in component header

// Represents a node where a component can be attached dynamically inside the framework
struct SnapNode {
    std::string id;
    Vector3D localPosition;
    ComponentType acceptedType;
    std::shared_ptr<DroneComponent> attachedComponent;

    SnapNode(std::string id_, Vector3D pos, ComponentType accepts)
        : id(std::move(id_)), localPosition(pos), acceptedType(accepts), attachedComponent(nullptr) {}
};

class DroneAssembly {
public:
    DroneAssembly();
    ~DroneAssembly() = default;

    // Create Base
    void setFrame(std::shared_ptr<DroneComponent> frame);
    std::shared_ptr<DroneComponent> getFrame() const { return m_frame; }

    bool attachComponent(const std::string& nodeId, std::shared_ptr<DroneComponent> component);
    void detachComponent(const std::string& nodeId);

    const std::vector<SnapNode>& getSnapNodes() const { return m_nodes; }

    // Aggregate outputs
    float getTotalMass() const;
    Vector3D getCenterOfGravity() const;
    float getTotalThrust() const;
    float getThrustToWeightRatio() const;
    float getHoverThrottle() const;
    InertiaTensor getInertiaTensor() const;

private:
    std::shared_ptr<DroneComponent> m_frame;
    std::vector<SnapNode> m_nodes;
};