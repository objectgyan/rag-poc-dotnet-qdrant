import { useState, useEffect } from 'react';

interface UseSSEOptions {
  onMessage: (data: any) => void;
  onError?: (error: Error) => void;
  onComplete?: () => void;
  headers?: Record<string, string>;
}

/**
 * React hook for Server-Sent Events (SSE) streaming using fetch (Phase 8).
 * Uses fetch instead of EventSource to support custom headers for authentication.
 * 
 * @param url - The SSE endpoint URL (set to null to close connection)
 * @param options - Callbacks for message, error, and completion events, plus headers
 * @returns Connection state and error
 */
export function useSSE(url: string | null, options: UseSSEOptions) {
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    if (!url) return;

    let abortController = new AbortController();
    let isMounted = true;

    const startStreaming = async () => {
      try {
        console.log('[SSE] Connecting to:', url);
        
        const response = await fetch(url, {
          method: 'GET',
          headers: {
            'Accept': 'text/event-stream',
            'Cache-Control': 'no-cache',
            ...options.headers,
          },
          signal: abortController.signal,
        });

        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(`HTTP ${response.status}: ${errorText}`);
        }

        if (!response.body) {
          throw new Error('Response body is null');
        }

        console.log('[SSE] Connection opened');
        setIsConnected(true);
        setError(null);

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (isMounted) {
          const { done, value } = await reader.read();
          
          if (done) {
            console.log('[SSE] Stream ended');
            break;
          }

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              const dataStr = line.slice(6);
              try {
                const data = JSON.parse(dataStr);
                
                if (data.done) {
                  console.log('[SSE] Stream complete');
                  options.onComplete?.();
                  setIsConnected(false);
                  return;
                } else if (data.error) {
                  const error = new Error(data.error);
                  console.error('[SSE] Server error:', error);
                  setError(error);
                  options.onError?.(error);
                  setIsConnected(false);
                  return;
                } else {
                  options.onMessage(data);
                }
              } catch (err) {
                console.error('[SSE] Failed to parse message:', err);
              }
            }
          }
        }

        setIsConnected(false);
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') {
          console.log('[SSE] Connection aborted');
          return;
        }
        
        const error = err instanceof Error ? err : new Error('Unknown SSE error');
        console.error('[SSE] Error:', error);
        setError(error);
        setIsConnected(false);
        options.onError?.(error);
      }
    };

    startStreaming();

    return () => {
      console.log('[SSE] Cleanup - aborting connection');
      isMounted = false;
      abortController.abort();
      setIsConnected(false);
    };
  }, [url]);

  return { isConnected, error };
}
