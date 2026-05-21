import axios, { AxiosResponse } from 'axios';
import Cookies from 'js-cookie';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:5001/api';

interface LoginCredentials {
  username: string;
  password: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

interface User {
  id: string;
  username: string;
  email: string;
  role: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  user: {
    id: string;
    username: string;
    email: string;
    roles: string[];
  };
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

class AuthService {
  private readonly TOKEN_KEY = 'doc_analyzer_token';
  private readonly REFRESH_TOKEN_KEY = 'doc_analyzer_refresh_token';
  private readonly USER_KEY = 'doc_analyzer_user';

  // Secure token storage with expiration
  private setTokens(token: string, refreshToken: string): void {
    const tokenData = {
      token,
      timestamp: Date.now(),
      expires: Date.now() + (24 * 60 * 60 * 1000) // 24 hours
    };

    try {
      sessionStorage.setItem(this.TOKEN_KEY, JSON.stringify(tokenData));
      sessionStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
      
      Cookies.set(this.TOKEN_KEY, token, {
        httpOnly: false,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        expires: 1, // 1 day
      });

      Cookies.set(this.REFRESH_TOKEN_KEY, refreshToken, {
        httpOnly: false,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        expires: 7, // 7 days
      });
    } catch (error) {
      console.error('Failed to store authentication tokens:', error);
      throw new Error('Authentication storage failed');
    }
  }

  getToken(): string | null {
    try {
      const tokenData = sessionStorage.getItem(this.TOKEN_KEY);
      if (!tokenData) {
        return Cookies.get(this.TOKEN_KEY) || null;
      }

      const parsed = JSON.parse(tokenData);
      if (Date.now() > parsed.expires) {
        this.clearTokens();
        return null;
      }

      return parsed.token;
    } catch (error) {
      console.error('Token retrieval error:', error);
      this.clearTokens();
      return Cookies.get(this.TOKEN_KEY) || null;
    }
  }

  getRefreshToken(): string | null {
    return sessionStorage.getItem(this.REFRESH_TOKEN_KEY) || Cookies.get(this.REFRESH_TOKEN_KEY) || null;
  }

  private clearTokens(): void {
    sessionStorage.removeItem(this.TOKEN_KEY);
    sessionStorage.removeItem(this.REFRESH_TOKEN_KEY);
    sessionStorage.removeItem(this.USER_KEY);
    Cookies.remove(this.TOKEN_KEY);
    Cookies.remove(this.REFRESH_TOKEN_KEY);
  }

  async login(credentials: LoginCredentials | LoginRequest): Promise<AuthResponse> {
    try {
      // Input validation
      if (!credentials.username || !credentials.password) {
        throw new Error('Username and password are required');
      }

      if (credentials.username.length < 3 || credentials.password.length < 8) {
        throw new Error('Invalid credentials format');
      }

      const response: AxiosResponse<AuthResponse> = await axios.post(
        `${API_BASE_URL}/auth/login`,
        {
          username: credentials.username.trim(),
          password: credentials.password
        },
        {
          headers: {
            'Content-Type': 'application/json',
          },
          withCredentials: true,
        }
      );

      const authData: AuthResponse = response.data;

      if (!authData.token || !authData.user) {
        throw new Error('Invalid authentication response');
      }

      this.setTokens(authData.token, authData.refreshToken);
      sessionStorage.setItem(this.USER_KEY, JSON.stringify(authData.user));

      return authData;
    } catch (error: any) {
      console.error('Login failed:', error);
      throw new Error(error.response?.data?.message || 'Authentication failed');
    }
  }

  async logout(): Promise<void> {
    try {
      const token = this.getToken();
      const refreshToken = this.getRefreshToken();
      
      if (token) {
        await axios.post(`${API_BASE_URL}/auth/logout`, 
          refreshToken ? { refreshToken } : {}, 
          {
            headers: { Authorization: `Bearer ${token}` }
          }
        );
      }
    } catch (error) {
      console.error('Logout request failed:', error);
    } finally {
      this.clearTokens();
    }
  }

  async refreshToken(): Promise<string | null> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      return null;
    }

    try {
      const response: AxiosResponse<AuthResponse> = await axios.post(
        `${API_BASE_URL}/auth/refresh`,
        { refreshToken }
      );

      if (response.data.token) {
        this.setTokens(response.data.token, response.data.refreshToken);
        return response.data.token;
      }
    } catch (error) {
      this.clearTokens();
    }

    return null;
  }

  getCurrentUser(): User | null {
    try {
      const userData = sessionStorage.getItem(this.USER_KEY);
      return userData ? JSON.parse(userData) : null;
    } catch (error) {
      console.error('User data retrieval error:', error);
      return null;
    }
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) return false;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp > Date.now() / 1000;
    } catch {
      return this.getToken() !== null && this.getCurrentUser() !== null;
    }
  }
}

export const authService = new AuthService();
export default new AuthService();
