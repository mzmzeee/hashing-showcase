import React, { useEffect, useState } from 'react';
import './App.css';

const defaultAuthState = { username: '', password: '' };

function App() {
    const [isLogin, setIsLogin] = useState(true);
    const [authForm, setAuthForm] = useState(defaultAuthState);
    const [authMessage, setAuthMessage] = useState('');
    const [session, setSession] = useState(() => {
        try {
            const stored = window.localStorage.getItem('hashing-demo-session');
            return stored ? JSON.parse(stored) : null;
        } catch (error) {
            console.warn('Failed to read session from storage', error);
            return null;
        }
    });
    const [recipients, setRecipients] = useState([]);
    const [selectedRecipient, setSelectedRecipient] = useState('');
    const [messageContent, setMessageContent] = useState('');
    const [statusMessage, setStatusMessage] = useState('');
    const [inbox, setInbox] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [isSending, setIsSending] = useState(false);
    const [visualizingId, setVisualizingId] = useState(null);
    const [videoUrl, setVideoUrl] = useState('');
    const [videoError, setVideoError] = useState('');

    const token = session?.token ?? '';
    const currentUser = session?.user ?? null;

    useEffect(() => {
        if (session) {
            window.localStorage.setItem('hashing-demo-session', JSON.stringify(session));
        } else {
            window.localStorage.removeItem('hashing-demo-session');
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

    const handleLogout = () => {
        if (videoUrl) {
            URL.revokeObjectURL(videoUrl);
        }
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

    const refreshInbox = async () => {
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
    };

    const handleVisualize = async (messageId) => {
        setVideoError('');
        setVisualizingId(messageId);

        try {
            const response = await authorizedFetch('/api/visualize/signature', token, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message_id: messageId }),
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Visualization failed.');
            }

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            if (videoUrl) {
                URL.revokeObjectURL(videoUrl);
            }
            setVideoUrl(url);
        } catch (error) {
            setVideoError(error.message);
        } finally {
            setVisualizingId(null);
        }
    };

    const closeVideoModal = () => {
        if (videoUrl) {
            URL.revokeObjectURL(videoUrl);
        }
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
            const isLoadingVideo = visualizingId === item.message_id;
            return (
                <div key={item.message_id} className="message-card">
                    <div className="message-card__header">
                        <span className="message-card__sender">From: {item.sender_username}</span>
                        <span className="message-card__time">{timestamp}</span>
                    </div>
                    <p className="message-card__content">{item.content}</p>
                    <button
                        className="primary-button"
                        onClick={() => handleVisualize(item.message_id)}
                        disabled={isLoadingVideo}
                    >
                        {isLoadingVideo ? 'Generating…' : 'Verify & Visualize'}
                    </button>
                </div>
            );
        });
    };

    return (
        <div className="App">
            <header className="warning-banner">
                Educational demo only. Do not use this custom hashing in production—use established password hashing such as bcrypt or Argon2.
            </header>

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
                            <p>Your public key is saved for others to verify your messages.</p>
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
                        <video src={videoUrl} controls autoPlay className="video-player" />
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

export default App;
