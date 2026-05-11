const Database = require('better-sqlite3');

function initDb(dbPath = ':memory:') {
  const db = new Database(dbPath);
  db.pragma('journal_mode = WAL');
  db.pragma('foreign_keys = ON');

  db.exec(`
    CREATE TABLE IF NOT EXISTS accounts (
      id            INTEGER PRIMARY KEY AUTOINCREMENT,
      handle        TEXT    UNIQUE NOT NULL,
      email         TEXT    UNIQUE NOT NULL,
      password_hash TEXT    NOT NULL,
      is_admin      INTEGER NOT NULL DEFAULT 0,
      created_at    TEXT    NOT NULL DEFAULT (datetime('now'))
    );

    CREATE TABLE IF NOT EXISTS packages (
      id           INTEGER PRIMARY KEY AUTOINCREMENT,
      scope        TEXT    NOT NULL,
      name         TEXT    NOT NULL,
      owner_handle TEXT    NOT NULL,
      created_at   TEXT    NOT NULL DEFAULT (datetime('now')),
      UNIQUE(scope, name)
    );

    CREATE TABLE IF NOT EXISTS versions (
      id           INTEGER PRIMARY KEY AUTOINCREMENT,
      package_id   INTEGER NOT NULL REFERENCES packages(id) ON DELETE CASCADE,
      version      TEXT    NOT NULL,
      manifest     TEXT    NOT NULL,
      tarball_path TEXT    NOT NULL,
      tarball_size INTEGER NOT NULL,
      integrity    TEXT    NOT NULL,
      downloads    INTEGER NOT NULL DEFAULT 0,
      published_at TEXT    NOT NULL DEFAULT (datetime('now')),
      UNIQUE(package_id, version)
    );
  `);

  return db;
}

module.exports = { initDb };
