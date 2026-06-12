import React, { useState, useEffect } from 'react';
import { Box, Typography, List, ListItem, ListItemButton, ListItemText, Paper, CircularProgress } from '@mui/material';
import { getTracks } from '../services/apiService';
import { formatDuration } from '../utils/formatDuration';

const TrackListHeader = () => (
  <ListItem sx={{ py: 0.5, px: 2, borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'background.paper' }}>
    <Box sx={{ display: 'flex', width: '100%', alignItems: 'center' }}>
      <Typography variant="caption" sx={{ flexBasis: '40%', mr: 2, fontWeight: 'bold' }}>Title</Typography>
      <Typography variant="caption" sx={{ flexBasis: '25%', mr: 2, fontWeight: 'bold' }}>Artist</Typography>
      <Typography variant="caption" sx={{ flexBasis: '25%', mr: 2, fontWeight: 'bold' }}>Album</Typography>
      <Typography variant="caption" sx={{ flexBasis: '10%', textAlign: 'right', fontWeight: 'bold' }}>Duration</Typography>
    </Box>
  </ListItem>
);

const TrackListItem = ({ track, onTrackDoubleClick }) => (
  <ListItem disablePadding>
    <ListItemButton 
      dense
      onDoubleClick={() => {
        console.log("Track double clicked:", track);
        onTrackDoubleClick(track);
      }}
      sx={{ py: 0.75, px: 2, borderBottom: '1px solid', borderColor: 'divider' }}
    >
      <Box sx={{ display: 'flex', width: '100%', alignItems: 'center' }}>
        <Typography variant="body2" noWrap sx={{ flexBasis: '40%', mr: 2 }}>{track.title}</Typography>
        <Typography variant="body2" color="text.secondary" noWrap sx={{ flexBasis: '25%', mr: 2 }}>{track.artist}</Typography>
        <Typography variant="body2" color="text.secondary" noWrap sx={{ flexBasis: '25%', mr: 2 }}>{track.albumTitle || '-'}</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ flexBasis: '10%', textAlign: 'right' }}>{formatDuration(track.duration)}</Typography>
      </Box>
    </ListItemButton>
  </ListItem>
);

const TrackListView = ({ onTrackDoubleClick }) => {
  const [tracks, setTracks] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    getTracks().then(data => { setTracks(data || []); setIsLoading(false); });
  }, []);

  if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}><CircularProgress /></Box>;
  
  return (
    <Paper sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
       <TrackListHeader />
       <Box sx={{ flexGrow: 1, overflowY: 'auto' }}>
         <List disablePadding>
           {tracks.map((track) => (
             <TrackListItem key={track.id} track={track} onTrackDoubleClick={onTrackDoubleClick} />
           ))}
         </List>
       </Box>
    </Paper>
  );
};

export default TrackListView;
