import React from 'react';
import { Box, Typography, IconButton } from '@mui/material';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import { formatDuration } from '../utils/formatDuration';

const QueueItem = ({ track, isActive, isPlaying, lastPlayedTrackId, index, onClick, onDragStart, onDragOver, onDrop }) => {
  if (!track) return null;

  const trackId = track.trackId !== undefined ? track.trackId : track.id;
  const isResumptionTrack = !isActive && lastPlayedTrackId === trackId;

  return (
    <Box
      onDoubleClick={onClick}
      draggable
      onDragStart={(e) => onDragStart(e, index)}
      onDragOver={(e) => onDragOver(e, index)}
      onDrop={(e) => onDrop(e, index)}
      className={isActive && !isPlaying ? 'pulse-animation' : (isResumptionTrack ? 'pulse-animation' : '')}
      sx={{
        display: 'flex',
        alignItems: 'center',
        py: 0.75,
        px: 1.5,
        borderRadius: 1.5,
        cursor: 'default',
        bgcolor: isActive ? 'rgba(59, 130, 246, 0.15)' : (isResumptionTrack ? 'rgba(255, 255, 255, 0.05)' : 'transparent'),
        border: isResumptionTrack ? '1px solid rgba(59, 130, 246, 0.3)' : '1px solid transparent',
        '&:hover': {
          bgcolor: isActive ? 'rgba(59, 130, 246, 0.2)' : 'rgba(255, 255, 255, 0.03)',
        },
        transition: 'all 0.2s ease',
        userSelect: 'none',
        position: 'relative'
      }}
    >
      <Typography 
        variant="caption" 
        sx={{ 
          width: 24, 
          textAlign: 'center', 
          mr: 1.5, 
          opacity: isActive ? 1 : 0.4,
          color: isActive ? 'primary.main' : 'inherit',
          fontWeight: isActive ? 600 : 400
        }}
      >
        {index + 1}
      </Typography>

      {/* Track Info */}
      <Box sx={{ flexGrow: 1, minWidth: 0, mr: 2 }}>
        <Typography 
          variant="body2" 
          noWrap 
          sx={{ 
            fontWeight: isActive ? 600 : 400,
            color: isActive ? 'primary.main' : 'text.primary',
            lineHeight: 1.3
          }}
        >
          {track.title}
        </Typography>
        <Typography 
          variant="caption" 
          noWrap 
          sx={{ 
            display: 'block', 
            opacity: 0.6,
            lineHeight: 1.2
          }}
        >
          {track.artist} {track.albumTitle ? ` • ${track.albumTitle}` : ''}
        </Typography>
      </Box>

      {/* Duration */}
      <Box sx={{ flexShrink: 0 }}>
        <Typography 
          variant="caption" 
          sx={{ 
            opacity: 0.5,
            fontFamily: 'monospace'
          }}
        >
          {formatDuration(track.duration)}
        </Typography>
      </Box>

      <IconButton size="small" sx={{ opacity: 0, '&:hover': { opacity: 1 }, transition: 'opacity 0.2s', ml: 1 }}>
        <MoreVertIcon sx={{ fontSize: 18 }} />
      </IconButton>
    </Box>
  );
};

export default QueueItem;
