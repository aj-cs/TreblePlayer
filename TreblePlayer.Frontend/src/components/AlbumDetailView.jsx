import React, { useState, useEffect } from 'react';
import { Box, Typography, List, Divider, ListItemButton, ListItemText, ListItemIcon, Paper } from '@mui/material';
import AlbumIcon from '@mui/icons-material/Album';
import { formatDuration } from '../utils/formatDuration';

const CompactTrackListItem = ({ track, albumArtist, onTrackDoubleClick }) => {
  const { number: trackNumber, title, duration } = track || {};
  const artist = albumArtist || "Artist Name";

  return (
    <ListItemButton 
      dense
      onDoubleClick={() => {
        console.log("Album track double clicked:", track);
        onTrackDoubleClick(track);
      }}
      sx={{ py: 0.5, px: 1 }}
    >
      <ListItemIcon sx={{ minWidth: 30, mr: 1, color: 'text.secondary' }}>
        <Typography variant="body2">{trackNumber || '-'}</Typography> 
      </ListItemIcon>
      <ListItemText 
          primary={title || "Track Title"} 
          secondary={artist}
          primaryTypographyProps={{ noWrap: true, variant: 'body2' }}
          secondaryTypographyProps={{ noWrap: true, variant: 'caption' }}
          sx={{ mr: 2 }}
      />
      <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
        {formatDuration(duration)}
      </Typography>
    </ListItemButton>
  );
};

const AlbumDetailView = ({ album, width = 320, onTrackDoubleClick }) => {
  const containerStyles = { width: width, minWidth: width, height: '100%', bgcolor: 'rgba(22, 22, 22, 0.95)', display: 'flex', flexDirection: 'column', overflow: 'hidden' };

  if (!album || album.isPlaceholder) {
    return (
      <Box sx={containerStyles}>
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', height: '100%', p: 3 }}>
          <Paper elevation={0} sx={{ bgcolor: 'background.default', p: 4, borderRadius: 2 }}>
            <AlbumIcon sx={{ fontSize: 80, color: 'text.secondary', mb: 2 }} />
            <Typography variant="h6">No Album Selected</Typography>
          </Paper>
        </Box>
      </Box>
    );
  }

  return (
    <Box sx={containerStyles}>
      <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center' }}>
          {album.artworkUrl && <Box component="img" src={album.artworkUrl} sx={{ width: 150, height: 150, borderRadius: 1, mb: 2 }} />}
          <Typography variant="h6">{album.title}</Typography>
          <Typography variant="body1" color="text.secondary">{album.artist}</Typography>
      </Box>
      <Divider sx={{ mb: 1 }} />
      <Box sx={{ flexGrow: 1, overflowY: 'auto' }}>
         <List dense>
          {(album.tracks || []).map((track) => (
            <CompactTrackListItem 
                key={track.id} 
                track={track}
                albumArtist={album.artist}
                onTrackDoubleClick={onTrackDoubleClick}
            />
          ))}
        </List>
      </Box>
    </Box>
  );
};

export default AlbumDetailView;
