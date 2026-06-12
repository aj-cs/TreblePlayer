import React from 'react';
import { Box, Typography, IconButton, Slider, Stack, alpha } from '@mui/material';
import { SkipPrevious, PlayArrow, Pause, SkipNext, VolumeUp } from '@mui/icons-material';
import { formatDuration } from '../utils/formatDuration';
import { usePlayback } from '../contexts/PlaybackContext';

const BottomPlaybackBar = () => {
  const { currentTrack, isPlaying, position, duration, togglePlay, next, previous, seek } = usePlayback();

  const handleSeek = (_, newValue) => {
    seek(newValue);
  };

  return (
    <Box sx={{ height: 100, width: '100%', bgcolor: 'background.paper', borderTop: '1px solid', borderColor: 'divider', display: 'flex', flexDirection: 'column', position: 'relative' }}>
      <Box sx={{ position: 'absolute', top: -6, left: 0, right: 0 }}>
        <Slider
          value={position}
          min={0}
          max={duration || 100}
          onChange={handleSeek}
          sx={{ height: 6, p: 0, '& .MuiSlider-thumb': { display: 'none' } }}
        />
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, minWidth: 250 }}>
          <Box sx={{ width: 60, height: 60, bgcolor: 'grey.800', borderRadius: 1 }}>
            {currentTrack?.artworkUrl && <img src={currentTrack.artworkUrl} style={{width: '100%', height: '100%', borderRadius: 4}} alt="Cover" />}
          </Box>
          <Box>
            <Typography variant="subtitle1" noWrap>{currentTrack?.title || 'Not playing'}</Typography>
            <Typography variant="body2" color="text.secondary" noWrap>{currentTrack?.artist || '-'}</Typography>
          </Box>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <IconButton onClick={previous}><SkipPrevious /></IconButton>
          <IconButton onClick={togglePlay} size="large">
            {isPlaying ? <Pause /> : <PlayArrow />}
          </IconButton>
          <IconButton onClick={next}><SkipNext /></IconButton>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 250, justifyContent: 'flex-end' }}>
          <Typography variant="caption">{formatDuration(position)} / {formatDuration(duration)}</Typography>
          <VolumeUp sx={{ color: 'text.secondary' }} />
        </Box>
      </Box>
    </Box>
  );
};

export default BottomPlaybackBar;
