
# Hashing & Digital Signature Showcase

This project is a simple web application designed to teach students about two important security concepts: **password hashing** and **digital signatures**.

You can:
- Create a user account and see how your password gets "hashed" so it can be stored safely.
- Send "signed" messages to other users.
- Watch a video animation that shows how the digital signature is checked to make sure the message is authentic.

The project uses a few different technologies:
- **Frontend (what you see in the browser):** React
- **Backend (the server that does the work):** .NET (C#)
- **Animation Service (creates the videos):** Python with Manim
- **Database (stores users and messages):** PostgreSQL

---

## How to Run the Project

The easiest way to run everything is with Docker, which handles all the setup for you.

### Prerequisites
- **Docker Desktop:** Make sure you have Docker installed and running on your computer.

### Quick Start
1.  **Open a terminal** (like Command Prompt, PowerShell, or Terminal on Mac/Linux).
2.  **Navigate to the project folder:**
    ```bash
    cd path/to/hash_showcasing
    ```
3.  **Run the start script:**
    -   On **Mac or Linux**:
        ```bash
        ./scripts/start-demo.sh
        ```
    -   On **Windows**:
        ```bash
        .\scripts\start-demo.ps1
        ```

This script does everything for you:
- It starts the database, the backend server, and the animation service.
- It opens the web application in your browser at `http://localhost:3000`.

When you're done, just press `Ctrl+C` in the terminal to stop everything.

### Demo Users
The app comes with three ready-to-use accounts. The password for all of them is `asdfasdf`.

-   `alice`: A standard user.
-   `bob`: Another standard user, so you can send messages back and forth.
-   `evil_bob`: A special user whose messages will always fail verification. This is to show you what happens when a signature is invalid.

---

## What's Happening in the App?

Here’s a simple breakdown of the application's flow.

### 1. Creating an Account (Password Hashing)
- When you register, you provide a username and password.
- The backend **does not** store your actual password. Instead, it uses a **hashing algorithm (Argon2)** to turn your password into a long, unique string called a "hash."
- This hash is stored in the database. When you log in, the backend hashes the password you entered and compares it to the stored hash. If they match, you're in!
- This is much safer because even if someone accessed the database, they would only see the hashes, not your real passwords.

### 2. Sending a Message (Digital Signatures)
- When you send a message, the backend creates a **digital signature** for it.
- This is like a unique, un-forgeable seal for your message. It's created using your own secret "private key."
- The signature proves two things:
    1.  **Authenticity:** The message really came from you.
    2.  **Integrity:** The message wasn't changed after you sent it.

### 3. Receiving and Verifying a Message
- When another user receives your message, their screen will show if the signature is "Valid" or "Invalid."
- The backend uses your "public key" (which everyone can see) to check the signature.
- If the check passes, the message is marked as **Valid (Green)**.
- If the check fails (like with `evil_bob`'s messages), it's marked as **Invalid (Red)**.

### 4. Visualizing the Signature
- Click the **"Visualize"** button on any message to see an animation of the verification process.
- The animation service creates a short video that shows:
    1.  The original message content.
    2.  The process of creating a hash of the message.
    3.  How the signature is checked using the public key.
    4.  The final result: whether the hashes match or not.

---

## Project Structure for Students

If you want to explore the code, here’s where to look:

-   `frontend/src/App.js`: The main file for the React user interface. This is where all the buttons and message cards are defined.
-   `backend/src/Api/Controllers/MessageController.cs`: The C# code that handles sending messages, checking the inbox, and creating signatures.
-   `animation_service/scene.py`: The Python code that defines the Manim animation scene. You can change this file to alter the video.
-   `docker-compose.yml`: This file tells Docker how to run all the different parts of the project together.

Have fun exploring!

