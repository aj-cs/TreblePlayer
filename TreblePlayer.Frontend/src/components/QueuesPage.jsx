import React, { useState, useEffect } from 'react';
import { Box, Typography, List, ListItem, ListItemButton, ListItemText, ListItemIcon, Radio, IconButton, Grid, Paper, Divider, Button, Stack, LinearProgress } from '@mui/material';
import { usePlayback } from '../contexts/PlaybackContext';
import * as api from '../services/apiService';
import QueueMusicIcon from '@mui/icons-material/QueueMusic';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import QueueItem from './QueueItem';
import { formatDuration } from '../utils/formatDuration';

const QueuesPage = () => {
    const { activeQueue, switchToQueue, playTrack, isPlaying, currentTrack } = usePlayback();
    const [queues, setQueues] = useState([]);
    const [selectedQueueId, setSelectedQueueId] = useState(null);
    const [selectedQueueData, setSelectedQueueData] = useState(null);
    const [selectedQueues, setSelectedQueues] = useState(new Set());
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        refreshQueues();
    }, []);

    useEffect(() => {
        if (queues.length > 0 && !selectedQueueId) {
            setSelectedQueueId(activeQueue?.id || queues[0].id);
        }
    }, [queues, activeQueue, selectedQueueId]);

    useEffect(() => {
        if (selectedQueueId) {
            api.getQueueById(selectedQueueId).then(data => {
                setSelectedQueueData(data);
            }).catch(err => console.error("Error fetching queue details:", err));
        }
    }, [selectedQueueId]);

    const refreshQueues = async () => {
        try {
            const data = await api.getQueues();
            setQueues(data || []);
        } catch (e) {
            console.error("Error fetching queues:", e);
        }
    };

    const handlePlayQueue = async (id) => {
        await api.switchToQueue(id);
        refreshQueues();
    };

    const handleDeleteQueue = async (id) => {
        if (!id) return;
        const previousQueues = [...queues];
        setQueues(prev => prev.filter(q => q.id !== id));
        if (selectedQueueId === id) {
            setSelectedQueueId(null);
            setSelectedQueueData(null);
        }
        
        setLoading(true);
        try {
            await api.deleteQueue(id);
        } catch (e) {
            console.error("Error deleting queue:", e);
            setQueues(previousQueues); // Rollback
        } finally {
            setLoading(false);
            refreshQueues();
        }
    };

    const handleBulkDelete = async () => {
        const previousQueues = [...queues];
        const idsToDelete = Array.from(selectedQueues);
        setQueues(prev => prev.filter(q => !selectedQueues.has(q.id)));
        setSelectedQueues(new Set());
        
        setLoading(true);
        try {
            for (const id of idsToDelete) await api.deleteQueue(id);
        } catch (e) {
            console.error("Error in bulk delete:", e);
            setQueues(previousQueues); // Rollback
        } finally {
            setLoading(false);
            refreshQueues();
        }
    };

    const handleDragStart = (e, index) => {
        e.dataTransfer.setData('trackIndex', index);
    };

    const handleDragOver = (e) => e.preventDefault();

    const handleDrop = async (e, targetIndex) => {
        const sourceIndex = parseInt(e.dataTransfer.getData('trackIndex'));
        if (sourceIndex === targetIndex) return;

        const newTracks = [...selectedQueueData.tracks];
        const [movedTrack] = newTracks.splice(sourceIndex, 1);
        newTracks.splice(targetIndex, 0, movedTrack);

        setSelectedQueueData({ ...selectedQueueData, tracks: newTracks });
        await api.reorderQueue(selectedQueueId, newTracks.map(t => t.id || t.trackId));
    };

    const handleSelectAll = () => setSelectedQueues(new Set(queues.map(q => q.id)));
    const handleDeselectAll = () => setSelectedQueues(new Set());
    const handleInvertSelection = () => {
        const next = new Set();
        queues.forEach(q => {
            if (!selectedQueues.has(q.id)) next.add(q.id);
        });
        setSelectedQueues(next);
    };

    return (
        <Box sx={{ p: 4, height: '100%', overflow: 'hidden', display: 'flex', flexDirection: 'column', pointerEvents: loading ? 'none' : 'auto', opacity: loading ? 0.7 : 1 }}>
            {loading && <LinearProgress sx={{ height: 3 }} />}
            <Box sx={{ mb: 4, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <Typography variant="h4" sx={{ fontWeight: 700 }}>Manage Queues</Typography>
                <Stack direction="row" spacing={1}>
                    {selectedQueues.size > 0 ? (
                        <>
                            <Button variant="outlined" size="small" onClick={handleSelectAll}>Select All</Button>
                            <Button variant="outlined" size="small" onClick={handleDeselectAll}>Deselect All</Button>
                            <Button variant="outlined" size="small" onClick={handleInvertSelection}>Invert Selection</Button>
                            <Button variant="contained" color="error" startIcon={<DeleteIcon />} onClick={handleBulkDelete}>Delete ({selectedQueues.size})</Button>
                        </>
                    ) : (
                        <Typography variant="body2" color="text.secondary">Select queues to manage multiple at once</Typography>
                    )}
                </Stack>
            </Box>

            <Grid container spacing={3} sx={{ flexGrow: 1, overflow: 'hidden' }}>
                <Grid item xs={12} md={4} sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
                    <Paper variant="outlined" sx={{ flexGrow: 1, overflowY: 'auto', bgcolor: 'rgba(255,255,255,0.02)' }}>
                        <List disablePadding>
                            {queues.map((q) => (
                                <ListItem 
                                    key={q.id} 
                                    disablePadding
                                    secondaryAction={
                                        <Radio 
                                            size="small" 
                                            checked={selectedQueues.has(q.id)} 
                                            onClick={(e) => { e.stopPropagation(); handleToggleSelectQueue(q.id); }} 
                                        />
                                    }
                                >
                                    <ListItemButton 
                                        selected={selectedQueueId === q.id}
                                        onClick={() => setSelectedQueueId(q.id)}
                                        sx={{ py: 1.5 }}
                                    >
                                        <ListItemIcon sx={{ minWidth: 40 }}>
                                            <PlayArrowIcon onClick={() => handlePlayQueue(q.id)} color={activeQueue?.id === q.id ? "primary" : "default"} />
                                        </ListItemIcon>
                                        <ListItemText 
                                            primary={q.title} 
                                            secondary={`${q.trackCount || 0} tracks • ${formatDuration(q.totalDuration || 0)}`}
                                            primaryTypographyProps={{ fontWeight: activeQueue?.id === q.id ? 600 : 400 }}
                                        />
                                    </ListItemButton>
                                </ListItem>
                            ))}
                        </List>
                    </Paper>
                </Grid>

                <Grid item xs={12} md={8} sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
                        <Typography variant="overline" color="text.secondary">Queue Contents: {selectedQueueData?.title}</Typography>
                        <IconButton size="small" color="error" onClick={() => handleDeleteQueue(selectedQueueId)}><DeleteIcon /></IconButton>
                    </Box>
                    <Paper variant="outlined" sx={{ flexGrow: 1, overflowY: 'auto', bgcolor: 'rgba(0,0,0,0.2)' }}>
                        {selectedQueueData ? (
                            <List dense>
                                {selectedQueueData.tracks?.sort((a,b) => (a.disc - b.disc) || (a.number - b.number))?.map((track, idx) => (
                                    <QueueItem
                                        key={`${track.id || track.trackId}-${idx}`}
                                        track={track}
                                        index={idx}
                                        isActive={activeQueue?.id === selectedQueueId && (currentTrack?.trackId === track.trackId || currentTrack?.id === track.id) && idx === activeQueue.currentTrackIndex}
                                        isPlaying={isPlaying}
                                        lastPlayedTrackId={selectedQueueData?.lastPlayedTrackId}
                                        onClick={() => {
                                            if (activeQueue?.id !== selectedQueueId) {
                                                handlePlayQueue(selectedQueueId);
                                            }
                                            playTrack(track.trackId || track.id);
                                        }}
                                        onDragStart={handleDragStart}
                                        onDragOver={handleDragOver}
                                        onDrop={handleDrop}
                                    />
                                ))}
                            </List>
                        ) : (
                            <Box sx={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', opacity: 0.5 }}>
                                <Typography>Select a queue to view tracks</Typography>
                            </Box>
                        )}
                    </Paper>
                </Grid>
            </Grid>
        </Box>
    );
};

export default QueuesPage;
