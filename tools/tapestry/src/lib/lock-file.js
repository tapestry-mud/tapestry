'use strict';

const fs = require('fs');
const path = require('path');
const { readYaml, writeYaml } = require('../util/yaml');

const LOCK_FILE = 'tapestry-lock.yaml';

function readLock(cwd) {
  const lockPath = path.join(cwd, LOCK_FILE);
  if (!fs.existsSync(lockPath)) {
    return null;
  }
  return readYaml(lockPath);
}

function writeLock(cwd, lock) {
  writeYaml(path.join(cwd, LOCK_FILE), lock);
}

module.exports = { readLock, writeLock };
