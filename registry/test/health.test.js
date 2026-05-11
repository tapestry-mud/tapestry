const request = require('supertest');
const { createApp } = require('../src/server');

test('GET /health returns 200', async () => {
  const app = createApp({ db: null, dataDir: null, config: {}, metrics: null });
  const res = await request(app).get('/health');
  expect(res.status).toBe(200);
  expect(res.body).toEqual({ status: 'ok' });
});

const { createTestApp, cleanupTestApp, seedAccount } = require('./helpers');
const { createMetrics } = require('../src/metrics');

describe('GET /metrics', () => {
  let app, db, dataDir;

  beforeEach(() => {
    const metrics = createMetrics();
    ({ app, db, dataDir } = createTestApp({ metrics }));
  });
  afterEach(() => cleanupTestApp({ db, dataDir }));

  test('returns prometheus-format metrics', async () => {
    const res = await request(app).get('/metrics');
    expect(res.status).toBe(200);
    expect(res.text).toContain('tapestry_downloads_total');
    expect(res.text).toContain('tapestry_publishes_total');
    expect(res.text).toContain('tapestry_storage_bytes');
    expect(res.text).toContain('tapestry_active_packages');
  });
});
