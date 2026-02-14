import { apiClient } from '@/lib/api-client';
import {
  AgentChatRequest,
  AgentChatResponse,
  ToolInfo,
} from '@/types';

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

  async searchCode(query: string, topK: number = 5): Promise<any[]> {
    return apiClient.post('/agent/search-code', { query, topK });
  }

  async ingestCodebase(directoryPath: string): Promise<any> {
    return apiClient.post('/agent/ingest-codebase', {
      directoryPath,
      includePatterns: ['*.cs', '*.py', '*.js', '*.ts'],
      parseSemanticStructure: true,
    });
  }
}

export const agentService = new AgentService();
