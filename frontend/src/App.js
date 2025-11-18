import React, { useCallback, useEffect, useState } from 'react';
import './App.css';

const defaultAuthState = { username: '', password: '' };
const storageKey = 'hashing-demo-session';

const isBrowserEnvironment = () =>
    typeof window !== 'undefined' && typeof window.document !== 'undefined';

const getBrowserStorage = () => {
    if (!isBrowserEnvironment()) {
        return null;
    }

    try {
        return window.localStorage;
    } catch (error) {
        console.warn('Local storage is unavailable', error);
        return null;
    }
};

const readSessionFromStorage = () => {
    const storage = getBrowserStorage();
    if (!storage) {
        return null;
    }

    try {
        const stored = storage.getItem(storageKey);
        return stored ? JSON.parse(stored) : null;
    } catch (error) {
        console.warn('Failed to read session from storage', error);
        return null;
    }
};

function App() {
    const [isLogin, setIsLogin] = useState(true);
    const [authForm, setAuthForm] = useState(defaultAuthState);
    const [authMessage, setAuthMessage] = useState('');
    const [session, setSession] = useState(readSessionFromStorage);
    const [recipients, setRecipients] = useState([]);
    const [selectedRecipient, setSelectedRecipient] = useState('');
    const [messageContent, setMessageContent] = useState('');
    const [statusMessage, setStatusMessage] = useState('');
    const [inbox, setInbox] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [isSending, setIsSending] = useState(false);
    const [videoUrl, setVideoUrl] = useState('');
    const [videoError, setVideoError] = useState('');
    const [deleteTargetId, setDeleteTargetId] = useState(null);

    const token = session?.token ?? '';
    const currentUser = session?.user ?? null;

    useEffect(() => {
        const storage = getBrowserStorage();
        if (!storage) {
            return;
        }

        try {
            if (session) {
                storage.setItem(storageKey, JSON.stringify(session));
            } else {
                storage.removeItem(storageKey);
            }
        } catch (error) {
            console.warn('Failed to persist session in storage', error);
        }
    }, [session]);

    useEffect(() => {
        if (!token) {
            setRecipients([]);
            setInbox([]);
            setSelectedRecipient('');
            return;
        }

        const abort = new AbortController();

        const fetchRecipients = async () => {
            try {
                const response = await fetch('/api/auth/public_keys', {
                    signal: abort.signal,
                });

                if (!response.ok) {
                    throw new Error('Failed to load recipients.');
                }

                const body = await response.json();
                const filtered = body.filter((user) => user.username !== currentUser?.username);
                setRecipients(filtered);
                if (filtered.length > 0) {
                    setSelectedRecipient((prev) => {
                        if (prev && filtered.some((u) => u.username === prev)) {
                            return prev;
                        }
                        return filtered[0].username;
                    });
                }
            } catch (error) {
                if (error.name !== 'AbortError') {
                    console.error(error);
                    setStatusMessage('Unable to load recipients.');
                }
            }
        };

        const fetchInbox = async () => {
            try {
                setIsLoading(true);
                const response = await authorizedFetch('/api/messages/inbox', token, {
                    signal: abort.signal,
                });

                if (!response.ok) {
                    throw new Error('Failed to load messages.');
                }

                const messages = await response.json();
                setInbox(messages);
            } catch (error) {
                if (error.name !== 'AbortError') {
                    console.error(error);
                    setStatusMessage('Unable to load inbox.');
                }
            } finally {
                setIsLoading(false);
            }
        };

        fetchRecipients();
        fetchInbox();

        return () => abort.abort();
    }, [token, currentUser]);

    const hasRecipients = recipients.length > 0;

    const handleAuthSubmit = async (event) => {
        event.preventDefault();
        setAuthMessage('');

        const url = isLogin ? '/api/auth/login' : '/api/auth/register';

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(authForm),
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Request failed.');
            }

            if (isLogin) {
                const payload = await response.json();
                setSession({ token: payload.token, user: { username: payload.username, userId: payload.userId, publicKey: payload.publicKey } });
                setAuthMessage('Login successful!');
                setAuthForm(defaultAuthState);
            } else {
                setAuthMessage('Registration successful! You can now log in.');
                setIsLogin(true);
                setAuthForm(defaultAuthState);
            }
        } catch (error) {
            setAuthMessage(`Error: ${error.message}`);
        }
    };

    const cleanupVideoResource = useCallback(() => {
        if (!videoUrl || !isBrowserEnvironment()) {
            return;
        }

        if (videoUrl.startsWith('blob:')) {
            URL.revokeObjectURL(videoUrl);
        }
    }, [videoUrl]);

    const handleLogout = () => {
        cleanupVideoResource();
        setVideoUrl('');
        setSession(null);
        setStatusMessage('');
        setVideoError('');
    };

    const handleSendMessage = async (event) => {
        event.preventDefault();
        if (!selectedRecipient) {
            setStatusMessage('Pick a recipient first.');
            return;
        }

        setStatusMessage('');
        setIsSending(true);
        try {
            const response = await authorizedFetch('/api/messages', token, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ recipient_username: selectedRecipient, content: messageContent }),
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Failed to send message.');
            }

            setMessageContent('');
            setStatusMessage('Message sent!');
            await refreshInbox();
        } catch (error) {
            setStatusMessage(`Error: ${error.message}`);
        } finally {
            setIsSending(false);
        }
    };

    const refreshInbox = useCallback(async () => {
        try {
            const response = await authorizedFetch('/api/messages/inbox', token);
            if (!response.ok) {
                throw new Error('Unable to refresh inbox.');
            }
            const messages = await response.json();
            setInbox(messages);
        } catch (error) {
            setStatusMessage(`Error: ${error.message}`);
        }
    }, [token]);

    // Poll for message updates (check every 2 seconds while videos are pending)
    useEffect(() => {
        if (!token || inbox.length === 0) {
            return;
        }

        // Check if any messages are pending video generation
        const hasPendingVideos = inbox.some(msg => !msg.visualization_url);

        if (!hasPendingVideos) {
            return; // No pending videos, stop polling
        }

        const interval = setInterval(() => {
            refreshInbox();
        }, 2000); // Poll every 2 seconds

        return () => clearInterval(interval);
    }, [token, inbox, refreshInbox]);

    const handleVisualize = async (messageId, visualizationUrl) => {
        setVideoError('');
        
        // If visualization URL is available, use it directly
        if (visualizationUrl) {
            // Construct full URL for the video
            cleanupVideoResource();
            const videoFullUrl = resolveVisualizationUrl(messageId, visualizationUrl);
            setVideoUrl(videoFullUrl);
            return;
        }

        // Fallback: if URL is not available yet, show error
        setVideoError('Video is still being generated. Please wait...');
    };

    const handleReanimate = async (messageId) => {
        setInbox(prev => prev.map(msg => msg.message_id === messageId ? { ...msg, visualization_url: null } : msg));
        setStatusMessage('Requesting re-animation...');
        try {
            const response = await authorizedFetch(`/api/messages/${messageId}/reanimate`, token, {
                method: 'POST',
            });
    
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Failed to re-animate message.');
            }
    
            setStatusMessage('Re-animation requested. The video will be updated shortly.');
            await refreshInbox();
        } catch (error) {
            setStatusMessage(`Error: ${error.message}`);
        }
    };

    const handleDelete = async (messageId) => {
        if (isBrowserEnvironment()) {
            const confirmed = window.confirm('Delete this message? This cannot be undone.');
            if (!confirmed) {
                return;
            }

            setStatusMessage('Deleting message...');
            setDeleteTargetId(messageId);

            try {
                const response = await authorizedFetch(`/api/messages/${messageId}`, token, {
                    method: 'DELETE',
                });

                if (!response.ok && response.status !== 204) {
                    const errorText = await response.text();
                    throw new Error(errorText || 'Failed to delete message.');
                }

                setInbox(prev => prev.filter(msg => msg.message_id !== messageId));

                if (videoUrl && videoUrl.includes(messageId)) {
                    cleanupVideoResource();
                    setVideoUrl('');
                }

                setStatusMessage('Message deleted.');
            } catch (error) {
                setStatusMessage(`Error: ${error.message}`);
            } finally {
                setDeleteTargetId(null);
            }
        }
    };

    const closeVideoModal = () => {
        cleanupVideoResource();
        setVideoUrl('');
    };

    const renderInbox = () => {
        if (isLoading) {
            return <p className="panel-status">Loading inbox…</p>;
        }

        if (inbox.length === 0) {
            return <p className="panel-status">No messages yet. Send a message to yourself from another account to populate the inbox.</p>;
        }

        return inbox.map((item) => {
            const timestamp = new Date(item.created_at_utc).toLocaleString();
            const hasVideo = !!item.visualization_url;
            const statusColors = {
                'Valid': '#2E7D32',
                'Invalid': '#C62828',
                'Unsigned': '#FF6F00'
            };
            const statusColor = statusColors[item.verification_status] || '#424242';
            
            return (
                <div key={item.message_id} className="message-card">
                    <div className="message-card__header">
                        <span className="message-card__sender">From: {item.sender_username}</span>
                        <span className="message-card__time">{timestamp}</span>
                    </div>
                    <div style={{ marginBottom: '8px' }}>
                        <span style={{ fontWeight: 'bold', color: statusColor }}>Status: {item.verification_status}</span>
                    </div>
                    <p className="message-card__content">{item.content}</p>
                    <div className="message-card__actions">
                        <button
                            onClick={() => handleVisualize(item.message_id, item.visualization_url)}
                            disabled={!hasVideo}
                            className="message-action-button message-action-button--primary"

                        >
                            {hasVideo ? 'Visualize' : 'Generating...'}
                        </button>
                        <button
                            onClick={() => handleReanimate(item.message_id)}
                            className="message-action-button message-action-button--primary"

                        >
                            Re-animate
                        </button>
                        <button
                            onClick={() => handleDelete(item.message_id)}
                            className="message-action-button message-action-button--danger"
                            disabled={deleteTargetId === item.message_id}
                        >
                            {deleteTargetId === item.message_id ? 'Deleting…' : 'Delete'}

                        </button>
                    </div>
                </div>
            );
        });
    };

    return (
        <div className="App">
            {!token && (
                <div className="auth-card">
                    <h2>{isLogin ? 'Login' : 'Register'}</h2>
                    <form onSubmit={handleAuthSubmit} className="auth-form">
                        <label>
                            Username
                            <input
                                type="text"
                                value={authForm.username}
                                onChange={(event) => setAuthForm({ ...authForm, username: event.target.value })}
                                required
                            />
                        </label>
                        <label>
                            Password
                            <input
                                type="password"
                                value={authForm.password}
                                onChange={(event) => setAuthForm({ ...authForm, password: event.target.value })}
                                required
                            />
                        </label>
                        <button type="submit" className="primary-button">{isLogin ? 'Login' : 'Register'}</button>
                    </form>
                    <button className="link-button" onClick={() => setIsLogin((prev) => !prev)}>
                        {isLogin ? 'Need to register?' : 'Have an account? Login'}
                    </button>
                    {authMessage && <p className="status-text">{authMessage}</p>}
                </div>
            )}

            {token && currentUser && (
                <div className="dashboard">
                    <div className="dashboard__header">
                        <div>
                            <h2>Hi {currentUser.username}!</h2>
                        </div>
                        <button className="link-button" onClick={handleLogout}>
                            Log out
                        </button>
                    </div>

                    <div className="dashboard__content">
                        <section className="panel">
                            <h3>Send a Message</h3>
                            {!hasRecipients && (
                                <p className="panel-status">No other users are registered yet. Open another browser or incognito window to create a second account.</p>
                            )}
                            {hasRecipients && (
                                <form onSubmit={handleSendMessage} className="message-form">
                                    <label>
                                        Recipient
                                        <select value={selectedRecipient} onChange={(event) => setSelectedRecipient(event.target.value)}>
                                            {recipients.map((recipient) => (
                                                <option key={recipient.username} value={recipient.username}>
                                                    {recipient.username}
                                                </option>
                                            ))}
                                        </select>
                                    </label>
                                    <label>
                                        Message
                                        <textarea
                                            value={messageContent}
                                            onChange={(event) => setMessageContent(event.target.value)}
                                            rows={4}
                                            placeholder="Explain what you are signing."
                                            required
                                        />
                                    </label>
                                    <button type="submit" className="primary-button" disabled={isSending}>
                                        {isSending ? 'Sending…' : 'Send Message'}
                                    </button>
                                </form>
                            )}
                            <button className="link-button" onClick={refreshInbox}>
                                Refresh Inbox
                            </button>
                            {statusMessage && <p className="status-text">{statusMessage}</p>}
                        </section>

                        <section className="panel">
                            <h3>Your Inbox</h3>
                            {videoError && <p className="status-text error">{videoError}</p>}
                            <div className="message-list">{renderInbox()}</div>
                        </section>
                    </div>
                </div>
            )}

            {videoUrl && (
                <div className="video-modal" role="dialog" aria-modal="true">
                    <div className="video-modal__content">
                        <button className="link-button video-modal__close" onClick={closeVideoModal}>
                            Close
                        </button>
                        <video 
                            src={videoUrl} 
                            controls 
                            autoPlay 
                            className="video-player"
                            onError={(e) => {
                                setVideoError('Failed to load video. Please try again.');
                                console.error('Video load error:', e);
                            }}
                        />
                    </div>
                </div>
            )}
        </div>
    );
}

function authorizedFetch(url, token, options = {}) {
    const headers = new Headers(options.headers || {});
    if (!headers.has('Authorization')) {
        headers.set('Authorization', `Bearer ${token}`);
    }
    return fetch(url, { ...options, headers });
}

function resolveVisualizationUrl(messageId, visualizationUrl) {
    const baseUrl = visualizationUrl || `/animation_videos/${messageId}.mp4`;
    const separator = baseUrl.includes('?') ? '&' : '?';
    return `${baseUrl}${separator}ts=${Date.now()}`;
}
export default App;
