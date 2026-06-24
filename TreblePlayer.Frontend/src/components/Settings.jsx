import React, { useState, useEffect } from 'react';
import { 
  Box, 
  Typography, 
  Paper, 
  List, 
  ListItem, 
  Button, 
  TextField, 
  IconButton, 
  Divider,
  CircularProgress,
  Alert,
  Stack,
  Tooltip,
  ToggleButton,
  ToggleButtonGroup
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import AddIcon from '@mui/icons-material/Add';
import FolderIcon from '@mui/icons-material/Folder';
import FolderOpenIcon from '@mui/icons-material/FolderOpen';
import { getMusicFolders, addMusicFolder, removeMusicFolder } from '../services/apiService';
import { useSettings } from '../contexts/SettingsContext';

const Settings = () => {
  const { albumArtStyle, setAlbumArtStyle } = useSettings();
  const [folders, setFolders] = useState([]);
  const [newFolder, setNewFolder] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [isElectron, setIsElectron] = useState(false);

  useEffect(() => {
    // Check if running in Electron environment
    setIsElectron(window.electron !== undefined);
    loadFolders();
  }, []);

  const loadFolders = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getMusicFolders();
      setFolders(data || []);
    } catch (err) {
      console.error('Failed to load folders:', err);
      setError('Failed to load music folders. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleAddFolder = async (e) => {
    e.preventDefault();
    if (!newFolder.trim()) return;
    
    try {
      setLoading(true);
      setError(null);
      await addMusicFolder(newFolder);
      setNewFolder('');
      setSuccess('Music folder added successfully.');
      await loadFolders(); // Reload the list
    } catch (err) {
      console.error('Failed to add folder:', err);
      setError('Failed to add folder. Make sure the path exists and is accessible.');
    } finally {
      setLoading(false);
    }
  };

  const handleRemoveFolder = async (id) => {
    try {
      setLoading(true);
      setError(null);
      await removeMusicFolder(id);
      setSuccess('Music folder removed successfully.');
      await loadFolders(); // Reload the list
    } catch (err) {
      console.error('Failed to remove folder:', err);
      setError('Failed to remove folder. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleBrowseFolder = async () => {
    try {
      if (isElectron && window.electron.showDirectoryPicker) {
        // Electron method
        const result = await window.electron.showDirectoryPicker();
        if (result && result.filePaths && result.filePaths.length > 0) {
          setNewFolder(result.filePaths[0]);
        }
      } else {
        // Web API method - requires secure context (HTTPS)
        try {
          // Using the modern File System Access API if available
          if ('showDirectoryPicker' in window) {
            const directoryHandle = await window.showDirectoryPicker();
            setNewFolder(directoryHandle.name); // We can only get the name, not the full path in web
          } else {
            alert('File browser not supported in this browser. Please type the path manually.');
          }
        } catch (err) {
          console.error('Browser file picker error:', err);
          // Fallback to manual input if permission denied or other error
        }
      }
    } catch (err) {
      console.error('Error selecting folder:', err);
    }
  };

  // Clear success message after 3 seconds
  useEffect(() => {
    if (success) {
      const timer = setTimeout(() => {
        setSuccess(null);
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [success]);

  return (
    <Box sx={{ p: 3, maxWidth: 800, mx: 'auto' }}>
      <Typography variant="h4" gutterBottom>
        Settings
      </Typography>
      
      <Paper elevation={3} sx={{ mt: 4, p: 3, borderRadius: 2 }}>
        <Typography variant="h5" gutterBottom>
          Music Folders
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          Add directories containing your music files. TreblePlayer will scan these folders for music.
        </Typography>
        
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        
        {success && (
          <Alert severity="success" sx={{ mb: 2 }}>
            {success}
          </Alert>
        )}
        
        <form onSubmit={handleAddFolder}>
          <Stack direction="row" spacing={1} sx={{ mb: 3 }}>
            <TextField
              fullWidth
              label="Music folder path"
              value={newFolder}
              onChange={(e) => setNewFolder(e.target.value)}
              variant="outlined"
              size="small"
              placeholder="e.g., /home/user/Music"
              disabled={loading}
            />
            <Tooltip title="Browse for folder">
              <IconButton 
                onClick={handleBrowseFolder}
                disabled={loading}
                color="primary"
              >
                <FolderOpenIcon />
              </IconButton>
            </Tooltip>
            <Button
              startIcon={<AddIcon />}
              variant="contained"
              onClick={handleAddFolder}
              disabled={loading || !newFolder.trim()}
              type="submit"
            >
              Add
            </Button>
          </Stack>
        </form>
        
        <Divider sx={{ mb: 2 }} />

        <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>
          UI Customization
        </Typography>
        <Box sx={{ mb: 3 }}>
            <Typography variant="body2" color="text.secondary" gutterBottom>Album Art Style</Typography>
            <ToggleButtonGroup
                value={albumArtStyle}
                exclusive
                onChange={(e, val) => val && setAlbumArtStyle(val)}
                size="small"
            >
                <ToggleButton value="curved">Curved Corners</ToggleButton>
                <ToggleButton value="square">Square</ToggleButton>
            </ToggleButtonGroup>
        </Box>
        
        <Divider sx={{ mb: 2 }} />
        
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
            <CircularProgress size={24} />
          </Box>
        ) : (
          <List sx={{ bgcolor: 'background.paper', borderRadius: 1 }}>
            {folders.length === 0 ? (
              <ListItem sx={{ justifyContent: 'center', py: 3 }}>
                <Typography color="text.secondary">No music folders configured</Typography>
              </ListItem>
            ) : (
              folders.map((folder) => (
                <ListItem
                  key={folder.id}
                  secondaryAction={
                    <IconButton 
                      edge="end" 
                      aria-label="delete"
                      onClick={() => handleRemoveFolder(folder.id)}
                      disabled={loading}
                    >
                      <DeleteIcon />
                    </IconButton>
                  }
                  sx={{ 
                    py: 1.5,
                    borderBottom: '1px solid',
                    borderColor: 'divider'
                  }}
                >
                  <FolderIcon sx={{ mr: 1, color: 'primary.main' }} />
                  <Typography sx={{ wordBreak: 'break-all' }}>{folder.path}</Typography>
                </ListItem>
              ))
            )}
          </List>
        )}
        
        <Box sx={{ mt: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Note: Adding or removing folders will trigger a rescan of your music library
          </Typography>
        </Box>
      </Paper>
    </Box>
  );
};

export default Settings; 
