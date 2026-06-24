import React, { createContext, useState, useContext, useEffect } from 'react';
import { useWebSocket } from './WebSocketContext';

// Create context
const StatusContext = createContext();

export const StatusProvider = ({ children }) => {
  const [status, setStatus] = useState(null); // 'scanning', 'adding', 'removing', 'processing', etc.
  const [message, setMessage] = useState('');
  const [visible, setVisible] = useState(false);
  const { subscribe } = useWebSocket();

  // Initialize WebSocket subscription
  useEffect(() => {
    const unsubscribe = subscribe((message) => {
      switch (message.type) {
        case 'LibraryUpdated':
          showStatus('processing', 'Library updated, refreshing data...');
          break;
        case 'MonitoredFoldersUpdated':
          showStatus('processing', 'Folder settings updated...');
          break;
        case 'LibraryCleaned':
          showStatus('removing', 'Library cleaned, removing orphaned files...');
          break;
        case 'ScanningStarted':
          showStatus('scanning', `Scanning ${message.data || ''} folder(s)...`);
          break;
        case 'ScanComplete':
          showStatus('processing', 'Scan completed successfully');
          break;
        case 'FolderAdded':
          showStatus('adding', `Added folder: ${message.data || ''}`);
          break;
        case 'FolderRemoved':
          showStatus('removing', `Removed folder: ${message.data || ''}`);
          break;
      }
    });

    return () => unsubscribe();
  }, [subscribe]);

  // Function to show status message
  const showStatus = (newStatus, newMessage) => {
    setStatus(newStatus);
    setMessage(newMessage);
    setVisible(true);
    
    // Automatically hide after a timeout (done in StatusRibbon component)
  };

  // Hide status message manually
  const hideStatus = () => {
    setVisible(false);
  };

  // Provide context value
  const contextValue = {
    status,
    message,
    visible,
    showStatus,
    hideStatus
  };

  return (
    <StatusContext.Provider value={contextValue}>
      {children}
    </StatusContext.Provider>
  );
};

// Custom hook for using the context
export const useStatus = () => {
  const context = useContext(StatusContext);
  if (!context) {
    throw new Error('useStatus must be used within a StatusProvider');
  }
  return context;
}; 