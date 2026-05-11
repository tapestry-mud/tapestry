const { signToken, verifyToken, hashPassword, comparePassword, requireAuth } = require('../src/auth');
const express = require('express');
const request = require('supertest');

test('signToken / verifyToken round-trip', () => {
  const token = signToken({ handle: 'mallek', email: 'mallek@example.com' });
  const payload = verifyToken(token);
  expect(payload.handle).toBe('mallek');
  expect(payload.email).toBe('mallek@example.com');
});

test('verifyToken throws on tampered token', () => {
  const token = signToken({ handle: 'mallek' });
  expect(() => verifyToken(token + 'tampered')).toThrow();
});

test('hashPassword / comparePassword round-trip', async () => {
  const hash = await hashPassword('hunter2');
  expect(await comparePassword('hunter2', hash)).toBe(true);
  expect(await comparePassword('wrong', hash)).toBe(false);
});

describe('requireAuth middleware', () => {
  let app;
  beforeEach(() => {
    app = express();
    app.get('/protected', requireAuth, (req, res) => {
      res.json({ handle: req.user.handle });
    });
  });

  test('rejects request with no Authorization header', async () => {
    const res = await request(app).get('/protected');
    expect(res.status).toBe(401);
  });

  test('rejects request with invalid token', async () => {
    const res = await request(app).get('/protected').set('Authorization', 'Bearer not-a-token');
    expect(res.status).toBe(401);
  });

  test('accepts request with valid token', async () => {
    const token = signToken({ handle: 'mallek', email: 'mallek@example.com' });
    const res = await request(app).get('/protected').set('Authorization', `Bearer ${token}`);
    expect(res.status).toBe(200);
    expect(res.body.handle).toBe('mallek');
  });
});
