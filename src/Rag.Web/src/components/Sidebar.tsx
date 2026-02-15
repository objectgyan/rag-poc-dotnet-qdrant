import React, { useState } from 'react';
import {
  Plus,
  MessageSquare,
  Trash2,
  Settings,
  ChevronLeft,
  Building2,
  Bot,
  LogOut,
  User,
} from 'lucide-react';
import { useAppStore } from '@/store/app-store';
import { TenantSwitcher } from './TenantSwitcher';
import { format } from 'date-fns';
import { cn } from '@/lib/utils';

export const Sidebar: React.FC = () => {
  const {
    user,
    conversations,
    currentConversation,
    currentTenant,
    setCurrentConversation,
    createConversation,
    deleteConversation,
    sidebarOpen,
    toggleSidebar,
    logout,
  } = useAppStore();

  const [showTenantSwitcher, setShowTenantSwitcher] = useState(false);

  const handleNewChat = () => {
    if (currentTenant) {
      const newConv = createConversation(currentTenant.id);
      setCurrentConversation(newConv);
    }
  };

  const tenantConversations = conversations.filter(
    (c) => c.tenantId === currentTenant?.id
  );

  const groupedByDate = tenantConversations.reduce((groups, conv) => {
    const date = format(conv.updatedAt, 'yyyy-MM-dd');
    if (!groups[date]) {
      groups[date] = [];
    }
    groups[date].push(conv);
    return groups;
  }, {} as Record<string, typeof conversations>);

  const getDateLabel = (dateStr: string) => {
    const today = format(new Date(), 'yyyy-MM-dd');
    const yesterday = format(new Date(Date.now() - 86400000), 'yyyy-MM-dd');
    if (dateStr === today) return 'Today';
    if (dateStr === yesterday) return 'Yesterday';
    return format(new Date(dateStr), 'MMM d, yyyy');
  };

  if (!sidebarOpen) {
    return (
      <button
        onClick={toggleSidebar}
        className="fixed left-4 top-4 z-50 p-2 bg-white border border-gray-200 rounded-lg shadow-sm hover:bg-gray-50"
      >
        <MessageSquare className="w-5 h-5" />
      </button>
    );
  }

  return (
    <>
      <div className="w-80 bg-gray-900 text-white flex flex-col h-screen">
        {/* Header */}
        <div className="p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h1 className="text-xl font-bold">RAG Assistant</h1>
            <button
              onClick={toggleSidebar}
              className="p-2 hover:bg-gray-800 rounded-lg transition-colors"
            >
              <ChevronLeft className="w-5 h-5" />
            </button>
          </div>

          {/* Tenant selector */}
          <button
            onClick={() => setShowTenantSwitcher(true)}
            className="w-full flex items-center gap-3 px-4 py-3 bg-gray-800 rounded-lg hover:bg-gray-700 transition-colors"
          >
            <Building2 className="w-5 h-5 flex-shrink-0" />
            <div className="flex-1 text-left min-w-0">
              <div className="text-sm font-medium truncate">
                {currentTenant?.name || 'Select Tenant'}
              </div>
              <div className="text-xs text-gray-400">
                {currentTenant ? 'Active workspace' : 'No workspace selected'}
              </div>
            </div>
          </button>

          {/* New chat button */}
          <button
            onClick={handleNewChat}
            disabled={!currentTenant}
            className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-primary-600 hover:bg-primary-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed font-medium"
          >
            <Plus className="w-5 h-5" />
            <span>New Chat</span>
          </button>

          {/* Agent mode indicator */}
          <div className="flex items-center gap-2 px-4 py-3 bg-gray-800 rounded-lg">
            <Bot className="w-5 h-5 text-purple-400" />
            <span className="text-sm font-medium">AI Agent Mode</span>
            <span className="ml-auto text-xs text-gray-400">RAG + Tools</span>
          </div>
        </div>

        {/* Conversations list */}
        <div className="flex-1 overflow-y-auto px-2">
          {tenantConversations.length === 0 ? (
            <div className="p-4 text-center text-gray-400 text-sm">
              No conversations yet. Start a new chat!
            </div>
          ) : (
            <div className="space-y-4">
              {Object.entries(groupedByDate)
                .sort(([a], [b]) => b.localeCompare(a))
                .map(([date, convs]) => (
                  <div key={date}>
                    <div className="px-3 py-1 text-xs font-medium text-gray-400 uppercase tracking-wider">
                      {getDateLabel(date)}
                    </div>
                    <div className="space-y-1 mt-1">
                      {convs.map((conv) => (
                        <div
                          key={conv.id}
                          className={cn(
                            'group relative flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer transition-colors',
                            currentConversation?.id === conv.id
                              ? 'bg-gray-800'
                              : 'hover:bg-gray-800'
                          )}
                          onClick={() => setCurrentConversation(conv)}
                        >
                          <MessageSquare className="w-4 h-4 flex-shrink-0 text-gray-400" />
                          <span className="flex-1 text-sm truncate">{conv.title}</span>
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              deleteConversation(conv.id);
                            }}
                            className="opacity-0 group-hover:opacity-100 p-1 hover:bg-gray-700 rounded transition-opacity"
                          >
                            <Trash2 className="w-4 h-4 text-gray-400 hover:text-red-400" />
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-gray-800 space-y-2">
          {/* User info */}
          <div className="flex items-center gap-3 px-4 py-2 text-gray-300">
            <User className="w-5 h-5" />
            <div className="flex-1 min-w-0">
              <div className="text-sm font-medium truncate">{user?.username}</div>
              <div className="text-xs text-gray-400">{user?.tier} • {user?.role}</div>
            </div>
          </div>
          
          {/* Logout button */}
          <button 
            onClick={logout}
            className="w-full flex items-center gap-3 px-4 py-3 hover:bg-gray-800 rounded-lg transition-colors text-gray-300 hover:text-red-400"
          >
            <LogOut className="w-5 h-5" />
            <span className="text-sm">Logout</span>
          </button>
        </div>
      </div>

      {/* Tenant switcher modal */}
      {showTenantSwitcher && (
        <TenantSwitcher onClose={() => setShowTenantSwitcher(false)} />
      )}
    </>
  );
};
