import React, { useRef, useState, useEffect } from 'react';
import { Box, Button, Typography } from '@mui/material';
import AlbumGrid from './AlbumGrid';
import TrackListView from './TrackListView';
import PlaylistGridView from './PlaylistGridView';
import QueueView from './QueueView';
import Settings from './Settings';
import SortIcon from '@mui/icons-material/Sort';

// Helper function to render the current view
const renderCurrentView = (
  view, 
  onAlbumClick, 
  onAlbumHold, 
  onCreatePlaylistClick, 
  gridColumns,
  onAlbumDoubleClick,
  onTrackDoubleClick,
  onPlaylistDoubleClick,
  setAlbumCount
) => {
  switch (view) {
    case 'Tracks':
      return <TrackListView onTrackDoubleClick={onTrackDoubleClick} />;
    case 'Playlists':
      return <PlaylistGridView 
        onCreatePlaylistClick={onCreatePlaylistClick} 
        onPlaylistDoubleClick={onPlaylistDoubleClick} 
      />;
    case 'Queue':
    case 'Queues':
      return <QueueView />;
    case 'Settings':
      return <Settings />;
    case 'Albums':
    default:
      return <AlbumGrid 
        onAlbumClick={onAlbumClick} 
        onAlbumHold={onAlbumHold} 
        gridColumns={gridColumns}
        onAlbumDoubleClick={onAlbumDoubleClick}
        setAlbumCount={setAlbumCount}
      />;
  }
};

const MainContent = ({ 
  currentView, 
  onAlbumClick, 
  onAlbumHold, 
  onAddToPlaylist, 
  gridColumns, 
  onAlbumDoubleClick, 
  onTrackDoubleClick, 
  onPlaylistDoubleClick 
}) => {
  const [albumCount, setAlbumCount] = useState(0);

  return (
    <Box
      sx={{
        width: '100%',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      {/* Filter/Sort Controls - Optional */}
      <Box sx={{ 
          display: 'flex', 
          justifyContent: 'space-between',
          alignItems: 'center', 
          py: 0,
          px: 2,
          flexShrink: 0,
          backgroundColor: 'transparent',
          height: '28px'
      }}>
        {currentView === 'Albums' && (
          <Typography 
            variant="caption" 
            color="text.secondary"
            sx={{ 
              fontSize: '0.75rem',
              fontWeight: 400,
              letterSpacing: '0.05rem',
            }}
          >
            {albumCount} ALBUMS
          </Typography>
        )}
        
        {currentView === 'Albums' && (
          <Button 
            variant="text" 
            startIcon={<SortIcon sx={{ fontSize: '0.9rem' }} />} 
            size="small"
            sx={{ 
              color: 'primary.main', 
              fontSize: '0.75rem',
              fontWeight: 400,
              letterSpacing: '0.05rem',
              padding: '2px 8px',
              minHeight: 0,
              height: '24px',
              '&:hover': {
                backgroundColor: 'rgba(255, 255, 255, 0.05)'
              }
            }}
          >
            SORT: ARTIST A-Z
          </Button>
        )}
      </Box>

      {/* Content Area */}
      <Box sx={{ flexGrow: 1, overflowY: 'auto', width: '100%' }}> 
        {renderCurrentView(
          currentView, 
          onAlbumClick, 
          onAlbumHold, 
          onAddToPlaylist,
          gridColumns, 
          onAlbumDoubleClick, 
          onTrackDoubleClick, 
          onPlaylistDoubleClick,
          setAlbumCount
        )}
      </Box>
    </Box>
  );
};

export default MainContent;