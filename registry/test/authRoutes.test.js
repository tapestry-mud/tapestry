const request = require('supertest');
const { createTestApp, cleanupTestApp } = require('./helpers');

let app, db, dataDir;
beforeEach(() => { ({ app, db, dataDir } = createTestApp()); });
afterEach(() => cleanupTestApp({ db, dataDir }));

describe('POST /v1/auth/register', () => {
  test('creates account and returns JWT', async () => {
    const res = await request(app).post('/v1/auth/register').send({
      handle: 'mallek',
      email: 'mallek@example.com',
      password: 'hunter2',
    });
    expect(res.status).toBe(201);
    expect(res.body.token).toBeDefined();
  });

  test('rejects duplicate handle', async () => {
    const body = { handle: 'mallek', email: 'mallek@example.com', password: 'hunter2' };
    await request(app).post('/v1/auth/register').send(body);
    const res = await request(app).post('/v1/auth/register').send({ ...body, email: 'other@example.com' });
    expect(res.status).toBe(409);
  });

  test('rejects duplicate email', async () => {
    const body = { handle: 'mallek', email: 'mallek@example.com', password: 'hunter2' };
    await request(app).post('/v1/auth/register').send(body);
    const res = await request(app).post('/v1/auth/register').send({ ...body, handle: 'other' });
    expect(res.status).toBe(409);
  });

  test('rejects invalid handle characters', async () => {
    const res = await request(app).post('/v1/auth/register').send({
      handle: 'Bad Handle!',
      email: 'x@x.com',
      password: 'pass',
    });
    expect(res.status).toBe(400);
  });

  test('rejects missing fields', async () => {
    const res = await request(app).post('/v1/auth/register').send({ handle: 'mallek' });
    expect(res.status).toBe(400);
  });
});

describe('POST /v1/auth/login', () => {
  beforeEach(async () => {
    await request(app).post('/v1/auth/register').send({
      handle: 'mallek', email: 'mallek@example.com', password: 'hunter2',
    });
  });

  test('returns JWT on valid credentials', async () => {
    const res = await request(app).post('/v1/auth/login').send({
      email: 'mallek@example.com', password: 'hunter2',
    });
    expect(res.status).toBe(200);
    expect(res.body.token).toBeDefined();
  });

  test('rejects wrong password', async () => {
    const res = await request(app).post('/v1/auth/login').send({
      email: 'mallek@example.com', password: 'wrong',
    });
    expect(res.status).toBe(401);
  });

  test('rejects unknown email', async () => {
    const res = await request(app).post('/v1/auth/login').send({
      email: 'nobody@example.com', password: 'hunter2',
    });
    expect(res.status).toBe(401);
  });
});
