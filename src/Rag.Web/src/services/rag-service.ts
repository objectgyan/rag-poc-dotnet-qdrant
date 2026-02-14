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
    return apiClient.post<IngestResponse>('/ingest', request);
  }

  async ask(request: AskRequest): Promise<AskResponse> {
    return apiClient.post<AskResponse>('/ask', request);
  }

  async uploadPdf(
    file: File,
    documentId: string,
    onProgress?: (progress: number) => void
  ): Promise<PdfUploadResponse> {
    return apiClient.uploadFile<PdfUploadResponse>(
      '/documents/upload-pdf',
      file,
      { documentId },
      onProgress
    );
  }

  async deleteDocument(documentId: string): Promise<void> {
    return apiClient.delete(`/documents/${documentId}`);
  }

  async updateDocument(
    documentId: string,
    text: string
  ): Promise<any> {
    return apiClient.put(`/documents/${documentId}`, {
      documentId,
      text,
    });
  }
}

export const ragService = new RagService();
