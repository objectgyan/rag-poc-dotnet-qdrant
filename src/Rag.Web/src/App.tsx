import { useEffect, useState } from 'react';
import { Sidebar } from './components/Sidebar';
import { ChatInterface } from './components/ChatInterface';
import { StreamingChat } from './components/StreamingChat';
import { Login } from './components/Login';
import { useAppStore } from './store/app-store';
import { apiClient } from './lib/api-client';

function App() {
  const { isAuthenticated, currentTenant } = useAppStore();
  const [viewMode, setViewMode] = useState<'chat' | 'streaming'>('chat');

  useEffect(() => {
    // Set initial tenant in API client
    if (currentTenant) {
      apiClient.setTenantId(currentTenant.id);
    }
  }, [currentTenant]);

  // Show login if not authenticated
  if (!isAuthenticated) {
    return <Login />;
  }

  return (
    <div className="flex h-screen bg-gray-50">
      <Sidebar />
      <div className="flex-1 flex flex-col">
        {/* View Mode Tabs */}
        <div className="bg-white border-b border-gray-200 px-6 py-3 flex gap-4 flex-shrink-0">
          <button
            onClick={() => setViewMode('chat')}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              viewMode === 'chat'
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
          >
            💬 Standard Chat
          </button>
          <button
            onClick={() => setViewMode('streaming')}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              viewMode === 'streaming'
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
          >
            ⚡ Streaming Chat (Phase 8)
          </button>
        </div>

        {/* Content Area */}
        {viewMode === 'chat' ? <ChatInterface /> : <StreamingChat />}
      </div>
    </div>
  );
}

export default App;
