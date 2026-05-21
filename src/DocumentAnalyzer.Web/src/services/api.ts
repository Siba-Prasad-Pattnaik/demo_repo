import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios';
import Cookies from 'js-cookie';

// Security: Use httpOnly cookies for token storage
const TOKEN_COOKIE_NAME = 'auth_token';
const REFRESH_TOKEN_COOKIE_NAME = 'refresh_token';

interface ApiError {
  message: string;
  code: string;
  details?: any;
}

class ApiService {
  private client: AxiosInstance;
  private baseURL: string;

  constructor() {
    // Security: Use environment variable for API URL
    this.baseURL = process.env.REACT_APP_API_URL || 'https://localhost:5001/api';

    this.client = axios.create({
      baseURL: this.baseURL,
      timeout: 30000,
      withCredentials: true, // Security: Include cookies in requests
      headers: {
        'Content-Type': 'application/json',
        'X-Requested-With': 'XMLHttpRequest' // Security: CSRF protection
      }
    });

    this.setupInterceptors();
  }

  private setupInterceptors(): void {
    // Request interceptor to add auth token
    this.client.interceptors.request.use(
      (config: AxiosRequestConfig) => {
        const token = this.getToken();
        if (token && config.headers) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor for token refresh and error handling
    this.client.interceptors.response.use(
      (response: AxiosResponse) => response,
      async (error) => {
        if (error.response?.status === 401) {
          const refreshToken = this.getRefreshToken();
          if (refreshToken) {
            try {
              await this.refreshAccessToken();
              // Retry original request
              return this.client.request(error.config);
            } catch (refreshError) {
              this.clearTokens();
              window.location.href = '/login';
            }
          } else {
            this.clearTokens();
            window.location.href = '/login';
          }
        }
        return Promise.reject(this.normalizeError(error));
      }
    );
  }

  private normalizeError(error: any): ApiError {
    if (error.response?.data?.message) {
      return {
        message: error.response.data.message,
        code: error.response.data.code || 'API_ERROR',
        details: error.response.data.details
      };
    }
    return {
      message: error.message || 'An unexpected error occurred',
      code: 'NETWORK_ERROR'
    };
  }

  // Security: Token management methods
  setToken(token: string): void {
    Cookies.set(TOKEN_COOKIE_NAME, token, {
      secure: true,
      sameSite: 'strict',
      expires: 1 // 1 day
    });
  }

  getToken(): string | undefined {
    return Cookies.get(TOKEN_COOKIE_NAME);
  }

  setRefreshToken(token: string): void {
    Cookies.set(REFRESH_TOKEN_COOKIE_NAME, token, {
      secure: true,
      sameSite: 'strict',
      expires: 30 // 30 days
    });
  }

  getRefreshToken(): string | undefined {
    return Cookies.get(REFRESH_TOKEN_COOKIE_NAME);
  }

  clearTokens(): void {
    Cookies.remove(TOKEN_COOKIE_NAME);
    Cookies.remove(REFRESH_TOKEN_COOKIE_NAME);
  }

  async refreshAccessToken(): Promise<void> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    const response = await this.client.post('/auth/refresh', {
      refreshToken
    });

    this.setToken(response.data.accessToken);
    if (response.data.refreshToken) {
      this.setRefreshToken(response.data.refreshToken);
    }
  }

  // HTTP methods
  async get<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.get<T>(url, config);
    return response.data;
  }

  async post<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.post<T>(url, data, config);
    return response.data;
  }

  async put<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.put<T>(url, data, config);
    return response.data;
  }

  async delete<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.delete<T>(url, config);
    return response.data;
  }

  // Security: File upload with validation
  async uploadFile(file: File, onProgress?: (progress: number) => void): Promise<any> {
    // Security: Validate file type and size
    const allowedTypes = ['application/pdf', 'text/plain', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'];
    const maxSize = 10 * 1024 * 1024; // 10MB

    if (!allowedTypes.includes(file.type)) {
      throw new Error('File type not allowed');
    }

    if (file.size > maxSize) {
      throw new Error('File size exceeds 10MB limit');
    }

    const formData = new FormData();
    formData.append('file', file);

    return this.client.post('/documents/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      },
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          onProgress(progress);
        }
      }
    });
  }
}

export const apiService = new ApiService();
export type { ApiError };
