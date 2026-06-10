import React from 'react';
import {
  Modal,
  Backdrop,
  Box,
  Typography,
  IconButton,
  List,
  Divider,
  CardMedia,
  Fade, // For transition
  ListItem,
  ListItemButton,
  ListItemText,
  ListItemIcon,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import { formatDuration } from '../utils/formatDuration'; // Import the duration formatting utility

const modalStyle = {
  position: 'absolute',
  top: '50%',
  left: '50%',
  transform: 'translate(-50%, -50%)',
  width: '90%', // Keep it responsive relative to viewport width
  maxWidth: 1000, // Increase max width from 500 to 1000
  maxHeight: '85vh', // Increase max height slightly as well
  bgcolor: 'background.paper',
  border: '1px solid #000',
  borderRadius: 2, // Corresponds to theme.shape.borderRadius * 1
  boxShadow: 24,
  p: 3,
  display: 'flex',
  flexDirection: 'column',
};

const StyledBackdrop = (props) => (
  <Backdrop
    {...props}
    sx={{ 
        // Apply backdrop filter for blur effect
        backdropFilter: 'blur(4px)',
        backgroundColor: 'rgba(0,0,0,0.5)' // Darken background slightly
    }} 
  />
);

// Custom track list item to show formatted durations
const TrackPreviewItem = ({ track }) => {
  const { number: trackNumber, title, duration } = track || {};
  
  return (
    <ListItemButton 
      dense
      sx={{ 
        py: 0.5,
        px: 1,
      }}
    >
      <ListItemIcon sx={{ minWidth: 30, mr: 1, color: 'text.secondary' }}>
        <Typography variant="body2">{trackNumber || '-'}</Typography> 
      </ListItemIcon>
      <ListItemText 
        primary={title || "Track Title"} 
        primaryTypographyProps={{ noWrap: true, variant: 'body2' }}
        sx={{ mr: 2 }}
      />
      <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
        {formatDuration(duration)}
      </Typography>
    </ListItemButton>
  );
};

const AlbumPreviewModal = ({ album, open, onClose }) => {
  if (!album) return null; // Don't render if no album selected

  // Get artwork URL from album data
  const artworkUrl = album.artworkUrl;

  return (
    <Modal
      aria-labelledby="album-preview-title"
      aria-describedby="album-preview-description"
      open={open}
      onClose={onClose} // Close when clicking backdrop
      closeAfterTransition
      BackdropComponent={StyledBackdrop}
      BackdropProps={{
        timeout: 500,
      }}
    >
      <Fade in={open}>
        <Box sx={modalStyle}>
          {/* Close Button */}
          <IconButton
            aria-label="close"
            onClick={onClose}
            sx={{ position: 'absolute', right: 8, top: 8, color: 'grey.500' }}
          >
            <CloseIcon />
          </IconButton>

          {/* Album Header Info */}
          <Box sx={{ display: 'flex', gap: 2, mb: 2, alignItems: 'center' }}>
             <CardMedia
                component="div"
                sx={{
                    width: 100,
                    height: 100,
                    bgcolor: artworkUrl ? 'transparent' : 'grey.700',
                    borderRadius: 1,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    flexShrink: 0,
                    boxShadow: '0 3px 10px rgba(0, 0, 0, 0.5)',
                    backgroundSize: 'cover',
                    backgroundPosition: 'center'
                }}
                image={artworkUrl} // Use album artwork URL
              >
                {!artworkUrl && (
                  <Typography variant="caption" color="text.secondary">Album Art</Typography>
                )}
              </CardMedia>
            <Box>
              <Typography id="album-preview-title" variant="h5" component="h2" gutterBottom>
                {album.title || 'Album Title'}
              </Typography>
              <Typography id="album-preview-description" variant="body1" color="text.secondary">
                {album.artist || 'Artist Name'}
              </Typography>
               {/* Add Play/Queue buttons here later? */}
            </Box>
          </Box>

          <Divider sx={{ my: 1 }} />

          {/* Track List (Scrollable) */}
          <Box sx={{ flexGrow: 1, overflowY: 'auto', mr: -2, pr: 2 /* Offset scrollbar */ }}>
             <List dense>
              {(album.tracks || []).map((track) => (
                <TrackPreviewItem 
                    key={track.id} 
                    track={track}
                />
              ))}
            </List>
          </Box>
        </Box>
      </Fade>
    </Modal>
  );
};

export default AlbumPreviewModal; 