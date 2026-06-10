import React from 'react';
import { Box, Typography, Card, CardContent, CardActionArea } from '@mui/material';
import PlaylistPlayIcon from '@mui/icons-material/PlaylistPlay';

const PlaylistCard = ({ playlist, onPlaylistClick, onPlaylistDoubleClick }) => {
  // Placeholder data
  const playlistName = playlist?.name || "Playlist Name";
  const description = playlist?.description || "Playlist Description"; // Or track count etc.

  const handleClick = () => {
    if (onPlaylistClick && playlist) {
        onPlaylistClick(playlist); // Pass the playlist data up
    }
  };

  // Handle double-click
  const handleDoubleClick = () => {
    if (onPlaylistDoubleClick && playlist) {
      onPlaylistDoubleClick(playlist);
    }
  };

  return (
    <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <CardActionArea 
        onClick={handleClick} 
        onDoubleClick={handleDoubleClick}
        sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column' }}
      >
        {/* Placeholder Visual - could be grid of 4 album arts later */}
        <Box sx={{ 
            height: 140, 
            width: '100%', 
            bgcolor: 'grey.700', 
            display: 'flex', 
            alignItems: 'center', 
            justifyContent: 'center',
            borderRadius: 'inherit', // Inherit from Card
            borderBottomLeftRadius: 0,
            borderBottomRightRadius: 0
        }}>
          <PlaylistPlayIcon sx={{ fontSize: 60, color: 'grey.500' }} />
        </Box>
        <CardContent sx={{ flexGrow: 1, width: '100%', overflow: 'hidden' }}>
          <Typography 
            gutterBottom 
            variant="h6" 
            component="div" 
            noWrap 
            sx={{ overflow: 'hidden', textOverflow: 'ellipsis' }}
           >
            {playlistName}
          </Typography>
          <Typography 
            variant="body2" 
            color="text.secondary" 
            noWrap 
            sx={{ overflow: 'hidden', textOverflow: 'ellipsis' }}
           >
            {description}
          </Typography>
        </CardContent>
      </CardActionArea>
    </Card>
  );
};

export default PlaylistCard; 