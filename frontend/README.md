# React Frontend

This React app provides a minimal registration and login form for the educational hashing demo. It submits requests directly to the ASP.NET Core API using `fetch` and displays the API responses.

## Local development

- `npm install`
- `npm start` — launches the dev server on <http://localhost:3000> with proxy forwarding to the API on port 8080.
- `npm test` — runs the CRA test suite in watch mode.
- `npm run build` — creates a production build inside `build/`.

## Security warning

This UI surfaces the same warning shown in the app: it is **demo code only**. Replace the custom hashing logic with a production-ready algorithm such as bcrypt or Argon2 before using it in real applications.
