import React, { useState, FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { authService } from '../services/authService';
import { sanitizeInput, validateEmail, rateLimitChecker } from '../utils/validation';
import { logSecurityEvent } from '../utils/security';

interface LoginFormProps {
  onLogin?: () => void;
}

export const LoginForm: React.FC<LoginFormProps> = ({ onLogin }) => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [attempts, setAttempts] = useState(0);
  const navigate = useNavigate();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    // Rate limiting
    if (!rateLimitChecker('login', 5, 15 * 60 * 1000)) {
      setError('Too many login attempts. Please try again later.');
      logSecurityEvent('LOGIN_RATE_LIMITED', { username: sanitizeInput(username) });
      return;
    }

    if (attempts >= 3) {
      setError('Account temporarily locked due to multiple failed attempts.');
      logSecurityEvent('LOGIN_ACCOUNT_LOCKED', { username: sanitizeInput(username) });
      return;
    }

    setLoading(true);
    setError('');

    try {
      const sanitizedUsername = sanitizeInput(username.trim());

      // Basic validation
      if (!sanitizedUsername || !password) {
        throw new Error('Username and password are required');
      }

      if (validateEmail(sanitizedUsername) && !validateEmail(sanitizedUsername)) {
        throw new Error('Invalid email format');
      }

      await authService.login({
        username: sanitizedUsername,
        password: password,
      });

      logSecurityEvent('LOGIN_SUCCESS', { username: sanitizedUsername });
      onLogin?.();
      navigate('/dashboard');
    } catch (err: any) {
      const newAttempts = attempts + 1;
      setAttempts(newAttempts);
      setError(err.message || 'Login failed');
      logSecurityEvent('LOGIN_FAILED', {
        username: sanitizeInput(username),
        attempt: newAttempts
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-form-container">
      <form onSubmit={handleSubmit} className="login-form">
        <h2>Login</h2>

        {error && (
          <div className="error-message" role="alert">
            {error}
          </div>
        )}

        <div className="form-group">
          <label htmlFor="username">Username/Email:</label>
          <input
            id="username"
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            disabled={loading}
            autoComplete="username"
            maxLength={100}
            required
          />
        </div>

        <div className="form-group">
          <label htmlFor="password">Password:</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={loading}
            autoComplete="current-password"
            maxLength={128}
            required
          />
        </div>

        <button
          type="submit"
          disabled={loading || attempts >= 3}
          className="login-button"
        >
          {loading ? 'Logging in...' : 'Login'}
        </button>

        {attempts > 0 && attempts < 3 && (
          <div className="warning-message">
            {3 - attempts} attempts remaining
          </div>
        )}
      </form>
    </div>
  );
};
