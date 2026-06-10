import React, { useState, useMemo } from 'react';
import {
  Modal,
  Backdrop,
  Box,
  Typography,
  IconButton,
  TextField,
  Button,
  Stack,
  Divider,
  ToggleButton,
  ToggleButtonGroup,
  Fade,
  List,
  ListItem,
  ListItemText,
  ListItemIcon,
  InputAdornment,
  CircularProgress
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import LibraryMusicIcon from '@mui/icons-material/LibraryMusic';
import AlbumIcon from '@mui/icons-material/Album';
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutline';
import RemoveCircleOutlineIcon from '@mui/icons-material/RemoveCircleOutline';
import SearchIcon from '@mui/icons-material/Search';
import { createPlaylist } from '../services/apiService'; // Import API function

// Reusing modal styling concepts
const modalStyle = {
  position: 'absolute',
  top: '50%',
  left: '50%',
  transform: 'translate(-50%, -50%)',
  width: '90%',
  maxWidth: 900, // Wider for two panels
  height: '85vh', // Use height instead of maxHeight for flex layout
  bgcolor: 'background.paper',
  border: '1px solid #000',
  borderRadius: 2, 
  boxShadow: 24,
  p: 3,
  display: 'flex',
  flexDirection: 'column',
};

const StyledBackdrop = (props) => (
  <Backdrop
    {...props}
    sx={{ backdropFilter: 'blur(4px)', backgroundColor: 'rgba(0,0,0,0.5)' }} 
  />
);

// Placeholder data for available items (replace with fetched data later)
const dummyTracks = Array.from({ length: 25 }, (_, i) => ({ id: `trk_${i}`, title: `Sample Track ${i+1}`, artist: `Artist ${String.fromCharCode(65 + i)}` }));
const dummyAlbums = Array.from({ length: 15 }, (_, i) => ({ id: `alb_${i}`, title: `Sample Album ${i+1}`, artist: `Artist ${String.fromCharCode(70 + i)}` }));

const CreatePlaylistModal = ({ open, onClose, onSave }) => {
  const [playlistName, setPlaylistName] = useState('');
  const [selectedItems, setSelectedItems] = useState([]); // Holds { type: 'track'/'album', id: ..., data: {...} }
  const [viewMode, setViewMode] = useState('tracks'); // 'tracks' or 'albums'
  const [searchTerm, setSearchTerm] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);

  const handleViewModeChange = (event, newMode) => {
    if (newMode !== null) {
      setViewMode(newMode);
      setSearchTerm(''); // Clear search when switching view
    }
  };

  // Filter available items based on search term
  const filteredAvailableItems = useMemo(() => {
    const lowerSearchTerm = searchTerm.toLowerCase();
    if (!lowerSearchTerm) {
      return viewMode === 'tracks' ? dummyTracks : dummyAlbums;
    }
    const source = viewMode === 'tracks' ? dummyTracks : dummyAlbums;
    return source.filter(item => 
        item.title.toLowerCase().includes(lowerSearchTerm) || 
        item.artist.toLowerCase().includes(lowerSearchTerm)
    );
  }, [searchTerm, viewMode]);

  // Add item to selected list
  const handleAddItem = (item, type) => {
      if (!selectedItems.some(selected => selected.id === item.id && selected.type === type)) {
          setSelectedItems([...selectedItems, { type, id: item.id, data: item }]);
      }
  };

  // Remove item from selected list
  const handleRemoveItem = (itemToRemove) => {
      setSelectedItems(selectedItems.filter(item => 
          !(item.id === itemToRemove.id && item.type === itemToRemove.type)
      ));
  };

  // Reset state when modal closes
  const handleClose = () => {
      setPlaylistName('');
      setSelectedItems([]);
      setViewMode('tracks');
      setSearchTerm('');
      setIsSaving(false);
      setSaveError(null);
      onClose(); // Call the original onClose handler
  };

  const handleSaveClick = async () => {
    if (!playlistName.trim()) {
        setSaveError("Please enter a playlist name.");
        return;
    }
    if (selectedItems.length === 0) {
        setSaveError("Please select at least one track or album.");
        return;
    }

    setIsSaving(true);
    setSaveError(null);
    try {
        // Pass only IDs or necessary data
        const itemsToSave = selectedItems.map(item => ({ type: item.type, id: item.id }));
        const newPlaylist = await createPlaylist(playlistName.trim(), itemsToSave);
        console.log("Playlist saved successfully (mock):", newPlaylist);
        // Optionally call the onSave prop passed from App.jsx if needed for global state update
        if(onSave) onSave(newPlaylist); 
        handleClose(); // Close modal on success
    } catch (err) {
        console.error("Failed to save playlist:", err);
        setSaveError(err.message || "Failed to save playlist. Please try again.");
        setIsSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose}
      closeAfterTransition
      BackdropComponent={StyledBackdrop}
      BackdropProps={{ timeout: 500 }}
    >
      <Fade in={open}>
        <Box sx={modalStyle}>
          {/* Header & Close Button */}
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexShrink: 0 }}>
            <Typography variant="h5" component="h2">
              Create New Playlist
            </Typography>
            <IconButton aria-label="close" onClick={handleClose} disabled={isSaving}>
              <CloseIcon />
            </IconButton>
          </Box>

          {/* Playlist Name Input */}
          <TextField 
            label="Playlist Name" 
            variant="outlined" 
            fullWidth 
            value={playlistName}
            onChange={(e) => setPlaylistName(e.target.value)}
            sx={{ mb: 2, flexShrink: 0 }}
            disabled={isSaving}
          />

          {/* Main Content Area: Two Panels */}
          <Box sx={{ display: 'flex', flexGrow: 1, overflow: 'hidden', gap: 2 }}>
            
            {/* Left Panel: Searchable Selection List */}
            <Box sx={{ width: '55%', display: 'flex', flexDirection: 'column', overflow: 'hidden', border: '1px solid', borderColor: 'divider', borderRadius: 1, p: 1.5 }}>
                <Stack direction="row" spacing={1} sx={{ mb: 1.5, flexShrink: 0 }} alignItems="center">
                    <TextField 
                        placeholder={`Search ${viewMode}...`}
                        variant="outlined" 
                        size="small" 
                        fullWidth
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        InputProps={{
                            startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment>,
                        }}
                    />
                    <ToggleButtonGroup value={viewMode} exclusive onChange={handleViewModeChange} size="small">
                        <ToggleButton value="tracks" aria-label="select tracks" title="Search Tracks"><LibraryMusicIcon /></ToggleButton>
                        <ToggleButton value="albums" aria-label="select albums" title="Search Albums"><AlbumIcon /></ToggleButton>
                    </ToggleButtonGroup>
                </Stack>
                
                <Box sx={{ flexGrow: 1, overflowY: 'auto' }}>
                   <List dense>
                     {filteredAvailableItems.map(item => {
                         const isSelected = selectedItems.some(sel => sel.id === item.id && sel.type === viewMode);
                         return (
                             <ListItem 
                                key={`${viewMode}-${item.id}`} 
                                secondaryAction={
                                    <IconButton edge="end" size="small" onClick={() => handleAddItem(item, viewMode)} disabled={isSelected}>
                                        <AddCircleOutlineIcon />
                                    </IconButton>
                                }
                                sx={{ pr: 1, opacity: isSelected ? 0.5 : 1, cursor: isSelected ? 'default' : 'pointer' }}
                             >
                                 <ListItemText 
                                     primary={item.title} 
                                     secondary={item.artist} 
                                     primaryTypographyProps={{ noWrap: true }} 
                                     secondaryTypographyProps={{ noWrap: true }} 
                                 />
                             </ListItem>
                         );
                     })}
                     {filteredAvailableItems.length === 0 && (
                         <Typography sx={{p:2, textAlign: 'center', color: 'text.secondary'}}>No {viewMode} found.</Typography>
                     )}
                   </List>
                </Box>
            </Box>

            {/* Right Panel: Selected Items List */}
            <Box sx={{ width: '45%', display: 'flex', flexDirection: 'column', border: '1px solid', borderColor: 'divider', borderRadius: 1, p: 1.5 }}>
                <Typography variant="subtitle1" gutterBottom sx={{ flexShrink: 0 }}>Selected ({selectedItems.length})</Typography>
                <Box sx={{ flexGrow: 1, overflowY: 'auto' }}>
                     <List dense>
                         {selectedItems.map(item => (
                            <ListItem 
                                key={`${item.type}-${item.id}`} 
                                secondaryAction={
                                     <IconButton edge="end" size="small" onClick={() => handleRemoveItem(item)}>
                                         <RemoveCircleOutlineIcon />
                                     </IconButton>
                                 }
                                sx={{ pr: 1 }}
                            >
                                <ListItemIcon sx={{minWidth: 36}}>
                                    {item.type === 'track' ? <LibraryMusicIcon fontSize="small"/> : <AlbumIcon fontSize="small"/>}
                                </ListItemIcon>
                                <ListItemText 
                                    primary={item.data.title} 
                                    secondary={item.data.artist} 
                                    primaryTypographyProps={{ noWrap: true }} 
                                    secondaryTypographyProps={{ noWrap: true }} 
                                />
                             </ListItem>
                         ))}
                         {selectedItems.length === 0 && (
                             <Typography sx={{p:2, textAlign: 'center', color: 'text.secondary'}}>Add items from the left.</Typography>
                         )}
                     </List>
                </Box>
            </Box>
          </Box>

          {/* Footer Buttons */}
          <Stack direction="row" spacing={2} justifyContent="flex-end" sx={{ mt: 3, flexShrink: 0 }}>
            {saveError && <Typography color="error" sx={{alignSelf: 'center', mr: 'auto'}}>{saveError}</Typography>}
            <Button onClick={handleClose} variant="outlined" disabled={isSaving}>Cancel</Button>
            <Button 
                onClick={handleSaveClick} 
                variant="contained" 
                disabled={selectedItems.length === 0 || !playlistName.trim() || isSaving}
            >
                {isSaving ? <CircularProgress size={24} color="inherit"/> : 'Save Playlist'}
            </Button>
          </Stack>
        </Box>
      </Fade>
    </Modal>
  );
};

export default CreatePlaylistModal; 