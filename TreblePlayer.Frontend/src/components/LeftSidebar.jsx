import React, { useState } from 'react';
import { Box, List, ListItem, ListItemButton, ListItemIcon, ListItemText } from '@mui/material';
import AlbumIcon from '@mui/icons-material/Album';
import MusicNoteIcon from '@mui/icons-material/MusicNote';
import QueueMusicIcon from '@mui/icons-material/QueueMusic';
import PlaylistPlayIcon from '@mui/icons-material/PlaylistPlay';
import SettingsIcon from '@mui/icons-material/Settings';
import HomeIcon from '@mui/icons-material/Home';

const navItems = [
  { view: 'Albums', text: 'Albums', icon: <AlbumIcon /> },
  { view: 'Tracks', text: 'Tracks', icon: <MusicNoteIcon /> },
  { view: 'Playlists', text: 'Playlists', icon: <PlaylistPlayIcon /> },
  { view: 'Queue', text: 'Queues', icon: <QueueMusicIcon /> },
];

const LeftSidebar = ({ onViewChange }) => {
  const [selectedIndex, setSelectedIndex] = useState(0);

  const handleListItemClick = (index, view) => {
    setSelectedIndex(index);
    if (onViewChange) {
      onViewChange(view);
    }
  };

  return (
    <Box sx={{
      width: 240,
      flexShrink: 0,
      height: '100%',
      bgcolor: 'rgba(20, 20, 20, 0.95)', // Slightly different shade for visual separation
      display: 'flex',
      flexDirection: 'column'
    }}>
      {/* Logo Area */}
      <Box sx={{ p: 2, textAlign: 'center' }}>
        <img src="/logo.png" alt="TreblePlayer Logo" style={{ maxWidth: '100%', height: 'auto' }} />
      </Box>

      {/* Navigation Items */}
      <List sx={{ flexGrow: 1 }}>
        {navItems.map((item, index) => (
          <ListItem key={item.view} disablePadding>
            <ListItemButton
              selected={selectedIndex === index}
              onClick={() => handleListItemClick(index, item.view)}
              sx={{
                '&.Mui-selected': {
                  backgroundColor: 'action.selected',
                  color: 'primary.main',
                  '& .MuiListItemIcon-root': {
                     color: 'primary.main',
                  }
                },
                 '&.Mui-selected:hover': {
                    backgroundColor: 'action.selected',
                 }
              }}
            >
              <ListItemIcon sx={{ minWidth: 40 }}>
                {item.icon}
              </ListItemIcon>
              <ListItemText primary={item.text} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>

      {/* Footer Icons - No divider, just a visual gap */}
      <Box sx={{ pt: 1, opacity: 0.85 }}> {/* Add a little padding top for visual separation */}
        <List>
          <ListItem disablePadding>
            <ListItemButton> 
              <ListItemIcon sx={{ minWidth: 40 }}><SettingsIcon /></ListItemIcon>
              <ListItemText primary="Settings" />
             </ListItemButton>
          </ListItem>
          <ListItem disablePadding>
             <ListItemButton>
               <ListItemIcon sx={{ minWidth: 40 }}><HomeIcon /></ListItemIcon>
               <ListItemText primary="Home" />
             </ListItemButton>
          </ListItem>
        </List>
      </Box>
    </Box>
  );
};

export default LeftSidebar; 