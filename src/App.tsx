import React, { useState } from 'react';
import TopNav from './components/layout/TopNav';
import BuildMode from './components/modes/BuildMode';
import ProtocolsMode from './components/modes/ProtocolsMode';
import ConfigMode from './components/modes/ConfigMode';

export default function App() {
  const [activeTab, setActiveTab] = useState('Build');

  return (
    <div className="flex flex-col h-screen bg-background-dark text-slate-100 overflow-hidden font-sans select-none">
      <TopNav activeTab={activeTab} setActiveTab={setActiveTab} />

      {activeTab === 'Build' && <BuildMode />}
      {activeTab === 'Protocols' && <ProtocolsMode />}
      {activeTab === 'Config' && <ConfigMode />}

      {/* Fallback for unbuilt tabs */}
      {!['Build', 'Protocols', 'Config'].includes(activeTab) && (
        <div className="flex-1 flex items-center justify-center text-slate-500 font-bold tracking-widest uppercase">
          {activeTab} Module Not Found
        </div>
      )}
    </div>
  );
}
