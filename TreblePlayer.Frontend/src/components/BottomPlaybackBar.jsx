import React from 'react';
import { Box, Typography, IconButton, Slider, Stack, alpha } from '@mui/material';
import { SkipPrevious, PlayArrow, Pause, SkipNext, VolumeUp, QueueMusic, Shuffle, Repeat } from '@mui/icons-material';
import { formatDuration } from '../utils/formatDuration';
import { usePlayback } from '../contexts/PlaybackContext';

const BottomPlaybackBar = ({ onToggleQueue, isQueueOpen }) => {
  const { currentTrack, isPlaying, position, duration, togglePlay, next, previous, seek } = usePlayback();

  const handleSeek = (_, newValue) => {
    seek(newValue);
  };

  return (
    <Box sx={{ 
        height: 88, 
        width: '100%', 
        bgcolor: '#080808', 
        borderTop: '1px solid rgba(255,255,255,0.05)', 
        display: 'flex', 
        flexDirection: 'column', 
        position: 'relative',
        zIndex: 1200
    }}>
      {/* Progress Bar (Full Width, Flush) */}
      <Box sx={{ position: 'absolute', top: -11, left: 0, right: 0, px: 0 }}>
        <Slider
          value={position}
          min={0}
          max={duration || 100}
          onChange={handleSeek}
          sx={{ 
            height: 4, 
            p: 0, 
            color: 'primary.main',
            '& .MuiSlider-thumb': { 
                width: 12, 
                height: 12, 
                opacity: 0,
                '&:hover, &.Mui-focusVisible': { opacity: 1, boxShadow: 'none' }
            },
            '& .MuiSlider-rail': { opacity: 0.1 },
            '&:hover .MuiSlider-thumb': { opacity: 1 }
          }}
        />
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, py: 1.5, flexGrow: 1 }}>
        {/* Track Info */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, width: '30%' }}>
          <Box sx={{ 
            width: 56, 
            height: 56, 
            bgcolor: 'rgba(255,255,255,0.03)', 
            borderRadius: 1.5,
            overflow: 'hidden',
            boxShadow: '0 4px 12px rgba(0,0,0,0.5)',
            border: '1px solid rgba(255,255,255,0.05)'
          }}>
            {currentTrack?.artworkUrl && <img src={currentTrack.artworkUrl} style={{width: '100%', height: '100%', objectFit: 'cover'}} alt="Cover" />}
          </Box>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="body1" noWrap sx={{ fontWeight: 600, color: '#fff', fontSize: '0.95rem', lineHeight: 1.2 }}>
              {currentTrack?.title || 'Not playing'}
            </Typography>
            <Typography variant="caption" color="text.secondary" noWrap sx={{ opacity: 0.7, fontWeight: 500, display: 'block' }}>
              {currentTrack ? `${currentTrack.artist} • ${currentTrack.albumTitle}` : '-'}
            </Typography>
          </Box>
        </Box>

        {/* Playback Controls */}
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0.5 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                <IconButton size="small" sx={{ color: 'rgba(255,255,255,0.3)' }}><Shuffle sx={{ fontSize: 18 }} /></IconButton>
                <IconButton onClick={previous} sx={{ color: '#fff' }}><SkipPrevious sx={{ fontSize: 28 }} /></IconButton>
                <IconButton 
                    onClick={togglePlay} 
                    sx={{ 
                        bgcolor: '#fff', 
                        color: '#000', 
                        '&:hover': { bgcolor: 'rgba(255,255,255,0.9)' },
                        width: 44,
                        height: 44
                    }}
                >
                    {isPlaying ? <Pause sx={{ fontSize: 28 }} /> : <PlayArrow sx={{ fontSize: 28 }} />}
                </IconButton>
                <IconButton onClick={next} sx={{ color: '#fff' }}><SkipNext sx={{ fontSize: 28 }} /></IconButton>
                <IconButton size="small" sx={{ color: 'rgba(255,255,255,0.3)' }}><Repeat sx={{ fontSize: 18 }} /></IconButton>
            </Box>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.4)', fontFamily: 'monospace', fontSize: '0.7rem' }}>
                    {formatDuration(position)}
                </Typography>
                <Box sx={{ width: 4, height: 4, borderRadius: '50%', bgcolor: 'rgba(255,255,255,0.1)' }} />
                <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.4)', fontFamily: 'monospace', fontSize: '0.7rem' }}>
                    {formatDuration(duration)}
                </Typography>
            </Box>
        </Box>

        {/* Volume & Queue */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, width: '30%', justifyContent: 'flex-end' }}>
          <Stack direction="row" alignItems="center" spacing={1} sx={{ width: 140 }}>
              <VolumeUp sx={{ color: 'rgba(255,255,255,0.3)', fontSize: 20 }} />
              <Slider 
                size="small" 
                defaultValue={70} 
                sx={{ 
                    color: 'rgba(255,255,255,0.2)',
                    '&:hover': { color: 'primary.main' },
                    '& .MuiSlider-thumb': { width: 0, height: 0, '&:hover': { width: 10, height: 10 } }
                }} 
              />
          </Stack>
          <IconButton 
            onClick={onToggleQueue} 
            sx={{ 
                color: isQueueOpen ? 'primary.main' : 'rgba(255,255,255,0.4)',
                bgcolor: isQueueOpen ? 'rgba(59, 130, 246, 0.1)' : 'transparent',
                '&:hover': { color: '#fff' }
            }}
          >
            <QueueMusic />
          </IconButton>
        </Box>
      </Box>
    </Box>
  );
};

export default BottomPlaybackBar;
