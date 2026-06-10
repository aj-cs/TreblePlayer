import React, { useState, useEffect } from 'react';
import {
  Box,
  Typography,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Paper,
  CircularProgress,
} from '@mui/material';
import { getTracks } from '../services/apiService';
import { formatDuration } from '../utils/formatDuration';

// Header Component (mimics TableHead)
const TrackListHeader = () => (
  <ListItem 
    sx={{ 
      py: 0.5, 
      px: 2, 
      borderBottom: '1px solid', 
      borderColor: 'divider',
      bgcolor: 'background.paper' // Match potential background
    }}
  >
    <Box sx={{ display: 'flex', width: '100%', alignItems: 'center' }}>
      <Typography variant="caption" sx={{ flexBasis: '40%', flexShrink: 1, mr: 2, fontWeight: 'bold', color: 'text.secondary' }}>Title</Typography>
      <Typography variant="caption" sx={{ flexBasis: '25%', flexShrink: 1, mr: 2, fontWeight: 'bold', color: 'text.secondary' }}>Artist</Typography>
      <Typography variant="caption" sx={{ flexBasis: '25%', flexShrink: 1, mr: 2, fontWeight: 'bold', color: 'text.secondary' }}>Album</Typography>
      <Typography variant="caption" sx={{ flexBasis: '10%', flexShrink: 0, textAlign: 'right', fontWeight: 'bold', color: 'text.secondary' }}>Duration</Typography>
    </Box>
  </ListItem>
);

// Component for a single track item in the list
const TrackListItem = ({ track, onDoubleClick }) => {
  return (
    <ListItem disablePadding>
      <ListItemButton 
        dense
        onDoubleClick={() => onDoubleClick(track)}
        sx={{ 
          py: 0.75,
          px: 2,
          borderBottom: '1px solid', 
          borderColor: 'divider' 
        }}
      >
        <Box sx={{ display: 'flex', width: '100%', alignItems: 'center' }}>
          <Typography 
            variant="body2" 
            noWrap 
            sx={{ flexBasis: '40%', flexShrink: 1, mr: 2 }}
          >
            {track.title}
          </Typography>
          <Typography 
            variant="body2" 
            color="text.secondary" 
            noWrap 
            sx={{ flexBasis: '25%', flexShrink: 1, mr: 2, overflow: 'hidden', textOverflow: 'ellipsis' }}
          >
            {track.artist}
          </Typography>
          <Typography 
            variant="body2" 
            color="text.secondary" 
            noWrap 
            sx={{ flexBasis: '25%', flexShrink: 1, mr: 2, overflow: 'hidden', textOverflow: 'ellipsis' }}
          >
            {track.albumTitle || '-'}
          </Typography>
          <Typography 
            variant="body2" 
            color="text.secondary" 
            sx={{ flexBasis: '10%', flexShrink: 0, textAlign: 'right' }}
          >
            {formatDuration(track.duration)}
          </Typography>
        </Box>
      </ListItemButton>
    </ListItem>
  );
};

// Main component using List instead of Table
const TrackListView = ({ onTrackDoubleClick }) => {
  const [tracks, setTracks] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const loadTracks = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const data = await getTracks();
        setTracks(data || []);
      } catch (err) {
        console.error("Failed to load tracks:", err);
        setError(err.message || 'Failed to load tracks.');
      } finally {
        setIsLoading(false);
      }
    };

    loadTracks();
  }, []);

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh' }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh' }}>
        <Typography color="error">Error loading tracks: {error}</Typography>
      </Box>
    );
  }
  
   if (tracks.length === 0) {
     return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh' }}>
        <Typography color="text.secondary">No tracks found.</Typography>
      </Box>
    );
  }

  return (
    <Paper sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
       <TrackListHeader />
       <Box sx={{ flexGrow: 1, overflowY: 'auto' }}>
         <List disablePadding>
           {tracks.map((track) => (
             <TrackListItem 
               key={track.id} 
               track={track} 
               onDoubleClick={onTrackDoubleClick} 
             />
           ))}
         </List>
       </Box>
    </Paper>
  );
};

export default TrackListView; 