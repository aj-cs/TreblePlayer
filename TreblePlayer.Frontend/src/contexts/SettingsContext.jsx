import React, { useState, useEffect, createContext, useContext } from 'react';

const SettingsContext = createContext();

export const SettingsProvider = ({ children }) => {
    const [albumArtStyle, setAlbumArtStyle] = useState(() => {
        return localStorage.getItem('albumArtStyle') || 'curved';
    });

    useEffect(() => {
        localStorage.setItem('albumArtStyle', albumArtStyle);
    }, [albumArtStyle]);

    return (
        <SettingsContext.Provider value={{ albumArtStyle, setAlbumArtStyle }}>
            {children}
        </SettingsContext.Provider>
    );
};

export const useSettings = () => useContext(SettingsContext);
