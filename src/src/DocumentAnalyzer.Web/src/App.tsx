import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { QueryClient, QueryClientProvider } from 'react-query';
import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import { AuthProvider } from './contexts/AuthContext';
import Layout from './components/layout/Layout';
import ProtectedRoute from './components/auth/ProtectedRoute';
import LoginPage from './pages/auth/LoginPage';
import RegisterPage from './pages/auth/RegisterPage';
import DashboardPage from './pages/dashboard/DashboardPage';
import Dashboard from './pages/dashboard/Dashboard';
import DocumentListPage from './pages/documents/DocumentListPage';
import DocumentsPage from './pages/documents/DocumentsPage';
import DocumentUploadPage from './pages/documents/DocumentUploadPage';
import DocumentViewPage from './pages/documents/DocumentViewPage';
import DocumentDetailPage from './pages/documents/DocumentDetailPage';
import ProfilePage from './pages/profile/ProfilePage';
import './App.css';

const theme = createTheme({
  palette: {
    primary: {
      main: '#1976d2',
    },
    secondary: {
      main: '#dc004e',
    },
  },
});

const queryClient = new QueryClient();

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <AuthProvider>
          <Router>
            <div className="App">
              <Routes>
                <Route path="/login" element={<LoginPage />} />
                <Route path="/register" element={<RegisterPage />} />
                <Route
                  path="/*"
                  element={
                    <ProtectedRoute>
                      <Layout>
                        <Routes>
                          <Route path="/" element={<DashboardPage />} />
                          <Route path="/dashboard" element={<DashboardPage />} />
                          <Route path="/documents" element={<DocumentListPage />} />
                          <Route path="/documents/upload" element={<DocumentUploadPage />} />
                          <Route path="/documents/:id" element={<DocumentViewPage />} />
                          <Route path="/profile" element={<ProfilePage />} />
                        </Routes>
                      </Layout>
                    </ProtectedRoute>
                  }
                />
              </Routes>
              <ToastContainer />
            </div>
          </Router>
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
