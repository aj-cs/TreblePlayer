import React, { useRef, useState, useEffect } from 'react';
import { Box, Typography, Card, CardContent, CardMedia } from '@mui/material';

const HOLD_DELAY_MS = 300; // Reduced from 500ms to 300ms for faster response

const AlbumCard = ({ album, onAlbumClick, onAlbumHold, onAlbumDoubleClick }) => {
  // Use passed album data, provide fallbacks
  const albumTitle = album?.title || "Album Title";
  const artistName = album?.artist || "Artist Name";
  const artworkUrl = album?.artworkUrl; // Get URL from props
  
  // Add state for image error handling and cache busting
  const [imgSrc, setImgSrc] = useState(artworkUrl);
  const [imgError, setImgError] = useState(false);
  
  // Update image source when album changes
  useEffect(() => {
    if (artworkUrl) {
      // Add cache busting parameter to force reload
      setImgSrc(`${artworkUrl}?t=${new Date().getTime()}`);
      setImgError(false);
    }
  }, [artworkUrl]);

  // Ref to store the timer ID
  const holdTimerRef = useRef(null);
  // State to track if the mouse is still down (prevent click after hold)
  const [isHolding, setIsHolding] = useState(false);

  // Cleanup timer on unmount
  useEffect(() => {
    return () => {
      if (holdTimerRef.current) {
        clearTimeout(holdTimerRef.current);
      }
    };
  }, []);

  const handleMouseDown = () => {
    setIsHolding(false); // Reset hold state
    // Start a timer. If it finishes before mouse up, trigger hold
    holdTimerRef.current = setTimeout(() => {
      if (onAlbumHold && album) {
        // console.log('Hold triggered!');
        onAlbumHold(album); 
        setIsHolding(true); // Mark as holding so click doesn't fire
      }
      holdTimerRef.current = null;
    }, HOLD_DELAY_MS);
  };

  const handleMouseUp = () => {
    // If the timer is still running, clear it (it wasn't a hold)
    if (holdTimerRef.current) {
      // console.log('Hold cancelled (mouse up)');
      clearTimeout(holdTimerRef.current);
      holdTimerRef.current = null;
    }
    // If mouse is up and we weren't holding, it's a click (handled by onClick)
  };

   const handleMouseLeave = () => {
    // Also clear timer if mouse leaves the card before hold triggers
     if (holdTimerRef.current) {
      // console.log('Hold cancelled (mouse leave)');
      clearTimeout(holdTimerRef.current);
      holdTimerRef.current = null;
    }
  }

  const handleClick = (event) => {
    // Only trigger click if we weren't holding
    if (!isHolding && onAlbumClick && album) {
       // console.log('Click triggered!');
       onAlbumClick(album); 
    } 
    // Reset hold state after click logic (or mouse up)
    setIsHolding(false);
  };

  // Double-click handler
  const handleDoubleClick = () => {
    if (onAlbumDoubleClick && album) {
      onAlbumDoubleClick(album);
    }
  }
  
  // Handle image loading errors
  const handleImageError = () => {
    setImgError(true);
    // Try to reload the image without cache
    if (artworkUrl && !imgSrc.includes('?reload=')) {
      setImgSrc(`${artworkUrl}?reload=true&t=${new Date().getTime()}`);
    }
  };

  return (
    <Card 
      onClick={handleClick}
      onMouseDown={handleMouseDown}
      onMouseUp={handleMouseUp}
      onMouseLeave={handleMouseLeave}
      onDoubleClick={handleDoubleClick}
      sx={{
        cursor: 'pointer',
        height: '100%', 
        width: '100%',
        display: 'flex',
        flexDirection: 'column',
        userSelect: 'none',
        borderRadius: 1.5, // Reduced rounding
        overflow: 'hidden',
        boxShadow: '0 3px 10px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.08)', // Enhanced border/outline effect
        border: '1px solid rgba(255, 255, 255, 0.05)', // Adding subtle border
        '&:hover': {
          boxShadow: '0 8px 25px rgba(0, 0, 0, 0.6), 0 0 0 1px rgba(255, 255, 255, 0.15)',
          transform: 'translateY(-4px)',
          transition: 'transform 0.3s ease-in-out'
        },
        transition: 'all 0.2s ease-in-out',
        bgcolor: 'rgba(25, 25, 25, 0.9)', // Slightly lighter background for contrast with shadow
      }}
    >
      {/* Album artwork takes up most of the card, but not overlapped by text */}
      <Box
        sx={{
          width: '100%',
          flex: '1 1 auto', // Takes up available space
          position: 'relative',
          overflow: 'hidden',
          backgroundColor: 'black', // Solid black background to prevent transparency issues
        }}
      >
        {/* Album artwork */}
        {imgSrc && !imgError ? (
          <Box
            component="img"
            src={imgSrc}
            alt={albumTitle}
            onError={handleImageError}
            loading="lazy"
            sx={{
              width: '100%',
              height: '100%',
              objectFit: 'cover', // Cover the entire area
              display: 'block', // Remove any extra space
              backgroundColor: 'black', // Add black background
              imageRendering: 'auto', // Let browser handle rendering
              transform: 'translateZ(0)', // Force hardware acceleration
              backfaceVisibility: 'hidden', // Prevent artifacts during transitions
              WebkitBackfaceVisibility: 'hidden', // For Safari
            }}
          />
        ) : (
          <Box
            sx={{
              width: '100%',
              height: '100%',
              bgcolor: 'grey.900',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Typography variant="h6" color="text.secondary">
              Album Art
            </Typography>
          </Box>
        )}
      </Box>
      
      {/* Text area below the artwork */}
      <Box
        sx={{
          backgroundColor: 'rgba(20, 20, 20, 0.9)', // Dark background for text
          color: 'white',
          padding: 1.5,
          flexShrink: 0, // Don't shrink this area
        }}
      >
        <Typography 
          variant="body1" 
          component="div" 
          noWrap
          fontWeight="medium"
          sx={{ color: 'white' }}
        >
          {albumTitle}
        </Typography>
        <Typography 
          variant="body2" 
          noWrap
          sx={{ color: 'rgba(255, 255, 255, 0.8)' }}
        >
          {artistName}
        </Typography>
      </Box>
    </Card>
  );
};

export default AlbumCard; 