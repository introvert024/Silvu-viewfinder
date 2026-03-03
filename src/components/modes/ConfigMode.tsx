import React from 'react';
import { cn } from '../../lib/utils';
import {
    Build,
    SettingsInputComponent,
    PrecisionManufacturing,
    BugReport,
    Terminal,
    Science,
    Lan,
    Monitoring,
    ShowChart,
    Insights
} from 'lucide-react'; // Some of these icons don't exist in standard lucide, mapping them to closest

export default function ConfigMode() {
    return (
        <div className="flex-1 flex overflow-hidden">
            {/* Sidebar Navigation */}
            <aside className="w-64 border-r border-border-dark flex flex-col gap-2 p-4 hidden md:flex shrink-0">
                <div className="mb-6 px-2">
                    <h1 className="text-white text-sm font-bold uppercase tracking-widest opacity-50">Engineering Mode</h1>
                </div>
                {[
                    { icon: 'build', label: 'Build' },
                    { icon: 'settings_input_component', label: 'Config', active: true },
                    { icon: 'precision_manufacturing', label: 'Simulation' },
                    { icon: 'bug_report', label: 'Debug' },
                    { icon: 'terminal', label: 'Software' },
                    { icon: 'science', label: 'Testing' },
                    { icon: 'lan', label: 'Protocols' },
                    { icon: 'monitoring', label: 'Telemetry' },
                ].map(item => (
                    <a
                        key={item.label}
                        href="#"
                        className={cn(
                            "flex items-center gap-3 px-3 py-2 rounded-lg transition-all",
                            item.active ? "bg-primary/10 text-primary" : "text-slate-400 hover:bg-neutral-dark"
                        )}
                    >
                        {/* Falling back to a simple dot as Material Symbols are not in Lucide out of the box without massive import maps */}
                        <span className="w-2 h-2 rounded-full bg-current opacity-70"></span>
                        <span className="text-sm font-medium">{item.label}</span>
                    </a>
                ))}
            </aside>

            {/* Main Content Area */}
            <main className="flex-1 flex flex-col overflow-y-auto max-h-[calc(100vh-[104px])] custom-scrollbar">
                {/* Sub-tabs */}
                <div className="px-8 border-b border-border-dark flex gap-8 shrink-0">
                    <a className="border-b-2 border-primary text-primary px-1 py-4 text-xs font-bold uppercase tracking-wider" href="#">PID Tuning</a>
                    <a className="border-b-2 border-transparent text-slate-500 hover:text-slate-300 px-1 py-4 text-xs font-bold uppercase tracking-wider transition-colors" href="#">Filters</a>
                    <a className="border-b-2 border-transparent text-slate-500 hover:text-slate-300 px-1 py-4 text-xs font-bold uppercase tracking-wider transition-colors" href="#">Rates & Expo</a>
                </div>

                <div className="p-8 grid grid-cols-12 gap-6 pb-20">
                    {/* Left Column: PID Adjustments */}
                    <div className="col-span-12 lg:col-span-7 flex flex-col gap-8">
                        {/* PID Group: Roll */}
                        <section className="bg-panel-dark/40 rounded-xl border border-border-dark p-6">
                            <div className="flex items-center justify-between mb-6">
                                <h3 className="text-white font-bold flex items-center gap-2">
                                    <span className="w-2 h-2 rounded-full bg-red-500"></span>
                                    Roll Axis
                                </h3>
                                <span className="text-[10px] text-slate-500 bg-border-dark px-2 py-0.5 rounded font-mono uppercase">Master Multiplier: 1.0x</span>
                            </div>
                            <div className="space-y-6">
                                {/* P Gain */}
                                <div className="space-y-2">
                                    <div className="flex justify-between text-xs font-mono uppercase text-slate-400">
                                        <span>Proportional (P)</span>
                                        <span className="text-primary font-bold">45</span>
                                    </div>
                                    <div className="flex items-center gap-4">
                                        <div className="h-1.5 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <div className="absolute inset-y-0 left-0 bg-primary rounded-full w-[45%]"></div>
                                        </div>
                                        <input readOnly className="w-12 h-7 bg-background-dark border border-border-dark text-[11px] text-center rounded focus:ring-1 focus:ring-primary focus:border-primary outline-none text-slate-200" type="text" value="45" />
                                    </div>
                                </div>

                                {/* I Gain */}
                                <div className="space-y-2">
                                    <div className="flex justify-between text-xs font-mono uppercase text-slate-400">
                                        <span>Integral (I)</span>
                                        <span className="text-primary font-bold">85</span>
                                    </div>
                                    <div className="flex items-center gap-4">
                                        <div className="h-1.5 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <div className="absolute inset-y-0 left-0 bg-primary rounded-full w-[85%]"></div>
                                        </div>
                                        <input readOnly className="w-12 h-7 bg-background-dark border border-border-dark text-[11px] text-center rounded focus:ring-1 focus:ring-primary focus:border-primary outline-none text-slate-200" type="text" value="85" />
                                    </div>
                                </div>

                                {/* D Gain */}
                                <div className="space-y-2">
                                    <div className="flex justify-between text-xs font-mono uppercase text-slate-400">
                                        <span>Derivative (D)</span>
                                        <span className="text-primary font-bold">32</span>
                                    </div>
                                    <div className="flex items-center gap-4">
                                        <div className="h-1.5 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <div className="absolute inset-y-0 left-0 bg-primary rounded-full w-[32%]"></div>
                                        </div>
                                        <input readOnly className="w-12 h-7 bg-background-dark border border-border-dark text-[11px] text-center rounded focus:ring-1 focus:ring-primary focus:border-primary outline-none text-slate-200" type="text" value="32" />
                                    </div>
                                </div>
                            </div>
                        </section>

                        {/* PID Group: Pitch */}
                        <section className="bg-panel-dark/40 rounded-xl border border-border-dark p-6">
                            <div className="flex items-center justify-between mb-6">
                                <h3 className="text-white font-bold flex items-center gap-2">
                                    <span className="w-2 h-2 rounded-full bg-red-500"></span>
                                    Pitch Axis
                                </h3>
                            </div>
                            <div className="space-y-6">
                                <div className="space-y-2">
                                    <div className="flex justify-between text-xs font-mono uppercase text-slate-400">
                                        <span>P / I / D Values</span>
                                        <span className="text-primary font-bold">48 / 90 / 35</span>
                                    </div>
                                    <div className="flex gap-2">
                                        <span className="h-1 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <span className="absolute inset-y-0 left-0 bg-primary rounded-full w-[48%]"></span>
                                        </span>
                                        <span className="h-1 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <span className="absolute inset-y-0 left-0 bg-primary rounded-full w-[90%]"></span>
                                        </span>
                                        <span className="h-1 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <span className="absolute inset-y-0 left-0 bg-primary rounded-full w-[35%]"></span>
                                        </span>
                                    </div>
                                </div>
                            </div>
                        </section>

                        {/* PID Group: Yaw */}
                        <section className="bg-panel-dark/40 rounded-xl border border-border-dark p-6">
                            <div className="flex items-center justify-between mb-6">
                                <h3 className="text-white font-bold flex items-center gap-2">
                                    <span className="w-2 h-2 rounded-full bg-red-500"></span>
                                    Yaw Axis
                                </h3>
                            </div>
                            <div className="space-y-6">
                                <div className="space-y-2">
                                    <div className="flex justify-between text-xs font-mono uppercase text-slate-400">
                                        <span>P / I / D Values</span>
                                        <span className="text-primary font-bold">70 / 110 / 0</span>
                                    </div>
                                    <div className="flex gap-2">
                                        <span className="h-1 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <span className="absolute inset-y-0 left-0 bg-primary rounded-full w-[70%]"></span>
                                        </span>
                                        <span className="h-1 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <span className="absolute inset-y-0 left-0 bg-primary rounded-full w-[95%]"></span>
                                        </span>
                                        <span className="h-1 flex-1 bg-border-dark rounded-full overflow-hidden relative">
                                            <span className="absolute inset-y-0 left-0 bg-primary rounded-full w-[0%]"></span>
                                        </span>
                                    </div>
                                </div>
                            </div>
                        </section>
                    </div>

                    {/* Right Column: Graphs & Status */}
                    <div className="col-span-12 lg:col-span-5 flex flex-col gap-6">
                        {/* Oscilloscope Preview */}
                        <div className="bg-[#080d10] rounded-xl border border-border-dark overflow-hidden flex flex-col h-[320px]">
                            <div className="p-4 border-b border-border-dark flex justify-between items-center bg-panel-dark/60">
                                <span className="text-[10px] font-bold uppercase tracking-widest text-slate-400 flex items-center gap-2">
                                    <span className="w-2 h-2 rounded bg-slate-600"></span>
                                    Live Response Graph
                                </span>
                                <div className="flex gap-3 text-[9px] font-mono">
                                    <div className="flex items-center gap-1"><span className="w-2 h-0.5 bg-primary"></span> SETPOINT</div>
                                    <div className="flex items-center gap-1"><span className="w-2 h-0.5 bg-red-500"></span> ACTUAL</div>
                                </div>
                            </div>
                            <div className="flex-1 relative bg-[linear-gradient(rgba(19,182,236,0.05)_1px,transparent_1px),linear-gradient(90deg,rgba(19,182,236,0.05)_1px,transparent_1px)] bg-[size:20px_20px]">
                                {/* SVG Oscilloscope Line Mockup */}
                                <svg className="absolute inset-0 w-full h-full" preserveAspectRatio="none" viewBox="0 0 500 300">
                                    <path d="M0,150 L50,150 L60,80 L100,220 L110,130 L150,150 L300,150 L310,90 L350,210 L360,150 L500,150" fill="none" stroke="#13b6ec" strokeWidth="2"></path>
                                    <path d="M0,152 L50,152 L65,90 L105,230 L120,140 L160,150 L300,150 L320,100 L365,220 L380,150 L500,150" fill="none" stroke="#ef4444" strokeDasharray="4" strokeWidth="2"></path>
                                </svg>
                            </div>
                        </div>

                        {/* Visual Curve Preview */}
                        <div className="bg-panel-dark/40 rounded-xl border border-border-dark p-6">
                            <h4 className="text-xs font-bold uppercase tracking-widest text-slate-400 mb-4 flex items-center gap-2">
                                <span className="w-2 h-2 rounded bg-slate-600"></span>
                                Rates & Expo Curve
                            </h4>
                            <div className="h-40 bg-background-dark/50 rounded border border-border-dark relative overflow-hidden">
                                <svg className="absolute inset-0 w-full h-full" preserveAspectRatio="none" viewBox="0 0 500 160">
                                    <path d="M0,160 Q100,160 250,80 T500,0" fill="none" stroke="#13b6ec" strokeWidth="3"></path>
                                </svg>
                            </div>
                            <div className="grid grid-cols-3 gap-2 mt-4">
                                <div className="bg-background-dark/80 p-2 rounded border border-border-dark">
                                    <span className="block text-[9px] text-slate-500 uppercase">Rate</span>
                                    <span className="text-xs font-mono font-bold text-slate-200">0.75</span>
                                </div>
                                <div className="bg-background-dark/80 p-2 rounded border border-border-dark">
                                    <span className="block text-[9px] text-slate-500 uppercase">Super</span>
                                    <span className="text-xs font-mono font-bold text-slate-200">0.68</span>
                                </div>
                                <div className="bg-background-dark/80 p-2 rounded border border-border-dark">
                                    <span className="block text-[9px] text-slate-500 uppercase">Expo</span>
                                    <span className="text-xs font-mono font-bold text-slate-200">0.12</span>
                                </div>
                            </div>
                        </div>

                        {/* Stability Score Card */}
                        <div className="bg-gradient-to-br from-[#162731] to-background-dark rounded-xl border border-primary/30 p-6">
                            <h4 className="text-xs font-bold uppercase tracking-widest text-primary mb-4">Stability Impact Score</h4>
                            <div className="flex items-end gap-4">
                                <div className="text-5xl font-bold text-white tracking-tighter">8.4</div>
                                <div className="flex-1 pb-2">
                                    <div className="h-2 w-full bg-border-dark rounded-full overflow-hidden">
                                        <div className="h-full bg-primary" style={{ width: '84%' }}></div>
                                    </div>
                                </div>
                            </div>
                            <p className="text-[11px] text-slate-300 mt-4 leading-relaxed font-medium">
                                Based on current PID values, the flight controller predicts high responsiveness with minimal oscillation. Recommended for freestyle operations.
                            </p>
                            <div className="mt-6 space-y-2">
                                <button className="w-full py-2.5 bg-primary text-black text-[11px] font-bold rounded hover:bg-white transition-all">
                                    Apply Recommended Tune
                                </button>
                                <button className="w-full py-2.5 border border-border-dark text-slate-400 text-[11px] font-bold rounded hover:text-white transition-all">
                                    Export CLI Config
                                </button>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Footer Status Bar */}
                <footer className="fixed bottom-0 w-full lg:w-[calc(100%-256px)] h-8 bg-panel-dark border-t border-border-dark px-6 flex items-center justify-between text-[10px] font-mono text-slate-500 z-20 shadow-[-10px_0_20px_#0f1619]">
                    <div className="flex gap-6">
                        <div className="flex items-center gap-1.5">
                            <span className="w-2 h-2 rounded-full bg-green-500 shadow-[0_0_8px_rgba(34,197,94,0.5)]"></span>
                            FC CONNECTED: MATEKH743
                        </div>
                        <div className="flex items-center gap-1.5">
                            CPU LOAD: 14%
                        </div>
                        <div className="flex items-center gap-1.5">
                            BATT: 16.4V
                        </div>
                    </div>
                    <div className="flex gap-6">
                        <span>FIRMWARE: VIEWFINDER v4.2.0</span>
                        <span className="text-primary/80 font-bold animate-pulse">SYNCING...</span>
                    </div>
                </footer>
            </main>
        </div>
    );
}
