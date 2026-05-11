const express = require('express');
const pinoHttp = require('pino-http');
const { createAuthRoutes } = require('./routes/authRoutes');
const { createPackageRoutes } = require('./routes/packageRoutes');
const { createPublishRoutes } = require('./routes/publishRoutes');

function createApp({ db, dataDir, config, metrics }) {
  const app = express();
  app.use(express.json());

  if (process.env.NODE_ENV !== 'test') {
    app.use(pinoHttp());
  }

  app.get('/health', (req, res) => {
    res.json({ status: 'ok' });
  });

  if (metrics) {
    app.get('/metrics', async (req, res) => {
      try {
        res.set('Content-Type', metrics.registry.contentType);
        res.end(await metrics.registry.metrics());
      } catch (err) {
        res.status(500).end(String(err));
      }
    });
  }

  if (db) {
    app.use('/v1/auth', createAuthRoutes(db));
    app.use('/v1', createPackageRoutes(db, dataDir, metrics));
    app.use('/v1', createPublishRoutes(db, dataDir, config || {}, metrics));
  }

  return app;
}

module.exports = { createApp };
