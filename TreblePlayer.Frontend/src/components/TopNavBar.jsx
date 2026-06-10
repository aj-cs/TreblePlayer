import React from 'react';
import { Box, Tabs, Tab, AppBar, Toolbar, IconButton, InputBase, Tooltip, Slider } from '@mui/material';
import { styled } from '@mui/material/styles';
import SearchIcon from '@mui/icons-material/Search';
import MinimizeIcon from '@mui/icons-material/Minimize';
import CropSquareIcon from '@mui/icons-material/CropSquare';
import CloseIcon from '@mui/icons-material/Close';
import SettingsIcon from '@mui/icons-material/Settings';
import GridViewIcon from '@mui/icons-material/GridView';

// Logo component
const Logo = () => (
  <Box 
    component="img"
    src="/logo.png"
    alt="TreblePlayer"
    sx={{ height: 24, width: 24, mr: 2 }}
  />
);

// Search button styled component
const SearchButton = styled(IconButton)(({ theme }) => ({
  color: theme.palette.text.secondary,
  '&:hover': {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
  },
}));

// Window control buttons
const WindowButton = styled(IconButton)(({ theme }) => ({
  color: theme.palette.text.secondary,
  borderRadius: 0,
  padding: '8px 16px',
  '&:hover': {
    backgroundColor: props => props.isClose ? 'rgba(255, 0, 0, 0.7)' : 'rgba(255, 255, 255, 0.1)',
  },
}));

const TopNavBar = ({ currentView, onViewChange, gridColumns = 6, onGridColumnsChange }) => {
  const handleChange = (event, newValue) => {
    onViewChange(newValue);
  };

  // Handle window controls
  const handleMinimize = () => {
    if (window.electron) {
      window.electron.windowControl('minimize');
    }
  };

  const handleMaximize = () => {
    if (window.electron) {
      window.electron.windowControl('maximize');
    }
  };

  const handleClose = () => {
    if (window.electron) {
      window.electron.windowControl('close');
    }
  };

  // Navigate to settings view
  const handleSettingsClick = () => {
    onViewChange('Settings');
  };
  
  // Handle grid columns slider change
  const handleGridColumnsChange = (event, newValue) => {
    if (onGridColumnsChange) {
      onGridColumnsChange(newValue);
    }
  };

  return (
    <AppBar 
      position="static" 
      color="default" 
      elevation={0}
      sx={{ 
        borderBottom: 'none',
        backgroundColor: '#121212',
        height: 42,
        '& .MuiToolbar-root': {
          padding: '0 8px',
        }
      }}
      className="draggable-area"
    >
      <Toolbar 
        variant="dense" 
        sx={{ 
          minHeight: 40,
          padding: '0 8px',
          justifyContent: 'space-between'
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          <Logo />
          
          <Tabs 
            value={currentView} 
            onChange={handleChange}
            className="non-draggable"
            sx={{
              minHeight: 40,
              '& .MuiTab-root': {
                minHeight: 40,
                fontWeight: 400,
                fontSize: '0.85rem',
                letterSpacing: '0.06rem',
                padding: '0 16px',
                minWidth: 'auto',
                color: 'rgba(255, 255, 255, 0.7)',
                '&.Mui-selected': {
                  color: '#fff',
                  fontWeight: 500,
                },
              },
              '& .MuiTabs-indicator': {
                height: 2,
                bottom: 0,
                position: 'absolute',
                borderRadius: '2px 2px 0 0'
              },
            }}
          >
            <Tab label="ARTISTS" value="Artists" />
            <Tab label="ALBUMS" value="Albums" />
            <Tab label="SONGS" value="Tracks" />
            <Tab label="PLAYLISTS" value="Playlists" />
          </Tabs>
        </Box>
        
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          {/* Grid columns slider - only show for Albums view */}
          {currentView === 'Albums' && (
            <Box 
              sx={{ 
                display: 'flex', 
                alignItems: 'center', 
                width: 140, 
                mr: 2 
              }}
              className="non-draggable"
            >
              <GridViewIcon 
                fontSize="small" 
                sx={{ color: 'text.secondary', mr: 1 }}
              />
              <Slider
                size="small"
                min={2}
                max={6}
                step={1}
                value={gridColumns}
                onChange={handleGridColumnsChange}
                aria-label="Albums per row"
                marks={[
                  { value: 2 },
                  { value: 3 },
                  { value: 4 },
                  { value: 5 },
                  { value: 6 }
                ]}
                track={false}
                sx={{
                  height: 2,
                  padding: '10px 0',
                  '& .MuiSlider-mark': {
                    height: '6px',
                    width: '1px',
                    backgroundColor: 'rgba(255, 255, 255, 0.3)'
                  },
                  '& .MuiSlider-markActive': {
                    backgroundColor: 'primary.main',
                    opacity: 1
                  },
                  '& .MuiSlider-thumb': {
                    width: 10,
                    height: 10,
                    '&:hover, &.Mui-focusVisible': {
                      boxShadow: '0px 0px 0px 6px rgba(59, 130, 246, 0.16)'
                    }
                  }
                }}
              />
            </Box>
          )}
          
          <Box sx={{ display: 'flex', alignItems: 'center', mr: 2 }}>
            <Tooltip title="Search">
              <SearchButton size="small" className="non-draggable">
                <SearchIcon />
              </SearchButton>
            </Tooltip>
            <Tooltip title="Settings">
              <SearchButton 
                size="small" 
                className="non-draggable" 
                onClick={handleSettingsClick}
              >
                <SettingsIcon />
              </SearchButton>
            </Tooltip>
          </Box>
          
          <Box sx={{ display: 'flex' }} className="non-draggable">
            <WindowButton size="small" onClick={handleMinimize}>
              <MinimizeIcon fontSize="small" />
            </WindowButton>
            <WindowButton size="small" onClick={handleMaximize}>
              <CropSquareIcon fontSize="small" />
            </WindowButton>
            <WindowButton size="small" isClose={true} onClick={handleClose}>
              <CloseIcon fontSize="small" />
            </WindowButton>
          </Box>
        </Box>
      </Toolbar>
    </AppBar>
  );
};

export default TopNavBar; 