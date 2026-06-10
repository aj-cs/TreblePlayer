import React, { createContext, useState, useContext, useEffect } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

// Create context
const StatusContext = createContext();

export const StatusProvider = ({ children }) => {
  const [status, setStatus] = useState(null); // 'scanning', 'adding', 'removing', 'processing', etc.
  const [message, setMessage] = useState('');
  const [visible, setVisible] = useState(false);
  const [connection, setConnection] = useState(null);

  // Initialize SignalR connection
  useEffect(() => {
    const newConnection = new HubConnectionBuilder()
      .withUrl('http://localhost:5155/datahub') // Use your actual API URL
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    setConnection(newConnection);

    // Start the connection
    const startConnection = async () => {
      try {
        await newConnection.start();
        console.log('SignalR connected for status updates');
        
        // Register SignalR event handlers
        newConnection.on('LibraryUpdated', () => {
          // Handle library updated event
          showStatus('processing', 'Library updated, refreshing data...');
        });
        
        newConnection.on('MonitoredFoldersUpdated', () => {
          // Handle monitored folders updated event
          showStatus('processing', 'Folder settings updated...');
        });
        
        newConnection.on('LibraryCleaned', () => {
          // Handle library cleaned event
          showStatus('removing', 'Library cleaned, removing orphaned files...');
        });
        
        // Custom events (can be added on backend)
        newConnection.on('ScanningStarted', (folderCount) => {
          showStatus('scanning', `Scanning ${folderCount} folder(s)...`);
        });
        
        newConnection.on('ScanComplete', () => {
          showStatus('processing', 'Scan completed successfully');
        });
        
        newConnection.on('FolderAdded', (folderPath) => {
          showStatus('adding', `Added folder: ${folderPath}`);
        });
        
        newConnection.on('FolderRemoved', (folderPath) => {
          showStatus('removing', `Removed folder: ${folderPath}`);
        });
        
      } catch (err) {
        console.error('SignalR Connection Error: ', err);
        setTimeout(startConnection, 5000); // Try to reconnect after 5 seconds
      }
    };

    startConnection();

    // Cleanup on unmount
    return () => {
      if (newConnection) {
        newConnection.stop();
      }
    };
  }, []);

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