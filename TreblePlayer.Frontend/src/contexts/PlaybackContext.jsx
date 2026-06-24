import React, { createContext, useState, useContext, useEffect, useCallback, useRef } from 'react';
import * as api from '../services/apiService';
import { useWebSocket } from './WebSocketContext';

const PlaybackContext = createContext();

export const PlaybackProvider = ({ children }) => {
  const [currentTrack, setCurrentTrack] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [position, setPosition] = useState(0);
  const [duration, setDuration] = useState(0);
  const [activeQueue, setActiveQueue] = useState(null);
  const lastTrackIdRef = useRef(null); // Idempotency check
  const { subscribe } = useWebSocket();

  const refreshActiveQueue = useCallback(async () => {
    try {
        const queue = await api.getActiveQueue();
        
        if (!queue) {
            setActiveQueue(null);
            setCurrentTrack(null);
            setPosition(0);
            return;
        }
        
        const track = (queue.tracks && queue.currentTrackIndex !== null && queue.currentTrackIndex >= 0 && queue.currentTrackIndex < queue.tracks.length) 
            ? queue.tracks[queue.currentTrackIndex] 
            : null;

        // Update track and state if the track exists
        if (track) {
            console.log("Updating playback state:", track.title);
            setCurrentTrack(track);
            setDuration(track.duration);
            lastTrackIdRef.current = track.id;
        } else {
            setCurrentTrack(null);
            setDuration(0);
            lastTrackIdRef.current = null;
        }

        setActiveQueue(queue);
        setPosition(queue.lastPlaybackPositionSeconds || 0);
    } catch (e) {
        console.error("Error refreshing active queue:", e);
        setActiveQueue(null);
        setCurrentTrack(null);
        setPosition(0);
    }
  }, []);

  useEffect(() => {
    refreshActiveQueue();

    const unsubscribe = subscribe((message) => {
      switch (message.type) {
        case 'QueuesUpdated':
          refreshActiveQueue();
          break;
        case 'PlaybackStarted':
          setIsPlaying(true);
          refreshActiveQueue();
          break;
        case 'PlaybackPaused':
          setIsPlaying(false);
          break;
        case 'PlaybackStopped':
          setIsPlaying(false);
          setCurrentTrack(null);
          setPosition(0);
          break;
        case 'PlaybackResumed':
          setIsPlaying(true);
          break;
        case 'PlaybackSeeked':
          setPosition(message.seconds);
          break;
        case 'PositionChanged':
          setPosition(message.seconds);
          break;
      }
    });

    return () => unsubscribe();
  }, [refreshActiveQueue, subscribe]);

  const playTrack = async (trackId) => {
    setIsPlaying(true);
    await api.playTrack(trackId);
    setTimeout(refreshActiveQueue, 800); 
  };

  const playCollection = async (id, type, startIndex = 0) => {
    console.log(`playCollection called: id=${id}, type=${type}, startIndex=${startIndex}`);
    setIsPlaying(true);
    await api.playCollection(id, type, startIndex);
    setTimeout(refreshActiveQueue, 800);
  };
  
  const togglePlay = async () => {
    setIsPlaying(!isPlaying);
    if (isPlaying) await api.pause();
    else await api.resume();
  };

  useEffect(() => {
    const handleKeyDown = (e) => {
        if (e.code === 'Space') {
            const activeTag = document.activeElement.tagName;
            if (activeTag !== 'INPUT' && activeTag !== 'TEXTAREA') {
                e.preventDefault();
                togglePlay();
            }
        }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [togglePlay]);

  const next = async () => { await api.next(); setTimeout(refreshActiveQueue, 500); };
  const previous = async () => { await api.previous(); setTimeout(refreshActiveQueue, 500); };
  const seek = async (seconds) => { await api.seek(seconds); setPosition(seconds); };

  return (
    <PlaybackContext.Provider value={{
      currentTrack, isPlaying, position, duration, activeQueue,
      playTrack, playCollection, togglePlay, next, previous, seek
    }}>
      {children}
    </PlaybackContext.Provider>
  );
};

export const usePlayback = () => useContext(PlaybackContext);
