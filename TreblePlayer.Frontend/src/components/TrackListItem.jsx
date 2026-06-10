import React from 'react';
import {
  ListItem,
  ListItemButton,
  ListItemText,
  ListItemIcon,
  Typography,
  Box,
} from '@mui/material';
import MusicNoteIcon from '@mui/icons-material/MusicNote'; // Example icon

const TrackListItem = ({ trackNumber, title, artist, duration }) => {
  return (
    <ListItem disablePadding>
      <ListItemButton sx={{ 
        // Add hover effect if desired
        '&:hover': { bgcolor: 'action.hover' } 
      }}>
        <ListItemIcon sx={{ minWidth: 30, color: 'text.secondary' }}>
          {/* Display track number or maybe a drag handle later */}
          <Typography variant="body2">{trackNumber || '-'}</Typography> 
        </ListItemIcon>
        <ListItemText 
            primary={title || "Track Title Placeholder"} 
            secondary={artist || "Artist Name Placeholder"} 
            primaryTypographyProps={{ noWrap: true, variant: 'body2' }}
            secondaryTypographyProps={{ noWrap: true, variant: 'caption' }}
        />
        <Box sx={{ ml: 2, color: 'text.secondary' }}>
            <Typography variant="caption">{duration || "3:45"}</Typography>
        </Box>
      </ListItemButton>
    </ListItem>
  );
};

export default TrackListItem; 