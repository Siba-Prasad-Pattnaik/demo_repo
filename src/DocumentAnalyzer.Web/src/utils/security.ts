export const generateCSRFToken = (): string => {
  return Array.from(crypto.getRandomValues(new Uint8Array(32)))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
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
