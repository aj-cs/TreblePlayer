import React, { useState, useEffect, useRef } from 'react'; // Import hooks
import { Box, Typography, CircularProgress, useTheme } from '@mui/material';
import AlbumCard from './AlbumCard'; // Import the AlbumCard component
import { getAlbums } from '../services/apiService'; // Import API function

// Constants for grid layout
const MIN_CARD_WIDTH = 180; // Minimum width for each album card
const GAP_SIZE = 24; // Gap between cards

// Accept gridColumns prop, provide default if necessary
const AlbumGrid = ({ onAlbumClick, onAlbumHold, gridColumns = 6, setAlbumCount }) => {
  const [albums, setAlbums] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const theme = useTheme();
  
  useEffect(() => {
    const loadAlbums = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const data = await getAlbums();
        setAlbums(data || []); 
        // Update album count in parent component
        if (setAlbumCount && Array.isArray(data)) {
          setAlbumCount(data.length);
        }
      } catch (err) {
        console.error("Failed to load albums:", err);
        setError(err.message || 'Failed to load albums.');
        // Reset album count on error
        if (setAlbumCount) {
          setAlbumCount(0);
        }
      } finally {
        setIsLoading(false);
      }
    };

    loadAlbums();
  }, []); // Run once on mount

  // Use the passed gridColumns prop
  const numColumns = gridColumns;

  // Define spacing between items
  const spacing = 2; // Corresponds to theme.spacing(2) = 16px
  const spacingValue = spacing * 8; // Convert theme spacing unit to pixels

  // Calculate flex-basis based on the numColumns from props
  const totalGap = spacingValue * (numColumns - 1);
  const itemFlexBasis = `calc((100% - ${totalGap}px) / ${numColumns})`;

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
        <Typography color="error">Error loading albums: {error}</Typography>
      </Box>
    );
  }

  if (albums.length === 0) {
     return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh' }}>
        <Typography color="text.secondary">No albums found.</Typography>
      </Box>
    );
  }
  
  return (
    <Box 
        sx={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: `${spacingValue}px`,
            p: spacing,
            overflowY: 'auto',
            height: '100%'
        }}
    >
      {albums.map((album) => (
        <Box 
            key={album.id} 
            sx={{
                flexGrow: 0,
                flexShrink: 0,
                flexBasis: itemFlexBasis,
                minWidth: 0,
                display: 'flex',
                alignItems: 'stretch'
            }}
        >
          <AlbumCard
            album={album}
            onAlbumClick={onAlbumClick}
            onAlbumHold={onAlbumHold}
          />
        </Box>
      ))}
    </Box>
  );
};

export default AlbumGrid; 