import React, { useState, useEffect } from 'react';
import { Box } from '@mui/material';
import { Resizable } from 'react-resizable';
import QueueView from './QueueView';
import DragHandleIcon from '@mui/icons-material/DragHandle';
import 'react-resizable/css/styles.css';

const MIN_WIDTH = 300;
const MAX_WIDTH = 600;
const DEFAULT_WIDTH = 350;

const ResizableQueuePanel = ({ onResize }) => {
  const [width, setWidth] = useState(
    parseInt(localStorage.getItem('queuePanelWidth')) || DEFAULT_WIDTH
  );

  useEffect(() => {
    localStorage.setItem('queuePanelWidth', width);
    if (onResize) onResize(width);
  }, [width, onResize]);

  const handleResize = (event, { size }) => {
    const newWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, size.width));
    setWidth(newWidth);
  };

  return (
    <Resizable
      width={width}
      height={0}
      minConstraints={[MIN_WIDTH, 0]}
      maxConstraints={[MAX_WIDTH, 0]}
      onResize={handleResize}
      resizeHandles={['w']}
      handle={
        <Box sx={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: '8px', cursor: 'col-resize', backgroundColor: 'transparent', '&:hover': { backgroundColor: 'rgba(100, 100, 100, 0.1)' }, zIndex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <DragHandleIcon sx={{ transform: 'rotate(90deg)', fontSize: 18, color: 'text.disabled', opacity: 0.5 }} />
        </Box>
      }
    >
      <Box sx={{ position: 'relative', width: `${width}px`, height: '100%', borderLeft: '1px solid', borderColor: 'divider' }}>
        <QueueView />
      </Box>
    </Resizable>
  );
};

export default ResizableQueuePanel;
