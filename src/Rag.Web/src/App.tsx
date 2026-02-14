import React, { useEffect } from 'react';
import { Sidebar } from './components/Sidebar';
import { ChatInterface } from './components/ChatInterface';
import { Login } from './components/Login';
import { useAppStore } from './store/app-store';
import { apiClient } from './lib/api-client';

function App() {
  const { isAuthenticated, currentTenant } = useAppStore();

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
        <ChatInterface />
      </div>
    </div>
  );
}

export default App;
