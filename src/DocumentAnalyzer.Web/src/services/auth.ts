import { apiService } from './api';
import { z } from 'zod';

// Security: Input validation schemas
const LoginSchema = z.object({
  email: z.string().email('Invalid email format'),
  password: z.string().min(8, 'Password must be at least 8 characters')
});

const RegisterSchema = z.object({
  email: z.string().email('Invalid email format'),
  password: z.string().min(8, 'Password must be at least 8 characters')
    .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]/,
      'Password must contain uppercase, lowercase, number and special character'),
  confirmPassword: z.string(),
  firstName: z.string().min(1, 'First name is required').max(50),
  lastName: z.string().min(1, 'Last name is required').max(50)
}).refine((data) => data.password === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword']
});

export type LoginData = z.infer<typeof LoginSchema>;
export type RegisterData = z.infer<typeof RegisterSchema>;

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  isEmailConfirmed: boolean;
}

export interface AuthResponse {
  user: User;
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

class AuthService {
  async login(credentials: LoginData): Promise<AuthResponse> {
    // Security: Validate input
    const validCredentials = LoginSchema.parse(credentials);

    const response = await apiService.post<AuthResponse>('/auth/login', {
      email: validCredentials.email.toLowerCase().trim(),
      password: validCredentials.password
    });

    // Security: Store tokens securely
    apiService.setToken(response.accessToken);
    apiService.setRefreshToken(response.refreshToken);

    return response;
  }

  async register(userData: RegisterData): Promise<AuthResponse> {
    // Security: Validate input
    const validUserData = RegisterSchema.parse(userData);

    const response = await apiService.post<AuthResponse>('/auth/register', {
      email: validUserData.email.toLowerCase().trim(),
      password: validUserData.password,
      firstName: validUserData.firstName.trim(),
      lastName: validUserData.lastName.trim()
    });

    // Security: Store tokens securely
    apiService.setToken(response.accessToken);
    apiService.setRefreshToken(response.refreshToken);

    return response;
  }

  async logout(): Promise<void> {
    try {
      await apiService.post('/auth/logout');
    } catch (error) {
      // Continue with logout even if API call fails
      console.error('Logout API call failed:', error);
    } finally {
      // Security: Clear tokens
      apiService.clearTokens();
    }
  }

  async getCurrentUser(): Promise<User> {
    return apiService.get<User>('/auth/me');
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    // Security: Validate new password
    const schema = z.string().min(8)
      .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]/);

    const validNewPassword = schema.parse(newPassword);

    await apiService.post('/auth/change-password', {
      currentPassword,
      newPassword: validNewPassword
    });
  }

  async requestPasswordReset(email: string): Promise<void> {
    const validEmail = z.string().email().parse(email);
    await apiService.post('/auth/forgot-password', {
      email: validEmail.toLowerCase().trim()
    });
  }

  async resetPassword(token: string, newPassword: string): Promise<void> {
    const schema = z.string().min(8)
      .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]/);

    const validNewPassword = schema.parse(newPassword);

    await apiService.post('/auth/reset-password', {
      token,
      newPassword: validNewPassword
    });
  }

  isAuthenticated(): boolean {
    return !!apiService.getToken();
  }

  // Security: Rate limiting helper
  private lastLoginAttempt: number = 0;
  private loginAttempts: number = 0;
  private readonly MAX_LOGIN_ATTEMPTS = 5;
  private readonly LOCKOUT_DURATION = 15 * 60 * 1000; // 15 minutes

  canAttemptLogin(): boolean {
    const now = Date.now();
    if (this.loginAttempts >= this.MAX_LOGIN_ATTEMPTS) {
      if (now - this.lastLoginAttempt < this.LOCKOUT_DURATION) {
        return false;
      } else {
        // Reset after lockout period
        this.loginAttempts = 0;
      }
    }
    return true;
  }

  recordLoginAttempt(): void {
    this.lastLoginAttempt = Date.now();
    this.loginAttempts++;
  }

  resetLoginAttempts(): void {
    this.loginAttempts = 0;
    this.lastLoginAttempt = 0;
  }
}

export const authService = new AuthService();
