import React from 'react';
import { ListItem, ListItemButton, ListItemText, ListItemIcon, Typography, Box } from '@mui/material';

const TrackListItem = ({ trackNumber, title, artist, duration, isActive, onClick }) => {
  return (
    <ListItem disablePadding>
      <ListItemButton 
        selected={isActive}
        onDoubleClick={onClick}
        sx={{ '&:hover': { bgcolor: 'action.hover' } }}
      >
        <ListItemIcon sx={{ minWidth: 30, color: 'text.secondary' }}>
          <Typography variant="body2">{trackNumber || '-'}</Typography> 
        </ListItemIcon>
        <ListItemText 
            primary={title} 
            secondary={artist} 
            primaryTypographyProps={{ noWrap: true, variant: 'body2' }}
            secondaryTypographyProps={{ noWrap: true, variant: 'caption' }}
        />
        <Box sx={{ ml: 2, color: 'text.secondary' }}>
            <Typography variant="caption">{duration}</Typography>
        </Box>
      </ListItemButton>
    </ListItem>
  );
};

export default TrackListItem;
