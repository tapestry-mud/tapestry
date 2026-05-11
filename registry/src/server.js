const express = require('express');
const pinoHttp = require('pino-http');

function createApp({ db, dataDir, config, metrics }) {
  const app = express();
  app.use(express.json());

  if (process.env.NODE_ENV !== 'test') {
    app.use(pinoHttp());
  }

  app.get('/health', (req, res) => {
    res.json({ status: 'ok' });
  });

  return app;
}

module.exports = { createApp };
