# Frontend Integration Guide

Complete guide for building a frontend application that consumes the RAG POC API.

## 📋 Table of Contents

1. [Quick Start](#quick-start)
2. [Authentication](#authentication)
3. [API Client Setup](#api-client-setup)
4. [Service Layer](#service-layer)
5. [React Components](#react-components)
6. [Vue Components](#vue-components)
7. [TypeScript Models](#typescript-models)
8. [State Management](#state-management)
9. [Error Handling](#error-handling)
10. [Feature Implementations](#feature-implementations)

## 🚀 Quick Start

### Base Configuration

```typescript
// config.ts
export const API_CONFIG = {
  BASE_URL: 'http://localhost:5129',
  API_KEY: 'secure_password', // Store securely in production
  TENANT_ID: 'default-tenant', // Optional
  TIMEOUT: 30000,
};
```

## 🔐 Authentication

### API Key Authentication

All requests require `X-API-Key` header:

```typescript
// auth.ts
export const getAuthHeaders = (tenantId?: string) => {
  const headers: Record<string, string> = {
    'X-API-Key': API_CONFIG.API_KEY,
    'Content-Type': 'application/json',
  };

  if (tenantId) {
    headers['X-Tenant-Id'] = tenantId;
  }

  return headers;
};
```

## 🛠️ API Client Setup

### Axios Configuration

```typescript
// api/client.ts
import axios, { AxiosInstance, AxiosError } from 'axios';
import { API_CONFIG, getAuthHeaders } from '../config';

export class ApiClient {
  private client: AxiosInstance;

  constructor(tenantId?: string) {
    this.client = axios.create({
      baseURL: API_CONFIG.BASE_URL,
      timeout: API_CONFIG.TIMEOUT,
      headers: getAuthHeaders(tenantId),
    });

    this.setupInterceptors();
  }

  private setupInterceptors() {
    // Request interceptor
    this.client.interceptors.request.use(
      (config) => {
        console.log(`[API] ${config.method?.toUpperCase()} ${config.url}`);
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError) => {
        if (error.response) {
          console.error('[API Error]', {
            status: error.response.status,
            data: error.response.data,
          });
        }
        return Promise.reject(error);
      }
    );
  }

  async get<T>(url: string, params?: any): Promise<T> {
    const response = await this.client.get<T>(url, { params });
    return response.data;
  }

  async post<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.post<T>(url, data);
    return response.data;
  }

  async put<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.put<T>(url, data);
    return response.data;
  }

  async delete<T>(url: string): Promise<T> {
    const response = await this.client.delete<T>(url);
    return response.data;
  }

  async uploadFile<T>(url: string, file: File, additionalData?: Record<string, string>): Promise<T> {
    const formData = new FormData();
    formData.append('file', file);
    
    if (additionalData) {
      Object.entries(additionalData).forEach(([key, value]) => {
        formData.append(key, value);
      });
    }

    const response = await this.client.post<T>(url, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  }
}

export const apiClient = new ApiClient();
```

## 📦 TypeScript Models

```typescript
// models/index.ts

// === RAG Models ===
export interface IngestRequest {
  documentId: string;
  text: string;
}

export interface IngestResponse {
  message: string;
  documentId: string;
  chunksCreated: number;
}

export interface AskRequest {
  question: string;
  topK?: number;
}

export interface Citation {
  documentId: string;
  chunkIndex: number;
  pageNumber?: number;
  relevanceScore: number;
}

export interface CostInfo {
  embeddingCost: number;
  chatCost: number;
  totalCost: number;
}

export interface AskResponse {
  answer: string;
  citations: Citation[];
  cost: CostInfo;
}

export interface DocumentUpdateRequest {
  documentId: string;
  text: string;
}

export interface PdfUploadResponse {
  message: string;
  jobId: string;
  documentId: string;
}

// === Evaluation Models ===
export interface EvaluationTestCase {
  id: string;
  question: string;
  expectedAnswer: string;
  requiredKeywords?: string[];
  category?: string;
  metadata?: Record<string, any>;
}

export interface EvaluationConfig {
  minSemanticSimilarity?: number;
  minKeywordMatch?: number;
  minCitationAccuracy?: number;
  maxHallucinationRate?: number;
  useSemanticEvaluation?: boolean;
  useKeywordEvaluation?: boolean;
  useLlmAsJudge?: boolean;
}

export interface EvaluationRunRequest {
  name: string;
  category?: string;
  config?: EvaluationConfig;
}

export interface EvaluationMetrics {
  averageAccuracy: number;
  averageSemanticSimilarity: number;
  averageKeywordMatch: number;
  averageCitationAccuracy: number;
  hallucinationRate: number;
  passRate: number;
}

export interface EvaluationRun {
  id: string;
  name: string;
  status: string;
  startTime: string;
  endTime?: string;
  totalTestCases: number;
  passedTestCases: number;
  failedTestCases: number;
  metrics: EvaluationMetrics;
  config: EvaluationConfig;
}

// === Agent Models ===
export interface AgentMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface AgentConfig {
  maxToolCalls?: number;
  allowParallelToolCalls?: boolean;
  useRagForContext?: boolean;
  topKDocuments?: number;
  enableChainOfThought?: boolean;
  systemPrompt?: string;
}

export interface ToolCall {
  toolName: string;
  arguments: Record<string, any>;
  result?: string;
  error?: string;
  reasoningTrace?: string;
}

export interface AgentMetrics {
  toolCallsCount: number;
  documentsRetrieved: number;
  durationMs: number;
  estimatedCost: number;
}

export interface AgentChatRequest {
  message: string;
  conversationHistory?: AgentMessage[];
  config?: AgentConfig;
}

export interface AgentChatResponse {
  answer: string;
  toolCalls: ToolCall[];
  retrievedDocuments: string[];
  metrics: AgentMetrics;
}

export interface ToolInfo {
  name: string;
  description: string;
  category: string;
  parameters: ToolParameter[];
}

export interface ToolParameter {
  name: string;
  type: string;
  description: string;
  required: boolean;
  defaultValue?: any;
}

export interface CodebaseIngestionRequest {
  directoryPath: string;
  includePatterns?: string[];
  excludePatterns?: string[];
  parseSemanticStructure?: boolean;
  chunkSize?: number;
  chunkOverlap?: number;
}

export interface CodebaseIngestionResult {
  totalFiles: number;
  totalLines: number;
  chunksCreated: number;
  extractedElements: number;
  durationSeconds: number;
}

export interface CodeSearchRequest {
  query: string;
  topK?: number;
  language?: string;
}

export interface CodeSearchResult {
  filePath: string;
  codeSnippet: string;
  relevanceScore: number;
  language: string;
  elementName?: string;
  elementType?: string;
}
```

## 🔧 Service Layer

### Document Service

```typescript
// services/documentService.ts
import { apiClient } from '../api/client';
import {
  IngestRequest,
  IngestResponse,
  PdfUploadResponse,
  DocumentUpdateRequest,
} from '../models';

export class DocumentService {
  async ingestText(request: IngestRequest): Promise<IngestResponse> {
    return apiClient.post<IngestResponse>('/ingest', request);
  }

  async uploadPdf(file: File, documentId: string): Promise<PdfUploadResponse> {
    return apiClient.uploadFile<PdfUploadResponse>(
      '/documents/upload-pdf',
      file,
      { documentId }
    );
  }

  async updateDocument(
    documentId: string,
    request: DocumentUpdateRequest
  ): Promise<any> {
    return apiClient.put(`/documents/${documentId}`, request);
  }

  async deleteDocument(documentId: string): Promise<any> {
    return apiClient.delete(`/documents/${documentId}`);
  }
}

export const documentService = new DocumentService();
```

### RAG Service

```typescript
// services/ragService.ts
import { apiClient } from '../api/client';
import { AskRequest, AskResponse } from '../models';

export class RagService {
  async ask(request: AskRequest): Promise<AskResponse> {
    return apiClient.post<AskResponse>('/ask', request);
  }
}

export const ragService = new RagService();
```

### Evaluation Service

```typescript
// services/evaluationService.ts
import { apiClient } from '../api/client';
import {
  EvaluationTestCase,
  EvaluationRunRequest,
  EvaluationRun,
  EvaluationMetrics,
} from '../models';

export class EvaluationService {
  // Test Cases
  async createTestCase(testCase: EvaluationTestCase): Promise<void> {
    return apiClient.post('/evaluation/test-cases', testCase);
  }

  async getTestCases(category?: string): Promise<EvaluationTestCase[]> {
    const params = category ? { category } : undefined;
    return apiClient.get<EvaluationTestCase[]>('/evaluation/test-cases', params);
  }

  async getTestCase(id: string): Promise<EvaluationTestCase> {
    return apiClient.get<EvaluationTestCase>(`/evaluation/test-cases/${id}`);
  }

  async updateTestCase(id: string, testCase: EvaluationTestCase): Promise<void> {
    return apiClient.put(`/evaluation/test-cases/${id}`, testCase);
  }

  async deleteTestCase(id: string): Promise<void> {
    return apiClient.delete(`/evaluation/test-cases/${id}`);
  }

  // Evaluation Runs
  async runEvaluation(request: EvaluationRunRequest): Promise<EvaluationRun> {
    return apiClient.post<EvaluationRun>('/evaluation/run', request);
  }

  async getEvaluationRun(runId: string): Promise<EvaluationRun> {
    return apiClient.get<EvaluationRun>(`/evaluation/runs/${runId}`);
  }

  async getEvaluationRuns(): Promise<EvaluationRun[]> {
    return apiClient.get<EvaluationRun[]>('/evaluation/runs');
  }

  async getAggregateMetrics(): Promise<EvaluationMetrics> {
    return apiClient.get<EvaluationMetrics>('/evaluation/metrics');
  }
}

export const evaluationService = new EvaluationService();
```

### Agent Service

```typescript
// services/agentService.ts
import { apiClient } from '../api/client';
import {
  AgentChatRequest,
  AgentChatResponse,
  ToolInfo,
  CodebaseIngestionRequest,
  CodebaseIngestionResult,
  CodeSearchRequest,
  CodeSearchResult,
} from '../models';

export class AgentService {
  async chat(request: AgentChatRequest): Promise<AgentChatResponse> {
    return apiClient.post<AgentChatResponse>('/agent/chat', request);
  }

  async getTools(): Promise<ToolInfo[]> {
    return apiClient.get<ToolInfo[]>('/agent/tools');
  }

  async getTool(name: string): Promise<ToolInfo> {
    return apiClient.get<ToolInfo>(`/agent/tools/${name}`);
  }

  async ingestCodebase(
    request: CodebaseIngestionRequest
  ): Promise<CodebaseIngestionResult> {
    return apiClient.post<CodebaseIngestionResult>(
      '/agent/ingest-codebase',
      request
    );
  }

  async searchCode(request: CodeSearchRequest): Promise<CodeSearchResult[]> {
    return apiClient.post<CodeSearchResult[]>('/agent/search-code', request);
  }

  async getCodeContext(
    filePath: string,
    startLine?: number,
    endLine?: number
  ): Promise<any> {
    const params = { filePath, startLine, endLine };
    return apiClient.get('/agent/code-context', params);
  }
}

export const agentService = new AgentService();
```

## ⚛️ React Components

### Chat Interface Component

```typescript
// components/ChatInterface.tsx
import React, { useState, useRef, useEffect } from 'react';
import { ragService } from '../services/ragService';
import { AskResponse, Citation } from '../models';
import './ChatInterface.css';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  citations?: Citation[];
  cost?: number;
  timestamp: Date;
}

export const ChatInterface: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSend = async () => {
    if (!input.trim() || loading) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      role: 'user',
      content: input,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setInput('');
    setLoading(true);

    try {
      const response: AskResponse = await ragService.ask({
        question: input,
        topK: 3,
      });

      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: response.answer,
        citations: response.citations,
        cost: response.cost.totalCost,
        timestamp: new Date(),
      };

      setMessages((prev) => [...prev, assistantMessage]);
    } catch (error) {
      console.error('Error asking question:', error);
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: 'Sorry, I encountered an error. Please try again.',
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <div className="chat-interface">
      <div className="chat-header">
        <h2>RAG Chat</h2>
      </div>

      <div className="chat-messages">
        {messages.map((message) => (
          <div key={message.id} className={`message message-${message.role}`}>
            <div className="message-content">
              <div className="message-text">{message.content}</div>
              
              {message.citations && message.citations.length > 0 && (
                <div className="citations">
                  <h4>Sources:</h4>
                  <ul>
                    {message.citations.map((citation, idx) => (
                      <li key={idx}>
                        <strong>{citation.documentId}</strong>
                        {citation.pageNumber && ` (Page ${citation.pageNumber})`}
                        <span className="relevance">
                          {(citation.relevanceScore * 100).toFixed(1)}% relevant
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {message.cost && (
                <div className="cost">
                  Cost: ${message.cost.toFixed(4)}
                </div>
              )}
            </div>
            
            <div className="message-timestamp">
              {message.timestamp.toLocaleTimeString()}
            </div>
          </div>
        ))}
        
        {loading && (
          <div className="message message-assistant loading">
            <div className="typing-indicator">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </div>
        )}
        
        <div ref={messagesEndRef} />
      </div>

      <div className="chat-input">
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Ask a question about your documents..."
          disabled={loading}
          rows={3}
        />
        <button onClick={handleSend} disabled={loading || !input.trim()}>
          {loading ? 'Thinking...' : 'Send'}
        </button>
      </div>
    </div>
  );
};
```

### Agent Chat Component

```typescript
// components/AgentChat.tsx
import React, { useState } from 'react';
import { agentService } from '../services/agentService';
import { AgentChatResponse, AgentMessage, ToolCall } from '../models';
import './AgentChat.css';

interface ExtendedMessage extends AgentMessage {
  id: string;
  toolCalls?: ToolCall[];
  metrics?: any;
  timestamp: Date;
}

export const AgentChat: React.FC = () => {
  const [messages, setMessages] = useState<ExtendedMessage[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [showToolCalls, setShowToolCalls] = useState(true);

  const handleSend = async () => {
    if (!input.trim() || loading) return;

    const userMessage: ExtendedMessage = {
      id: Date.now().toString(),
      role: 'user',
      content: input,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    const conversationHistory: AgentMessage[] = messages.map((m) => ({
      role: m.role,
      content: m.content,
    }));

    setInput('');
    setLoading(true);

    try {
      const response: AgentChatResponse = await agentService.chat({
        message: input,
        conversationHistory,
        config: {
          maxToolCalls: 5,
          allowParallelToolCalls: true,
          enableChainOfThought: true,
        },
      });

      const assistantMessage: ExtendedMessage = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: response.answer,
        toolCalls: response.toolCalls,
        metrics: response.metrics,
        timestamp: new Date(),
      };

      setMessages((prev) => [...prev, assistantMessage]);
    } catch (error) {
      console.error('Error chatting with agent:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="agent-chat">
      <div className="agent-header">
        <h2>🤖 AI Agent</h2>
        <label>
          <input
            type="checkbox"
            checked={showToolCalls}
            onChange={(e) => setShowToolCalls(e.target.checked)}
          />
          Show tool calls
        </label>
      </div>

      <div className="agent-messages">
        {messages.map((message) => (
          <div key={message.id} className={`message message-${message.role}`}>
            <div className="message-content">{message.content}</div>

            {showToolCalls && message.toolCalls && message.toolCalls.length > 0 && (
              <div className="tool-calls">
                <h4>🛠️ Tools Used:</h4>
                {message.toolCalls.map((tc, idx) => (
                  <div key={idx} className="tool-call">
                    <div className="tool-name">{tc.toolName}</div>
                    {tc.reasoningTrace && (
                      <div className="tool-reasoning">{tc.reasoningTrace}</div>
                    )}
                    <details>
                      <summary>Arguments</summary>
                      <pre>{JSON.stringify(tc.arguments, null, 2)}</pre>
                    </details>
                  </div>
                ))}
              </div>
            )}

            {message.metrics && (
              <div className="metrics">
                ⏱️ {message.metrics.durationMs}ms | 
                🔧 {message.metrics.toolCallsCount} tools | 
                💰 ${message.metrics.estimatedCost.toFixed(4)}
              </div>
            )}

            <div className="timestamp">
              {message.timestamp.toLocaleTimeString()}
            </div>
          </div>
        ))}
      </div>

      <div className="agent-input">
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Ask the agent anything..."
          disabled={loading}
          rows={3}
        />
        <button onClick={handleSend} disabled={loading || !input.trim()}>
          {loading ? '🤔 Thinking...' : '🚀 Send'}
        </button>
      </div>
    </div>
  );
};
```

### Document Upload Component

```typescript
// components/DocumentUpload.tsx
import React, { useState } from 'react';
import { documentService } from '../services/documentService';
import './DocumentUpload.css';

export const DocumentUpload: React.FC = () => {
  const [file, setFile] = useState<File | null>(null);
  const [documentId, setDocumentId] = useState('');
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState('');

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (selectedFile) {
      setFile(selectedFile);
      if (!documentId) {
        setDocumentId(selectedFile.name.replace(/\.[^/.]+$/, ''));
      }
    }
  };

  const handleUpload = async () => {
    if (!file || !documentId) {
      setMessage('Please select a file and provide a document ID');
      return;
    }

    setUploading(true);
    setMessage('');

    try {
      const response = await documentService.uploadPdf(file, documentId);
      setMessage(`✅ Upload started! Job ID: ${response.jobId}`);
      setFile(null);
      setDocumentId('');
    } catch (error) {
      setMessage('❌ Upload failed. Please try again.');
      console.error('Upload error:', error);
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="document-upload">
      <h2>Upload Document</h2>

      <div className="upload-form">
        <div className="form-group">
          <label>Document ID:</label>
          <input
            type="text"
            value={documentId}
            onChange={(e) => setDocumentId(e.target.value)}
            placeholder="my-document-001"
            disabled={uploading}
          />
        </div>

        <div className="form-group">
          <label>Select PDF:</label>
          <input
            type="file"
            accept=".pdf"
            onChange={handleFileChange}
            disabled={uploading}
          />
        </div>

        {file && (
          <div className="file-info">
            Selected: {file.name} ({(file.size / 1024).toFixed(2)} KB)
          </div>
        )}

        <button
          onClick={handleUpload}
          disabled={!file || !documentId || uploading}
          className="upload-button"
        >
          {uploading ? '⏳ Uploading...' : '📤 Upload'}
        </button>

        {message && (
          <div className={`message ${message.startsWith('✅') ? 'success' : 'error'}`}>
            {message}
          </div>
        )}
      </div>
    </div>
  );
};
```

### Evaluation Dashboard Component

```typescript
// components/EvaluationDashboard.tsx
import React, { useState, useEffect } from 'react';
import { evaluationService } from '../services/evaluationService';
import { EvaluationRun, EvaluationMetrics } from '../models';
import './EvaluationDashboard.css';

export const EvaluationDashboard: React.FC = () => {
  const [runs, setRuns] = useState<EvaluationRun[]>([]);
  const [metrics, setMetrics] = useState<EvaluationMetrics | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [runsData, metricsData] = await Promise.all([
        evaluationService.getEvaluationRuns(),
        evaluationService.getAggregateMetrics(),
      ]);
      setRuns(runsData);
      setMetrics(metricsData);
    } catch (error) {
      console.error('Error loading evaluation data:', error);
    } finally {
      setLoading(false);
    }
  };

  const runNewEvaluation = async () => {
    try {
      setLoading(true);
      await evaluationService.runEvaluation({
        name: `Evaluation ${new Date().toLocaleString()}`,
      });
      await loadData();
    } catch (error) {
      console.error('Error running evaluation:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <div className="loading">Loading evaluation data...</div>;
  }

  return (
    <div className="evaluation-dashboard">
      <div className="dashboard-header">
        <h2>Evaluation Dashboard</h2>
        <button onClick={runNewEvaluation} className="run-button">
          🧪 Run New Evaluation
        </button>
      </div>

      {metrics && (
        <div className="aggregate-metrics">
          <h3>Aggregate Metrics</h3>
          <div className="metrics-grid">
            <div className="metric-card">
              <div className="metric-label">Pass Rate</div>
              <div className="metric-value">
                {(metrics.passRate * 100).toFixed(1)}%
              </div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Avg Accuracy</div>
              <div className="metric-value">
                {(metrics.averageAccuracy * 100).toFixed(1)}%
              </div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Hallucination Rate</div>
              <div className="metric-value">
                {(metrics.hallucinationRate * 100).toFixed(1)}%
              </div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Semantic Similarity</div>
              <div className="metric-value">
                {(metrics.averageSemanticSimilarity * 100).toFixed(1)}%
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="evaluation-runs">
        <h3>Recent Evaluation Runs</h3>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Status</th>
              <th>Pass Rate</th>
              <th>Tests</th>
              <th>Started</th>
            </tr>
          </thead>
          <tbody>
            {runs.map((run) => (
              <tr key={run.id}>
                <td>{run.name}</td>
                <td>
                  <span className={`status status-${run.status}`}>
                    {run.status}
                  </span>
                </td>
                <td>{(run.metrics.passRate * 100).toFixed(1)}%</td>
                <td>
                  {run.passedTestCases}/{run.totalTestCases}
                </td>
                <td>{new Date(run.startTime).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};
```

## 🎨 CSS Styles

### Chat Interface Styles

```css
/* ChatInterface.css */
.chat-interface {
  display: flex;
  flex-direction: column;
  height: 100vh;
  max-width: 800px;
  margin: 0 auto;
  background: #fff;
}

.chat-header {
  padding: 1rem;
  background: #6366f1;
  color: white;
  text-align: center;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 1rem;
  background: #f9fafb;
}

.message {
  margin-bottom: 1.5rem;
  padding: 1rem;
  border-radius: 8px;
}

.message-user {
  background: #6366f1;
  color: white;
  margin-left: 20%;
}

.message-assistant {
  background: white;
  border: 1px solid #e5e7eb;
  margin-right: 20%;
}

.message-text {
  white-space: pre-wrap;
  line-height: 1.6;
}

.citations {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
}

.citations h4 {
  font-size: 0.875rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
}

.citations ul {
  list-style: none;
  padding: 0;
}

.citations li {
  padding: 0.5rem;
  margin-bottom: 0.25rem;
  background: #f3f4f6;
  border-radius: 4px;
  font-size: 0.875rem;
}

.relevance {
  float: right;
  color: #6366f1;
  font-weight: 600;
}

.cost {
  margin-top: 0.5rem;
  font-size: 0.75rem;
  color: #6b7280;
}

.message-timestamp {
  margin-top: 0.5rem;
  font-size: 0.75rem;
  color: #9ca3af;
}

.chat-input {
  padding: 1rem;
  background: white;
  border-top: 1px solid #e5e7eb;
  display: flex;
  gap: 0.5rem;
}

.chat-input textarea {
  flex: 1;
  padding: 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-family: inherit;
  font-size: 0.875rem;
  resize: none;
}

.chat-input button {
  padding: 0.75rem 1.5rem;
  background: #6366f1;
  color: white;
  border: none;
  border-radius: 6px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}

.chat-input button:hover:not(:disabled) {
  background: #4f46e5;
}

.chat-input button:disabled {
  background: #9ca3af;
  cursor: not-allowed;
}

.typing-indicator {
  display: flex;
  gap: 0.5rem;
  padding: 1rem;
}

.typing-indicator span {
  width: 8px;
  height: 8px;
  background: #6366f1;
  border-radius: 50%;
  animation: bounce 1.4s infinite ease-in-out both;
}

.typing-indicator span:nth-child(1) {
  animation-delay: -0.32s;
}

.typing-indicator span:nth-child(2) {
  animation-delay: -0.16s;
}

@keyframes bounce {
  0%, 80%, 100% {
    transform: scale(0);
  }
  40% {
    transform: scale(1);
  }
}
```

## 🚀 Complete App Example

### Main App Component

```typescript
// App.tsx
import React, { useState } from 'react';
import { ChatInterface } from './components/ChatInterface';
import { AgentChat } from './components/AgentChat';
import { DocumentUpload } from './components/DocumentUpload';
import { EvaluationDashboard } from './components/EvaluationDashboard';
import './App.css';

type View = 'chat' | 'agent' | 'upload' | 'evaluation';

export const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<View>('chat');

  return (
    <div className="app">
      <nav className="sidebar">
        <h1>RAG POC</h1>
        <ul>
          <li
            className={currentView === 'chat' ? 'active' : ''}
            onClick={() => setCurrentView('chat')}
          >
            💬 Chat
          </li>
          <li
            className={currentView === 'agent' ? 'active' : ''}
            onClick={() => setCurrentView('agent')}
          >
            🤖 Agent
          </li>
          <li
            className={currentView === 'upload' ? 'active' : ''}
            onClick={() => setCurrentView('upload')}
          >
            📤 Upload
          </li>
          <li
            className={currentView === 'evaluation' ? 'active' : ''}
            onClick={() => setCurrentView('evaluation')}
          >
            📊 Evaluation
          </li>
        </ul>
      </nav>

      <main className="main-content">
        {currentView === 'chat' && <ChatInterface />}
        {currentView === 'agent' && <AgentChat />}
        {currentView === 'upload' && <DocumentUpload />}
        {currentView === 'evaluation' && <EvaluationDashboard />}
      </main>
    </div>
  );
};
```

## 📊 State Management (Optional)

### Using React Context

```typescript
// contexts/AppContext.tsx
import React, { createContext, useContext, useState } from 'react';

interface AppState {
  tenantId: string | null;
  setTenantId: (id: string | null) => void;
}

const AppContext = createContext<AppState | undefined>(undefined);

export const AppProvider: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const [tenantId, setTenantId] = useState<string | null>(null);

  return (
    <AppContext.Provider value={{ tenantId, setTenantId }}>
      {children}
    </AppContext.Provider>
  );
};

export const useApp = () => {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useApp must be used within AppProvider');
  }
  return context;
};
```

## 🛡️ Error Handling

### Global Error Boundary

```typescript
// components/ErrorBoundary.tsx
import React, { Component, ErrorInfo, ReactNode } from 'react';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = {
    hasError: false,
  };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Error caught by boundary:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="error-boundary">
          <h1>Something went wrong</h1>
          <p>{this.state.error?.message}</p>
          <button onClick={() => window.location.reload()}>
            Reload Page
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
```

## 🎯 Summary

This guide provides everything you need to build a production-ready frontend for the RAG POC API:

1. **Type-safe** - Complete TypeScript models
2. **Service-based** - Clean separation of concerns
3. **Component library** - Ready-to-use React components
4. **Styled** - Professional CSS styling
5. **Error handling** - Robust error management
6. **State management** - Optional context-based state

### Next Steps

1. Clone the examples
2. Customize the UI/UX
3. Add more features (streaming, notifications, etc.)
4. Deploy to production

### Additional Resources

- [Main README](README.md)
- [Phase 5 Agent Documentation](PHASE5-AGENT-LAYER.md)
- [Evaluation Documentation](PHASE4-EVALUATION-QUALITY.md)
- [API Examples](tests.http)

---

**Ready to build! 🚀**
