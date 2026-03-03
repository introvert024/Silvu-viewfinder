import React from 'react';
import { cn } from '../lib/utils';
import { Search, User } from 'lucide-react';

interface TopNavProps {
    activeTab: string;
    setActiveTab: (tab: string) => void;
}

const TABS = ['Build', 'Config', 'Simulation', 'Debug', 'Software', 'Testing', 'Protocols', 'Telemetry'];
const APP_MENU = ['File', 'Edit', 'View', 'Assets', 'Settings', 'Data', 'Education', 'Workspace'];

export default function TopNav({ activeTab, setActiveTab }: TopNavProps) {
    return (
        <div className="flex flex-col shrink-0 z-50">
            {/* OS Menu Bar Replacement / App Menu */}
            <div className="h-8 bg-black/60 border-b border-border-dark flex items-center px-4 w-full select-none" data-tauri-drag-region>
                <div className="flex items-center gap-5 text-[11px] font-medium text-slate-300 pointer-events-none">
                    {APP_MENU.map(item => (
                        <div key={item} className="pointer-events-auto hover:text-white cursor-default">{item}</div>
                    ))}
                </div>
            </div>

            {/* Main Header Area */}
            <header className="flex items-center justify-between border-b border-border-dark bg-panel-dark px-6 py-3">
                <div className="flex items-center gap-8">
                    <div className="flex items-center gap-4">
                        <img src="/src/assets/logo.png" alt="SILVU" className="h-6 object-contain" />
                        <h2 className="text-xl font-bold tracking-tighter">
                            <span className="text-[#13b6ec]">VIEWFINDER</span>
                        </h2>
                    </div>
                    <nav className="flex items-center gap-1">
                        {TABS.map((tab) => (
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
                    <div className="w-10 h-10 rounded-full border border-border-dark flex items-center justify-center cursor-pointer hover:bg-border-dark transition-colors overflow-hidden">
                        {/* Use fake profile pic from user prompt if available, fallback to icon */}
                        <div className="bg-center bg-no-repeat bg-cover w-full h-full" style={{ backgroundImage: 'url("https://lh3.googleusercontent.com/aida-public/AB6AXuCRsorsDmeRn6GU9xaO02LpmWr_Bild9rfAF_KteLFMgNnSlzPxuX1JsZ9vN9ZLQRjOMArJFh9p5E2dFuuJBJGEewKVzqSvzlEzZvli25kwCIWc4ymwRWYQxBZ7dctc9CyeYjWhA7jqDg2y-i9zuKBZKxkynTVymQQ1xSVULH9SJEqb3hWCRptM2qWZELPNL8CaA2S--SR7rgyytXpDBrIv5b_XeE8mdBwhKz5G1Pv8OX8688XKQdnn43wNehSg-8_1MQBJgtkLvjQI")' }}></div>
                    </div>
                </div>
            </header>
        </div>
    );
}
