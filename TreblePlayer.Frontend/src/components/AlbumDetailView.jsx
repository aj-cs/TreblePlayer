import React from 'react';
import { Box, Typography, List, Divider, ListItemButton, ListItemText, ListItemIcon, Paper, alpha } from '@mui/material';
import AlbumIcon from '@mui/icons-material/Album';
import { formatDuration } from '../utils/formatDuration';

const CompactTrackListItem = ({ track, albumArtist, onTrackDoubleClick }) => {
  const { number: trackNumber, title, duration } = track || {};
  const artist = albumArtist || "Artist Name";

  return (
    <ListItemButton 
      dense
      onDoubleClick={() => onTrackDoubleClick(track)}
      sx={{ 
        py: 0.75, 
        px: 2.5,
        borderRadius: 2,
        mx: 1,
        mb: 0.25,
        '&:hover': { bgcolor: 'rgba(255,255,255,0.03)' }
      }}
    >
      <ListItemIcon sx={{ minWidth: 32, mr: 1, color: 'text.secondary', opacity: 0.5 }}>
        <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>
            {trackNumber?.toString().padStart(2, '0') || '--'}
        </Typography> 
      </ListItemIcon>
      <ListItemText 
          primary={title || "Track Title"} 
          secondary={artist}
          primaryTypographyProps={{ noWrap: true, variant: 'body2', fontWeight: 500 }}
          secondaryTypographyProps={{ noWrap: true, variant: 'caption', sx: { opacity: 0.5 } }}
          sx={{ mr: 2 }}
      />
      <Typography variant="caption" sx={{ ml: 'auto', opacity: 0.4, fontFamily: 'monospace' }}>
        {formatDuration(duration)}
      </Typography>
    </ListItemButton>
  );
};

const AlbumDetailView = ({ album, width = 400, onTrackDoubleClick }) => {
  const containerStyles = { 
    width: width, 
    minWidth: width, 
    height: '100%', 
    display: 'flex', 
    flexDirection: 'column', 
    overflow: 'hidden' 
  };

  if (!album || album.isPlaceholder) {
    return (
      <Box sx={containerStyles}>
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', height: '100%', p: 3, opacity: 0.2 }}>
            <AlbumIcon sx={{ fontSize: 64, mb: 2 }} />
            <Typography variant="body2" sx={{ letterSpacing: '0.1em' }}>SELECT AN ALBUM</Typography>
        </Box>
      </Box>
    );
  }

  return (
    <Box sx={containerStyles}>
      <Box sx={{ p: 4, display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center' }}>
          <Box 
            sx={{ 
                width: 200, 
                height: 200, 
                borderRadius: 3, 
                mb: 3, 
                boxShadow: '0 20px 40px rgba(0,0,0,0.4)',
                overflow: 'hidden',
                border: '1px solid rgba(255,255,255,0.1)'
            }}
          >
            {album.artworkUrl ? (
                <Box component="img" src={album.artworkUrl} sx={{ width: '100%', height: '100%', objectFit: 'cover' }} />
            ) : (
                <Box sx={{ width: '100%', height: '100%', bgcolor: 'rgba(255,255,255,0.05)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <AlbumIcon sx={{ fontSize: 48, opacity: 0.2 }} />
                </Box>
            )}
          </Box>
          <Typography variant="h6" sx={{ mb: 0.5 }}>{album.title}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 500, opacity: 0.6 }}>{album.artist}</Typography>
      </Box>
      
      <Box sx={{ flexGrow: 1, overflowY: 'auto', pb: 4 }}>
         <List disablePadding>
          {(album.tracks || []).map((track, index) => (
            <CompactTrackListItem 
                key={track.id} 
                track={track}
                albumArtist={album.artist}
                onTrackDoubleClick={() => onTrackDoubleClick(track, index)}
            />
          ))}
        </List>
      </Box>
    </Box>
  );
};

export default AlbumDetailView;
