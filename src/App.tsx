import { useState } from 'react';
import {
  Hexagon,
  Zap,
  Battery,
  Search,
  User,
  ChevronDown,
  Plus,
  Video,
  Layers,
  Box as LucideBox,
  ArrowUp
} from 'lucide-react';
import { motion } from 'motion/react';
import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';
import { Canvas } from '@react-three/fiber';
import { OrbitControls, Box } from '@react-three/drei';

function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export default function App() {
  const [activeTab, setActiveTab] = useState('Build');

  return (
    <div className="flex flex-col h-screen bg-background-dark text-slate-100 overflow-hidden font-sans">
      {/* Header */}
      <header className="flex items-center justify-between border-b border-border-dark bg-panel-dark px-6 py-3 shrink-0 z-10">
        <div className="flex items-center gap-8">
          <div className="flex items-center gap-4">
            <img src="/src/assets/logo.png" alt="SILVU" className="h-6 object-contain" />
            <h2 className="text-xl font-bold tracking-tighter">
              <span className="text-[#13b6ec]">VIEWFINDER</span>
            </h2>
          </div>
          <nav className="flex items-center gap-1">
            {['Build', 'Config', 'Simulation', 'Debug', 'Software', 'Testing', 'Protocols', 'Telemetry'].map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className={cn(
                  "px-4 py-1.5 text-sm font-semibold transition-colors rounded uppercase tracking-wider",
                  activeTab === tab
                    ? "bg-primary text-white"
                    : "text-slate-400 hover:text-slate-100"
                )}
              >
                {tab}
              </button>
            ))}
          </nav>
        </div>

        <div className="flex items-center gap-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 w-4 h-4" />
            <input
              type="text"
              placeholder="Search components..."
              className="bg-border-dark border-none rounded pl-10 pr-4 py-1.5 text-sm w-64 focus:ring-1 focus:ring-primary text-slate-100 outline-none"
            />
          </div>
          <button className="bg-primary hover:bg-primary-dark text-white px-6 py-1.5 rounded text-sm font-bold flex items-center gap-2 transition-all shadow-lg shadow-primary/20">
            Export<br />CAD
          </button>
          <div className="w-10 h-10 rounded-full border border-border-dark flex items-center justify-center cursor-pointer hover:bg-border-dark transition-colors">
            <User className="text-primary w-5 h-5" />
          </div>
        </div>
      </header>

      <main className="flex flex-1 overflow-hidden">
        {/* Left Sidebar */}
        <aside className="w-80 border-r border-border-dark flex flex-col bg-panel-dark shrink-0">
          <div className="p-5">
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#13b6ec] mb-1">Build Structure</h3>
            <div className="flex items-center justify-between mt-2">
              <span className="text-lg font-bold text-slate-100 tracking-tight">Carbon Fiber X-8</span>
              <span className="text-[10px] text-primary border border-primary/30 px-2 py-0.5 rounded font-bold tracking-wider">ACTIVE</span>
            </div>
          </div>

          <div className="flex-1 overflow-y-auto custom-scrollbar px-3 space-y-1">
            {/* Frame Component */}
            <div className="p-3 rounded-lg border border-primary/40 bg-primary/5">
              <div className="flex items-center gap-3">
                <Hexagon className="text-primary w-5 h-5 flex-shrink-0" />
                <span className="text-base font-bold text-slate-100 tracking-tight">Frame</span>
                <ChevronDown className="ml-auto w-4 h-4 text-slate-500" />
              </div>
              <div className="pl-8 mt-1">
                <span className="text-xs text-slate-400 italic">Carbon Fiber X-8</span>
                <div className="mt-3 grid grid-cols-2 gap-2">
                  <div className="bg-background-dark/80 p-2 rounded border border-border-dark">
                    <p className="text-[9px] text-slate-500 uppercase font-bold tracking-widest">Mass</p>
                    <p className="text-xs font-bold text-primary mt-1 tracking-tight">1.2kg total</p>
                  </div>
                  <div className="bg-background-dark/80 p-2 rounded border border-border-dark">
                    <p className="text-[9px] text-slate-500 uppercase font-bold tracking-widest">Stiffness</p>
                    <p className="text-xs font-bold text-primary mt-1 tracking-tight">92GPa</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Other Components */}
            <div className="p-3 border-b border-border-dark/50">
              <div className="flex flex-col gap-1">
                <div className="flex items-center gap-3">
                  <Settings className="text-slate-400 w-5 h-5 flex-shrink-0" />
                  <span className="text-base font-bold text-slate-300 tracking-tight">Motors</span>
                </div>
                <div className="pl-8">
                  <span className="text-xs text-slate-400">T-Motor F60 Pro × 8</span>
                  <p className="text-xs text-slate-500 mt-0.5">2200KV</p>
                </div>
              </div>
            </div>

            <div className="p-3 border-b border-border-dark/50">
              <div className="flex flex-col gap-1">
                <div className="flex items-center gap-3">
                  <Zap className="text-slate-400 w-5 h-5 flex-shrink-0" />
                  <span className="text-base font-bold text-slate-300 tracking-tight">ESCs</span>
                </div>
                <div className="pl-8">
                  <span className="text-xs text-slate-400">Hobbywing 60A 4-in-1</span>
                </div>
              </div>
            </div>

            <div className="p-3 opacity-50">
              <div className="flex flex-col gap-1">
                <div className="flex items-center gap-3">
                  <Battery className="text-slate-400 w-5 h-5 flex-shrink-0" />
                  <span className="text-base font-bold text-slate-300 tracking-tight">Battery</span>
                </div>
              </div>
            </div>
          </div>

          <div className="p-5 border-t border-border-dark space-y-5 bg-background-dark/40">
            {/* Mass Breakdown */}
            <div>
              <div className="flex justify-between items-center mb-3">
                <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#13b6ec]">Mass Breakdown</h4>
                <ChevronDown className="w-3 h-3 text-slate-500" />
              </div>
              <div className="space-y-1.5 text-xs text-slate-400 font-medium">
                <div className="flex justify-between"><span>Frame</span><span className="text-slate-200">210g</span></div>
                <div className="flex justify-between"><span>Motors</span><span className="text-slate-200">160g</span></div>
                <div className="flex justify-between"><span>ESC</span><span className="text-slate-200">35g</span></div>
                <div className="flex justify-between"><span>Battery</span><span className="text-slate-200">120g</span></div>
                <div className="flex justify-between"><span>Payload</span><span className="text-slate-200">57g</span></div>
                <div className="flex justify-between font-bold text-slate-100 pt-1 mt-1 border-t border-border-dark">
                  <span>Total Mass</span><span>582g</span>
                </div>
              </div>
            </div>

            {/* Payload Status */}
            <div>
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#13b6ec] mb-3">Payload Status</h4>
              <div className="space-y-1.5 text-xs text-slate-400 font-medium">
                <div className="flex justify-between"><span>Max Payload (Safe)</span><span className="text-slate-200">450g</span></div>
                <div className="flex justify-between"><span>Current Payload</span><span className="text-primary">57g</span></div>
                <div className="flex justify-between"><span>Remaining</span><span className="text-[#10b981]">393g</span></div>
              </div>
            </div>

            <button className="w-full py-2.5 mt-2 border border-border-dark rounded text-xs font-bold text-slate-400 hover:text-slate-200 hover:border-slate-500 transition-all flex items-center justify-center gap-2">
              <Plus className="w-4 h-4" />
              Add Component
            </button>
          </div>
        </aside>

        {/* Center Panel: Viewport */}
        <section className="flex-1 flex flex-col bg-background-dark relative grid-bg overflow-hidden">
          {/* HUD Overlay Top Left */}
          <div className="absolute top-5 left-5 flex gap-3 z-10">
            <div className="bg-panel-dark/80 backdrop-blur-md border border-border-dark px-3 py-1.5 rounded flex items-center gap-2 text-[10px] font-bold text-slate-300">
              <span className="w-2 h-2 rounded-full bg-primary animate-pulse shadow-[0_0_8px_#f04242]"></span>
              PHYSICS ENGINE ACTIVE
            </div>
            <div className="bg-panel-dark/80 backdrop-blur-md border border-border-dark px-3 py-1.5 rounded flex items-center gap-2 text-[10px] font-bold text-slate-300 uppercase">
              MODEL: LOD 0
            </div>
          </div>

          {/* HUD Overlay Top Right */}
          <div className="absolute top-5 right-5 flex flex-col gap-2 z-10">
            {[Video, Layers, LucideBox].map((Icon, idx) => (
              <button key={idx} className="bg-panel-dark/80 backdrop-blur-md border border-border-dark p-2.5 rounded text-slate-400 hover:text-slate-200 hover:bg-border-dark transition-all">
                <Icon className="w-4 h-4" />
              </button>
            ))}
          </div>

          {/* 3D Visualization Area */}
          <div className="flex-1 flex items-center justify-center relative">
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.6, ease: "easeOut" }}
              className="relative w-full max-w-[600px] aspect-square flex items-center justify-center max-h-[80vh]"
            >
              {/* Massive Faint Red Diamond Frame Background */}
              <div className="absolute inset-0 border-[1.5px] border-primary/10 rounded-3xl scale-[0.8] rotate-45 pointer-events-none z-0"></div>

              {/* WebGL Canvas Background */}
              <div className="absolute inset-0 z-0 scale-[1.2]">
                <Canvas camera={{ position: [0, 5, 5], fov: 50 }}>
                  <ambientLight intensity={0.5} />
                  <directionalLight position={[10, 10, 5]} intensity={1} />
                  <OrbitControls makeDefault enableZoom={false} enablePan={false} autoRotate autoRotateSpeed={0.5} />
                  {/* Simple placeholder drone structure for WebGL */}
                  <group rotation={[0, Math.PI / 4, 0]}>
                    <Box args={[4, 0.2, 0.2]} position={[0, 0, 0]}>
                      <meshStandardMaterial color="#213038" metalness={0.8} roughness={0.2} />
                    </Box>
                    <Box args={[0.2, 0.2, 4]} position={[0, 0, 0]}>
                      <meshStandardMaterial color="#213038" metalness={0.8} roughness={0.2} />
                    </Box>
                    <mesh position={[0, 0.2, 0]}>
                      <cylinderGeometry args={[0.4, 0.4, 0.2, 32]} />
                      <meshStandardMaterial color="#233f48" metalness={0.9} roughness={0.1} />
                    </mesh>
                  </group>
                  <gridHelper args={[10, 10, '#f04242', '#1e2d33']} position={[0, -1, 0]} />
                </Canvas>
              </div>

              {/* Drone Body Simulation */}
              <div className="absolute w-[80%] h-12 bg-[#213038]/20 backdrop-blur-sm rounded-full rotate-45 flex items-center justify-between px-2 pointer-events-none"></div>
              <div className="absolute w-[80%] h-12 bg-[#213038]/20 backdrop-blur-sm rounded-full -rotate-45 flex items-center justify-between px-2 pointer-events-none"></div>

              {/* Center Details */}
              <div className="absolute flex flex-col items-center justify-center gap-2 z-20">
                <div className="text-[10px] text-primary font-bold uppercase tracking-widest text-center leading-tight">
                  CG (COMPUTED)<br />
                  <span className="text-[8px] text-primary/60">GEOMETRIC CENTER</span>
                </div>
                <div className="relative flex items-center justify-center">
                  <div className="w-8 h-8 rounded-full border border-primary/50 relative"></div>
                  <div className="w-2 h-2 bg-[#13b6ec] rounded-full absolute shadow-[0_0_10px_#13b6ec]"></div>
                  <div className="w-1.5 h-1.5 border border-primary rounded-full absolute top-[28px]"></div>
                </div>
                <div className="text-[8px] text-[#10b981] font-bold tracking-widest uppercase bg-[#10b981]/10 px-2 py-0.5 rounded border border-[#10b981]/20 mt-4">
                  CG ALIGNMENT: STABLE
                </div>
              </div>

              {/* Motors */}
              {[
                { top: '22%', left: '22%' },
                { top: '22%', right: '22%' },
                { bottom: '22%', left: '22%' },
                { bottom: '22%', right: '22%' },
              ].map((pos, idx) => (
                <div
                  key={idx}
                  style={{ ...pos }}
                  className="absolute z-10 flex flex-col items-center"
                >
                  <ArrowUp className="text-primary w-6 h-6 mb-1" strokeWidth={2.5} />
                  {idx === 0 && <span className="absolute -top-6 text-[10px] text-primary font-bold">14.2N</span>}
                  <div className="w-14 h-14 bg-[#4B5E6B] rounded-xl border border-[#3A4A55] shadow-lg flex items-center justify-center">
                    <div className="w-12 h-12 rounded-full border border-[#3A4A55] opacity-50"></div>
                  </div>
                </div>
              ))}
            </motion.div>

            {/* Axis Legend Bottom Left */}
            <div className="absolute bottom-6 left-6 flex flex-col gap-2.5 font-bold tracking-widest text-[9px] uppercase">
              <div className="flex items-center gap-3"><span className="w-12 h-[2px] bg-primary"></span> <span className="text-primary">X (ROLL)</span></div>
              <div className="flex items-center gap-3"><span className="w-12 h-[2px] bg-[#10b981]"></span> <span className="text-[#10b981]">Y (PITCH)</span></div>
              <div className="flex items-center gap-3"><span className="w-12 h-[2px] bg-[#94a3b8]"></span> <span className="text-[#94a3b8]">Z (YAW)</span></div>
            </div>
          </div>

          {/* Bottom Console Panel */}
          <footer className="h-44 border-t border-border-dark bg-panel-dark px-6 py-4 flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#13b6ec]">Live Metrics & Physics Console</h3>
              <div className="flex items-center gap-4 text-[9px] font-bold tracking-widest">
                <span className="text-[#10b981] flex items-center gap-1.5 uppercase">
                  <span className="w-1 h-1 rounded-full bg-[#10b981] animate-pulse"></span> STREAMING: 120Hz
                </span>
                <span className="text-slate-500 uppercase">BUFFER: 24MB</span>
              </div>
            </div>

            <div className="flex gap-10">
              {/* Values Block */}
              <div className="flex-1 grid grid-cols-3 gap-8 pb-2">
                <MetricBar title="Voltage (V)" value="24.8V" color="#10b981" progress="85%" />
                <MetricBar title="Current (A)" value="12.4A" color="#f97316" progress="45%" />
                <MetricBar title="Motor RPM" value="12,400" color="#f04242" progress="65%" />
              </div>

              {/* Inertia Matrix Component */}
              <div className="w-[300px] border-l border-border-dark pl-10">
                <div className="flex justify-between items-center mb-2">
                  <span className="text-[10px] font-bold text-slate-300">Inertia Tensor Indices</span>
                  <span className="text-[9px] font-bold text-slate-500">kg·m²</span>
                </div>
                <div className="grid grid-cols-3 text-xs font-mono font-bold font-stretch-semi-expanded gap-y-1.5">
                  <div className="text-primary">0.012</div><div className="text-slate-100">0.000</div><div className="text-slate-100">0.000</div>
                  <div className="text-slate-100">0.000</div><div className="text-primary">0.015</div><div className="text-slate-100">0.000</div>
                  <div className="text-slate-100">0.000</div><div className="text-slate-100">0.000</div><div className="text-primary">0.024</div>
                </div>
              </div>
            </div>
          </footer>
        </section>

        {/* Right Sidebar: Health & Diagnostics */}
        <aside className="w-80 border-l border-border-dark bg-panel-dark flex flex-col shrink-0 overflow-y-auto custom-scrollbar">
          <div className="p-5">
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#13b6ec] mb-1">Health & Diagnostics</h3>
            <span className="text-lg font-bold text-slate-100 tracking-tight">Calculated Metrics</span>
          </div>

          <div className="px-5 pb-5 space-y-6">

            {/* Thrust Dynamics */}
            <div>
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-slate-500 mb-4 border-b border-border-dark pb-2">Thrust Dynamics</h4>
              <div className="space-y-3 text-xs font-bold text-slate-400">
                <div className="flex justify-between items-end">
                  <span>Hover Throttle</span>
                  <span className="text-[#10b981] font-mono text-sm">32% <span className="text-xs">Optimal</span></span>
                </div>
                <div className="flex justify-between items-end">
                  <span>Thrust Margin</span>
                  <span className="text-[#10b981] font-mono text-sm">4:1 <span className="text-xs">Optimal</span></span>
                </div>
                <div className="flex justify-between items-end">
                  <span>Max peak</span>
                  <span className="text-slate-200 font-mono text-sm">48.2kg</span>
                </div>
                <div className="flex justify-between items-end">
                  <span>Thermal Prediction</span>
                  <span className="text-primary font-mono text-sm">82°C <span className="text-xs">Warning</span></span>
                </div>
              </div>
              <div className="mt-3 p-2 bg-danger-bg border border-primary/20 rounded">
                <p className="text-[9px] text-primary/80 font-medium leading-relaxed">
                  Possible ESC throttling predicted in high-load maneuvers.
                </p>
              </div>
              <div className="flex justify-between items-end mt-4 pt-4 border-t border-border-dark/50">
                <span className="text-xs font-bold text-slate-400">Stability Index</span>
                <span className="text-[#13b6ec] font-mono font-bold text-sm tracking-tight">9.2/10 Stable</span>
              </div>
            </div>

            {/* System Health Boxes */}
            <div>
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-slate-500 mb-4 border-b border-border-dark pb-2">System Health</h4>
              <div className="grid grid-cols-2 gap-3">
                <div className="border border-[#10b981]/30 bg-[#10b981]/5 rounded flex flex-col items-center justify-center p-3 gap-1">
                  <span className="text-[9px] text-slate-500 uppercase font-bold tracking-widest">Link Qual</span>
                  <span className="text-[#10b981] font-bold text-lg font-mono">98%</span>
                </div>
                <div className="border border-primary/30 bg-primary/5 rounded flex flex-col items-center justify-center p-3 gap-1">
                  <span className="text-[9px] text-slate-500 uppercase font-bold tracking-widest">Vibes</span>
                  <span className="text-primary font-bold text-lg font-mono">LOW</span>
                </div>
              </div>
            </div>

            {/* Validation Console Output */}
            <div className="pt-2">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-slate-500 mb-3 border-b border-border-dark pb-2">Validation Console</h4>
              <div className="bg-background-dark rounded border border-border-dark p-3 font-mono text-[9px] leading-relaxed space-y-1 h-32 overflow-y-auto custom-scrollbar">
                <p className="text-slate-400">[12:44:01] Inertia tensor recalculated</p>
                <p className="text-slate-400">[12:44:02] CG offset: +1.2mm</p>
                <p className="text-slate-400">[12:44:05] Static thrust testing complete</p>
                <p className="text-primary font-bold bg-primary/10 -mx-1 px-1">[12:44:10] ERROR: ESC 3 heat soak alert</p>
                <p className="text-slate-400">[12:44:12] Monitoring thermals...</p>
              </div>
            </div>

          </div>
        </aside>
      </main>
    </div>
  );
}

// Custom simple setting icon as lucide 'Settings2' isn't matching perfectly
function Settings({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
      <path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"></path>
      <circle cx="12" cy="12" r="3"></circle>
    </svg>
  );
}

function MetricBar({ title, value, color, progress }: { title: string, value: string, color: string, progress: string }) {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex justify-between text-[11px] font-bold">
        <span className="text-slate-300">{title}</span>
        <span style={{ color }} className="font-mono">{value}</span>
      </div>
      <div className="h-1 bg-[#1e2d33] rounded w-full overflow-hidden mt-1">
        <div style={{ width: progress, backgroundColor: color }} className="h-full rounded shadow-[0_0_8px_currentColor]"></div>
      </div>
    </div>
  );
}
