import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { apiClient } from '@/lib/api-client';
import { Tenant, Conversation, ChatMessage, DocumentInfo, User } from '@/types';

interface AppState {
  // Authentication
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;

  // Tenants
  tenants: Tenant[];
  currentTenant: Tenant | null;
  addTenant: (tenant: Tenant) => void;
  removeTenant: (tenantId: string) => void;
  setCurrentTenant: (tenant: Tenant | null) => void;

  // Conversations
  conversations: Conversation[];
  currentConversation: Conversation | null;
  createConversation: (tenantId: string) => Conversation;
  setCurrentConversation: (conversation: Conversation | null) => void;
  addMessage: (conversationId: string, message: ChatMessage) => void;
  updateMessage: (conversationId: string, messageId: string, updates: Partial<ChatMessage>) => void;
  deleteConversation: (conversationId: string) => void;
  clearConversations: () => void;

  // Documents
  documents: DocumentInfo[];
  addDocument: (doc: DocumentInfo) => void;
  updateDocument: (docId: string, updates: Partial<DocumentInfo>) => void;
  removeDocument: (docId: string) => void;

  // UI State
  sidebarOpen: boolean;
  toggleSidebar: () => void;
}

export const useAppStore = create<AppState>()(
  persist(
    (set, _get) => ({
      // Authentication
      user: null,
      token: null,
      isAuthenticated: false,

      login: async (username: string, password: string) => {
        try {
          const response = await apiClient.post<{
            token: string;
            userId: string;
            username: string;
            tenantId: string;
            tenantName: string;
            role: string;
            tier: string;
          }>('/api/v1/authentication/login', { username, password });

          const { token, userId, username: userName, tenantId, tenantName, role, tier } = response;

          const user: User = {
            id: userId,
            username: userName,
            role,
            tier,
          };

          const tenant: Tenant = {
            id: tenantId,
            name: tenantName,
            apiKey: 'jwt', // No longer needed with JWT
            color: '#0ea5e9',
          };

          // Set token in API client
          apiClient.setToken(token);
          apiClient.setTenantId(tenantId);

          set({
            user,
            token,
            isAuthenticated: true,
            currentTenant: tenant,
            tenants: [tenant],
          });
        } catch (error: any) {
          const errorMessage = error.response?.data?.error || 'Invalid username or password';
          throw new Error(errorMessage);
        }
      },

      logout: () => {
        apiClient.setToken(null);
        apiClient.setTenantId(null);
        set({
          user: null,
          token: null,
          isAuthenticated: false,
          currentTenant: null,
          conversations: [],
          currentConversation: null,
          documents: [],
        });
      },

      // Tenants
      tenants: [{ id: 'tenant-mayank', name: 'Mayank', apiKey: 'secure_password' }],
      currentTenant: { id: 'tenant-mayank', name: 'Mayank', apiKey: 'secure_password' },
      
      addTenant: (tenant) => {
        set((state) => ({
          tenants: [...state.tenants, tenant],
        }));
      },

      removeTenant: (tenantId) => {
        set((state) => ({
          tenants: state.tenants.filter((t) => t.id !== tenantId),
          currentTenant:
            state.currentTenant?.id === tenantId ? null : state.currentTenant,
        }));
      },

      setCurrentTenant: (tenant) => {
        set({ currentTenant: tenant });
        apiClient.setTenantId(tenant?.id || null);
      },

      // Conversations
      conversations: [],
      currentConversation: null,

      createConversation: (tenantId) => {
        const conversation: Conversation = {
          id: `conv-${Date.now()}`,
          title: 'New Chat',
          messages: [],
          createdAt: new Date(),
          updatedAt: new Date(),
          tenantId,
        };
        set((state) => ({
          conversations: [conversation, ...state.conversations],
          currentConversation: conversation,
        }));
        return conversation;
      },

      setCurrentConversation: (conversation) => {
        set({ currentConversation: conversation });
      },

      addMessage: (conversationId, message) => {
        set((state) => {
          const conversations = state.conversations.map((conv) => {
            if (conv.id === conversationId) {
              const updatedMessages = [...conv.messages, message];
              // Update title from first user message
              const title =
                conv.messages.length === 0 && message.role === 'user'
                  ? message.content.slice(0, 50) + (message.content.length > 50 ? '...' : '')
                  : conv.title;
              return {
                ...conv,
                messages: updatedMessages,
                title,
                updatedAt: new Date(),
              };
            }
            return conv;
          });

          return {
            conversations,
            currentConversation: conversations.find((c) => c.id === conversationId) || state.currentConversation,
          };
        });
      },

      updateMessage: (conversationId, messageId, updates) => {
        set((state) => {
          const conversations = state.conversations.map((conv) => {
            if (conv.id === conversationId) {
              return {
                ...conv,
                messages: conv.messages.map((msg) =>
                  msg.id === messageId ? { ...msg, ...updates } : msg
                ),
                updatedAt: new Date(),
              };
            }
            return conv;
          });

          return {
            conversations,
            currentConversation: conversations.find((c) => c.id === conversationId) || state.currentConversation,
          };
        });
      },

      deleteConversation: (conversationId) => {
        set((state) => ({
          conversations: state.conversations.filter((c) => c.id !== conversationId),
          currentConversation:
            state.currentConversation?.id === conversationId
              ? null
              : state.currentConversation,
        }));
      },

      clearConversations: () => {
        set({ conversations: [], currentConversation: null });
      },

      // Documents
      documents: [],

      addDocument: (doc) => {
        set((state) => ({
          documents: [...state.documents, doc],
        }));
      },

      updateDocument: (docId, updates) => {
        set((state) => ({
          documents: state.documents.map((doc) =>
            doc.id === docId ? { ...doc, ...updates } : doc
          ),
        }));
      },

      removeDocument: (docId) => {
        set((state) => ({
          documents: state.documents.filter((doc) => doc.id !== docId),
        }));
      },

      // UI State
      sidebarOpen: true,
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
    }),
    {
      name: 'rag-app-storage',
      partialize: (state) => ({
        user: state.user,
        token: state.token,
        isAuthenticated: state.isAuthenticated,
        tenants: state.tenants,
        currentTenant: state.currentTenant,
        conversations: state.conversations,
      }),
      onRehydrateStorage: () => (state) => {
        // Restore token in API client after rehydration
        if (state?.token) {
          apiClient.setToken(state.token);
        }
        if (state?.currentTenant?.id) {
          apiClient.setTenantId(state.currentTenant.id);
        }
      },
    }
  )
);