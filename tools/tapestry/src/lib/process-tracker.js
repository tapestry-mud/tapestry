'use strict';

const fs = require('fs');
const path = require('path');

const PID_FILE = '.tapestry.pid';

function writePid(cwd, pid) {
  fs.writeFileSync(path.join(cwd, PID_FILE), String(pid), 'utf8');
}

function readPid(cwd) {
  const pidFile = path.join(cwd, PID_FILE);
  if (!fs.existsSync(pidFile)) {
    return null;
  }
  const pid = parseInt(fs.readFileSync(pidFile, 'utf8').trim(), 10);
  return isNaN(pid) || pid <= 0 ? null : pid;
}

function clearPid(cwd) {
  const pidFile = path.join(cwd, PID_FILE);
  if (fs.existsSync(pidFile)) {
    fs.unlinkSync(pidFile);
  }
}

module.exports = { writePid, readPid, clearPid };
