import React, { useRef, useState, useEffect } from 'react';
import { Box, Typography, Card } from '@mui/material';
import { useSettings } from '../contexts/SettingsContext';

const HOLD_DELAY_MS = 300;

const AlbumCard = ({ album, onAlbumClick, onAlbumHold, onAlbumDoubleClick }) => {
  const { albumArtStyle } = useSettings();
  const albumTitle = album?.title || "Album Title";
  const artistName = album?.artist || "Artist Name";
  const artworkUrl = album?.artworkUrl;
  
  const [imgSrc, setImgSrc] = useState(artworkUrl);
  const [imgError, setImgError] = useState(false);
  
  useEffect(() => {
    if (artworkUrl) {
      setImgSrc(`${artworkUrl}?t=${new Date().getTime()}`);
      setImgError(false);
    }
  }, [artworkUrl]);

  const holdTimerRef = useRef(null);
  const [isHolding, setIsHolding] = useState(false);

  useEffect(() => {
    return () => {
      if (holdTimerRef.current) clearTimeout(holdTimerRef.current);
    };
  }, []);

  const handleMouseDown = () => {
    setIsHolding(false);
    holdTimerRef.current = setTimeout(() => {
      if (onAlbumHold && album) {
        onAlbumHold(album); 
        setIsHolding(true);
      }
      holdTimerRef.current = null;
    }, HOLD_DELAY_MS);
  };

  const handleMouseUp = () => {
    if (holdTimerRef.current) {
      clearTimeout(holdTimerRef.current);
      holdTimerRef.current = null;
    }
  };

   const handleMouseLeave = () => {
     if (holdTimerRef.current) {
      clearTimeout(holdTimerRef.current);
      holdTimerRef.current = null;
    }
  }

  const handleClick = () => {
    if (!isHolding && onAlbumClick && album) {
       onAlbumClick(album); 
    } 
    setIsHolding(false);
  };

  const handleDoubleClick = () => {
    if (onAlbumDoubleClick && album) onAlbumDoubleClick(album);
  }
  
  const handleImageError = () => {
    setImgError(true);
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
        borderRadius: albumArtStyle === 'square' ? 1 : 3,
        overflow: 'hidden',
        bgcolor: 'rgba(255, 255, 255, 0.03)',
        border: '1px solid rgba(255, 255, 255, 0.05)',
        transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
        '&:hover': {
          bgcolor: 'rgba(255, 255, 255, 0.06)',
          borderColor: 'rgba(255, 255, 255, 0.12)',
          transform: 'translateY(-6px)',
          boxShadow: '0 20px 40px rgba(0,0,0,0.6)'
        }
      }}
    >
      <Box sx={{ width: '100%', aspectRatio: '1/1', position: 'relative', overflow: 'hidden', bgcolor: '#000', borderRadius: albumArtStyle === 'square' ? 0 : 2 }}>
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
              objectFit: 'cover',
              transition: 'transform 0.5s ease',
              '.MuiCard-root:hover &': { transform: albumArtStyle === 'square' ? 'scale(1.0)' : 'scale(1.08)' }
            }}
          />
        ) : (
          <Box sx={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Typography variant="caption" color="text.secondary">NO ART</Typography>
          </Box>
        )}
      </Box>
      
      <Box sx={{ p: 2, flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
        <Typography variant="body2" noWrap sx={{ fontWeight: 600, mb: 0.2, color: '#fff' }}>
          {albumTitle}
        </Typography>
        <Typography variant="caption" noWrap sx={{ color: 'rgba(255, 255, 255, 0.5)', fontWeight: 500 }}>
          {artistName}
        </Typography>
      </Box>
    </Card>
  );
};

export default AlbumCard;
