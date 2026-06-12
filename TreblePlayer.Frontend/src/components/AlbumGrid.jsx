import React, { useState, useEffect } from 'react';
import { Box, Typography, CircularProgress } from '@mui/material';
import AlbumCard from './AlbumCard';
import { getAlbums } from '../services/apiService';

const AlbumGrid = ({ onAlbumClick, onAlbumHold, gridColumns = 6, setAlbumCount, onAlbumDoubleClick }) => {
  const [albums, setAlbums] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  
  useEffect(() => {
    getAlbums().then(data => {
      setAlbums(data || []);
      if (setAlbumCount) setAlbumCount(data?.length || 0);
      setIsLoading(false);
    });
  }, []);

  // Fix: Ensure gridColumns is a number and provide a safe fallback
  const columns = parseInt(gridColumns, 10) || 6;
  const gap = 16;
  const itemFlexBasis = `calc((100% - ${(columns - 1) * gap}px) / ${columns})`;

  if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}><CircularProgress /></Box>;
  
  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: `${gap}px`, p: 2 }}>
      {albums.map((album) => (
        <Box key={album.id} sx={{ 
            flexGrow: 0, 
            flexShrink: 0, 
            flexBasis: itemFlexBasis, 
            display: 'flex',
            maxWidth: itemFlexBasis // Crucial for preventing grid breaking
        }}>
          <AlbumCard
            album={album}
            onAlbumClick={onAlbumClick}
            onAlbumHold={onAlbumHold}
            onAlbumDoubleClick={onAlbumDoubleClick}
          />
        </Box>
      ))}
    </Box>
  );
};

export default AlbumGrid;
