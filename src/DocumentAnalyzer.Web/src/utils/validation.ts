// Input validation utilities with security focus
export class ValidationUtils {
  // Sanitize user input to prevent XSS
  static sanitizeInput(input: string): string {
    if (typeof input !== 'string') return '';

    return input
      .replace(/[<>]/g, '') // Remove angle brackets
      .replace(/javascript:/gi, '') // Remove javascript: protocol
      .replace(/on\w+=/gi, '') // Remove event handlers
      .trim();
  }

  // Validate email format
  static isValidEmail(email: string): boolean {
    const emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
    return emailRegex.test(email) && email.length <= 254;
  }

  // Validate password strength
  static isValidPassword(password: string): boolean {
    if (password.length < 8) return false;
    if (!/[A-Z]/.test(password)) return false; // Upper case
    if (!/[a-z]/.test(password)) return false; // Lower case
    if (!/\d/.test(password)) return false; // Number
    if (!/[!@#$%^&*(),.?":{}|<>]/.test(password)) return false; // Special char
    return true;
  }

  // Validate file size and type
  static isValidFile(file: File): { valid: boolean; error?: string } {
    const allowedTypes = ['application/pdf', 'text/plain', 'application/msword'];
    const maxSize = 10 * 1024 * 1024; // 10MB

    if (!allowedTypes.includes(file.type)) {
      return { valid: false, error: 'Invalid file type. Only PDF, TXT, and DOC files are allowed.' };
    }

    if (file.size > maxSize) {
      return { valid: false, error: 'File size exceeds 10MB limit.' };
    }

    return { valid: true };
  }

  // Generate secure random ID
  static generateSecureId(): string {
    const array = new Uint32Array(4);
    crypto.getRandomValues(array);
    return Array.from(array, dec => ('0' + dec.toString(16)).substr(-8)).join('');
  }
}
