import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { LoginForm } from '../components/LoginForm';
import { authService } from '../services/authService';

export const Login: React.FC = () => {
  const navigate = useNavigate();

  useEffect(() => {
    // Redirect if already authenticated
    if (authService.isAuthenticated()) {
      navigate('/dashboard');
    }
  }, [navigate]);

  const handleLoginSuccess = () => {
    navigate('/dashboard');
  };

  return (
    <div className="login-page">
      <div className="login-container">
        <div className="login-header">
          <h1>Document Analyzer</h1>
          <p>Secure document analysis platform</p>
        </div>

        <LoginForm onLogin={handleLoginSuccess} />

        <div className="login-footer">
          <p>
            <small>
              This is a secure system. All activities are logged and monitored.
            </small>
          </p>
        </div>
      </div>
    </div>
  );
};
