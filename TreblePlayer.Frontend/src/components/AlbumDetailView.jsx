import React, { useState, useEffect } from 'react';
import {
  Box,
  Typography,
  List,
  Divider,
  CardMedia,
  ListItem,
  ListItemButton,
  ListItemText,
  ListItemIcon,
  Paper
} from '@mui/material';
import AlbumIcon from '@mui/icons-material/Album';
// Remove local TrackListItem import if no longer needed
// import TrackListItem from './TrackListItem'; 
import { formatDuration } from '../utils/formatDuration'; // Import the utility

// Remove local formatDuration function
/*
const formatDuration = (seconds) => {
  if (isNaN(seconds) || seconds < 0) return "--:--";
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = Math.floor(seconds % 60);
  return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
};
*/

// More compact list item for this view
const CompactTrackListItem = ({ track, albumArtist, onDoubleClick }) => {
  const { number: trackNumber, title, duration } = track || {};
  const artist = albumArtist || "Artist Name"; // Use album artist if track artist isn't available

  return (
    <ListItemButton 
      dense
      onClick={(e) => { /* Single click logic if needed */ }}
      onDoubleClick={() => onDoubleClick(track)}
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
  const artworkUrl = album?.artworkUrl; // Get artwork URL
  
  // Add state for image loading and error handling
  const [imgSrc, setImgSrc] = useState(artworkUrl);
  const [imgLoaded, setImgLoaded] = useState(false);
  const [imgError, setImgError] = useState(false);
  
  // Update image source when album changes with cache busting
  useEffect(() => {
    if (artworkUrl) {
      setImgSrc(`${artworkUrl}?t=${new Date().getTime()}`);
      setImgLoaded(false);
      setImgError(false);
    } else {
      setImgSrc(null);
    }
  }, [artworkUrl]);
  
  // Handle image loading error
  const handleImageError = () => {
    setImgError(true);
    if (artworkUrl && !imgSrc.includes('?reload=')) {
      // Try reloading once with cache busting
      setImgSrc(`${artworkUrl}?reload=true&t=${new Date().getTime()}`);
    }
  };
  
  // Handle successful image load
  const handleImageLoad = () => {
    setImgLoaded(true);
  };

  // Common panel container styles
  const containerStyles = {
    width: width, // Use passed width prop instead of fixed value
    minWidth: width, // Prevent shrinking
    height: '100%',
    bgcolor: 'rgba(22, 22, 22, 0.95)', // Slightly different shade than the main background
    display: 'flex',
    flexDirection: 'column',
    overflow: 'hidden' // Prevent outer scroll
  };

  // If no album or it's the placeholder, show the empty state
  if (!album || album.isPlaceholder) {
    return (
      <Box sx={containerStyles}>
        <Box 
          sx={{ 
            display: 'flex', 
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            textAlign: 'center',
            height: '100%',
            p: 3
          }}
        >
          <Paper 
            elevation={0} 
            sx={{ 
              bgcolor: 'background.default', 
              p: 4, 
              borderRadius: 2,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              mb: 2
            }}
          >
            <AlbumIcon sx={{ fontSize: 80, color: 'text.secondary', mb: 2 }} />
            <Typography variant="h6" gutterBottom>
              No Album Selected
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 260 }}>
              Click on an album from the collection to view details and tracks
            </Typography>
          </Paper>
        </Box>
      </Box>
    );
  }

  return (
    <Box sx={containerStyles}>
      {/* Album Header Info */}
      <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center' }}>
         {imgSrc && !imgError ? (
           <Box
             sx={{
               width: 150,
               height: 150,
               bgcolor: 'black',
               borderRadius: 1,
               mb: 2,
               overflow: 'hidden',
               boxShadow: '0 3px 10px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.08)',
               border: '1px solid rgba(255, 255, 255, 0.05)', // Adding subtle border
               position: 'relative',
             }}
           >
             <Box
               component="img"
               src={imgSrc}
               alt={album.title || "Album"}
               onError={handleImageError}
               onLoad={handleImageLoad}
               loading="lazy"
               sx={{
                 width: '100%',
                 height: '100%',
                 objectFit: 'cover',
                 display: 'block',
                 backgroundColor: 'black',
                 opacity: imgLoaded ? 1 : 0,
                 transition: 'opacity 0.3s ease',
                 transform: 'translateZ(0)',
                 WebkitBackfaceVisibility: 'hidden',
                 backfaceVisibility: 'hidden',
               }}
             />
             {!imgLoaded && (
               <Box
                 sx={{
                   position: 'absolute',
                   top: 0,
                   left: 0,
                   width: '100%',
                   height: '100%',
                   display: 'flex',
                   alignItems: 'center',
                   justifyContent: 'center',
                   bgcolor: 'grey.900',
                 }}
               >
                 <Typography variant="caption" color="text.secondary">
                   Loading...
                 </Typography>
               </Box>
             )}
           </Box>
         ) : (
           <Box
             sx={{
               width: 150,
               height: 150,
               bgcolor: 'grey.900',
               borderRadius: 1,
               mb: 2,
               display: 'flex',
               alignItems: 'center',
               justifyContent: 'center',
               boxShadow: '0 3px 10px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.08)',
               border: '1px solid rgba(255, 255, 255, 0.05)', // Adding subtle border
             }}
           >
             <Typography variant="caption" color="text.secondary">
               No Artwork
             </Typography>
           </Box>
         )}
          <Typography variant="h6" component="h2" gutterBottom>
            {album.title || 'Album Title'}
          </Typography>
          <Typography variant="body1" color="text.secondary">
            {album.artist || 'Artist Name'}
          </Typography>
          {/* Maybe add Year, Genre, Play/Queue buttons later */}
      </Box>

      <Divider sx={{ mb: 1 }} />

      {/* Track List (Scrollable) */}
      <Box sx={{ flexGrow: 1, overflowY: 'auto' }}>
         <List dense>
          {(album.tracks || []).map((track) => (
            <CompactTrackListItem 
                key={track.id} 
                track={track}
                albumArtist={album.artist} // Pass album artist
                onDoubleClick={onTrackDoubleClick} // Pass handler down
            />
          ))}
        </List>
      </Box>
    </Box>
  );
};

export default AlbumDetailView;