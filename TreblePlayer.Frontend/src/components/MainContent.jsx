import React, { useState } from 'react';
import { Box, Button, Typography } from '@mui/material';
import AlbumGrid from './AlbumGrid';
import TrackListView from './TrackListView';
import PlaylistGridView from './PlaylistGridView';
import QueueView from './QueueView';
import ArtistGridView from './ArtistGridView';
import Settings from './Settings';
import SortIcon from '@mui/icons-material/Sort';

const renderCurrentView = (view, props) => {
  const { onAlbumClick, onAlbumHold, gridColumns, onAlbumDoubleClick, onTrackDoubleClick, onPlaylistDoubleClick, setAlbumCount } = props;
  switch (view) {
    case 'Artists': return <ArtistGridView />;
    case 'Tracks': return <TrackListView onTrackDoubleClick={onTrackDoubleClick} />;
    case 'Playlists': return <PlaylistGridView onPlaylistDoubleClick={onPlaylistDoubleClick} />;
    case 'Queue': return <QueueView />;
    case 'Settings': return <Settings />;
    case 'Albums':
    default: return <AlbumGrid onAlbumClick={onAlbumClick} onAlbumHold={onAlbumHold} gridColumns={gridColumns} onAlbumDoubleClick={onAlbumDoubleClick} setAlbumCount={setAlbumCount} />;
  }
};

const MainContent = (props) => {
  const [albumCount, setAlbumCount] = useState(0);
  return (
    <Box sx={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', py: 0, px: 2, flexShrink: 0, height: '28px' }}>
        {props.currentView === 'Albums' && <Typography variant="caption" color="text.secondary">{albumCount} ALBUMS</Typography>}
        {props.currentView === 'Albums' && <Button variant="text" startIcon={<SortIcon sx={{ fontSize: '0.9rem' }} />} size="small" sx={{ color: 'primary.main', fontSize: '0.75rem' }}>SORT: ARTIST A-Z</Button>}
      </Box>
      <Box sx={{ flexGrow: 1, overflowY: 'auto', width: '100%' }}> 
        {renderCurrentView(props.currentView, { ...props, setAlbumCount })}
      </Box>
    </Box>
  );
};

export default MainContent;
