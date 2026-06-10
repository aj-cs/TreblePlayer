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

// Basic Theme (can be customized later)
const theme = createTheme({
  palette: {
    mode: 'dark', // Enable dark mode
    primary: {
      main: '#3B82F6', // Keep primary blue
    },
    background: {
      default: '#121212', // Standard dark background
      paper: '#1e1e1e',   // Slightly lighter surface color
    },
    text: {
      primary: '#ffffff',
      secondary: '#b0b0b0', // Lighter grey for secondary text
    }
  },
  shape: {
    borderRadius: 8, // Apply global rounded corners
  },
  typography: {
    // Keep existing or adjust as needed for dark mode readability
    h1: {
      fontSize: '2.5rem',
      fontWeight: 500,
    },
     h2: {
      fontSize: '1.5rem',
      fontWeight: 500,
    },
    // Add other variants based on your sketch (body, etc.)
  },
  // Optional: Component Overrides for finer control later
  // components: {
  //   MuiButton: {
  //     styleOverrides: {
  //       root: { ... }
  //     }
  //   }
  // }
});

// A placeholder value for when no album is selected
const DETAIL_PANEL_PLACEHOLDER = {
  id: 'placeholder',
  isPlaceholder: true
};

// StatusRibbon wrapper component to consume context
const StatusRibbonWithContext = () => {
  const { status, message, visible } = useStatus();
  return <StatusRibbon status={status} message={message} visible={visible} />;
};

function AppContent() {
  // ---- State ----
  const [currentView, setCurrentView] = useState('Albums'); // 'Albums', 'Tracks', 'Playlists', 'Queue'
  const [detailPanelWidth, setDetailPanelWidth] = useState(320); // Track the detail panel width
  
  // Initialize with either stored album or placeholder
  const [selectedAlbumDetail, setSelectedAlbumDetail] = useState(null);

  // Data state (Should eventually be fetched, but initialize here for now)
  const [albums, setAlbums] = useState([]); 
  const [tracks, setTracks] = useState([]); 
  const [playlists, setPlaylists] = useState([]);
  const [selectedAlbum, setSelectedAlbum] = useState(null); // If needed separately from detail

  // Grid Columns Setting State
  const [gridColumns, setGridColumns] = useState(() => {
    const savedColumns = localStorage.getItem('gridColumns');
    return savedColumns ? parseInt(savedColumns, 10) : 6; // Default to 6
  });

  // Placeholder handlers (Implement actual logic later)
  const handleAddToPlaylist = (item) => console.log('Add to playlist:', item);
  const handlePlayTrack = (track) => console.log('Play track:', track);
  const handlePlayAlbum = (album) => console.log('Play album:', album);
  const handlePlayPlaylist = (playlist) => console.log('Play playlist:', playlist);

  // Placeholder double-click handlers
  const handleAlbumDoubleClick = (album) => {
    console.log('Double-clicked album:', album.title);
    // TODO: Call play album API endpoint
  };
  const handleTrackDoubleClick = (track) => {
    console.log('Double-clicked track:', track.title);
    // TODO: Call play track API endpoint (e.g., play starting from this track)
  };
  const handlePlaylistDoubleClick = (playlist) => {
    console.log('Double-clicked playlist:', playlist.name);
    // TODO: Call play playlist API endpoint
  };

  // Effect to save gridColumns to localStorage when it changes
  useEffect(() => {
    localStorage.setItem('gridColumns', gridColumns.toString());
  }, [gridColumns]);

  // Handler to update grid columns setting
  const handleGridColumnsChange = (newColumns) => {
    // Ensure value is within range 2-6
    const clampedColumns = Math.max(2, Math.min(6, newColumns)); 
    setGridColumns(clampedColumns);
  };

  // Retrieve last selected album from localStorage on mount
  useEffect(() => {
    try {
      const savedAlbum = localStorage.getItem('selectedAlbum');
      
      if (savedAlbum) {
        setSelectedAlbumDetail(JSON.parse(savedAlbum));
      } else {
        // If no saved album, use placeholder
        setSelectedAlbumDetail(DETAIL_PANEL_PLACEHOLDER);
      }
    } catch (error) {
      console.error("Error loading saved album:", error);
      setSelectedAlbumDetail(DETAIL_PANEL_PLACEHOLDER);
    }
  }, []);
  
  // Save selected album to localStorage when it changes
  useEffect(() => {
    if (selectedAlbumDetail && !selectedAlbumDetail.isPlaceholder) {
      localStorage.setItem('selectedAlbum', JSON.stringify(selectedAlbumDetail));
    }
  }, [selectedAlbumDetail]);

  // Modal State (for hold preview)
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [selectedAlbumPreview, setSelectedAlbumPreview] = useState(null);

  // State for Create Playlist Modal
  const [isCreatePlaylistModalOpen, setIsCreatePlaylistModalOpen] = useState(false);

  // ---- Handlers ----

  // Called by TopNavBar to change main view
  const handleViewChange = (view) => {
    setCurrentView(view);
    // Don't clear detail view when changing main view
  };

  // Called by AlbumCard onClick to show detail view
  const handleAlbumClick = (albumData) => {
    setSelectedAlbumDetail(albumData);
  };

  // Called by AlbumCard onHold to show preview modal
  const handleAlbumHold = (albumData) => {
    setSelectedAlbumPreview(albumData);
    setIsPreviewOpen(true);
  };

  // Called by Modal onClose
  const handleClosePreview = () => {
    setIsPreviewOpen(false);
  };

  // Handlers for Create Playlist Modal
  const handleOpenCreatePlaylistModal = () => {
    setIsCreatePlaylistModalOpen(true);
  };

  const handleCloseCreatePlaylistModal = () => {
    setIsCreatePlaylistModalOpen(false);
  };

  const handleSavePlaylist = (name, items) => {
    console.log("Saving playlist:", name, "with items:", items);
    setIsCreatePlaylistModalOpen(false);
  };
  
  // Handle detail panel resize
  const handleDetailPanelResize = (width) => {
    setDetailPanelWidth(width);
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline /> {/* Ensures baseline styles & background color */}
      <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
        {/* Top Navigation Bar */}
        <TopNavBar 
          currentView={currentView} 
          onViewChange={handleViewChange}
          gridColumns={gridColumns}
          onGridColumnsChange={handleGridColumnsChange}
        />
        
        {/* Main Content Area */}
        <Box sx={{ display: 'flex', flexGrow: 1, overflow: 'hidden' }}>
          <MainContent 
            currentView={currentView} 
            albums={albums}
            tracks={tracks}
            playlists={playlists}
            selectedAlbum={selectedAlbum}
            onAlbumClick={handleAlbumClick} 
            onAlbumHold={handleAlbumHold}
            selectedAlbumDetail={selectedAlbumDetail}
            onAddToPlaylist={handleAddToPlaylist} 
            onPlayTrack={handlePlayTrack}
            onPlayAlbum={handlePlayAlbum}
            onPlayPlaylist={handlePlayPlaylist}
            onAlbumDoubleClick={handleAlbumDoubleClick}
            onTrackDoubleClick={handleTrackDoubleClick}
            onPlaylistDoubleClick={handlePlaylistDoubleClick}
            gridColumns={gridColumns}
            isDetailViewOpen={!!selectedAlbumDetail && !selectedAlbumDetail.isPlaceholder}
          />
          
          {/* Resizable Detail Panel */}
          <ResizableDetailPanel 
            album={selectedAlbumDetail}
            onResize={handleDetailPanelResize}
          />
        </Box>

        {/* Bottom Playback Bar */}
        <BottomPlaybackBar />
        
        {/* Status Ribbon */}
        <StatusRibbonWithContext />
      </Box>

      {/* Album Preview Modal */}
      <AlbumPreviewModal
        album={selectedAlbumPreview}
        open={isPreviewOpen}
        onClose={handleClosePreview}
      />

      {/* Create Playlist Modal */}
      <CreatePlaylistModal
        open={isCreatePlaylistModalOpen}
        onClose={handleCloseCreatePlaylistModal}
        onSave={handleSavePlaylist}
      />
    </ThemeProvider>
  );
}

// Wrapper component with context provider
function App() {
  return (
    <StatusProvider>
      <AppContent />
    </StatusProvider>
  );
}

export default App;
