import React from 'react';
import ReactMarkdown from 'react-markdown';
import { User, Bot, FileText, Wrench, Clock, DollarSign } from 'lucide-react';
import { ChatMessage as ChatMessageType } from '@/types';
import { cn, formatCost, formatDuration } from '@/lib/utils';

interface Props {
  message: ChatMessageType;
}

export const ChatMessage: React.FC<Props> = ({ message }) => {
  const isUser = message.role === 'user';

  return (
    <div className={cn('flex gap-4 px-6 py-4', isUser ? 'bg-gray-50' : 'bg-white')}>
      <div className="flex-shrink-0">
        {isUser ? (
          <div className="w-8 h-8 rounded-full bg-primary-600 flex items-center justify-center">
            <User className="w-5 h-5 text-white" />
          </div>
        ) : (
          <div className="w-8 h-8 rounded-full bg-gradient-to-br from-purple-500 to-pink-500 flex items-center justify-center">
            <Bot className="w-5 h-5 text-white" />
          </div>
        )}
      </div>

      <div className="flex-1 space-y-3">
        {/* Message content */}
        {message.content ? (
          <div className="prose prose-sm max-w-none">
            <ReactMarkdown>{message.content}</ReactMarkdown>
            {/* Show cursor when streaming */}
            {message.isLoading && (
              <span className="inline-block w-0.5 h-4 ml-1 bg-blue-600 animate-pulse"></span>
            )}
          </div>
        ) : message.isLoading ? (
          <div className="flex items-center gap-2 text-gray-500">
            <div className="flex gap-1">
              <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '0ms' }}></span>
              <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '150ms' }}></span>
              <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '300ms' }}></span>
            </div>
            <span className="text-sm">Thinking...</span>
          </div>
        ) : null}

        {/* Other message parts - only show when not loading or when content exists */}
        {!message.isLoading && (
          <>

            {/* Tool calls */}
            {message.toolCalls && message.toolCalls.length > 0 && (
              <div className="mt-4 space-y-2">
                <div className="flex items-center gap-2 text-sm font-medium text-gray-700">
                  <Wrench className="w-4 h-4" />
                  <span>Tools Used ({message.toolCalls.length})</span>
                </div>
                <div className="space-y-2">
                  {message.toolCalls.map((toolCall, idx) => (
                    <div
                      key={idx}
                      className="bg-gray-50 border border-gray-200 rounded-lg p-3 text-sm"
                    >
                      <div className="flex items-center justify-between mb-2">
                        <span className="font-medium text-primary-700">
                          {toolCall.toolName}
                        </span>
                        {toolCall.error && (
                          <span className="text-xs text-red-600">Error</span>
                        )}
                      </div>
                      {toolCall.reasoningTrace && (
                        <p className="text-gray-600 mb-2">{toolCall.reasoningTrace}</p>
                      )}
                      <details className="text-xs text-gray-500">
                        <summary className="cursor-pointer hover:text-gray-700">
                          View arguments
                        </summary>
                        <pre className="mt-2 p-2 bg-gray-100 rounded overflow-x-auto">
                          {JSON.stringify(toolCall.arguments, null, 2)}
                        </pre>
                      </details>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Citations */}
            {message.citations && message.citations.length > 0 && (
              <div className="mt-4 space-y-2">
                <div className="flex items-center gap-2 text-sm font-medium text-gray-700">
                  <FileText className="w-4 h-4" />
                  <span>Sources ({message.citations.length})</span>
                </div>
                <div className="space-y-2">
                  {message.citations.map((citation, idx) => (
                    <div
                      key={idx}
                      className="flex items-center justify-between bg-blue-50 border border-blue-200 rounded-lg p-3 text-sm"
                    >
                      <div>
                        <span className="font-medium text-blue-900">
                          {citation.documentId}
                        </span>
                        {citation.pageNumber && (
                          <span className="text-blue-700 ml-2">
                            Page {citation.pageNumber}
                          </span>
                        )}
                      </div>
                      <span className="text-blue-600 font-medium">
                        {((citation.score || citation.relevanceScore || 0) * 100).toFixed(0)}% relevant
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Metrics */}
            {message.metrics && (
              <div className="flex items-center gap-4 mt-3 text-xs text-gray-500">
                {message.metrics.durationMs !== undefined && (
                  <div className="flex items-center gap-1">
                    <Clock className="w-3 h-3" />
                    <span>{formatDuration(message.metrics.durationMs)}</span>
                  </div>
                )}
                {message.metrics.toolCallsCount > 0 && (
                  <div className="flex items-center gap-1">
                    <Wrench className="w-3 h-3" />
                    <span>{message.metrics.toolCallsCount} tools</span>
                  </div>
                )}
                {message.metrics.estimatedCost > 0 && (
                  <div className="flex items-center gap-1">
                    <DollarSign className="w-3 h-3" />
                    <span>{formatCost(message.metrics.estimatedCost)}</span>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
};
