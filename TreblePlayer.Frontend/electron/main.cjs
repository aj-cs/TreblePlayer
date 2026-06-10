const { app, BrowserWindow, dialog, ipcMain, session } = require('electron');
const path = require('node:path');
const url = require('node:url');

// Handle creating/removing shortcuts on Windows when installing/uninstalling.
if (require('electron-squirrel-startup')) {
  app.quit();
}

const isDev = process.env.NODE_ENV !== 'production';
let mainWindow;

// For development only - disable certificate validation
if (isDev) {
  app.whenReady().then(() => {
    // Disable certificate verification for localhost
    session.defaultSession.webRequest.onBeforeSendHeaders((details, callback) => {
      callback({ cancel: false });
    });
    session.defaultSession.setCertificateVerifyProc((request, callback) => {
      // Always approve certificate for localhost
      const { hostname } = new URL(request.requestUrl);
      if (hostname === 'localhost') {
        callback(0); // Approve
      } else {
        callback(-3); // Use normal validation
      }
    });
  });
}

function createWindow() {
  // Create the browser window.
  mainWindow = new BrowserWindow({
    width: 1440,
    height: 900,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true, // Important for security
      nodeIntegration: false, // Disable Node.js integration in the renderer process
      webSecurity: false, // Disable web security for development only
    },
    frame: false, // Remove standard window frame
    titleBarStyle: 'hidden', // Hide title bar (macOS)
    autoHideMenuBar: true, // Auto-hide menu bar (Windows/Linux)
    backgroundColor: '#121212', // Match dark theme background color
  });

  // Disable certificate error handling
  mainWindow.webContents.session.setCertificateVerifyProc((request, callback) => {
    callback(0); // 0 means success, accepting all certificates
  });

  // Load the index.html of the app.
  const startUrl = isDev
    ? 'http://localhost:5173' // Vite dev server URL (default)
    : url.format({
        pathname: path.join(__dirname, '../dist/index.html'), // Path to the built React app
        protocol: 'file:',
        slashes: true,
      });

  mainWindow.loadURL(startUrl);

  // Open the DevTools in development.
  if (isDev) {
    mainWindow.webContents.openDevTools();
  }
}

// Set up IPC handlers
ipcMain.handle('show-directory-picker', async () => {
  const { canceled, filePaths } = await dialog.showOpenDialog({
    properties: ['openDirectory'],
    title: 'Select Music Folder',
    buttonLabel: 'Select Folder'
  });
  if (canceled) {
    return { canceled: true };
  }
  return { canceled: false, filePaths };
});

// Handle window control actions
ipcMain.on('window-control', (event, action) => {
  if (!mainWindow) return;
  
  switch (action) {
    case 'minimize':
      mainWindow.minimize();
      break;
    case 'maximize':
      if (mainWindow.isMaximized()) {
        mainWindow.unmaximize();
      } else {
        mainWindow.maximize();
      }
      break;
    case 'close':
      mainWindow.close();
      break;
    default:
      console.log(`Unknown window control action: ${action}`);
  }
});

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.whenReady().then(() => {
  createWindow();

  // On OS X it's common to re-create a window in the app when the
  // dock icon is clicked and there are no other windows open.
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

// Quit when all windows are closed, except on macOS. There, it's common
// for applications and their menu bar to stay active until the user quits
// explicitly with Cmd + Q.
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and import them here. 