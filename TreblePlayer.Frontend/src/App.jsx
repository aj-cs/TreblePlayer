import React, { useState, useEffect } from 'react';
import { Box, CssBaseline, ThemeProvider, createTheme } from '@mui/material';

// Import Components
import TopNavBar from './components/TopNavBar';
import MainContent from './components/MainContent';
import ResizableDetailPanel from './components/ResizableDetailPanel';
import BottomPlaybackBar from './components/BottomPlaybackBar';
import AlbumPreviewModal from './components/AlbumPreviewModal';
import CreatePlaylistModal from './components/CreatePlaylistModal';
import StatusRibbon from './components/StatusRibbon';

// Import Contexts
import { StatusProvider, useStatus } from './contexts/StatusContext';
import { PlaybackProvider, usePlayback } from './contexts/PlaybackContext';

const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#3B82F6' },
    background: { default: '#121212', paper: '#1e1e1e' },
    text: { primary: '#ffffff', secondary: '#b0b0b0' }
  },
  shape: { borderRadius: 8 },
});

const DETAIL_PANEL_PLACEHOLDER = { id: 'placeholder', isPlaceholder: true };

const StatusRibbonWithContext = () => {
  const { status, message, visible } = useStatus();
  return <StatusRibbon status={status} message={message} visible={visible} />;
};

function AppContent() {
  const { playTrack, playCollection } = usePlayback();
  const [currentView, setCurrentView] = useState('Albums');
  const [detailPanelWidth, setDetailPanelWidth] = useState(320);
  const [selectedAlbumDetail, setSelectedAlbumDetail] = useState(null);
  const [gridColumns, setGridColumns] = useState(6);

  const handleAlbumDoubleClick = (album) => playCollection(album.id, 0); 
  const handleTrackDoubleClick = (track) => {
    // API returns 'trackId' for Tracks, 'id' for Albums/Playlists
    const idToPlay = track.trackId !== undefined ? track.trackId : track.id;
    
    if (idToPlay !== undefined) {
        playTrack(idToPlay);
    } else {
        console.error("Could not determine ID to play for track object:", track);
    }
  };
  const handlePlaylistDoubleClick = (playlist) => playCollection(playlist.id, 1);

  useEffect(() => {
    try {
      const savedAlbum = localStorage.getItem('selectedAlbum');
      setSelectedAlbumDetail(savedAlbum ? JSON.parse(savedAlbum) : DETAIL_PANEL_PLACEHOLDER);
    } catch (error) { setSelectedAlbumDetail(DETAIL_PANEL_PLACEHOLDER); }
  }, []);
  
  useEffect(() => {
    if (selectedAlbumDetail && !selectedAlbumDetail.isPlaceholder) {
      localStorage.setItem('selectedAlbum', JSON.stringify(selectedAlbumDetail));
    }
  }, [selectedAlbumDetail]);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
        <TopNavBar currentView={currentView} onViewChange={setCurrentView} gridColumns={gridColumns} onGridColumnsChange={setGridColumns} />
        <Box sx={{ display: 'flex', flexGrow: 1, overflow: 'hidden' }}>
          <MainContent 
            currentView={currentView} 
            onAlbumClick={setSelectedAlbumDetail} 
            selectedAlbumDetail={selectedAlbumDetail}
            onAlbumDoubleClick={handleAlbumDoubleClick}
            onTrackDoubleClick={handleTrackDoubleClick}
            onPlaylistDoubleClick={handlePlaylistDoubleClick}
            gridColumns={gridColumns}
          />
          <ResizableDetailPanel album={selectedAlbumDetail} onResize={setDetailPanelWidth} onTrackDoubleClick={handleTrackDoubleClick} />
        </Box>
        <BottomPlaybackBar />
        <StatusRibbonWithContext />
      </Box>
    </ThemeProvider>
  );
}

function App() {
  return (
    <StatusProvider>
      <PlaybackProvider>
        <AppContent />
      </PlaybackProvider>
    </StatusProvider>
  );
}

export default App;
