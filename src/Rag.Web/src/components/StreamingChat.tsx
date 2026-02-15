import { useState, useCallback, FormEvent, useRef, useEffect } from 'react';
import { Paperclip } from 'lucide-react';
import { useSSE } from '../hooks/useSSE';
import { FileUpload } from './FileUpload';
import { ChatMessage } from './ChatMessage';
import { useAppStore } from '@/store/app-store';
import { generateId } from '@/lib/utils';
import { ChatMessage as ChatMessageType } from '@/types';

/**
 * Streaming RAG Chat Component (Phase 8).
 * Demonstrates token-by-token streaming using Server-Sent Events.
 */
export function StreamingChat() {
  const {
    currentConversation,
    currentTenant,
    addMessage,
    updateMessage,
    createConversation,
  } = useAppStore();
  
  const [question, setQuestion] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [sseUrl, setSseUrl] = useState<string | null>(null);
  const [showFileUpload, setShowFileUpload] = useState(false);
  const [streamingMessageId, setStreamingMessageId] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const conversationIdRef = useRef<string | null>(null);
  const streamingContentRef = useRef('');

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [currentConversation?.messages, streamingContent]);

  // SSE hook with callbacks
  const handleMessage = useCallback((data: any) => {
    if (data.token) {
      // Update local state immediately for real-time display
      setStreamingContent(prev => {
        const newContent = prev + data.token;
        streamingContentRef.current = newContent;
        return newContent;
      });
    }
  }, []);

  const handleComplete = useCallback(() => {
    console.log('Streaming completed, content length:', streamingContentRef.current.length);
    if (streamingMessageId && conversationIdRef.current) {
      // Save final content to store using ref (not stale closure)
      updateMessage(conversationIdRef.current, streamingMessageId, {
        content: streamingContentRef.current,
        isLoading: false,
      });
    }
    setIsStreaming(false);
    setSseUrl(null);
    setStreamingMessageId(null);
    setStreamingContent('');
    streamingContentRef.current = '';
    conversationIdRef.current = null;
  }, [streamingMessageId, updateMessage]);

  const handleError = useCallback((error: Error) => {
    console.error('Streaming error:', error);
    if (streamingMessageId && conversationIdRef.current) {
      const errorContent = streamingContentRef.current || `Error: ${error.message}`;
      updateMessage(conversationIdRef.current, streamingMessageId, {
        content: errorContent,
        isLoading: false,
      });
    }
    setIsStreaming(false);
    setSseUrl(null);
    setStreamingMessageId(null);
    setStreamingContent('');
    streamingContentRef.current = '';
    conversationIdRef.current = null;
  }, [streamingMessageId, updateMessage]);

  // Get auth headers
  const token = localStorage.getItem('token');
  const tenantId = currentTenant?.id || 'default-tenant';
  
  const headers: Record<string, string> = {
    'X-Tenant-Id': tenantId,
  };

  // Add either JWT token or API key
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  } else {
    headers['X-API-Key'] = 'secure_password'; // Fallback to default API key
  }

  const { isConnected } = useSSE(sseUrl, {
    onMessage: handleMessage,
    onComplete: handleComplete,
    onError: handleError,
    headers: headers,
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();

    if (!question.trim() || isStreaming || !currentTenant) return;

    // Create conversation if it doesn't exist
    let conversation = currentConversation;
    if (!conversation) {
      conversation = createConversation(currentTenant.id);
    }

    // Add user message
    const userMessage: ChatMessageType = {
      id: generateId(),
      role: 'user',
      content: question.trim(),
      timestamp: new Date(),
    };
    addMessage(conversation.id, userMessage);

    // Add empty assistant message that will be filled by streaming
    const assistantMessageId = generateId();
    const assistantMessage: ChatMessageType = {
      id: assistantMessageId,
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      isLoading: true,
    };
    addMessage(conversation.id, assistantMessage);
    setStreamingMessageId(assistantMessageId);
    
    // Store conversation ID and reset streaming content
    conversationIdRef.current = conversation.id;
    setStreamingContent('');
    streamingContentRef.current = '';

    // Start streaming
    setIsStreaming(true);
    const params = new URLSearchParams({
      question: question.trim(),
      topK: '5',
    });
    const url = `/api/v1/ask/stream?${params}`;
    setSseUrl(url);
    
    // Clear input
    setQuestion('');
  };

  const handleStop = () => {
    setSseUrl(null);
    setIsStreaming(false);
    if (streamingMessageId && conversationIdRef.current) {
      // Save current streaming content to store using ref
      updateMessage(conversationIdRef.current, streamingMessageId, {
        content: streamingContentRef.current,
        isLoading: false,
      });
    }
    setStreamingMessageId(null);
    setStreamingContent('');
    streamingContentRef.current = '';
    conversationIdRef.current = null;
  };

  const messages = currentConversation?.messages || [];
  
  // For display: replace streaming message content with live streaming content
  const displayMessages = messages.map(msg => {
    if (msg.id === streamingMessageId && isStreaming) {
      return { ...msg, content: streamingContent };
    }
    return msg;
  });

  return (
    <div className="flex-1 flex flex-col h-full">
      {/* Header */}
      <div className="flex-shrink-0 border-b border-gray-200 bg-white px-6 py-4">
        <h2 className="text-xl font-bold text-gray-900">
          ⚡ Streaming RAG Chat
        </h2>
        <p className="text-sm text-gray-600 mt-1">
          Token-by-token streaming with Server-Sent Events
        </p>
      </div>

      {/* Messages area */}
      <div className="flex-1 overflow-y-auto px-6">
        {displayMessages.length === 0 ? (
          <div className="flex items-center justify-center h-full text-center">
            <div className="max-w-md space-y-4">
              <h2 className="text-3xl font-bold text-gray-900">⚡ Streaming Chat</h2>
              <p className="text-gray-600">
                Ask questions and watch responses stream in real-time with lower perceived latency.
              </p>
              <div className="flex flex-wrap gap-2 justify-center">
                <button
                  onClick={() => setQuestion('What is RAG?')}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 text-sm"
                >
                  What is RAG?
                </button>
                <button
                  onClick={() => setQuestion('How does streaming work?')}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 text-sm"
                >
                  How does streaming work?
                </button>
              </div>
            </div>
          </div>
        ) : (
          <div className="pb-4">
            {displayMessages.map((message) => (
              <ChatMessage key={message.id} message={message} />
            ))}
            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Streaming status indicator */}
      {isStreaming && (
        <div className="flex-shrink-0 px-6 py-2 bg-blue-50 border-t border-blue-100">
          <div className="flex items-center gap-2 text-sm text-blue-700">
            <div className="w-2 h-2 bg-blue-600 rounded-full animate-pulse"></div>
            <span>{isConnected ? 'Streaming tokens...' : 'Connecting...'}</span>
          </div>
        </div>
      )}

      {/* Input area */}
      <div className="flex-shrink-0 border-t border-gray-200 bg-white px-6 py-4">
        <div className="max-w-4xl mx-auto">
          <form onSubmit={handleSubmit}>
            <div className="flex items-end gap-3">
              <button
                type="button"
                onClick={() => setShowFileUpload(!showFileUpload)}
                className="flex-shrink-0 p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
                title="Upload file"
              >
                <Paperclip className="w-5 h-5" />
              </button>

              <div className="flex-1 relative">
                <input
                  type="text"
                  value={question}
                  onChange={(e) => setQuestion(e.target.value)}
                  placeholder="Ask a question and see tokens stream in real-time..."
                  className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                  disabled={isStreaming || !currentTenant}
                />
              </div>

              {!isStreaming ? (
                <button
                  type="submit"
                  disabled={!question.trim() || !currentTenant}
                  className="flex-shrink-0 px-6 py-3 bg-primary-600 text-white rounded-xl hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  Ask
                </button>
              ) : (
                <button
                  type="button"
                  onClick={handleStop}
                  className="flex-shrink-0 px-6 py-3 bg-red-600 text-white rounded-xl hover:bg-red-700 transition-colors"
                >
                  Stop
                </button>
              )}
            </div>

            {!currentTenant && (
              <p className="mt-2 text-sm text-red-600">
                Please select a tenant to start chatting
              </p>
            )}
          </form>
        </div>
      </div>

      {/* File upload modal */}
      {showFileUpload && (
        <FileUpload onClose={() => setShowFileUpload(false)} />
      )}
    </div>
  );
}
