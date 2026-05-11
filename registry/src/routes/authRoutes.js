const express = require('express');
const { hashPassword, comparePassword, signToken } = require('../auth');

function createAuthRoutes(db) {
  const router = express.Router();

  router.post('/register', async (req, res) => {
    const { handle, email, password } = req.body || {};
    if (!handle || !email || !password) {
      return res.status(400).json({ error: 'handle, email, and password are required' });
    }
    if (!/^[a-z0-9-]+$/.test(handle)) {
      return res.status(400).json({ error: 'handle must be lowercase alphanumeric with hyphens' });
    }
    try {
      const passwordHash = await hashPassword(password);
      db.prepare(`INSERT INTO accounts (handle, email, password_hash) VALUES (?, ?, ?)`).run(handle, email, passwordHash);
      res.status(201).json({ token: signToken({ handle, email }) });
    } catch (err) {
      if (err.message.includes('UNIQUE constraint failed')) {
        return res.status(409).json({ error: 'handle or email already taken' });
      }
      res.status(500).json({ error: 'registration failed' });
    }
  });

  router.post('/login', async (req, res) => {
    const { email, password } = req.body || {};
    if (!email || !password) {
      return res.status(400).json({ error: 'email and password are required' });
    }
    try {
      const account = db.prepare(`SELECT * FROM accounts WHERE email = ?`).get(email);
      if (!account || !(await comparePassword(password, account.password_hash))) {
        return res.status(401).json({ error: 'invalid credentials' });
      }
      res.json({ token: signToken({ handle: account.handle, email: account.email }) });
    } catch (err) {
      res.status(500).json({ error: 'login failed' });
    }
  });

  return router;
}

module.exports = { createAuthRoutes };
