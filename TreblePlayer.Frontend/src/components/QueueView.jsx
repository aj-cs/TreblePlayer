import React, { useState, useEffect, useMemo } from 'react';
import { Box, Typography, Divider, IconButton, Stack, List, ListItem, ListItemButton, ListItemIcon, ListItemText, Radio, Button, Grid, Paper, LinearProgress, Collapse, CircularProgress, Skeleton } from '@mui/material';
import QueueItem from './QueueItem';
import { usePlayback } from '../contexts/PlaybackContext';
import { formatDuration } from '../utils/formatDuration';
import SaveIcon from '@mui/icons-material/Save';
import QueueMusicIcon from '@mui/icons-material/QueueMusic';
import EditIcon from '@mui/icons-material/Edit';
import CloseIcon from '@mui/icons-material/Close';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import PauseIcon from '@mui/icons-material/Pause';
import DeleteIcon from '@mui/icons-material/Delete';
import * as api from '../services/apiService';

const QueueView = ({ onClose, onDeletingChange }) => {
  const { activeQueue, currentTrack, playTrack, isPlaying, togglePlay } = usePlayback();
  const [allQueues, setAllQueues] = useState([]);
  const [selectedQueueId, setSelectedQueueId] = useState(null);
  const [selectedQueueData, setSelectedQueueData] = useState(null);
  const [selectedQueues, setSelectedQueues] = useState(new Set());
  const [loadingQueues, setLoadingQueues] = useState(true);
  const [loadingQueueData, setLoadingQueueData] = useState(false);
  const [deletingQueues, setDeletingQueues] = useState(new Set());

  useEffect(() => {
    refreshQueues();
  }, []);

  useEffect(() => {
      onDeletingChange && onDeletingChange(deletingQueues.size > 0);
  }, [deletingQueues, onDeletingChange]);

  useEffect(() => {
    if (activeQueue && !selectedQueueId) {
      setSelectedQueueId(activeQueue.id);
    }
  }, [activeQueue]);

  useEffect(() => {
    if (selectedQueueId) {
        setLoadingQueueData(true);
        api.getQueueById(selectedQueueId).then(data => {
            setSelectedQueueData(data);
        }).catch(err => console.error("Error fetching queue details:", err))
        .finally(() => setLoadingQueueData(false));
    }
  }, [selectedQueueId]);

  const refreshQueues = async () => {
    try {
      setLoadingQueues(true);
      const queues = await api.getQueues();
      setAllQueues(queues || []);
    } catch (e) {
      console.error("Error fetching queues:", e);
    } finally {
      setLoadingQueues(false);
    }
  };

  const sortedTracks = useMemo(() => {
    if (!selectedQueueData?.tracks) return [];
    return [...selectedQueueData.tracks].sort((a,b) => (a.disc - b.disc) || (a.number - b.number));
  }, [selectedQueueData?.tracks]);

  const handlePlayQueue = async (queueId) => {
    try {
      await api.switchToQueue(queueId);
      refreshQueues();
    } catch (e) {
      console.error("Error switching queue:", e);
    }
  };

  const handleDeleteQueue = async (queueId) => {
    if (!queueId) return;
    
    setDeletingQueues(prev => new Set(prev).add(queueId));
    
    try {
      await api.deleteQueue(queueId);
      if (selectedQueueId === queueId) {
        setSelectedQueueId(null);
        setSelectedQueueData(null);
      }
      setAllQueues(prev => prev.filter(q => q.id !== queueId));
    } catch (e) {
      console.error("Error deleting queue:", e);
    } finally {
      setDeletingQueues(prev => {
        const next = new Set(prev);
        next.delete(queueId);
        return next;
      });
    }
  };

  const handleToggleSelectQueue = (queueId) => {
    const newSelected = new Set(selectedQueues);
    if (newSelected.has(queueId)) {
      newSelected.delete(queueId);
    } else {
      newSelected.add(queueId);
    }
    setSelectedQueues(newSelected);
  };

  const handleBulkDelete = async () => {
    const idsToDelete = Array.from(selectedQueues);
    setDeletingQueues(new Set(idsToDelete));
    
    try {
      for (const id of idsToDelete) {
        await api.deleteQueue(id);
      }
      setAllQueues(prev => prev.filter(q => !selectedQueues.has(q.id)));
      setSelectedQueues(new Set());
    } catch (e) {
      console.error("Error in bulk delete:", e);
    } finally {
      setDeletingQueues(new Set());
    }
  };

  const handleDragStart = (e, index) => {
    e.dataTransfer.setData('trackIndex', index);
  };

  const handleDragOver = (e) => {
    e.preventDefault();
  };

  const handleDrop = async (e, targetIndex) => {
    const sourceIndex = parseInt(e.dataTransfer.getData('trackIndex'));
    if (sourceIndex === targetIndex) return;

    const newTracks = [...selectedQueueData.tracks];
    const [movedTrack] = newTracks.splice(sourceIndex, 1);
    newTracks.splice(targetIndex, 0, movedTrack);

    setSelectedQueueData({ ...selectedQueueData, tracks: newTracks });

    try {
        await api.reorderQueue(selectedQueueId, newTracks.map(t => t.id || t.trackId));
    } catch (err) {
        console.error("Error reordering queue:", err);
    }
  };

  const handleSelectAll = () => setSelectedQueues(new Set(allQueues.map(q => q.id)));
  const handleDeselectAll = () => setSelectedQueues(new Set());
  const handleInvertSelection = () => {
      const next = new Set();
      allQueues.forEach(q => {
          if (!selectedQueues.has(q.id)) next.add(q.id);
      });
      setSelectedQueues(next);
  };

  const isPlayingActive = isPlaying && activeQueue?.id === selectedQueueId;

  return (
    <Box sx={{ flexGrow: 1, height: '100%', display: 'flex', flexDirection: 'column', overflow: 'hidden', pointerEvents: deletingQueues.size > 0 ? 'none' : 'auto', opacity: deletingQueues.size > 0 ? 0.7 : 1 }}>
        {(loadingQueues || loadingQueueData) && <LinearProgress sx={{ height: 3 }} />}
        <Box sx={{ px: 3, py: 2, display: 'flex', alignItems: 'center', justifyContent: 'space-between', bgcolor: 'rgba(255,255,255,0.02)' }}>
            <Stack direction="row" spacing={2} alignItems="center">
                <Box sx={{ width: 40, height: 40, borderRadius: 2, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <QueueMusicIcon sx={{ color: 'white' }} />
                </Box>
                <Box>
                    <Typography variant="h6" sx={{ lineHeight: 1.2 }}>Queue Management</Typography>
                    {selectedQueues.size > 0 ? (
                        <Stack direction="row" spacing={1} sx={{ mt: 0.5 }}>
                            <Button variant="outlined" size="small" onClick={handleSelectAll}>Select All</Button>
                            <Button variant="outlined" size="small" onClick={handleDeselectAll}>Deselect All</Button>
                            <Button variant="outlined" size="small" onClick={handleInvertSelection}>Invert</Button>
                        </Stack>
                    ) : (
                        <Typography variant="caption" color="text.secondary">Organize and switch session queues</Typography>
                    )}
                </Box>
            </Stack>
            {onClose && (
                <IconButton onClick={onClose} sx={{ bgcolor: 'rgba(255,255,255,0.05)', '&:hover': { bgcolor: 'rgba(255,255,255,0.1)' } }}>
                    <CloseIcon />
                </IconButton>
            )}
        </Box>

        <Grid container sx={{ flexGrow: 1, overflow: 'hidden' }}>
            <Grid item xs={4} sx={{ borderRight: '1px solid', borderColor: 'divider', height: '100%', display: 'flex', flexDirection: 'column' }}>
                <Box sx={{ p: 2 }}>
                    <Typography variant="overline" color="text.secondary" sx={{ fontWeight: 600 }}>Available Queues</Typography>
                </Box>
                <Box sx={{ flexGrow: 1, overflowY: 'auto', px: 1 }}>
                    <List disablePadding>
                        {loadingQueues ? (
                            Array.from(new Array(5)).map((_, i) => <Skeleton key={i} variant="rectangular" sx={{ mb: 1, height: 50, borderRadius: 2 }} />)
                        ) : (
                            allQueues.map((q) => (
                                <Collapse key={q.id} in={!deletingQueues.has(q.id)}>
                                    <ListItem 
                                        disablePadding
                                        sx={{ mb: 0.5 }}
                                        secondaryAction={
                                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                                {activeQueue?.id === q.id && !deletingQueues.has(q.id) && (
                                                    <Box sx={{ 
                                                        width: 8, 
                                                        height: 8, 
                                                        borderRadius: '50%', 
                                                        bgcolor: 'primary.main',
                                                        boxShadow: '0 0 8px #3B82F6'
                                                    }} />
                                                )}
                                                {deletingQueues.has(q.id) ? (
                                                    <CircularProgress size={20} />
                                                ) : (
                                                    <Radio 
                                                        size="small"
                                                        checked={selectedQueues.has(q.id)}
                                                        onClick={(e) => { e.stopPropagation(); handleToggleSelectQueue(q.id); }}
                                                        sx={{ p: 0.5 }}
                                                    />
                                                )}
                                            </Box>
                                        }
                                    >
                                        <ListItemButton 
                                            selected={selectedQueueId === q.id}
                                            onClick={() => setSelectedQueueId(q.id)}
                                            sx={{ 
                                                borderRadius: 2,
                                                py: 1.5,
                                                '&.Mui-selected': {
                                                    bgcolor: 'rgba(59, 130, 246, 0.08)',
                                                    '&:hover': { bgcolor: 'rgba(59, 130, 246, 0.12)' }
                                                }
                                            }}
                                        >
                                            <ListItemText 
                                                primary={q.title} 
                                                secondary={`${q.trackCount || 0} tracks`}
                                                primaryTypographyProps={{ variant: 'body2', fontWeight: selectedQueueId === q.id ? 600 : 400 }}
                                            />
                                        </ListItemButton>
                                    </ListItem>
                                </Collapse>
                            ))
                        )}
                    </List>
                </Box>
                <Box sx={{ p: 2, borderTop: '1px solid', borderColor: 'divider' }}>
                    {selectedQueues.size > 0 ? (
                        <Stack direction="row" spacing={1}>
                            <Button fullWidth variant="outlined" color="error" size="small" startIcon={deletingQueues.size > 0 ? <CircularProgress size={16} /> : <DeleteIcon />} onClick={handleBulkDelete}>Delete ({selectedQueues.size})</Button>
                        </Stack>
                    ) : (
                        <Typography variant="caption" color="text.secondary" sx={{ textAlign: 'center', display: 'block' }}>Select queues to manage</Typography>
                    )}
                </Box>
            </Grid>

            <Grid item xs={8} sx={{ height: '100%', display: 'flex', flexDirection: 'column', bgcolor: 'rgba(0,0,0,0.1)' }}>
                {loadingQueueData ? (
                    <Box sx={{ p: 2 }}>
                        {Array.from(new Array(8)).map((_, i) => <Skeleton key={i} variant="rectangular" sx={{ mb: 1, height: 40, borderRadius: 1.5 }} />)}
                    </Box>
                ) : selectedQueueData ? (
                    <>
                        <Box sx={{ p: 2.5, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                            <Box>
                                <Typography variant="h5" sx={{ fontWeight: 700 }}>{selectedQueueData.title}</Typography>
                                <Typography variant="caption" color="text.secondary">
                                    {selectedQueueData.tracks?.length || 0} tracks • {formatDuration(selectedQueueData.tracks?.reduce((s, t) => s + (t.duration || 0), 0) || 0)}
                                </Typography>
                            </Box>
                            <Stack direction="row" spacing={1.5}>
                                <Button 
                                    variant="contained" 
                                    startIcon={isPlayingActive ? <PauseIcon /> : <PlayArrowIcon />}
                                    onClick={() => activeQueue?.id === selectedQueueId ? togglePlay() : handlePlayQueue(selectedQueueId)}
                                    sx={{ px: 3, borderRadius: 10 }}
                                >
                                    {activeQueue?.id === selectedQueueId ? (isPlaying ? 'Pause' : 'Resume') : 'Play Now'}
                                </Button>
                                <IconButton size="small" sx={{ bgcolor: 'rgba(255,255,255,0.05)' }}><EditIcon sx={{ fontSize: 20 }} /></IconButton>
                                <IconButton size="small" color="error" sx={{ bgcolor: 'rgba(255,0,0,0.05)' }} onClick={() => handleDeleteQueue(selectedQueueId)}><DeleteIcon sx={{ fontSize: 20 }} /></IconButton>
                            </Stack>
                        </Box>
                        <Divider sx={{ opacity: 0.1 }} />
                        <Box sx={{ flexGrow: 1, overflowY: 'auto', p: 2 }}>
                            {sortedTracks.map((track, index) => (
                                <QueueItem
                                    key={`${track.trackId}-${index}`}
                                    track={track}
                                    index={index}
                                    isActive={activeQueue?.id === selectedQueueId && currentTrack?.trackId === track.trackId && index === activeQueue.currentTrackIndex}
                                    isPlaying={isPlaying}
                                    lastPlayedTrackId={selectedQueueData?.lastPlayedTrackId}
                                    onClick={() => {
                                        if (activeQueue?.id !== selectedQueueId) {
                                            handlePlayQueue(selectedQueueId);
                                        }
                                        playTrack(track.trackId);
                                    }}
                                    onDragStart={handleDragStart}
                                    onDragOver={handleDragOver}
                                    onDrop={handleDrop}
                                />
                            ))}
                        </Box>
                    </>
                ) : (
                    <Box sx={{ flexGrow: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', opacity: 0.4 }}>
                        <Typography>Select a queue to view tracks</Typography>
                    </Box>
                )}
            </Grid>
        </Grid>
    </Box>
  );
};

export default QueueView;
