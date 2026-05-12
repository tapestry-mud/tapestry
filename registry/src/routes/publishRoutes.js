const express = require('express');
const multer = require('multer');
const path = require('path');
const fs = require('fs');
const { getLimitsForScope } = require('../config');
const { requireAuth } = require('../auth');
const { computeIntegrity } = require('../integrity');

const upload = multer({
  storage: multer.memoryStorage(),
  limits: { fileSize: 50 * 1024 * 1024 }, // 50MB hard ceiling; scope limits enforced separately
});

const REQUIRED_MANIFEST_FIELDS = ['name', 'version', 'description', 'type', 'author', 'license', 'engine', 'tag_validation'];
const SEMVER_RE = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([\da-zA-Z-]+(?:\.[\da-zA-Z-]+)*))?(?:\+([\da-zA-Z-]+(?:\.[\da-zA-Z-]+)*))?$/;

function parseScopedName(packageName) {
  const match = packageName.match(/^@([^/]+)\/(.+)$/);
  if (!match) {
    throw new Error(`invalid package name: ${packageName} (must be @scope/name)`);
  }
  return { scope: match[1], name: match[2] };
}

function checkPublishLimits(db, config, scope, packageName, tarballSize) {
  const limits = getLimitsForScope(config, scope);
  if (!limits) {
    return null; // bypassed
  }

  const maxTarballBytes = limits.max_tarball_mb * 1024 * 1024;
  if (tarballSize > maxTarballBytes) {
    return { error: `tarball exceeds ${limits.max_tarball_mb}MB limit (got ${(tarballSize / 1024 / 1024).toFixed(2)}MB)` };
  }

  const rawScope = scope.startsWith('@') ? scope.slice(1) : scope;

  // Check scope storage unconditionally (new packages can also push scope over limit)
  const scopeStorageBytes = db.prepare(`
    SELECT COALESCE(SUM(v.tarball_size), 0) as total
    FROM versions v
    JOIN packages p ON p.id = v.package_id
    WHERE p.scope = ?
  `).get(rawScope).total;
  if (scopeStorageBytes + tarballSize > limits.max_scope_mb * 1024 * 1024) {
    return { error: `storage limit of ${limits.max_scope_mb}MB exceeded for scope @${rawScope}` };
  }

  const pkg = db.prepare(`SELECT id FROM packages WHERE scope = ? AND name = ?`).get(rawScope, packageName);
  if (pkg) {
    const versionCount = db.prepare(`SELECT COUNT(*) as c FROM versions WHERE package_id = ?`).get(pkg.id).c;
    if (versionCount >= limits.max_versions) {
      return { error: `version limit of ${limits.max_versions} reached for @${rawScope}/${packageName}` };
    }
  }

  return null;
}

function createPublishRoutes(db, dataDir, config, metrics) {
  const router = express.Router();

  router.post('/publish', requireAuth, upload.single('tarball'), (req, res) => {
    if (!req.file) {
      return res.status(400).json({ error: 'tarball file required' });
    }
    if (!req.body.metadata) {
      return res.status(400).json({ error: 'metadata field required' });
    }

    let manifest;
    try {
      manifest = JSON.parse(req.body.metadata);
    } catch {
      return res.status(400).json({ error: 'metadata must be valid JSON' });
    }

    for (const field of REQUIRED_MANIFEST_FIELDS) {
      if (!manifest[field]) {
        return res.status(400).json({ error: `manifest missing required field: ${field}` });
      }
    }

    if (!SEMVER_RE.test(manifest.version)) {
      return res.status(400).json({ error: `invalid version: ${manifest.version} (must be valid semver)` });
    }

    let scope, name;
    try {
      ({ scope, name } = parseScopedName(manifest.name));
    } catch (err) {
      return res.status(400).json({ error: err.message });
    }

    // Scope ownership: user's handle must match scope, or user is admin
    const account = db.prepare(`SELECT is_admin FROM accounts WHERE handle = ?`).get(req.user.handle);
    if (scope !== req.user.handle && !account?.is_admin) {
      return res.status(403).json({ error: `scope @${scope} is not owned by @${req.user.handle}` });
    }

    const tarball = req.file.buffer;
    const limitError = checkPublishLimits(db, config, `@${scope}`, name, tarball.length);
    if (limitError) {
      return res.status(400).json(limitError);
    }

    const integrity = computeIntegrity(tarball);

    const tgzDir = path.join(dataDir, 'packages', `@${scope}`, name);
    const tgzPath = path.join(tgzDir, `${manifest.version}.tgz`);
    const tmpPath = path.join(tgzDir, `${manifest.version}.tgz.tmp`);

    // DB first, then write tarball — no orphans on crash
    db.prepare(`INSERT OR IGNORE INTO packages (scope, name, owner_handle) VALUES (?, ?, ?)`).run(scope, name, req.user.handle);
    const pkg = db.prepare(`SELECT id FROM packages WHERE scope = ? AND name = ?`).get(scope, name);

    try {
      db.prepare(`
        INSERT INTO versions (package_id, version, manifest, tarball_path, tarball_size, integrity)
        VALUES (?, ?, ?, ?, ?, ?)
      `).run(pkg.id, manifest.version, JSON.stringify(manifest), tgzPath, tarball.length, integrity);
    } catch (err) {
      if (err.message.includes('UNIQUE constraint failed')) {
        return res.status(409).json({ error: `version ${manifest.version} already exists` });
      }
      return res.status(500).json({ error: 'publish failed' });
    }

    // DB succeeded — write tarball to temp, then rename for atomicity
    fs.mkdirSync(tgzDir, { recursive: true });
    fs.writeFileSync(tmpPath, tarball);
    fs.renameSync(tmpPath, tgzPath);

    if (metrics) {
      metrics.publishes.inc({ scope: `@${scope}`, name });
    }

    res.status(201).json({ name: manifest.name, version: manifest.version, integrity });
  });

  return router;
}

module.exports = { createPublishRoutes, checkPublishLimits };
