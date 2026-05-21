import { apiService } from './apiService';

export interface Document {
  id: string;
  name: string;
  size: number;
  type: string;
  uploadedAt: string;
  status: 'processing' | 'completed' | 'failed';
  analysis?: DocumentAnalysis;
}

export interface DocumentAnalysis {
  id: string;
  documentId: string;
  summary: string;
  keyEntities: string[];
  sentiment: 'positive' | 'negative' | 'neutral';
  confidence: number;
  createdAt: string;
}

export interface UploadDocumentRequest {
  file: File;
  metadata?: Record<string, any>;
}

class DocumentService {
  async uploadDocument(request: UploadDocumentRequest): Promise<Document> {
    // Validate file before upload
    this.validateFile(request.file);

    const formData = new FormData();
    formData.append('file', request.file);

    if (request.metadata) {
      formData.append('metadata', JSON.stringify(request.metadata));
    }

    return apiService.post<Document>('/documents/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
      timeout: 120000, // 2 minutes for file upload
    });
  }

  async getDocuments(): Promise<Document[]> {
    return apiService.get<Document[]>('/documents');
  }

  async getDocument(id: string): Promise<Document> {
    return apiService.get<Document>(`/documents/${id}`);
  }

  async deleteDocument(id: string): Promise<void> {
    return apiService.delete(`/documents/${id}`);
  }

  async analyzeDocument(id: string): Promise<DocumentAnalysis> {
    return apiService.post<DocumentAnalysis>(`/documents/${id}/analyze`);
  }

  async getAnalysis(documentId: string): Promise<DocumentAnalysis> {
    return apiService.get<DocumentAnalysis>(`/analysis/${documentId}`);
  }

  private validateFile(file: File): void {
    const MAX_SIZE = 50 * 1024 * 1024; // 50MB
    const ALLOWED_TYPES = [
      'application/pdf',
      'application/msword',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      'text/plain',
    ];

    if (file.size > MAX_SIZE) {
      throw new Error('File size exceeds 50MB limit');
    }

    if (!ALLOWED_TYPES.includes(file.type)) {
      throw new Error('File type not supported');
    }

    // Additional filename validation
    if (!/^[a-zA-Z0-9._-]+$/.test(file.name)) {
      throw new Error('Invalid filename. Only alphanumeric characters, dots, hyphens, and underscores allowed');
    }
  }
}

export const documentService = new DocumentService();
