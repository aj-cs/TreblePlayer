const API_BASE_URL = 'http://localhost:5155/api';

async function fetchApi(endpoint, options = {}) {
  const url = API_BASE_URL + endpoint;
  const defaultHeaders = { 'Content-Type': 'application/json' };
  const config = { ...options, headers: { ...defaultHeaders, ...options.headers } };

  try {
    const response = await fetch(url, config);
    if (!response.ok) throw new Error("HTTP error! status: " + response.status);
    if (response.status === 204) return null;
    return await response.json();
  } catch (error) {
    console.error('Fetch API Error:', error);
    throw error; 
  }
}

export const getAlbums = () => fetchApi('/Music/albums');
export const getArtists = () => fetchApi('/Music/artists');
export const getTracks = () => fetchApi('/Music/tracks');
export const getPlaylists = () => fetchApi('/Music/playlists');
export const getQueues = () => fetchApi('/Music/queues');
export const getQueueById = (id) => fetchApi('/Music/queue/' + id);
export const reorderQueue = (id, trackIds) => fetchApi('/Music/queue/' + id + '/reorder', { method: 'POST', body: JSON.stringify(trackIds) });
export const deleteQueue = (queueId) => fetchApi('/Music/queue/' + queueId, { method: 'DELETE' });
export const getStatus = () => fetchApi('/Music/status');
export const getActiveQueue = () => fetchApi('/Music/queue/active');
export const switchToQueue = (queueId) => fetchApi('/Music/queue/switch/' + queueId, { method: 'POST' });

export const playTrack = (trackId) => fetchApi('/Music/play/' + trackId, { method: 'POST' });
export const playCollection = (id, type, startIndex = 0) => fetchApi(`/Music/playCollection/${id}/${type}/${startIndex}`, { method: 'POST' });
export const resume = () => fetchApi('/Music/resume', { method: 'POST' });
export const pause = () => fetchApi('/Music/pause', { method: 'POST' });
export const stop = () => fetchApi('/Music/stop', { method: 'POST' });
export const next = () => fetchApi('/Music/next', { method: 'POST' });
export const previous = () => fetchApi('/Music/previous', { method: 'POST' });
export const seek = (seconds) => fetchApi('/Music/seek/' + seconds, { method: 'POST' });
export const setLoopMode = (mode) => fetchApi('/Music/loop/set/' + mode, { method: 'POST' });
export const toggleLoopMode = () => fetchApi('/Music/loop/toggle', { method: 'POST' });
export const setShuffle = (enable) => fetchApi('/Music/shuffle/' + enable, { method: 'POST' });

export const createPlaylist = (name, trackIds) => fetchApi('/Music/playlist/create', { 
    method: 'POST', 
    body: JSON.stringify({ title: name, trackIds })
});

export const getMusicFolders = () => fetchApi('/Settings/monitoredfolders');
export const addMusicFolder = (path) => fetchApi('/Settings/monitoredfolders', {
    method: 'POST',
    body: JSON.stringify({ Path: path })
});
export const removeMusicFolder = (id) => fetchApi('/Settings/monitoredfolders/' + id, { method: 'DELETE' });
export const scanAll = () => fetchApi('/Settings/scan/all', { method: 'POST' });
