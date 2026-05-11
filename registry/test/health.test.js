const request = require('supertest');
const { createApp } = require('../src/server');

test('GET /health returns 200', async () => {
  const app = createApp({ db: null, dataDir: null, config: {}, metrics: null });
  const res = await request(app).get('/health');
  expect(res.status).toBe(200);
  expect(res.body).toEqual({ status: 'ok' });
});
