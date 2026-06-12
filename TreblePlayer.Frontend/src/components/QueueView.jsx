import React from 'react';
import { Box, Typography, List, Divider, Button } from '@mui/material';
import TrackListItem from './TrackListItem';
import { usePlayback } from '../contexts/PlaybackContext';

const QueueView = () => {
  const { activeQueue, currentTrack, playTrack } = usePlayback();

  if (!activeQueue) return <Box sx={{ p: 3 }}><Typography>No active queue.</Typography></Box>;

  return (
    <Box sx={{ flexGrow: 1, height: '100%', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <Box sx={{ p: 3, pb: 2, flexShrink: 0 }}><Typography variant="h4">{activeQueue.title}</Typography></Box>
        <Divider />
        <Box sx={{ flexGrow: 1, overflowY: 'auto', px: 3 }}>
            <List dense>
                {activeQueue.tracks?.map((track, index) => (
                    <TrackListItem
                        key={track.trackId + '-' + index}
                        trackNumber={track.trackNumber}
                        title={track.title}
                        artist={track.artist}
                        duration={track.duration}
                        isActive={currentTrack?.trackId === track.trackId && index === activeQueue.currentTrackIndex}
                        onClick={() => playTrack(track.trackId)}
                    />
                ))}
            </List>
        </Box>
    </Box>
  );
};

export default QueueView;
