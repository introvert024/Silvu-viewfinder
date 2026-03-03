import React from 'react';
import { cn } from '../../lib/utils';
import {
    Radio,
    SettingsInputComponent,
    Terminal,
    Globe2,
    Activity,
    AlertTriangle,
    TrendingUp,
    Satellite
} from 'lucide-react'; // Mapping closest icons

export default function ProtocolsMode() {
    return (
        <div className="flex-1 flex overflow-hidden">
            {/* Sidebar Navigation */}
            <aside className="w-64 border-r border-border-dark flex flex-col gap-4 p-4 shrink-0 bg-panel-dark/50">
                <div className="flex flex-col gap-1">
                    <div className="flex items-center gap-3 p-3 bg-primary/10 rounded-lg text-primary">
                        <Radio className="w-5 h-5" />
                        <span className="text-sm font-bold">Radio Status</span>
                    </div>
                    {[
                        { icon: SettingsInputComponent, label: 'Link Config' },
                        { icon: Terminal, label: 'MAVLink Terminal' },
                        { icon: Globe2, label: 'Sat Link' },
                        { icon: Activity, label: 'Diagnostics' }
                    ].map((item, idx) => (
                        <button key={idx} className="flex items-center gap-3 p-3 hover:bg-border-dark rounded-lg text-slate-400 transition-colors">
                            <item.icon className="w-5 h-5" />
                            <span className="text-sm font-medium">{item.label}</span>
                        </button>
                    ))}
                </div>

                <div className="mt-auto p-4 bg-red-500/10 border border-red-500/30 rounded-xl flex flex-col gap-3">
                    <div className="flex items-center gap-2 text-red-500">
                        <AlertTriangle className="w-4 h-4" />
                        <span className="text-xs font-bold uppercase tracking-wider">Safety Protocols</span>
                    </div>
                    <button className="w-full bg-red-600 hover:bg-red-700 text-white py-3 rounded-lg font-bold text-sm uppercase tracking-widest shadow-[0_0_15px_rgba(220,38,38,0.3)] transition-all">
                        EMERGENCY KILL
                    </button>
                </div>
            </aside>

            {/* Main Content Area */}
            <main className="flex-1 flex flex-col gap-6 p-6 overflow-y-auto custom-scrollbar">
                {/* Top Protocol Tabs */}
                <div className="border-b border-border-dark flex gap-8 px-2 shrink-0">
                    <button className="pb-3 text-primary border-b-2 border-primary font-bold text-sm px-4">ELRS</button>
                    <button className="pb-3 text-slate-400 hover:text-slate-200 font-bold text-sm px-4 transition-colors">Crossfire</button>
                    <button className="pb-3 text-slate-400 hover:text-slate-200 font-bold text-sm px-4 transition-colors">MAVLink</button>
                    <button className="pb-3 text-slate-400 hover:text-slate-200 font-bold text-sm px-4 transition-colors">Satellite</button>
                </div>

                {/* Stats Grid */}
                <div className="grid grid-cols-1 xl:grid-cols-4 gap-4 shrink-0">
                    <div className="bg-panel-dark/40 border border-border-dark p-5 rounded-xl">
                        <div className="text-slate-400 text-xs font-bold uppercase tracking-wider mb-1">Link Status</div>
                        <div className="text-2xl font-bold text-slate-100 tracking-tight">ACTIVE</div>
                        <div className="text-green-500 text-xs mt-1 flex items-center gap-1 font-medium">
                            <TrendingUp className="w-3 h-3" /> 0% Latency Jitter
                        </div>
                    </div>
                    <div className="bg-panel-dark/40 border border-border-dark p-5 rounded-xl">
                        <div className="text-slate-400 text-xs font-bold uppercase tracking-wider mb-1">Packet Rate</div>
                        <div className="text-2xl font-bold text-slate-100 tracking-tight">500Hz</div>
                        <div className="text-primary text-xs mt-1 font-medium">ExpressLRS F1000</div>
                    </div>
                    <div className="bg-panel-dark/40 border border-border-dark p-5 rounded-xl">
                        <div className="text-slate-400 text-xs font-bold uppercase tracking-wider mb-1">Telemetry Ratio</div>
                        <div className="text-2xl font-bold text-slate-100 tracking-tight">1:32</div>
                        <div className="text-red-400 text-xs mt-1 flex items-center gap-1 font-medium">
                            <AlertTriangle className="w-3 h-3 text-red-400" /> -5% Packet Loss
                        </div>
                    </div>
                    <div className="bg-panel-dark/40 border border-border-dark p-5 rounded-xl">
                        <div className="text-slate-400 text-xs font-bold uppercase tracking-wider mb-1">RSSI (dBm)</div>
                        <div className="text-2xl font-bold text-slate-100 tracking-tight">-84.2</div>
                        <div className="text-primary text-xs mt-1 font-medium">LQ: 100%</div>
                    </div>
                </div>

                {/* Configuration & Terminal */}
                <div className="grid grid-cols-1 xl:grid-cols-3 gap-6 flex-1 min-h-[400px]">

                    {/* Configuration Panel */}
                    <div className="xl:col-span-1 flex flex-col gap-4">
                        <h3 className="text-sm font-bold text-slate-400 uppercase tracking-widest px-1">Link Configuration</h3>

                        <div className="flex flex-col gap-3">
                            <label className="flex items-center justify-between p-4 bg-primary/5 border border-primary/20 rounded-xl cursor-pointer">
                                <div className="flex flex-col">
                                    <span className="text-sm font-bold text-white">Packet Rate</span>
                                    <span className="text-xs text-slate-400">Global sync frequency</span>
                                </div>
                                <div className="flex items-center gap-3">
                                    <span className="text-primary text-sm font-bold">500Hz</span>
                                    <input readOnly checked className="w-5 h-5 text-primary border-slate-600 bg-transparent focus:ring-primary accent-primary" name="rate" type="radio" />
                                </div>
                            </label>

                            <label className="flex items-center justify-between p-4 bg-panel-dark/40 border border-border-dark rounded-xl cursor-pointer hover:border-slate-600 transition-colors">
                                <div className="flex flex-col">
                                    <span className="text-sm font-bold text-slate-300">Telemetry Ratio</span>
                                    <span className="text-xs text-slate-500">Data downlink overhead</span>
                                </div>
                                <div className="flex items-center gap-3">
                                    <span className="text-slate-500 text-sm font-bold">1:32</span>
                                    <input readOnly className="w-5 h-5 border-slate-600 bg-transparent focus:ring-primary accent-primary" name="telemetry" type="radio" />
                                </div>
                            </label>

                            <label className="flex items-center justify-between p-4 bg-panel-dark/40 border border-border-dark rounded-xl cursor-pointer hover:border-slate-600 transition-colors">
                                <div className="flex flex-col">
                                    <span className="text-sm font-bold text-slate-300">Switch Mode</span>
                                    <span className="text-xs text-slate-500">Hybrid Wide dynamic</span>
                                </div>
                                <div className="flex items-center gap-3">
                                    <span className="text-slate-500 text-sm font-bold">HYBRID</span>
                                    <input readOnly className="w-5 h-5 border-slate-600 bg-transparent focus:ring-primary accent-primary" name="mode" type="radio" />
                                </div>
                            </label>
                        </div>

                        <div className="mt-2 p-5 bg-panel-dark/40 border border-border-dark rounded-xl">
                            <div className="flex items-center gap-2 mb-4 text-primary">
                                <Satellite className="w-4 h-4" />
                                <span className="text-xs font-bold uppercase tracking-wider">Satellite Link Details</span>
                            </div>
                            <div className="space-y-3">
                                <div className="flex justify-between text-xs">
                                    <span className="text-slate-400">Uplink Encryption</span>
                                    <span className="text-green-500 font-mono font-bold">AES-256-GCM</span>
                                </div>
                                <div className="flex justify-between text-xs">
                                    <span className="text-slate-400">Polarization</span>
                                    <span className="font-mono text-slate-300">Circular Left</span>
                                </div>
                                <div className="flex justify-between text-xs">
                                    <span className="text-slate-400">Orbital Slot</span>
                                    <span className="font-mono text-slate-300">112.5°W</span>
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Terminal */}
                    <div className="xl:col-span-2 flex flex-col border border-border-dark bg-[#0a0f12] rounded-xl overflow-hidden h-full">
                        <div className="bg-panel-dark px-4 py-2.5 border-b border-border-dark flex items-center justify-between">
                            <div className="flex items-center gap-2">
                                <Terminal className="text-primary w-4 h-4" />
                                <span className="text-xs font-bold text-slate-400 tracking-wider">MAVLINK MESSAGE INSPECTOR</span>
                            </div>
                            <div className="flex gap-2">
                                <button className="text-[10px] bg-primary/20 text-primary px-2.5 py-1 rounded uppercase font-bold hover:bg-primary/30 transition-colors">Pause</button>
                                <button className="text-[10px] bg-slate-800 text-slate-400 px-2.5 py-1 rounded uppercase font-bold hover:bg-slate-700 transition-colors">Clear</button>
                            </div>
                        </div>

                        <div className="flex-1 p-4 font-mono text-[11px] leading-relaxed overflow-y-auto custom-scrollbar">
                            <div className="text-slate-500 mb-1">[14:22:01] <span className="text-primary">MAV_MSG_ID_GPS_RAW_INT</span>: lat=407127840, lon=-740059410, alt=12000, fix_type=3</div>
                            <div className="text-slate-500 mb-1">[14:22:01] <span className="text-primary">MAV_MSG_ID_ATTITUDE</span>: roll=0.02, pitch=-0.01, yaw=1.57</div>
                            <div className="text-slate-500 mb-1">[14:22:02] <span className="text-primary">MAV_MSG_ID_SYS_STATUS</span>: sensors_enabled=0x000F, load=450</div>
                            <div className="text-slate-500 mb-1">[14:22:02] <span className="text-yellow-500">MAV_MSG_ID_STATUSTEXT</span>: "HEARTBEAT active: Protocol ELRS 3.0"</div>
                            <div className="text-slate-500 mb-1">[14:22:03] <span className="text-primary">MAV_MSG_ID_BATTERY_STATUS</span>: volt=16.8, curr=12.5, remain=88</div>
                            <div className="text-slate-500 mb-1">[14:22:03] <span className="text-primary">MAV_MSG_ID_RC_CHANNELS</span>: [1500, 1500, 1000, 1500, 2000, 1500]</div>
                            <div className="text-slate-500 mb-1">[14:22:04] <span className="text-primary">MAV_MSG_ID_VFR_HUD</span>: air_speed=12.4, ground_speed=14.1, heading=90</div>
                            <div className="text-slate-500 mb-1">[14:22:04] <span className="text-red-400">MAV_MSG_ID_STATUSTEXT</span>: "WARNING: High interference detected on 2.4GHz"</div>
                            <div className="text-slate-500 mb-1">[14:22:05] <span className="text-primary">MAV_MSG_ID_HEARTBEAT</span>: type=2, autopilot=3, base_mode=81</div>
                            <div className="animate-pulse text-primary mt-2">_</div>
                        </div>

                        <div className="p-3 border-t border-border-dark bg-panel-dark flex gap-3">
                            <div className="flex-1 relative">
                                <span className="absolute left-3 top-1/2 -translate-y-1/2 text-primary font-mono text-xs font-bold">&gt;</span>
                                <input
                                    className="w-full bg-background-dark border-none focus:ring-1 focus:ring-primary rounded-lg pl-8 text-xs font-mono py-2 text-slate-200 outline-none"
                                    placeholder="Enter MAVLink command..."
                                    type="text"
                                />
                            </div>
                            <button className="bg-primary hover:bg-primary-dark transition-colors px-6 py-2 rounded-lg text-black text-xs font-bold uppercase tracking-wider">Send</button>
                        </div>
                    </div>
                </div>
            </main>

            {/* Footer Stats Grid */}
            <footer className="fixed bottom-0 w-full lg:w-[calc(100%-256px)] right-0 bg-panel-dark/80 backdrop-blur-md border-t border-border-dark px-10 py-2.5 flex items-center justify-between text-[10px] font-bold text-slate-400 uppercase tracking-widest z-50">
                <div className="flex gap-8">
                    <div className="flex items-center gap-2">
                        <div className="w-2 h-2 rounded-full bg-green-500"></div> LINK: ELRS 3.2.1
                    </div>
                    <div className="flex items-center gap-2">
                        <div className="w-2 h-2 rounded-full bg-primary animate-pulse"></div> TELEM: ACTIVE (14kbps)
                    </div>
                    <div className="flex items-center gap-2">
                        <div className="w-2 h-2 rounded-full bg-slate-600"></div> UART: 420,000 BAUD
                    </div>
                </div>
                <div className="flex items-center gap-6">
                    <span>Uptime: <span className="font-mono">04:22:12</span></span>
                    <span className="text-primary">Lat: <span className="font-mono">2.4ms</span> (AVG)</span>
                </div>
            </footer>
        </div>
    );
}
