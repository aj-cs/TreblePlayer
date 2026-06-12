import React, { useState, useEffect } from 'react';
import { Box, Typography, Grid, Card, CardContent, CardActionArea } from '@mui/material';
import * as api from '../services/apiService';

const ArtistGridView = ({ onArtistClick }) => {
  const [artists, setArtists] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getArtists().then(data => {
      setArtists(data); // Wait, this is a typo. Should be setArtists(data)
      setLoading(false);
    });
  }, []);

  return (
    <Box sx={{ p: 3 }}>
      <Grid container spacing={2}>
        {artists.map(artist => (
          <Grid item key={artist.name} xs={12} sm={6} md={4} lg={3} xl={2}>
            <Card sx={{ bgcolor: 'background.paper' }}>
              <CardActionArea>
                <CardContent>
                  <Typography variant="h6" noWrap>{artist.name}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {artist.albumCount} Albums • {artist.trackCount} Tracks
                  </Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
};

export default ArtistGridView;
