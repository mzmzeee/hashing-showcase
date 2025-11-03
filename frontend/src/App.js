import React, { useState } from 'react';
import './App.css';

function App() {
    const [isLogin, setIsLogin] = useState(true);
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [message, setMessage] = useState('');

    const handleSubmit = async (e) => {
        e.preventDefault();
        setMessage('');

        const url = isLogin ? '/api/auth/login' : '/api/auth/register';
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ username, password }),
            });

            if (response.ok) {
                setMessage(isLogin ? 'Login successful!' : 'Registration successful!');
            } else {
                const errorText = await response.text();
                setMessage(`Error: ${errorText}`);
            }
        } catch (error) {
            setMessage(`Error: ${error.message}`);
        }
    };

    return (
        <div className="App">
            <div className="form-container">
                <p className="warning-banner">
                    Educational demo only. Do not use this custom hashing in production—use established password hashing such as bcrypt or Argon2.
                </p>
                <h2>{isLogin ? 'Login' : 'Register'}</h2>
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Username</label>
                        <input
                            type="text"
                            value={username}
                            onChange={(e) => setUsername(e.target.value)}
                            required
                        />
                    </div>
                    <div className="form-group">
                        <label>Password</label>
                        <input
                            type="password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            required
                        />
                    </div>
                    <button type="submit">{isLogin ? 'Login' : 'Register'}</button>
                </form>
                <button className="toggle-btn" onClick={() => setIsLogin(!isLogin)}>
                    {isLogin ? 'Need to register?' : 'Have an account? Login'}
                </button>
                {message && <p className="message">{message}</p>}
            </div>
        </div>
    );
}

export default App;
