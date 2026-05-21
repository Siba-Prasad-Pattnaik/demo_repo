import { apiService } from './api';
import { z } from 'zod';

export interface Document {
  id: string;
  fileName: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  uploadDate: string;
  status: 'uploading' | 'processing' | 'completed' | 'failed';
  analysisResults?: AnalysisResult[];
  tags: string[];
  userId: string;
}

export interface AnalysisResult {
  id: string;
  documentId: string;
  analysisType: 'sentiment' | 'entity' | 'summary' | 'classification';
  results: any;
  confidence: number;
  createdAt: string;
}

export interface SearchQuery {
  query?: string;
  tags?: string[];
  dateFrom?: string;
  dateTo?: string;
  analysisType?: string;
  page?: number;
  pageSize?: number;
}

export interface SearchResults {
  documents: Document[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Security: Input validation schemas
const SearchQuerySchema = z.object({
  query: z.string().max(200).optional(),
  tags: z.array(z.string().max(50)).max(10).optional(),
  dateFrom: z.string().datetime().optional(),
  dateTo: z.string().datetime().optional(),
  analysisType: z.enum(['sentiment', 'entity', 'summary', 'classification']).optional(),
  page: z.number().int().min(1).max(1000).optional(),
  pageSize: z.number().int().min(1).max(100).optional()
});

class DocumentService {
  async uploadDocument(
    file: File,
    tags: string[] = [],
    onProgress?: (progress: number) => void
  ): Promise<Document> {
    // Security: Validate tags
    const validTags = z.array(z.string().max(50)).max(10).parse(tags);

    const response = await apiService.uploadFile(file, onProgress);

    // Add tags if provided
    if (validTags.length > 0) {
      await this.updateDocumentTags(response.id, validTags);
    }

    return response;
  }

  async getDocuments(page: number = 1, pageSize: number = 20): Promise<SearchResults> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: Math.min(pageSize, 100).toString() // Security: Limit page size
    });

    return apiService.get<SearchResults>(`/documents?${params}`);
  }

  async getDocument(id: string): Promise<Document> {
    // Security: Validate ID format
    const validId = z.string().uuid().parse(id);
    return apiService.get<Document>(`/documents/${validId}`);
  }

  async deleteDocument(id: string): Promise<void> {
    const validId = z.string().uuid().parse(id);
    await apiService.delete(`/documents/${validId}`);
  }

  async searchDocuments(query: SearchQuery): Promise<SearchResults> {
    // Security: Validate search query
    const validQuery = SearchQuerySchema.parse(query);

    return apiService.post<SearchResults>('/documents/search', validQuery);
  }

  async analyzeDocument(id: string, analysisType: string): Promise<AnalysisResult> {
    const validId = z.string().uuid().parse(id);
    const validAnalysisType = z.enum(['sentiment', 'entity', 'summary', 'classification']).parse(analysisType);

    return apiService.post<AnalysisResult>(`/documents/${validId}/analyze`, {
      analysisType: validAnalysisType
    });
  }

  async getAnalysisResults(documentId: string): Promise<AnalysisResult[]> {
    const validId = z.string().uuid().parse(documentId);
    return apiService.get<AnalysisResult[]>(`/documents/${validId}/analysis`);
  }

  async updateDocumentTags(id: string, tags: string[]): Promise<Document> {
    const validId = z.string().uuid().parse(id);
    const validTags = z.array(z.string().max(50)).max(10).parse(tags);

    return apiService.put<Document>(`/documents/${validId}/tags`, {
      tags: validTags
    });
  }

  async downloadDocument(id: string): Promise<Blob> {
    const validId = z.string().uuid().parse(id);
    const response = await apiService.get(`/documents/${validId}/download`, {
      responseType: 'blob'
    });
    return response as unknown as Blob;
  }

  // Security: Content validation before display
  sanitizeContent(content: string): string {
    // Remove potentially dangerous HTML tags and scripts
    return content
      .replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '')
      .replace(/<iframe\b[^<]*(?:(?!<\/iframe>)<[^<]*)*<\/iframe>/gi, '')
      .replace(/javascript:/gi, '')
      .replace(/on\w+\s*=/gi, '');
  }

  // Security: File type validation
  isValidFileType(file: File): boolean {
    const allowedTypes = [
      'application/pdf',
      'text/plain',
      'application/msword',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      'text/csv',
      'application/json'
    ];
    return allowedTypes.includes(file.type);
  }

  // Security: File size validation
  isValidFileSize(file: File): boolean {
    const maxSize = 10 * 1024 * 1024; // 10MB
    return file.size <= maxSize;
  }

  validateFile(file: File): string | null {
    if (!this.isValidFileType(file)) {
      return 'File type not supported. Please upload PDF, Word, or text documents.';
    }
    if (!this.isValidFileSize(file)) {
      return 'File size must be less than 10MB.';
    }
    return null;
  }
}

export const documentService = new DocumentService();
