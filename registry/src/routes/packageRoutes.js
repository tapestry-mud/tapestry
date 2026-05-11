const express = require('express');
const path = require('path');
const fs = require('fs');

function createPackageRoutes(db, dataDir, metrics) {
  const router = express.Router();

  router.get('/index.json', (req, res) => {
    const rows = db.prepare(`
      SELECT p.scope, p.name, p.owner_handle,
             v.version, v.manifest, v.integrity, v.tarball_size, v.published_at
      FROM packages p
      JOIN versions v ON v.package_id = p.id
      ORDER BY p.scope, p.name, v.published_at DESC
    `).all();

    const packages = {};
    for (const row of rows) {
      const key = `@${row.scope}/${row.name}`;
      let manifest;
      try {
        manifest = JSON.parse(row.manifest);
      } catch (_) {
        manifest = {};
      }
      if (!packages[key]) {
        packages[key] = {
          latest: row.version,
          versions: [],
          description: manifest.description || '',
          keywords: manifest.meta?.keywords || [],
          integrity: {},
        };
      }
      packages[key].versions.push(row.version);
      packages[key].integrity[row.version] = row.integrity;
    }

    res.json({ updated: new Date().toISOString(), packages });
  });

  router.get('/packages/@:scope/:name', (req, res) => {
    const { scope, name } = req.params;
    const pkg = db.prepare(`SELECT * FROM packages WHERE scope = ? AND name = ?`).get(scope, name);
    if (!pkg) {
      return res.status(404).json({ error: 'package not found' });
    }
    const versions = db.prepare(`
      SELECT version, manifest, integrity, tarball_size, downloads, published_at
      FROM versions WHERE package_id = ?
      ORDER BY published_at DESC
    `).all(pkg.id);

    res.json({
      name: `@${scope}/${name}`,
      owner: pkg.owner_handle,
      versions: versions.map(v => {
        let manifest;
        try {
          manifest = JSON.parse(v.manifest);
        } catch (_) {
          manifest = null;
        }
        return { ...v, manifest };
      }),
    });
  });

  return router;
}

module.exports = { createPackageRoutes };
