const fs = require('fs');
const path = require('path');
const { initDb } = require('./db');
const { loadConfig } = require('./config');
const { createApp } = require('./server');
const { createMetrics, initMetricsFromDb } = require('./metrics');

const DATA_DIR = process.env.DATA_DIR || '/var/tapestry-registry/data';
const DB_PATH = process.env.DB_PATH || path.join(DATA_DIR, 'registry.db');
const CONFIG_PATH = process.env.CONFIG_PATH || '/var/tapestry-registry/registry-config.yaml';
const PORT = parseInt(process.env.PORT || '3002', 10);

fs.mkdirSync(DATA_DIR, { recursive: true });

const db = initDb(DB_PATH);
const config = loadConfig(CONFIG_PATH);
const metrics = createMetrics();
initMetricsFromDb(db, metrics);

const app = createApp({ db, dataDir: DATA_DIR, config, metrics });

app.listen(PORT, () => {
  console.log(JSON.stringify({ level: 'info', msg: `registry listening on :${PORT}`, data_dir: DATA_DIR }));
});
