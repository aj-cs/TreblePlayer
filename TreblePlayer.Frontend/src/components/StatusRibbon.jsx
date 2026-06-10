import React, { useState, useEffect } from 'react';
import { Box, Typography, LinearProgress, Fade } from '@mui/material';
import { SyncOutlined, DeleteOutlined, AddOutlined } from '@mui/icons-material';

const StatusRibbon = ({ status, message, visible }) => {
  // Auto-hide timeout (ms)
  const autoHideTimeout = 5000;
  const [isVisible, setIsVisible] = useState(visible);

  // Set up icons based on operation type
  const getIcon = () => {
    switch (status) {
      case 'scanning':
      case 'processing':
        return <SyncOutlined sx={{ mr: 1, animation: 'spin 2s linear infinite' }} />;
      case 'adding':
        return <AddOutlined sx={{ mr: 1 }} />;
      case 'removing':
        return <DeleteOutlined sx={{ mr: 1 }} />;
      default:
        return null;
    }
  };

  // Get background color based on status
  const getBackgroundColor = () => {
    switch (status) {
      case 'scanning':
      case 'processing':
        return 'rgba(25, 118, 210, 0.9)'; // Blue for processing
      case 'adding':
        return 'rgba(46, 125, 50, 0.9)'; // Green for adding
      case 'removing':
        return 'rgba(211, 47, 47, 0.9)'; // Red for removing
      default:
        return 'rgba(0, 0, 0, 0.7)'; // Default dark
    }
  };

  // Auto-hide the ribbon after timeout
  useEffect(() => {
    setIsVisible(visible);
    
    if (visible) {
      const timer = setTimeout(() => {
        setIsVisible(false);
      }, autoHideTimeout);
      
      return () => clearTimeout(timer);
    }
  }, [visible, message]);

  // If not visible, don't render anything
  if (!isVisible) return null;

  return (
    <Fade in={isVisible}>
      <Box
        sx={{
          position: 'fixed',
          bottom: 70, // Above the playback controls
          left: '50%',
          transform: 'translateX(-50%)',
          zIndex: 1500,
          width: 'auto',
          minWidth: '300px',
          maxWidth: '80%',
          bgcolor: getBackgroundColor(),
          color: 'white',
          borderRadius: 2,
          boxShadow: 3,
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            p: 1.5,
          }}
        >
          {getIcon()}
          <Typography variant="body1" component="div">
            {message || 'Processing...'}
          </Typography>
        </Box>
        {(status === 'scanning' || status === 'processing') && (
          <LinearProgress 
            sx={{ 
              height: 4,
              '& .MuiLinearProgress-bar': {
                transition: 'transform 0.2s linear'
              }
            }}
          />
        )}
      </Box>
    </Fade>
  );
};

export default StatusRibbon;

// Add the spinning animation
const styleTag = document.createElement('style');
styleTag.textContent = `
  @keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
  }
`;
document.head.appendChild(styleTag); 