import DOMPurify from 'isomorphic-dompurify';

// Security: HTML sanitization
export const sanitizeHtml = (dirty: string): string => {
  return DOMPurify.sanitize(dirty, {
    ALLOWED_TAGS: ['b', 'i', 'em', 'strong', 'a', 'p', 'br', 'ul', 'ol', 'li'],
    ALLOWED_ATTR: ['href', 'target'],
    ALLOW_DATA_ATTR: false
  });
};

// Security: XSS prevention for text content
export const escapeHtml = (unsafe: string): string => {
  return unsafe
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
};

// Security: Input validation helpers
export const validateEmail = (email: string): boolean => {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email) && email.length <= 254;
};

export const validatePassword = (password: string): string[] => {
  const errors: string[] = [];

  if (password.length < 8) {
    errors.push('Password must be at least 8 characters long');
  }
  if (!/[a-z]/.test(password)) {
    errors.push('Password must contain at least one lowercase letter');
  }
  if (!/[A-Z]/.test(password)) {
    errors.push('Password must contain at least one uppercase letter');
  }
  if (!/\d/.test(password)) {
    errors.push('Password must contain at least one number');
  }
  if (!/[@$!%*?&]/.test(password)) {
    errors.push('Password must contain at least one special character');
  }

  return errors;
};

// Security: Rate limiting helper
class RateLimiter {
  private attempts: Map<string, { count: number; lastAttempt: number }> = new Map();
  private maxAttempts: number;
  private windowMs: number;

  constructor(maxAttempts: number = 5, windowMs: number = 15 * 60 * 1000) {
    this.maxAttempts = maxAttempts;
    this.windowMs = windowMs;
  }

  canAttempt(key: string): boolean {
    const now = Date.now();
    const record = this.attempts.get(key);

    if (!record) {
      return true;
    }

    if (now - record.lastAttempt > this.windowMs) {
      this.attempts.delete(key);
      return true;
    }

    return record.count < this.maxAttempts;
  }

  recordAttempt(key: string): void {
    const now = Date.now();
    const record = this.attempts.get(key);

    if (!record || now - record.lastAttempt > this.windowMs) {
      this.attempts.set(key, { count: 1, lastAttempt: now });
    } else {
      record.count++;
      record.lastAttempt = now;
    }
  }

  getRemainingAttempts(key: string): number {
    const record = this.attempts.get(key);
    if (!record) return this.maxAttempts;

    const now = Date.now();
    if (now - record.lastAttempt > this.windowMs) {
      return this.maxAttempts;
    }

    return Math.max(0, this.maxAttempts - record.count);
  }
}

export const loginRateLimiter = new RateLimiter(5, 15 * 60 * 1000); // 5 attempts per 15 minutes

// Security: CSRF token helpers
export const getCsrfToken = (): string | null => {
  const meta = document.querySelector('meta[name="csrf-token"]') as HTMLMetaElement;
  return meta?.content || null;
};

export const generateCSRFToken = (): string => {
  return Array.from(crypto.getRandomValues(new Uint8Array(32)))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
};

// Security: Content Security Policy violation reporting
export const setupCspReporting = (): void => {
  if (typeof window !== 'undefined') {
    document.addEventListener('securitypolicyviolation', (event) => {
      console.error('CSP Violation:', {
        directive: event.violatedDirective,
        blocked: event.blockedURI,
        source: event.sourceFile,
        line: event.lineNumber
      });

      // Report to monitoring service in production
      if (process.env.NODE_ENV === 'production') {
        // Send violation report to security monitoring
        fetch('/api/security/csp-violation', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            directive: event.violatedDirective,
            blocked: event.blockedURI,
            source: event.sourceFile,
            line: event.lineNumber,
            timestamp: new Date().toISOString()
          })
        }).catch(() => {}); // Fail silently
      }
    });
  }
};

// Security: Generate secure random strings
export const generateSecureId = (length: number = 32): string => {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
  const array = new Uint8Array(length);
  crypto.getRandomValues(array);
  return Array.from(array, byte => chars[byte % chars.length]).join('');
};

export const hashString = async (input: string): Promise<string> => {
  const encoder = new TextEncoder();
  const data = encoder.encode(input);
  const hashBuffer = await crypto.subtle.digest('SHA-256', data);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
};

export const isSecureContext = (): boolean => {
  return window.isSecureContext || window.location.protocol === 'https:';
};

export const logSecurityEvent = (event: string, details?: any): void => {
  console.warn(`Security Event: ${event}`, details);
  // In production, send to security monitoring service
};

export const rateLimitChecker = (key: string, limit: number, windowMs: number): boolean => {
  const now = Date.now();
  const windowStart = now - windowMs;

  const attempts = JSON.parse(localStorage.getItem(`rl_${key}`) || '[]')
    .filter((timestamp: number) => timestamp > windowStart);

  if (attempts.length >= limit) {
    return false;
  }

  attempts.push(now);
  localStorage.setItem(`rl_${key}`, JSON.stringify(attempts));
  return true;
};

// Security: Secure localStorage wrapper
export const secureStorage = {
  setItem: (key: string, value: string): void => {
    try {
      // Encrypt sensitive data before storing (in production, use proper encryption)
      const encrypted = btoa(value); // Basic encoding - use proper encryption in production
      localStorage.setItem(key, encrypted);
    } catch (error) {
      console.error('Failed to store item securely:', error);
    }
  },

  getItem: (key: string): string | null => {
    try {
      const encrypted = localStorage.getItem(key);
      if (!encrypted) return null;
      // Decrypt data after retrieving (in production, use proper decryption)
      return atob(encrypted); // Basic decoding - use proper decryption in production
    } catch (error) {
      console.error('Failed to retrieve item securely:', error);
      return null;
    }
  },

  removeItem: (key: string): void => {
    localStorage.removeItem(key);
  }
};
