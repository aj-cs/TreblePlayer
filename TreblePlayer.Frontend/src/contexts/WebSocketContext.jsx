import React, { createContext, useContext, useEffect, useRef, useState } from 'react';

const WebSocketContext = createContext(null);

export const WebSocketProvider = ({ children }) => {
  const [isConnected, setIsConnected] = useState(false);
  const socketRef = useRef(null);
  const handlersRef = useRef(new Set());

  useEffect(() => {
    const connect = () => {
      const socket = new WebSocket('ws://localhost:5155/ws');

      socket.onopen = () => {
        console.log('WebSocket Connected');
        setIsConnected(true);
      };

      socket.onmessage = (event) => {
        const message = JSON.parse(event.data);
        handlersRef.current.forEach(handler => handler(message));
      };

      socket.onclose = () => {
        console.log('WebSocket Disconnected, retrying...');
        setIsConnected(false);
        setTimeout(connect, 3000);
      };

      socket.onerror = (error) => {
        console.error('WebSocket Error:', error);
        socket.close();
      };

      socketRef.current = socket;
    };

    connect();

    return () => {
      if (socketRef.current) {
        socketRef.current.close();
      }
    };
  }, []);

  const subscribe = (handler) => {
    handlersRef.current.add(handler);
    return () => handlersRef.current.delete(handler);
  };

  const sendMessage = (message) => {
    if (socketRef.current && socketRef.current.readyState === WebSocket.OPEN) {
      socketRef.current.send(JSON.stringify(message));
    }
  };

  return (
    <WebSocketContext.Provider value={{ isConnected, subscribe, sendMessage }}>
      {children}
    </WebSocketContext.Provider>
  );
};

export const useWebSocket = () => {
  const context = useContext(WebSocketContext);
  if (!context) {
    throw new Error('useWebSocket must be used within a WebSocketProvider');
  }
  return context;
};
