// const API_BASE_URL = 'https://localhost:5001/api'; // Changed back to HTTPS default
const API_BASE_URL = 'http://localhost:5155/api'; // Use HTTP for development
/**
 * Helper function to make fetch requests and handle JSON responses/errors.
 * @param {string} endpoint - The API endpoint (e.g., '/albums')
 * @param {RequestInit} [options={}] - Fetch options (method, headers, body, etc.)
 * @returns {Promise<any>} - The JSON response data
 * @throws {Error} - Throws an error if the network response is not ok
 */
async function fetchApi(endpoint, options = {}) {
  const url = `${API_BASE_URL}${endpoint}`;
  
  const defaultHeaders = {
    'Content-Type': 'application/json',
    // Add other default headers if needed (e.g., Authorization)
  };

  const config = {
    ...options,
    headers: {
      ...defaultHeaders,
      ...options.headers,
    },
  };

  try {
    const response = await fetch(url, config);

    if (!response.ok) {
      // Attempt to parse error details from response body
      let errorData = null;
      try {
          errorData = await response.json();
      } catch (e) { /* Ignore if response body is not JSON */ }
      
      console.error('API Error Response:', { 
          status: response.status, 
          statusText: response.statusText, 
          url: response.url, 
          data: errorData 
      });
      throw new Error(`HTTP error! status: ${response.status} ${response.statusText}`);
    }

    // Handle cases with no content (e.g., 204 No Content)
    if (response.status === 204) {
        return null;
    }

    return await response.json();
  } catch (error) {
    console.error('Fetch API Error:', error);
    // Re-throw the error so calling components can handle it
    throw error; 
  }
}

// --- Specific API Methods --- 

export const getAlbums = () => {
  // Assuming you will add an endpoint like [HttpGet("albums")] to MusicController
  return fetchApi('/Music/albums'); 
};

export const getTracks = () => {
  // Assuming you will add an endpoint like [HttpGet("tracks")] to MusicController
  return fetchApi('/Music/tracks'); 
};

export const getPlaylists = () => {
  // Correct path based on MusicController
  return fetchApi('/Music/playlists'); 
};

export const createPlaylist = (name, items) => {
   // Correct path based on MusicController
  const payload = { title: name, trackIds: items.map(i => i.id) }; // Assuming backend expects { title, trackIds }
  // Note: The backend currently expects a PlaylistCreateModel { Title } 
  // and separate calls to AddTrackToPlaylist. 
  // This frontend code assumes a simpler create endpoint accepting trackIds directly.
  // You might need to adjust backend or frontend for full functionality.
  console.warn("Backend endpoint /Music/playlist/create likely needs adjustment to accept track IDs.");
  
  return fetchApi('/Music/playlist/create', { 
    method: 'POST', 
    body: JSON.stringify({ title: name }) // Sending only title as per current backend method signature
    // TODO: After creating, potentially call AddTrackToPlaylist for each selected item ID
  });
};

// --- Settings API Methods ---

export const getMusicFolders = () => {
  return fetchApi('/Settings/monitoredfolders');
};

export const addMusicFolder = (folderPath) => {
  return fetchApi('/Settings/monitoredfolders', {
    method: 'POST',
    body: JSON.stringify({ Path: folderPath })
  });
};

export const removeMusicFolder = (folderId) => {
  return fetchApi(`/Settings/monitoredfolders/${folderId}`, {
    method: 'DELETE'
  });
};

// Add more functions as needed (e.g., getAlbumById, getTrackById, getPlaylistById, etc.) 