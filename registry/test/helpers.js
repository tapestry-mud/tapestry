const os = require('os');
const fs = require('fs');
const path = require('path');
const bcrypt = require('bcryptjs');
const { initDb } = require('../src/db');
const { createApp } = require('../src/server');
const { loadConfig } = require('../src/config');

function createTestApp({ config = loadConfig('/nonexistent'), metrics = null } = {}) {
  const db = initDb(':memory:');
  const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-test-'));
  const app = createApp({ db, dataDir, config, metrics });
  return { app, db, dataDir };
}

function cleanupTestApp({ db, dataDir }) {
  db.close();
  fs.rmSync(dataDir, { recursive: true, force: true });
}

function seedAccount(db, { handle = 'testuser', email = 'test@example.com' } = {}) {
  const hash = bcrypt.hashSync('password', 1);
  db.prepare(`INSERT INTO accounts (handle, email, password_hash) VALUES (?, ?, ?)`).run(handle, email, hash);
  return { handle, email, password: 'password' };
}

function seedPackage(db, {
  scope = 'testscope',
  name = 'testpkg',
  version = '1.0.0',
  ownerHandle = 'testuser',
  description = 'A test package',
  keywords = ['test'],
} = {}) {
  const pkg = db.prepare(`INSERT OR IGNORE INTO packages (scope, name, owner_handle) VALUES (?, ?, ?)`).run(scope, name, ownerHandle);
  const packageId = pkg.lastInsertRowid ||
    db.prepare(`SELECT id FROM packages WHERE scope = ? AND name = ?`).get(scope, name).id;

  const manifest = JSON.stringify({
    name: `@${scope}/${name}`,
    version,
    description,
    meta: { keywords },
  });

  db.prepare(`
    INSERT INTO versions (package_id, version, manifest, tarball_path, tarball_size, integrity)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run(packageId, version, manifest, `/packages/@${scope}/${name}/${version}.tgz`, 1024, `sha256-testhash`);

  return { scope, name, version, packageId };
}

module.exports = { createTestApp, cleanupTestApp, seedAccount, seedPackage };
