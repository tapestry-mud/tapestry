const express = require('express');
const { getLimitsForScope } = require('../config');

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
  const rawName = packageName;
  const pkg = db.prepare(`SELECT id FROM packages WHERE scope = ? AND name = ?`).get(rawScope, rawName);
  if (pkg) {
    const versionCount = db.prepare(`SELECT COUNT(*) as c FROM versions WHERE package_id = ?`).get(pkg.id).c;
    if (versionCount >= limits.max_versions) {
      return { error: `version limit of ${limits.max_versions} reached for @${rawScope}/${rawName}` };
    }

    const scopeStorageBytes = db.prepare(`
      SELECT COALESCE(SUM(v.tarball_size), 0) as total
      FROM versions v
      JOIN packages p ON p.id = v.package_id
      WHERE p.scope = ?
    `).get(rawScope).total;
    const maxScopeBytes = limits.max_scope_mb * 1024 * 1024;
    if (scopeStorageBytes + tarballSize > maxScopeBytes) {
      return { error: `storage limit of ${limits.max_scope_mb}MB exceeded for scope @${rawScope}` };
    }
  }

  return null;
}

function createPublishRoutes(db, dataDir, config, metrics) {
  const router = express.Router();
  // POST /publish added in Task 10
  return router;
}

module.exports = { createPublishRoutes, checkPublishLimits };
