import React, { ReactNode } from 'react';
import { Box } from '@mui/material';
import { useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import Header from './Header';
import Navbar from './Navbar';
import Sidebar from './Sidebar';

interface LayoutProps {
  children: ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const isAuthPage = location.pathname === '/login' || location.pathname === '/register';

  if (isAuthPage || !isAuthenticated) {
    return <Box>{children}</Box>;
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <Header />
      <Navbar />
      <Box sx={{ display: 'flex' }}>
        <Sidebar />
        <Box
          component="main"
          sx={{
            flexGrow: 1,
            p: 3,
            mt: 8, // Account for navbar height
            ml: 30, // Account for sidebar width
          }}
        >
          <div className="container">
            {children}
          </div>
        </Box>
      </Box>
    </div>
  );
};

export default Layout;
