import axios, { AxiosResponse } from 'axios';
import Cookies from 'js-cookie';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:5001/api';

export interface LoginRequest {
  username: string;
  password: string;
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
  private readonly TOKEN_KEY = 'auth_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';

  async login(credentials: LoginRequest): Promise<AuthResponse> {
    try {
      const response: AxiosResponse<AuthResponse> = await axios.post(
        `${API_BASE_URL}/auth/login`,
        credentials,
        {
          headers: {
            'Content-Type': 'application/json',
          },
          withCredentials: true,
        }
      );

      if (response.data.token) {
        this.setTokens(response.data.token, response.data.refreshToken);
      }

      return response.data;
    } catch (error: any) {
      throw new Error(error.response?.data?.message || 'Login failed');
    }
  }

  async logout(): Promise<void> {
    try {
      const refreshToken = this.getRefreshToken();
      if (refreshToken) {
        await axios.post(`${API_BASE_URL}/auth/logout`, { refreshToken });
      }
    } catch (error) {
      console.warn('Logout request failed:', error);
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

  getToken(): string | null {
    return Cookies.get(this.TOKEN_KEY) || null;
  }

  getRefreshToken(): string | null {
    return Cookies.get(this.REFRESH_TOKEN_KEY) || null;
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) return false;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp > Date.now() / 1000;
    } catch {
      return false;
    }
  }

  private setTokens(token: string, refreshToken: string): void {
    Cookies.set(this.TOKEN_KEY, token, {
      httpOnly: false, // Can't be httpOnly in client-side
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
  }

  private clearTokens(): void {
    Cookies.remove(this.TOKEN_KEY);
    Cookies.remove(this.REFRESH_TOKEN_KEY);
  }
}

export const authService = new AuthService();
