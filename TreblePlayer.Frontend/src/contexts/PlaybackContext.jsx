import React, { createContext, useState, useContext, useEffect, useCallback, useRef } from 'react';
import * as api from '../services/apiService';

const PlaybackContext = createContext();

export const PlaybackProvider = ({ children }) => {
  const [currentTrack, setCurrentTrack] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [position, setPosition] = useState(0);
  const [duration, setDuration] = useState(0);
  const [activeQueue, setActiveQueue] = useState(null);
  const lastTrackIdRef = useRef(null); // Idempotency check

  const refreshActiveQueue = useCallback(async () => {
    try {
        const queue = await api.getActiveQueue();
        
        if (!queue) return;
        
        const track = (queue.tracks && queue.currentTrackIndex !== null && queue.currentTrackIndex >= 0 && queue.currentTrackIndex < queue.tracks.length) 
            ? queue.tracks[queue.currentTrackIndex] 
            : null;

        // ONLY update if track actually changed to prevent flickering
        if (track && track.id !== lastTrackIdRef.current) {
            console.log("Track changed, updating state:", track.title);
            setCurrentTrack(track);
            setDuration(track.duration);
            lastTrackIdRef.current = track.id;
        }

        setActiveQueue(queue);
        setPosition(queue.lastPlaybackPositionSeconds || 0);
    } catch (e) {
        console.error("Error refreshing active queue:", e);
    }
  }, []);

  useEffect(() => {
    const pollInterval = setInterval(refreshActiveQueue, 2000);
    refreshActiveQueue();

    const positionInterval = setInterval(() => {
        if (isPlaying) setPosition(prev => prev + 1);
    }, 1000);

    return () => { clearInterval(pollInterval); clearInterval(positionInterval); };
  }, [refreshActiveQueue, isPlaying]);

  const playTrack = async (trackId) => {
    setIsPlaying(true);
    await api.playTrack(trackId);
    setTimeout(refreshActiveQueue, 800); 
  };

  const playCollection = async (id, type, startIndex = 0) => {
    setIsPlaying(true);
    await api.playCollection(id, type, startIndex);
    setTimeout(refreshActiveQueue, 800);
  };
  
  const togglePlay = async () => {
    setIsPlaying(!isPlaying);
    if (isPlaying) await api.pause();
    else await api.resume();
  };

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
