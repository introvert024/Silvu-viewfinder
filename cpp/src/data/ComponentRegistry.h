#pragma once

#include "DroneComponent.h"
#include <map>
#include <memory>
#include <vector>

class ComponentRegistry {
public:
    static ComponentRegistry& getInstance() {
        static ComponentRegistry instance;
        return instance;
    }

    void loadDefaults() {
        if (m_loaded) return; // Only load once
        m_loaded = true;

        // ── FRAMES ──
        regFrame("F1", "5\" Freestyle",      125.0f);
        regFrame("F2", "3\" Toothpick",       25.0f);
        regFrame("F3", "7\" Long Range",     185.0f);
        regFrame("F4", "5\" Racing",         105.0f);
        regFrame("F5", "10\" X8 Heavy Lift", 420.0f);
        regFrame("F6", "3\" Cinewhoop",       72.0f);
        regFrame("F7", "5\" DeadCat",        135.0f);

        // ── MOTORS ──
        regMotor("M1",  "2207 1960KV",  29.5f, 1960, 1600.0f);
        regMotor("M2",  "1404 4600KV",   9.0f, 4600,  400.0f);
        regMotor("M3",  "2306 2450KV",  33.0f, 2450, 1400.0f);
        regMotor("M4",  "2806.5 1300KV",42.0f, 1300, 2200.0f);
        regMotor("M5",  "1103 11000KV",  4.5f,11000,  160.0f);
        regMotor("M6",  "2004 1700KV",  12.0f, 1700,  650.0f);
        regMotor("M7",  "2812 900KV",   58.0f,  900, 3200.0f);
        regMotor("M8",  "2507 1800KV",  38.0f, 1800, 1900.0f);

        // ── ESCs ──
        regESC("E1", "BLHeli_32 35A",     8.0f,  35.0f, 3);
        regESC("E2", "BLHeli_S 30A",      6.5f,  30.0f, 2);
        regESC("E3", "Hobbywing 60A 4in1",32.0f,  60.0f, 4);
        regESC("E4", "T-Motor F55A 4in1", 18.0f,  55.0f, 4);
        regESC("E5", "HAKRC 45A",         10.0f,  45.0f, 3);

        // ── PROPELLERS ──
        regProp("P1", "5040 Tri-blade",    4.5f, 5.0f, 4.0f, 3);
        regProp("P2", "5145 Bi-blade",     3.0f, 5.1f, 4.5f, 2);
        regProp("P3", "7035 Bi-blade",     8.0f, 7.0f, 3.5f, 2);
        regProp("P4", "3018 Tri-blade",    1.2f, 3.0f, 1.8f, 3);
        regProp("P5", "51433 Tri-blade",   5.0f, 5.1f, 4.33f,3);

        // ── BATTERIES ──
        regBatt("B1", "6S 1100mAh LiPo",  195.0f, 6, 1100, 120.0f);
        regBatt("B2", "4S 850mAh LiPo",   100.0f, 4,  850,  80.0f);
        regBatt("B3", "6S 1550mAh LiPo",  245.0f, 6, 1550, 150.0f);
        regBatt("B4", "4S 1300mAh LiPo",  155.0f, 4, 1300, 100.0f);
        regBatt("B5", "6S 2200mAh Li-Ion", 310.0f,6, 2200,  40.0f);
        regBatt("B6", "2S 450mAh LiHV",    28.0f, 2,  450,  40.0f);

        // ── FLIGHT CONTROLLERS ──
        regGeneric("FC1", "SpeedyBee F405 V4", ComponentType::FlightController, 8.5f);
        regGeneric("FC2", "Matek H743 Slim",   ComponentType::FlightController, 10.0f);
        regGeneric("FC3", "BetaFlight F722",   ComponentType::FlightController, 7.0f);

        // ── CAMERAS ──
        regGeneric("CAM1", "DJI O3 Air Unit", ComponentType::Camera, 36.0f);
        regGeneric("CAM2", "Walksnail Avatar", ComponentType::Camera, 28.0f);
        regGeneric("CAM3", "Analog Micro Cam", ComponentType::Camera, 5.5f);
        regGeneric("CAM4", "GoPro Hero 12 (naked)", ComponentType::Camera, 28.0f);

        // ── VTX ──
        regGeneric("VTX1", "Rush Tank Ultimate", ComponentType::VTX, 8.0f);
        regGeneric("VTX2", "TBS Unify Pro32",    ComponentType::VTX, 6.0f);

        // ── RECEIVERS ──
        regGeneric("RX1", "ELRS EP2 Receiver",   ComponentType::Receiver, 1.5f);
        regGeneric("RX2", "TBS Crossfire Nano",   ComponentType::Receiver, 2.0f);
        regGeneric("RX3", "FrSky R-XSR",          ComponentType::Receiver, 1.6f);

        // ── GPS ──
        regGeneric("GPS1", "BN-880 GPS/Compass",  ComponentType::GPS, 10.0f);
        regGeneric("GPS2", "Matek SAM-M10Q",       ComponentType::GPS, 6.5f);
    }

    // Add a user-made custom component
    void addCustom(std::shared_ptr<DroneComponent> comp) {
        m_catalog[comp->getId()] = comp;
    }

    std::vector<std::shared_ptr<DroneComponent>> getComponentsByType(ComponentType type) {
        std::vector<std::shared_ptr<DroneComponent>> result;
        for (const auto& pair : m_catalog) {
            if (pair.second->getType() == type) {
                result.push_back(pair.second);
            }
        }
        return result;
    }

    std::shared_ptr<DroneComponent> getComponent(const std::string& id) {
        auto it = m_catalog.find(id);
        return it != m_catalog.end() ? it->second : nullptr;
    }

    std::vector<ComponentType> getAllTypes() const {
        return {
            ComponentType::Frame, ComponentType::Motor, ComponentType::ESC,
            ComponentType::Propeller, ComponentType::Battery, ComponentType::FlightController,
            ComponentType::Camera, ComponentType::VTX, ComponentType::Receiver, ComponentType::GPS
        };
    }

private:
    ComponentRegistry() = default;
    bool m_loaded = false;
    std::map<std::string, std::shared_ptr<DroneComponent>> m_catalog;

    void regMotor(std::string id, std::string name, float mass, float kv, float thrust) {
        m_catalog[id] = std::make_shared<MotorComponent>(id, name, mass, kv, thrust);
    }
    void regBatt(std::string id, std::string name, float mass, int cells, float cap, float draw) {
        m_catalog[id] = std::make_shared<BatteryComponent>(id, name, mass, cells, cap, draw);
    }
    void regESC(std::string id, std::string name, float mass, float amps, int protocol) {
        m_catalog[id] = std::make_shared<ESCComponent>(id, name, mass, amps, protocol);
    }
    void regProp(std::string id, std::string name, float mass, float dia, float pitch, int blades) {
        m_catalog[id] = std::make_shared<PropellerComponent>(id, name, mass, dia, pitch, blades);
    }
    void regFrame(std::string id, std::string name, float mass) {
        m_catalog[id] = std::make_shared<DroneComponent>(id, name, ComponentType::Frame, mass);
    }
    void regGeneric(std::string id, std::string name, ComponentType type, float mass) {
        m_catalog[id] = std::make_shared<DroneComponent>(id, name, type, mass);
    }
};
