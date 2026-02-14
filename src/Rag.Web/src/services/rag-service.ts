import { apiClient } from '@/lib/api-client';
import {
  IngestRequest,
  IngestResponse,
  AskRequest,
  AskResponse,
  PdfUploadResponse,
} from '@/types';

export class RagService {
  async ingestText(request: IngestRequest): Promise<IngestResponse> {
    return apiClient.post<IngestResponse>('/api/v1/ingest', request);
  }

  async ask(request: AskRequest): Promise<AskResponse> {
    return apiClient.post<AskResponse>('/api/v1/ask', request);
  }

  async uploadPdf(
    file: File,
    documentId: string,
    onProgress?: (progress: number) => void
  ): Promise<PdfUploadResponse> {
    return apiClient.uploadFile<PdfUploadResponse>(
      '/api/v1/documents/upload-pdf',
      file,
      { documentId },
      onProgress
    );
  }

  async deleteDocument(documentId: string): Promise<void> {
    return apiClient.delete(`/api/v1/documents/${documentId}`);
  }

  async updateDocument(
    documentId: string,
    text: string
  ): Promise<any> {
    return apiClient.put(`/api/v1/documents/${documentId}`, {
      documentId,
      text,
    });
  }
}

export const ragService = new RagService();
