import apiService from './apiService';

export interface Document {
  id: string;
  name: string;
  type: string;
  size: number;
  uploadedAt: string;
  status: 'uploading' | 'processing' | 'completed' | 'failed';
  analysis?: DocumentAnalysis;
}

export interface DocumentAnalysis {
  id: string;
  documentId: string;
  extractedText: string;
  entities: Entity[];
  sentiment: SentimentAnalysis;
  summary: string;
  confidence: number;
  keyEntities: string[];
  createdAt: string;
}

export interface Entity {
  text: string;
  label: string;
  confidence: number;
  startIndex: number;
  endIndex: number;
}

export interface SentimentAnalysis {
  score: number;
  label: 'positive' | 'negative' | 'neutral';
  confidence: number;
}

export interface UploadDocumentRequest {
  file: File;
  metadata?: Record<string, any>;
}

class DocumentService {
  private readonly ALLOWED_FILE_TYPES = [
    'application/pdf',
    'text/plain',
    'application/msword',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
  ];

  private readonly MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB

  private validateFile(file: File): void {
    if (!this.ALLOWED_FILE_TYPES.includes(file.type)) {
      throw new Error(`File type ${file.type} is not allowed. Please upload PDF, TXT, or DOC files.`);
    }

    if (file.size > this.MAX_FILE_SIZE) {
      throw new Error(`File size exceeds maximum limit of ${this.MAX_FILE_SIZE / 1024 / 1024}MB.`);
    }

    // Check for suspicious file names
    const suspiciousPatterns = [/\.exe$/i, /\.bat$/i, /\.cmd$/i, /\.scr$/i, /\.js$/i];
    if (suspiciousPatterns.some(pattern => pattern.test(file.name))) {
      throw new Error('File name contains potentially dangerous extensions.');
    }

    // Additional filename validation
    if (!/^[a-zA-Z0-9._-]+$/.test(file.name)) {
      throw new Error('Invalid filename. Only alphanumeric characters, dots, hyphens, and underscores allowed');
    }
  }

  async uploadDocument(file: File): Promise<Document>;
  async uploadDocument(request: UploadDocumentRequest): Promise<Document>;
  async uploadDocument(fileOrRequest: File | UploadDocumentRequest): Promise<Document> {
    try {
      let file: File;
      let metadata: Record<string, any> | undefined;

      if (fileOrRequest instanceof File) {
        file = fileOrRequest;
      } else {
        file = fileOrRequest.file;
        metadata = fileOrRequest.metadata;
      }

      this.validateFile(file);

      const formData = new FormData();
      formData.append('file', file);
      formData.append('checksum', await this.calculateFileChecksum(file));

      if (metadata) {
        formData.append('metadata', JSON.stringify(metadata));
      }

      const response = await apiService.post<Document>('/documents/upload', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        timeout: 120000, // 2 minutes for file upload
      });

      return response;
    } catch (error: any) {
      console.error('Document upload failed:', error);
      throw new Error(error.message || 'Failed to upload document');
    }
  }

  private async calculateFileChecksum(file: File): Promise<string> {
    const buffer = await file.arrayBuffer();
    const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
  }

  async getDocuments(): Promise<Document[]> {
    try {
      return await apiService.get<Document[]>('/documents');
    } catch (error) {
      console.error('Failed to fetch documents:', error);
      throw new Error('Failed to load documents');
    }
  }

  async getDocument(id: string): Promise<Document> {
    try {
      if (!id || typeof id !== 'string') {
        throw new Error('Invalid document ID');
      }

      return await apiService.get<Document>(`/documents/${encodeURIComponent(id)}`);
    } catch (error) {
      console.error('Failed to fetch document:', error);
      throw new Error('Failed to load document');
    }
  }

  async deleteDocument(id: string): Promise<void> {
    try {
      if (!id || typeof id !== 'string') {
        throw new Error('Invalid document ID');
      }

      await apiService.delete(`/documents/${encodeURIComponent(id)}`);
    } catch (error) {
      console.error('Failed to delete document:', error);
      throw new Error('Failed to delete document');
    }
  }

  async analyzeDocument(id: string): Promise<DocumentAnalysis> {
    try {
      if (!id || typeof id !== 'string') {
        throw new Error('Invalid document ID');
      }

      return await apiService.post<DocumentAnalysis>(`/analysis/analyze/${encodeURIComponent(id)}`);
    } catch (error) {
      console.error('Document analysis failed:', error);
      throw new Error('Failed to analyze document');
    }
  }

  async getAnalysis(documentId: string): Promise<DocumentAnalysis> {
    return apiService.get<DocumentAnalysis>(`/analysis/${documentId}`);
  }
}

const documentService = new DocumentService();
export default documentService;
export { documentService };
