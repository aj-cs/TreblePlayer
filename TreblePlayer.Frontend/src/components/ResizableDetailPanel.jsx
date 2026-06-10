import React, { useState, useEffect } from 'react';
import { Box } from '@mui/material';
import { Resizable } from 'react-resizable';
import AlbumDetailView from './AlbumDetailView';
import DragHandleIcon from '@mui/icons-material/DragHandle';
import 'react-resizable/css/styles.css';

// Minimum and maximum widths for the panel
const MIN_WIDTH = 280;
const MAX_WIDTH = 600;
const DEFAULT_WIDTH = 320; // Default width to fit about 6 albums per row

const ResizableDetailPanel = ({ album, onResize }) => {
  // State to track the panel width
  const [width, setWidth] = useState(
    parseInt(localStorage.getItem('detailPanelWidth')) || DEFAULT_WIDTH
  );

  // Save width to localStorage when it changes
  useEffect(() => {
    localStorage.setItem('detailPanelWidth', width);
    // Call onResize callback to notify App that available space for albums has changed
    if (onResize) onResize(width);
  }, [width, onResize]);

  const handleResize = (event, { size }) => {
    // Constrain to min/max width
    const newWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, size.width));
    setWidth(newWidth);
  };

  return (
    <Resizable
      width={width}
      height={0} // Height doesn't matter since we resize width only
      minConstraints={[MIN_WIDTH, 0]}
      maxConstraints={[MAX_WIDTH, 0]}
      onResize={handleResize}
      resizeHandles={['w']} // Only allow resizing from the left side
      handle={
        <Box
          sx={{
            position: 'absolute',
            left: 0,
            top: 0,
            bottom: 0,
            width: '8px',
            cursor: 'col-resize',
            backgroundColor: 'transparent',
            '&:hover': {
              backgroundColor: 'rgba(100, 100, 100, 0.1)',
            },
            zIndex: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <DragHandleIcon
            sx={{
              transform: 'rotate(90deg)',
              fontSize: 18,
              color: 'text.disabled',
              opacity: 0.5,
            }}
          />
        </Box>
      }
    >
      <Box
        sx={{
          position: 'relative',
          width: `${width}px`,
          height: '100%',
        }}
      >
        <AlbumDetailView album={album} width={width} />
      </Box>
    </Resizable>
  );
};

export default ResizableDetailPanel; 