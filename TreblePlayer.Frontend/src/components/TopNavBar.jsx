import React from 'react';
import { Box, Tabs, Tab, AppBar, Toolbar, IconButton, Tooltip, Slider, alpha, Stack } from '@mui/material';
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
    sx={{ height: 20, width: 20, mr: 3, filter: 'brightness(1.2)' }}
  />
);

// Styled Tab for pill effect
const StyledTab = styled(Tab)(({ theme }) => ({
  minHeight: 32,
  height: 32,
  fontWeight: 500,
  fontSize: '0.75rem',
  letterSpacing: '0.05em',
  padding: '0 16px',
  minWidth: 'auto',
  color: 'rgba(255, 255, 255, 0.5)',
  borderRadius: 16,
  margin: '0 4px',
  transition: 'all 0.2s ease',
  '&.Mui-selected': {
    color: '#fff',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
  },
  '&:hover': {
    color: '#fff',
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
  }
}));

// Window control buttons
const WindowButton = styled(IconButton)(({ theme, isClose }) => ({
  color: 'rgba(255, 255, 255, 0.4)',
  borderRadius: 0,
  padding: '8px 14px',
  transition: 'all 0.2s ease',
  '&:hover': {
    backgroundColor: isClose ? '#e81123' : 'rgba(255, 255, 255, 0.1)',
    color: '#fff'
  },
}));

const TopNavBar = ({ currentView, onViewChange, gridColumns = 6, onGridColumnsChange }) => {
  const handleChange = (event, newValue) => {
    onViewChange(newValue);
  };

  const handleMinimize = () => window.electron?.windowControl('minimize');
  const handleMaximize = () => window.electron?.windowControl('maximize');
  const handleClose = () => window.electron?.windowControl('close');
  const handleSettingsClick = () => onViewChange('Settings');
  const handleGridColumnsChange = (event, newValue) => onGridColumnsChange && onGridColumnsChange(newValue);

  return (
    <AppBar 
      position="static" 
      color="default" 
      elevation={0}
      sx={{ 
        borderBottom: '1px solid rgba(255,255,255,0.05)',
        backgroundColor: '#080808',
        height: 48,
        zIndex: 1100
      }}
      className="draggable-area"
    >
      <Toolbar 
        variant="dense" 
        sx={{ 
          minHeight: 48,
          px: '12px !important',
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
              minHeight: 32,
              '& .MuiTabs-indicator': { display: 'none' },
              '& .MuiTabs-flexContainer': { alignItems: 'center' }
            }}
          >
            <StyledTab label="ARTISTS" value="Artists" />
            <StyledTab label="ALBUMS" value="Albums" />
            <StyledTab label="SONGS" value="Tracks" />
            <StyledTab label="PLAYLISTS" value="Playlists" />
            <StyledTab label="QUEUES" value="Queue" />
          </Tabs>
        </Box>
        
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          {currentView === 'Albums' && (
            <Box sx={{ display: 'flex', alignItems: 'center', width: 120, mr: 3 }} className="non-draggable">
              <GridViewIcon fontSize="small" sx={{ color: 'rgba(255,255,255,0.3)', mr: 1.5, fontSize: 16 }} />
              <Slider
                size="small"
                min={5}
                max={10}
                step={1}
                value={gridColumns}
                onChange={handleGridColumnsChange}
                sx={{
                  color: 'primary.main',
                  height: 2,
                  '& .MuiSlider-thumb': { width: 8, height: 8 },
                  '& .MuiSlider-rail': { opacity: 0.2 },
                  '& .MuiSlider-track': { border: 'none' }
                }}
              />
            </Box>
          )}
          
          <Stack direction="row" spacing={0.5} sx={{ mr: 1 }} className="non-draggable">
            <Tooltip title="Search">
              <IconButton size="small" sx={{ color: 'rgba(255,255,255,0.4)', '&:hover': { color: '#fff' } }}>
                <SearchIcon sx={{ fontSize: 18 }} />
              </IconButton>
            </Tooltip>
            <Tooltip title="Settings">
              <IconButton size="small" onClick={handleSettingsClick} sx={{ color: 'rgba(255,255,255,0.4)', '&:hover': { color: '#fff' } }}>
                <SettingsIcon sx={{ fontSize: 18 }} />
              </IconButton>
            </Tooltip>
          </Stack>
          
          <Box sx={{ display: 'flex', ml: 1 }} className="non-draggable">
            <WindowButton size="small" onClick={handleMinimize}><MinimizeIcon sx={{ fontSize: 14 }} /></WindowButton>
            <WindowButton size="small" onClick={handleMaximize}><CropSquareIcon sx={{ fontSize: 14 }} /></WindowButton>
            <WindowButton size="small" isClose={true} onClick={handleClose}><CloseIcon sx={{ fontSize: 14 }} /></WindowButton>
          </Box>
        </Box>
      </Toolbar>
    </AppBar>
  );
};

export default TopNavBar;
