const client = require('prom-client');

function createMetrics() {
  const registry = new client.Registry();
  client.collectDefaultMetrics({ register: registry });

  const downloads = new client.Counter({
    name: 'tapestry_downloads_total',
    help: 'Total package tarball downloads',
    labelNames: ['scope', 'name', 'version'],
    registers: [registry],
  });

  const publishes = new client.Counter({
    name: 'tapestry_publishes_total',
    help: 'Total package versions published',
    labelNames: ['scope', 'name'],
    registers: [registry],
  });

  const storageBytes = new client.Gauge({
    name: 'tapestry_storage_bytes',
    help: 'Total tarball storage in bytes',
    registers: [registry],
  });

  const activePackages = new client.Gauge({
    name: 'tapestry_active_packages',
    help: 'Total number of distinct packages in registry',
    registers: [registry],
  });

  return { registry, downloads, publishes, storageBytes, activePackages };
}

function initMetricsFromDb(db, metrics) {
  const storageRow = db.prepare(`SELECT COALESCE(SUM(tarball_size), 0) as total FROM versions`).get();
  metrics.storageBytes.set(storageRow.total);

  const pkgRow = db.prepare(`SELECT COUNT(*) as c FROM packages`).get();
  metrics.activePackages.set(pkgRow.c);
}

module.exports = { createMetrics, initMetricsFromDb };
