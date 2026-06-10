import React from 'react';
import {
  Box, 
  Typography, 
  List, 
  Divider, 
  Button, 
  Select, // For future queue switching
  MenuItem, // For future queue switching
  FormControl, // For future queue switching
  InputLabel // For future queue switching
} from '@mui/material';
import TrackListItem from './TrackListItem'; // Reuse track item

// Dummy data for placeholder tracks
const queueTracks = [
  { id: 1, number: 1, title: 'Track 1 Title', artist: 'Artist A', duration: '3:15' },
  { id: 2, number: 2, title: 'A Slightly Longer Track Title That Might Wrap', artist: 'Artist B', duration: '4:02' },
  { id: 3, number: 3, title: 'Track 3', artist: 'Artist C', duration: '2:58' },
  { id: 4, number: 4, title: 'Track 4', artist: 'Artist D', duration: '3:30' },
];

// Dummy data for other queues (replace with real data later)
const availableQueues = [
  { id: 'current', name: 'Currently Playing' },
  { id: 'saved1', name: 'My Saved Queue 1' },
  { id: 'saved2', name: 'Workout Mix' },
];

const QueueView = () => {
  const [selectedQueueId, setSelectedQueueId] = React.useState('current');

  const handleQueueChange = (event) => {
    setSelectedQueueId(event.target.value);
    // TODO: Add logic to fetch and display the selected queue's tracks
    // This will likely involve API calls and state management in App.jsx
    console.log("Switching to queue:", event.target.value);
  };

  return (
    <Box sx={{
        flexGrow: 1,
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden' 
    }}>
        {/* Queue Title and Selector */}
        <Box sx={{ p: 3, pb: 2, flexShrink: 0, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography variant="h4" component="h1">
                Queues
            </Typography>
            {/* Placeholder for viewing/switching queues */}
             <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="queue-select-label">Select Queue</InputLabel>
              <Select
                labelId="queue-select-label"
                id="queue-select"
                value={selectedQueueId}
                label="Select Queue"
                onChange={handleQueueChange}
              >
                {availableQueues.map(q => (
                  <MenuItem key={q.id} value={q.id}>{q.name}</MenuItem>
                ))}
              </Select>
            </FormControl>
        </Box>

        <Divider />

        {/* Track List (Scrollable) - Should show tracks for selectedQueueId */}
        <Box sx={{ flexGrow: 1, overflowY: 'auto', px: 3 }}>
            {/* TODO: Conditionally render based on selectedQueueId */}
            {selectedQueueId === 'current' && (
                 <List dense>
                    {queueTracks.map((track) => (
                        <TrackListItem
                            key={track.id}
                            trackNumber={track.number}
                            title={track.title}
                            artist={track.artist}
                            duration={track.duration}
                        />
                    ))}
                </List>
            )}
             {selectedQueueId !== 'current' && (
                <Typography sx={{p: 2, textAlign: 'center', color: 'text.secondary'}}>
                    Track list for selected queue would load here.
                </Typography>
            )}
        </Box>

        <Divider />

        {/* Bottom Button Area - Actions specific to the selected queue? */}
        <Box sx={{ p: 2, flexShrink: 0 }}>
            <Button variant="contained" fullWidth disabled={selectedQueueId !== 'current'}>
                {/* Example action - maybe Clear Current Queue? */}
                Clear Current Queue
            </Button>
        </Box>
    </Box>
  );
};

export default QueueView; 