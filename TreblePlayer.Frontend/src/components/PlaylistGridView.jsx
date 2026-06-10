import React, { useState, useEffect } from 'react';
import {
  Box, 
  Grid, 
  Card, 
  CardActionArea, 
  Typography,
  Icon,
  CircularProgress
} from '@mui/material';
import PlaylistCard from './PlaylistCard'; // Import the PlaylistCard component
import AddIcon from '@mui/icons-material/Add';
import { getPlaylists } from '../services/apiService'; // Import API function

const PlaylistGridView = ({ onCreatePlaylistClick, onPlaylistDoubleClick }) => {
  const [playlists, setPlaylists] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const loadPlaylists = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const data = await getPlaylists(); 
        // TODO: Adapt backend data structure if needed
        // Assuming data is array { id, name, description, ... }
        setPlaylists(data || []);
      } catch (err) {
        console.error("Failed to load playlists:", err);
        setError(err.message || 'Failed to load playlists.');
      } finally {
        setIsLoading(false);
      }
    };
    loadPlaylists();
  }, []);

  const handlePlaylistClick = (playlist) => {
    console.log("Clicked playlist:", playlist.name);
    // TODO: Implement navigation/detail view for playlists
  };

  const handleCreateClick = () => {
    if (onCreatePlaylistClick) {
      onCreatePlaylistClick();
    }
  };

  // Early return for loading and error states
  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh' }}>
        <CircularProgress />
      </Box>
    );
  }
  if (error) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh' }}>
        <Typography color="error">Error loading playlists: {error}</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ flexGrow: 1, p: 2 }}> {/* Add padding for consistency */}
      {/* Add search/filter/sort for playlists later? */}
      <Grid container spacing={3}> 
        {playlists.map((playlist) => (
          <Grid item key={playlist.id} xs={12} sm={6} md={4} lg={3} sx={{ display: 'flex' }}> {/* Ensure Grid item uses flex */}
            <PlaylistCard 
              playlist={playlist} 
              onPlaylistClick={handlePlaylistClick} 
              onPlaylistDoubleClick={onPlaylistDoubleClick} // Pass handler
            /> 
          </Grid>
        ))}
        
        {/* Add New Playlist Card */}
        <Grid item xs={12} sm={6} md={4} lg={3} sx={{ display: 'flex' }}> {/* Ensure Grid item uses flex */}
          <Card sx={{
            height: '100%',
            display: 'flex',
            flexGrow: 1,
            bgcolor: 'transparent'
          }}>
            <CardActionArea
              onClick={handleCreateClick}
              sx={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                flexGrow: 1,
                bgcolor: 'transparent',
                border: (theme) => `2px dashed ${theme.palette.grey[700]}`,
                color: 'text.secondary',
                height: '100%',
                width: '100%',
                borderRadius: 0,
                py: 4,
                '&:hover': {
                  bgcolor: 'action.hover',
                  borderColor: (theme) => theme.palette.grey[600]
                }
              }}
            >
              <AddIcon sx={{ fontSize: 60 }} />
              <Typography variant="h6" sx={{ mt: 2 }}>
                Create Playlist
              </Typography>
            </CardActionArea>
          </Card>
        </Grid>

        {/* Optional: Message if no playlists exist BUT loading finished ok */}
        {playlists.length === 0 && !isLoading && !error && (
          <Grid item xs={12} sx={{textAlign: 'center', mt: 4}}>
            <Typography color="text.secondary">No playlists created yet.</Typography>
          </Grid>
        )}

      </Grid>
    </Box>
  );
};

export default PlaylistGridView; 