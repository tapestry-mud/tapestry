'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const yaml = require('js-yaml');

const RC_PATH = path.join(os.homedir(), '.tapestryrc');

function loadToken() {
  if (!fs.existsSync(RC_PATH)) {
    return null;
  }
  try {
    const data = yaml.load(fs.readFileSync(RC_PATH, 'utf8'));
    return data?.token ?? null;
  } catch {
    return null;
  }
}

function saveToken(token) {
  fs.writeFileSync(RC_PATH, yaml.dump({ token }, { lineWidth: -1 }), { mode: 0o600 });
}

function requireToken() {
  const token = loadToken();
  if (!token) {
    throw new Error('Not logged in. Run: tapestry login');
  }
  return token;
}

module.exports = { RC_PATH, loadToken, saveToken, requireToken };
