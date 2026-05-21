export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  createdAt: string;
}

export interface Document {
  id: string;
  fileName: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
  userId: string;
  status: 'Uploaded' | 'Processing' | 'Analyzed' | 'Failed';
  analysisResults?: AnalysisResult[];
}

export interface AnalysisResult {
  id: string;
  documentId: string;
  analysisType: string;
  result: any;
  confidence: number;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  user: User;
  expiresAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface ApiError {
  message: string;
  statusCode: number;
  errors?: Record<string, string[]>;
}

export interface SearchRequest {
  query: string;
  filters?: {
    contentType?: string;
    dateRange?: {
      start: string;
      end: string;
    };
  };
}

export interface SearchResult {
  documents: Document[];
  totalCount: number;
  pageSize: number;
  currentPage: number;
}
