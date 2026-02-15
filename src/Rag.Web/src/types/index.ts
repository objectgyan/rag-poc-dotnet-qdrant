// API Types
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
  score: number;
  relevanceScore?: number; // Alias for score
}

export interface CostInfo {
  embeddingCost: number;
  chatCost: number;
  totalCost: number;
}

export interface AskResponse {
  answer: string;
  citations: Citation[];
  cost?: CostInfo;
  tenantId?: string;
}

export interface PdfUploadResponse {
  message: string;
  jobId: string;
  documentId: string;
}

// Agent Types
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
  citations: Citation[];
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

// UI Types
export interface ChatMessage extends AgentMessage {
  id: string;
  timestamp: Date;
  citations?: Citation[];
  toolCalls?: ToolCall[];
  metrics?: AgentMetrics;
  isLoading?: boolean;
}

export interface Conversation {
  id: string;
  title: string;
  messages: ChatMessage[];
  createdAt: Date;
  updatedAt: Date;
  tenantId: string;
}

export interface User {
  id: string;
  username: string;
  role: string;
  tier: string;
}

export interface Tenant {
  id: string;
  name: string;
  apiKey: string;
  color?: string;
}

export interface DocumentInfo {
  id: string;
  name: string;
  uploadedAt: Date;
  status: 'uploading' | 'processing' | 'ready' | 'error';
  tenantId: string;
}
