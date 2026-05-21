import React, { useState } from 'react';
import {
  Container,
  Paper,
  TextField,
  Button,
  Typography,
  Box,
  Alert,
  Link,
  Grid,
} from '@mui/material';
import { useNavigate, Link as RouterLink } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { validateEmail, validatePassword, validateRequired } from '../../utils/validation';

const RegisterPage: React.FC = () => {
  const [formData, setFormData] = useState({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    confirmPassword: '',
  });
  const [errors, setErrors] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  const { register } = useAuth();
  const navigate = useNavigate();

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value,
    }));
  };

  const validateForm = () => {
    const newErrors: string[] = [];

    const firstNameError = validateRequired(formData.firstName, 'First name');
    if (firstNameError) newErrors.push(firstNameError);

    const lastNameError = validateRequired(formData.lastName, 'Last name');
    if (lastNameError) newErrors.push(lastNameError);

    if (!validateEmail(formData.email)) {
      newErrors.push('Please enter a valid email address');
    }

    const passwordErrors = validatePassword(formData.password);
    newErrors.push(...passwordErrors);

    if (formData.password !== formData.confirmPassword) {
      newErrors.push('Passwords do not match');
    }

    return newErrors;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const validationErrors = validateForm();
    if (validationErrors.length > 0) {
      setErrors(validationErrors);
      return;
    }

    setErrors([]);
    setIsLoading(true);

    try {
      await register(
        formData.email,
        formData.password,
        formData.firstName,
        formData.lastName
      );
      navigate('/');
    } catch (err: any) {
      setErrors([err.message || 'Registration failed']);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <Container component="main" maxWidth="sm">
      <Box
        sx={{
          marginTop: 8,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
        }}
      >
        <Paper elevation={3} sx={{ padding: 4, width: '100%' }}>
          <Typography
