import axios from 'axios';

interface LoginCredentials {
  username: string;
  password: string;
}

interface User {
  id: string;
  username: string;
  email: string;
  role: string;
}

interface AuthResponse {
  user: User;
  token: string;
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
    } catch (error) {
      console.error('Failed to store authentication tokens:', error);
      throw new Error('Authentication storage failed');
    }
  }

  getToken(): string | null {
    try {
      const tokenData = sessionStorage.getItem(this.TOKEN_KEY);
      if (!tokenData) return null;

      const parsed = JSON.parse(tokenData);
      if (Date.now() > parsed.expires) {
        this.clearTokens();
        return null;
      }

      return parsed.token;
    } catch (error) {
      console.error('Token retrieval error:', error);
      this.clearTokens();
      return null;
    }
  }

  private clearTokens(): void {
    sessionStorage.removeItem(this.TOKEN_KEY);
    sessionStorage.removeItem(this.REFRESH_TOKEN_KEY);
    sessionStorage.removeItem(this.USER_KEY);
  }

  async login(credentials: LoginCredentials): Promise<AuthResponse> {
    try {
      // Input validation
      if (!credentials.username || !credentials.password) {
        throw new Error('Username and password are required');
      }

      if (credentials.username.length < 3 || credentials.password.length < 8) {
        throw new Error('Invalid credentials format');
      }

      const response = await axios.post('/api/auth/login', {
        username: credentials.username.trim(),
        password: credentials.password
      });

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
      if (token) {
        await axios.post('/api/auth/logout', {}, {
          headers: { Authorization: `Bearer ${token}` }
        });
      }
    } catch (error) {
      console.error('Logout request failed:', error);
    } finally {
      this.clearTokens();
    }
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
    return this.getToken() !== null && this.getCurrentUser() !== null;
  }
}

export default new AuthService();
