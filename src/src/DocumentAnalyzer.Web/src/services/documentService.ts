import apiClient from './api';
import { Document, AnalysisResult, SearchRequest, SearchResult } from '../types';

class DocumentService {
  async uploadDocument(file: File): Promise<Document> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await apiClient.post<Document>('/documents/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return response.data;
  }

  async getDocuments(page: number = 1, pageSize: number = 10): Promise<SearchResult> {
    const response = await apiClient.get<SearchResult>('/documents', {
      params: { page, pageSize },
    });
    return response.data;
  }

  async getDocument(id: string): Promise<Document> {
    const response = await apiClient.get<Document>(`/documents/${id}`);
    return response.data;
  }

  async deleteDocument(id: string): Promise<void> {
    await apiClient.delete(`/documents/${id}`);
  }

  async downloadDocument(id: string): Promise<Blob> {
    const response = await apiClient.get(`/documents/${id}/download`, {
      responseType: 'blob',
    });
    return response.data;
  }

  async getAnalysisResults(documentId: string): Promise<AnalysisResult[]> {
    const response = await apiClient.get<AnalysisResult[]>(`/analysis/document/${documentId}`);
    return response.data;
  }

  async analyzeDocument(documentId: string, analysisType: string): Promise<AnalysisResult> {
    const response = await apiClient.post<AnalysisResult>('/analysis/analyze', {
      documentId,
      analysisType,
    });
    return response.data;
  }

  async searchDocuments(searchRequest: SearchRequest): Promise<SearchResult> {
    const response = await apiClient.post<SearchResult>('/search', searchRequest);
    return response.data;
  }
}

export default new DocumentService();
