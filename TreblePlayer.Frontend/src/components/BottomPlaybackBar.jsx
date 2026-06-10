import React, { useState } from 'react';
import {
  Box,
  Typography,
  IconButton,
  Slider, // For playback progress and volume
  Stack, // For layout
  alpha, // For color manipulation
} from '@mui/material';
import {
  SkipPrevious,
  PlayArrow,
  Pause,
  SkipNext,
  Loop,
  Shuffle,
  VolumeDown,
  VolumeUp,
} from '@mui/icons-material';
import { formatDuration } from '../utils/formatDuration'; // Import duration utility

const BottomPlaybackBar = () => {
  const [isPlaying, setIsPlaying] = useState(false);
  const [isLooping, setIsLooping] = useState(false);
  const [isShuffling, setIsShuffling] = useState(false);
  const [volume, setVolume] = useState(50); // Example volume state
  const [position, setPosition] = useState(0); // Current playback position in seconds
  const [duration, setDuration] = useState(180); // Example track duration in seconds
  const [isDragging, setIsDragging] = useState(false); // Track if user is dragging the seeker

  const handlePlayPause = () => {
    setIsPlaying(!isPlaying);
    // Add playback logic here
  };

  const handleVolumeChange = (event, newValue) => {
    setVolume(newValue);
    // Add volume change logic here
  };

  const handleSeek = (event, newValue) => {
    setPosition(newValue);
    // Implement actual seeking logic here
  };
  
  const handleDragStart = () => {
    setIsDragging(true);
  };
  
  const handleDragEnd = () => {
    setIsDragging(false);
  };

  // Shared slider styling for consistency
  const sliderStyle = {
    '& .MuiSlider-thumb': {
      width: 14,
      height: 14,
      transition: '0.2s cubic-bezier(.47,1.64,.41,.8)',
      '&::before': {
        boxShadow: '0 2px 12px 0 rgba(0,0,0,0.4)',
      },
      '&:hover, &.Mui-focusVisible': {
        boxShadow: '0px 0px 0px 8px rgba(70, 130, 240, 0.2)',
        width: 16,
        height: 16,
      },
      '&.Mui-active': {
        width: 18,
        height: 18,
      },
    },
    '& .MuiSlider-rail': {
      opacity: 0.4,
      backgroundColor: '#555',
    },
    '& .MuiSlider-track': {
      background: 'linear-gradient(90deg, #3a8dff 0%, #86b7ff 100%)',
      border: 'none',
    },
  };

  return (
    <Box sx={{
      height: 100,
      width: '100%',
      bgcolor: 'rgba(26, 26, 26, 0.95)', // Slightly different shade for visual separation
      display: 'flex',
      flexDirection: 'column',
      position: 'relative',
    }}>
      {/* Playback Seeker at the top */}
      <Box sx={{ 
        position: 'absolute', 
        top: 0, 
        left: 0, 
        right: 0, 
        display: 'flex',
        alignItems: 'center',
      }}>
        <Slider
          aria-label="Playback Position"
          value={position}
          min={0}
          max={duration}
          onChange={handleSeek}
          onMouseDown={handleDragStart}
          onTouchStart={handleDragStart}
          onMouseUp={handleDragEnd}
          onTouchEnd={handleDragEnd}
          valueLabelDisplay={isDragging ? "on" : "off"}
          valueLabelFormat={(value) => formatDuration(value)}
          sx={{
            height: 6,
            p: 0,
            ...sliderStyle,
            '& .MuiSlider-track': {
              ...sliderStyle['& .MuiSlider-track'],
              height: 6,
            },
            '& .MuiSlider-thumb': {
              ...sliderStyle['& .MuiSlider-thumb'],
              width: 12,
              height: 12,
              display: position > 0 ? 'block' : 'none', // Only show thumb when playing
              '&:hover, &.Mui-focusVisible': {
                boxShadow: '0px 0px 0px 8px rgba(70, 130, 240, 0.2)',
                width: 16,
                height: 16,
              },
            },
            '& .MuiSlider-valueLabel': {
              backgroundColor: 'rgba(0, 0, 0, 0.85)',
              borderRadius: '4px',
              fontSize: '0.75rem',
              padding: '2px 6px',
              fontWeight: 'bold',
              transform: 'translateY(-20px) translateX(-50%) !important',
              '&:before': {
                display: 'none',
              },
              top: -6,
              left: '50%',
            },
          }}
        />
      </Box>

      {/* Main Playback Controls */}
      <Box sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        p: 2,
        mt: 1, // Add margin top to account for the seeker
        height: 'calc(100% - 4px)', // Adjust height to account for seeker
      }}>
        {/* Left Zone: Art + Info */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, minWidth: 200 }}>
          <Box sx={{ 
            width: 60, 
            height: 60, 
            bgcolor: 'grey.300', 
            borderRadius: 1, 
            display: 'flex', 
            alignItems: 'center', 
            justifyContent: 'center',
            boxShadow: '0 2px 8px rgba(0,0,0,0.2)'
          }}>
             <Typography variant="caption" color="text.secondary">Art</Typography>
          </Box>
          <Box>
            <Typography variant="subtitle1" noWrap>Track Title Placeholder</Typography>
            <Typography variant="body2" color="text.secondary" noWrap>Album & Artist Placeholder</Typography>
          </Box>
        </Box>

        {/* Center Zone: Controls */}
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1 }}>
          <Box sx={{ display: 'flex', alignItems: 'center' }}>
            <IconButton 
              aria-label="previous track"
              sx={{ 
                '&:hover': { 
                  color: theme => theme.palette.primary.main,
                  transform: 'scale(1.1)'
                },
                transition: 'all 0.2s'
              }}
            >
              <SkipPrevious />
            </IconButton>
            <IconButton 
              aria-label={isPlaying ? 'pause' : 'play'} 
              onClick={handlePlayPause} 
              size="large"
              sx={{ 
                bgcolor: theme => alpha(theme.palette.primary.main, 0.1),
                '&:hover': { 
                  bgcolor: theme => alpha(theme.palette.primary.main, 0.2),
                  transform: 'scale(1.05)'
                },
                transition: 'all 0.2s'
              }}
            >
              {isPlaying ? <Pause sx={{ fontSize: 40 }} /> : <PlayArrow sx={{ fontSize: 40 }} />}
            </IconButton>
            <IconButton 
              aria-label="next track"
              sx={{ 
                '&:hover': { 
                  color: theme => theme.palette.primary.main,
                  transform: 'scale(1.1)'
                },
                transition: 'all 0.2s'
              }}
            >
              <SkipNext />
            </IconButton>
          </Box>
          
          {/* Time Display */}
          <Box sx={{ display: 'flex', alignItems: 'center', mt: -1 }}>
            <Typography variant="caption" color="text.secondary">
              {formatDuration(position)} / {formatDuration(duration)}
            </Typography>
          </Box>
        </Box>

        {/* Right Zone: Toggles + Volume */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 200, justifyContent: 'flex-end' }}>
          <IconButton 
            aria-label="loop" 
            onClick={() => setIsLooping(!isLooping)} 
            color={isLooping ? 'primary' : 'default'}
            sx={{ 
              '&:hover': { transform: 'scale(1.1)' },
              transition: 'all 0.2s'
            }}
          >
            <Loop />
          </IconButton>
          <IconButton 
            aria-label="shuffle" 
            onClick={() => setIsShuffling(!isShuffling)} 
            color={isShuffling ? 'primary' : 'default'}
            sx={{ 
              '&:hover': { transform: 'scale(1.1)' },
              transition: 'all 0.2s'
            }}
          >
            <Shuffle />
          </IconButton>
          <Stack spacing={1} direction="row" sx={{ ml: 1, width: 150 }} alignItems="center">
               <VolumeDown sx={{ color: 'text.secondary' }} />
               <Slider 
                 aria-label="Volume" 
                 value={volume} 
                 onChange={handleVolumeChange} 
                 sx={{ 
                   height: 4,
                   width: 100,
                   ...sliderStyle,
                   '& .MuiSlider-track': {
                     ...sliderStyle['& .MuiSlider-track'],
                     height: 4,
                   },
                 }}
               />
               <VolumeUp sx={{ color: 'text.secondary' }} />
          </Stack>
        </Box>
      </Box>
    </Box>
  );
};

export default BottomPlaybackBar;