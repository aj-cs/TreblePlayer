import React, { useState, useEffect } from 'react';
import { Box, CssBaseline, ThemeProvider, createTheme, Modal, Backdrop, Fade, Slide, Zoom } from '@mui/material';

// Import Components
import TopNavBar from './components/TopNavBar';
import MainContent from './components/MainContent';
import AlbumDetailView from './components/AlbumDetailView';
import BottomPlaybackBar from './components/BottomPlaybackBar';
import StatusRibbon from './components/StatusRibbon';
import QueueView from './components/QueueView';

// Import Contexts
import { StatusProvider, useStatus } from './contexts/StatusContext';
import { PlaybackProvider, usePlayback } from './contexts/PlaybackContext';
import { WebSocketProvider } from './contexts/WebSocketContext';
import { SettingsProvider } from './contexts/SettingsContext';

const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#3B82F6' },
    background: { 
      default: '#080808', 
      paper: '#121212' 
    },
    text: { 
      primary: '#ffffff', 
      secondary: 'rgba(255, 255, 255, 0.65)' 
    },
    divider: 'rgba(255, 255, 255, 0.08)'
  },
  shape: { borderRadius: 12 },
  typography: {
    fontFamily: 'Inter, system-ui, -apple-system, sans-serif',
    h6: { fontWeight: 600, letterSpacing: '-0.01em' },
    subtitle1: { fontWeight: 500 },
    body2: { letterSpacing: '0.01em' }
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          borderRadius: 8,
          fontWeight: 500
        }
      }
    },
    MuiCard: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          backgroundColor: 'rgba(25, 25, 25, 0.4)',
          backdropFilter: 'blur(20px)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          boxShadow: '0 8px 32px 0 rgba(0, 0, 0, 0.37)'
        }
      }
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none'
        }
      }
    }
  }
});

const DETAIL_PANEL_PLACEHOLDER = { id: 'placeholder', isPlaceholder: true };

const StatusRibbonWithContext = () => {
  const { status, message, visible } = useStatus();
  return <StatusRibbon status={status} message={message} visible={visible} />;
};

function AppContent() {
  const { playTrack, playCollection } = usePlayback();
  const [currentView, setCurrentView] = useState('Albums');
  const [selectedAlbumDetail, setSelectedAlbumDetail] = useState(null);
  const [gridColumns, setGridColumns] = useState(6);
  const [isQueueOpen, setIsQueueOpen] = useState(false);
  const [isQueueDeleting, setIsQueueDeleting] = useState(false);

  const handleAlbumDoubleClick = (album) => playCollection(album.id, 0); 
  const handleTrackDoubleClick = (track, index) => {
    if (selectedAlbumDetail && selectedAlbumDetail.id) {
        playCollection(selectedAlbumDetail.id, 0, index);
    } else {
        const idToPlay = track.trackId !== undefined ? track.trackId : track.id;
        if (idToPlay !== undefined) {
            playTrack(idToPlay);
        } else {
            console.error("Could not determine ID to play for track object:", track);
        }
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
      <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh', bgcolor: 'background.default' }}>
        <TopNavBar currentView={currentView} onViewChange={setCurrentView} gridColumns={gridColumns} onGridColumnsChange={setGridColumns} />
        <Box sx={{ display: 'flex', flexGrow: 1, overflow: 'hidden', position: 'relative' }}>
          <MainContent 
            currentView={currentView} 
            onAlbumClick={setSelectedAlbumDetail} 
            selectedAlbumDetail={selectedAlbumDetail}
            onAlbumDoubleClick={handleAlbumDoubleClick}
            onTrackDoubleClick={handleTrackDoubleClick}
            onPlaylistDoubleClick={handlePlaylistDoubleClick}
            gridColumns={gridColumns}
          />
          
          {/* Right Detail Panel (Fixed Width with Glass Effect) */}
          {currentView === 'Albums' && selectedAlbumDetail && !selectedAlbumDetail.isPlaceholder && (
            <Slide direction="left" in={true} mountOnEnter unmountOnExit>
              <Box sx={{ 
                width: 400, 
                borderLeft: '1px solid', 
                borderColor: 'divider', 
                height: '100%', 
                flexShrink: 0, 
                bgcolor: 'rgba(10, 10, 10, 0.4)',
                backdropFilter: 'blur(20px)'
              }}>
                  <AlbumDetailView album={selectedAlbumDetail} width={400} onTrackDoubleClick={handleTrackDoubleClick} />
              </Box>
            </Slide>
          )}

          {/* Centered Queue Overlay */}
          <Modal
            open={isQueueOpen}
            onClose={isQueueDeleting ? undefined : () => setIsQueueOpen(false)}
            closeAfterTransition
            slots={{ backdrop: Backdrop }}
            slotProps={{
              backdrop: {
                sx: {
                  backdropFilter: 'blur(8px)',
                  backgroundColor: 'rgba(0, 0, 0, 0.4)'
                }
              },
            }}
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              '& .MuiModal-container': {
                width: '100%',
                display: 'flex',
                justifyContent: 'center'
              }
            }}
          >
              <Box sx={{
                width: '85vw',
                maxWidth: 1200,
                height: '75vh',
                bgcolor: 'rgba(15, 15, 15, 0.7)',
                backdropFilter: 'blur(40px)',
                borderRadius: 4,
                border: '1px solid rgba(255, 255, 255, 0.1)',
                boxShadow: '0 24px 64px rgba(0,0,0,0.9)',
                overflow: 'hidden',
                display: 'flex',
                flexDirection: 'column',
                outline: 'none'
              }}>
                <QueueView onClose={() => setIsQueueOpen(false)} onDeletingChange={setIsQueueDeleting} />
              </Box>
          </Modal>
        </Box>
        <BottomPlaybackBar onToggleQueue={() => setIsQueueOpen(!isQueueOpen)} isQueueOpen={isQueueOpen} />
        <StatusRibbonWithContext />
      </Box>
    </ThemeProvider>
  );
}

function App() {
  return (
    <WebSocketProvider>
      <StatusProvider>
        <PlaybackProvider>
            <SettingsProvider>
                <AppContent />
            </SettingsProvider>
        </PlaybackProvider>
      </StatusProvider>
    </WebSocketProvider>
  );
}

export default App;
