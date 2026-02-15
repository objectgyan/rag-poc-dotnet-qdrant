import React, { useState, useRef, useEffect } from 'react';
import { Send, Paperclip, Loader2 } from 'lucide-react';
import { ChatMessage } from './ChatMessage';
import { FileUpload } from './FileUpload';
import { useAppStore } from '@/store/app-store';
import { agentService } from '@/services/agent-service';
import { generateId } from '@/lib/utils';
import { ChatMessage as ChatMessageType } from '@/types';

export const ChatInterface: React.FC = () => {
  const {
    currentConversation,
    currentTenant,
    addMessage,
    updateMessage,
    createConversation,
  } = useAppStore();

  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showFileUpload, setShowFileUpload] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [currentConversation?.messages]);

  useEffect(() => {
    adjustTextareaHeight();
  }, [input]);

  const adjustTextareaHeight = () => {
    const textarea = textareaRef.current;
    if (textarea) {
      textarea.style.height = 'auto';
      textarea.style.height = `${Math.min(textarea.scrollHeight, 200)}px`;
    }
  };

  const handleSend = async () => {
    if (!input.trim() || isLoading || !currentTenant) return;

    let conversation = currentConversation;
    if (!conversation) {
      conversation = createConversation(currentTenant.id);
    }

    const userMessage: ChatMessageType = {
      id: generateId(),
      role: 'user',
      content: input.trim(),
      timestamp: new Date(),
    };

    addMessage(conversation.id, userMessage);
    const userInput = input.trim();
    setInput('');
    setIsLoading(true);

    // Add loading message
    const loadingMessageId = generateId();
    const loadingMessage: ChatMessageType = {
      id: loadingMessageId,
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      isLoading: true,
    };
    addMessage(conversation.id, loadingMessage);

    try {
      // Use agent mode (includes RAG via useRagForContext)
      const conversationHistory = conversation.messages
        .filter((m) => !m.isLoading)
        .map((m) => ({
          role: m.role,
          content: m.content,
        }));

      const response = await agentService.chat({
        message: userInput,
        conversationHistory,
        config: {
          maxToolCalls: 5,
          allowParallelToolCalls: true,
          useRagForContext: true,
          enableChainOfThought: true,
        },
      });

      // Update loading message with response
      updateMessage(conversation.id, loadingMessageId, {
        content: response.answer,
        toolCalls: response.toolCalls,
        metrics: response.metrics,
        isLoading: false,
      });
    } catch (error) {
      console.error('Error sending message:', error);
      updateMessage(conversation.id, loadingMessageId, {
        content: 'Sorry, I encountered an error. Please try again.',
        isLoading: false,
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const messages = currentConversation?.messages || [];

  return (
    <div className="flex flex-col h-screen">
      {/* Messages area */}
      <div className="flex-1 overflow-y-auto">
        {messages.length === 0 ? (
          <div className="flex items-center justify-center h-full text-center px-6">
            <div className="max-w-md space-y-4">
              <h2 className="text-3xl font-bold text-gray-900">
                🤖 AI Agent Assistant
              </h2>
              <p className="text-gray-600">
                I can search documents, GitHub repositories, remember conversations, and use various tools to help you.
              </p>
              <div className="flex flex-wrap gap-2 justify-center">
                <button
                  onClick={() => setInput('What is Qdrant?')}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 text-sm"
                >
                  What is Qdrant?
                </button>
                <button
                  onClick={() => setInput('Find popular vector database repos on GitHub')}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 text-sm"
                >
                  Search GitHub
                </button>
                <button
                  onClick={() => setInput('Research vector databases using docs and GitHub')}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 text-sm"
                >
                  Multi-step research
                </button>
                <button
                  onClick={() => setInput('Remember that I prefer detailed technical explanations')}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 text-sm"
                >
                  Store Memory
                </button>
              </div>
            </div>
          </div>
        ) : (
          <div className="pb-4">
            {messages.map((message) => (
              <ChatMessage key={message.id} message={message} />
            ))}
            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Input area */}
      <div className="border-t border-gray-200 bg-white px-6 py-4">
        <div className="max-w-4xl mx-auto">
          <div className="flex items-end gap-3">
            <button
              onClick={() => setShowFileUpload(!showFileUpload)}
              className="flex-shrink-0 p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
              title="Upload file"
            >
              <Paperclip className="w-5 h-5" />
            </button>

            <div className="flex-1 relative">
              <textarea
                ref={textareaRef}
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Ask anything... I can search docs, GitHub, remember conversations, and more"
                className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent resize-none"
                rows={1}
                disabled={isLoading || !currentTenant}
              />
            </div>

            <button
              onClick={handleSend}
              disabled={!input.trim() || isLoading || !currentTenant}
              className="flex-shrink-0 p-3 bg-primary-600 text-white rounded-xl hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isLoading ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <Send className="w-5 h-5" />
              )}
            </button>
          </div>

          {!currentTenant && (
            <p className="mt-2 text-sm text-red-600">
              Please select a tenant to start chatting
            </p>
          )}
        </div>
      </div>

      {/* File upload modal */}
      {showFileUpload && (
        <FileUpload onClose={() => setShowFileUpload(false)} />
      )}
    </div>
  );
};
