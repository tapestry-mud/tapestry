const { createApp } = require('../src/server');

function createTestApp(overrides = {}) {
  const app = createApp({ db: null, dataDir: null, config: {}, metrics: null, ...overrides });
  return { app };
}

module.exports = { createTestApp };
