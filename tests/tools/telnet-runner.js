'use strict';

const fs = require('fs');
const path = require('path');
const { spawn, execSync } = require('child_process');
const net = require('net');

// ─── Scenario Parser ───────────────────────────────────────────────

function parseScenarioFile(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8');
  return parseScenarioContent(content);
}

function parseScenarioContent(content) {
  const lines = content.split('\n').map(l => l.trimEnd());
  const scenarios = [];
  let current = null;
  let section = null;
  let loginPlayer = null;

  for (const line of lines) {
    if (/^# [^#]/.test(line)) {
      continue;
    }

    const scenarioMatch = line.match(/^## Scenario:\s*(.+)/);
    if (scenarioMatch) {
      if (current) {
        scenarios.push(current);
      }
      current = {
        name: scenarioMatch[1].trim(),
        players: [],
        room: 'same',
        login: {},
        steps: [],
        skip: null
      };
      section = 'setup';
      continue;
    }

    if (/^## Setup/.test(line) && !current) {
      const titleMatch = content.match(/^# (.+)/m);
      current = {
        name: titleMatch ? titleMatch[1].trim() : 'unnamed',
        players: [],
        room: 'same',
        login: {},
        steps: [],
        skip: null
      };
      section = 'setup';
      continue;
    }

    if (!current) {
      continue;
    }

    if (/^## Login/.test(line)) {
      section = 'login';
      continue;
    }
    if (/^### Steps/.test(line) || /^## Steps/.test(line)) {
      section = 'steps';
      continue;
    }

    if (section === 'login') {
      const playerMatch = line.match(/^### (.+)/);
      if (playerMatch) {
        loginPlayer = playerMatch[1].trim();
        current.login[loginPlayer] = [];
        continue;
      }
      const loginStep = parseLoginStep(line);
      if (loginStep && loginPlayer) {
        current.login[loginPlayer].push(loginStep);
      }
      continue;
    }

    if (section === 'setup') {
      const playersMatch = line.match(/^- Players:\s*(.+)/);
      if (playersMatch) {
        current.players = playersMatch[1].split(',').map(p => p.trim());
        continue;
      }
      const roomMatch = line.match(/^- Room:\s*(.+)/);
      if (roomMatch) {
        current.room = roomMatch[1].trim().toLowerCase();
        continue;
      }
      const skipMatch = line.match(/^- Skip:\s*(.+)/);
      if (skipMatch) {
        current.skip = skipMatch[1].trim();
        continue;
      }
    }

    if (section === 'steps') {
      const step = parseStep(line);
      if (step) {
        current.steps.push(step);
      }
    }
  }

  if (current) {
    scenarios.push(current);
  }

  return scenarios;
}

function parseLoginStep(line) {
  const sendMatch = line.match(/^\d+\.\s*Send:\s*`(.+)`/);
  if (sendMatch) {
    return { type: 'send', text: sendMatch[1] };
  }
  const waitMatch = line.match(/^\d+\.\s*Wait for:\s*`(.+)`/);
  if (waitMatch) {
    return { type: 'wait', text: waitMatch[1] };
  }
  return null;
}

function parseStep(line) {
  const cmdMatch = line.match(/^\d+\.\s*(\w+):\s*`(.+)`/);
  if (cmdMatch) {
    return { type: 'command', player: cmdMatch[1], text: cmdMatch[2] };
  }
  const waitSeesMatch = line.match(/^\d+\.\s*Wait for (\w+) sees:\s*`(.+)`/);
  if (waitSeesMatch) {
    return { type: 'wait_for_sees', player: waitSeesMatch[1], text: waitSeesMatch[2] };
  }
  const seesOneOfMatch = line.match(/^\d+\.\s*Assert (\w+) sees one of:\s*(.+)/);
  if (seesOneOfMatch) {
    const texts = seesOneOfMatch[2].split(',').map(t => t.trim().replace(/^`|`$/g, ''));
    return { type: 'assert_sees_one_of', player: seesOneOfMatch[1], texts: texts };
  }
  const seesMatch = line.match(/^\d+\.\s*Assert (\w+) sees:\s*`(.+)`/);
  if (seesMatch) {
    return { type: 'assert_sees', player: seesMatch[1], text: seesMatch[2] };
  }
  const notSeesMatch = line.match(/^\d+\.\s*Assert (\w+) does not see:\s*`(.+)`/);
  if (notSeesMatch) {
    return { type: 'assert_not_sees', player: notSeesMatch[1], text: notSeesMatch[2] };
  }
  return null;
}

function parseDefaultLogin(defaultsDir) {
  const loginPath = path.join(defaultsDir, 'login.md');
  if (!fs.existsSync(loginPath)) {
    return [];
  }
  const content = fs.readFileSync(loginPath, 'utf-8');
  const steps = [];
  for (const line of content.split('\n')) {
    const step = parseLoginStep(line);
    if (step) {
      steps.push(step);
    }
  }
  return steps;
}

// ─── Telnet Client ─────────────────────────────────────────────────

class TelnetClient {
  constructor(name, port = 4000, host = 'localhost') {
    this.name = name;
    this.port = port;
    this.host = host;
    this.socket = null;
    this.buffer = '';
    this.connected = false;
    this._resolve = null;
  }

  connect() {
    return new Promise((resolve, reject) => {
      this.socket = new net.Socket();
      this.socket.setEncoding('utf-8');

      this.socket.on('data', (data) => {
        this.buffer += data;
        if (this._resolve) {
          const fn = this._resolve;
          this._resolve = null;
          fn();
        }
      });

      this.socket.on('error', (err) => {
        this.connected = false;
        reject(err);
      });

      this.socket.on('close', () => {
        this.connected = false;
      });

      this.socket.connect(this.port, this.host, () => {
        this.connected = true;
        resolve();
      });
    });
  }

  send(text) {
    if (!this.socket || !this.connected) {
      throw new Error(`${this.name}: not connected`);
    }
    this.socket.write(text + '\n');
  }

  waitFor(text, timeoutMs = 3000) {
    return new Promise((resolve, reject) => {
      let timer = null;
      let settled = false;

      const done = (fn, arg) => {
        if (settled) {
          return;
        }
        settled = true;
        if (timer) {
          clearTimeout(timer);
        }
        this._resolve = null;
        fn(arg);
      };

      const check = () => {
        if (this.buffer.toLowerCase().includes(text.toLowerCase())) {
          done(resolve, true);
          return;
        }
        this._resolve = check;
      };

      timer = setTimeout(() => {
        done(reject, new Error(
          `${this.name}: timeout waiting for "${text}" — buffer:\n${this.buffer.slice(-500)}`
        ));
      }, timeoutMs);

      check();
    });
  }

  clearBuffer() {
    this.buffer = '';
  }

  drain() {
    const content = this.buffer;
    this.buffer = '';
    return content;
  }

  waitForData(timeoutMs = 3000) {
    return new Promise((resolve, reject) => {
      if (this.buffer.length > 0) {
        resolve();
        return;
      }
      let timer = null;
      let settled = false;
      const done = (fn, arg) => {
        if (settled) {
          return;
        }
        settled = true;
        if (timer) {
          clearTimeout(timer);
        }
        this._resolve = null;
        fn(arg);
      };
      this._resolve = () => done(resolve);
      timer = setTimeout(() => {
        done(reject, new Error(`${this.name}: timeout waiting for any data`));
      }, timeoutMs);
    });
  }

  settle(ms = 500) {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  disconnect() {
    if (this.socket) {
      this.socket.destroy();
      this.connected = false;
    }
  }
}

// ─── Runner Core ───────────────────────────────────────────────────

function stripAnsi(text) {
  return text.replace(/\x1b\[[0-9;]*m/g, '');
}

async function runScenario(scenario, defaultLoginSteps, port, delay) {
  const result = {
    name: scenario.name,
    status: 'pass',
    steps: scenario.steps.length,
    failures: [],
    transcript: []
  };

  const clients = {};

  try {
    for (const playerName of scenario.players) {
      const client = new TelnetClient(playerName, port);
      await client.connect();
      clients[playerName] = client;
      result.transcript.push(`[${playerName} connected]`);
    }

    for (const playerName of scenario.players) {
      const client = clients[playerName];
      const loginSteps = scenario.login[playerName]
        || resolveDefaultLogin(defaultLoginSteps, playerName);

      for (const step of loginSteps) {
        if (step.type === 'wait') {
          await client.waitFor(step.text);
        } else if (step.type === 'send') {
          client.send(step.text);
          await client.settle(150);
        }
      }
      await client.waitForData();
      if (delay > 0) {
        await client.settle(delay);
      }

      // Reset position to spawn point between scenarios
      client.send('recall');
      await client.waitForData();
      await client.settle(delay > 0 ? delay : 150);

      client.clearBuffer();
    }

    if (scenario.room === 'different' && scenario.players.length > 1) {
      for (let i = 1; i < scenario.players.length; i++) {
        const client = clients[scenario.players[i]];
        client.send('north');
        await client.waitForData();
        if (delay > 0) {
          await client.settle(delay);
        }
        client.clearBuffer();
      }
      clients[scenario.players[0]].clearBuffer();
    }

    if (scenario.room && scenario.room !== 'same' && scenario.room !== 'different') {
      const adminClient = new TelnetClient('Admin', port);
      await adminClient.connect();
      const adminLoginSteps = resolveDefaultLogin(defaultLoginSteps, 'Admin');
      for (const step of adminLoginSteps) {
        if (step.type === 'wait') { await adminClient.waitFor(step.text); }
        else if (step.type === 'send') { adminClient.send(step.text); }
      }
      await adminClient.waitForData();
      for (const playerName of scenario.players) {
        adminClient.send(`teleport ${playerName} ${scenario.room}`);
        await adminClient.waitForData();
        await adminClient.settle(300);
      }
      adminClient.disconnect();
      await new Promise(r => setTimeout(r, 2000));
      for (const playerName of scenario.players) {
        clients[playerName].clearBuffer();
      }
    }

    for (let i = 0; i < scenario.steps.length; i++) {
      const step = scenario.steps[i];
      const client = clients[step.player];

      if (!client) {
        result.failures.push({
          step: i + 1,
          error: `Unknown player "${step.player}"`
        });
        result.status = 'fail';
        continue;
      }

      if (step.type === 'command') {
        client.clearBuffer();
        client.send(step.text);
        result.transcript.push(`> ${step.player}: ${step.text}`);
        await client.waitForData();
        await client.settle(delay > 0 ? delay : 150);
      } else if (step.type === 'assert_sees') {
        const buf = stripAnsi(client.buffer);
        if (buf.toLowerCase().includes(step.text.toLowerCase())) {
          result.transcript.push(`< ${step.player}: ✓ sees "${step.text}"`);
        } else {
          result.transcript.push(`< ${step.player}: ✗ expected to see "${step.text}" in "${buf.slice(-200).replace(/\n/g, '\\n')}"`);
          result.failures.push({
            step: i + 1,
            assertion: 'sees',
            player: step.player,
            expected: step.text,
            actual: buf.slice(-300)
          });
          result.status = 'fail';
        }
      } else if (step.type === 'wait_for_sees') {
        try {
          await client.waitFor(step.text, 30000);
          // settle so other clients can receive related messages
          await client.settle(delay > 0 ? delay : 500);
          const buf = stripAnsi(client.buffer);
          result.transcript.push(`< ${step.player}: ✓ waited and sees "${step.text}"`);
        } catch (err) {
          const buf = stripAnsi(client.buffer);
          result.transcript.push(`< ${step.player}: ✗ timed out waiting for "${step.text}" in "${buf.slice(-200).replace(/\n/g, '\\n')}"`);
          result.failures.push({
            step: i + 1,
            assertion: 'wait_for_sees',
            player: step.player,
            expected: step.text,
            actual: buf.slice(-300)
          });
          result.status = 'fail';
        }
      } else if (step.type === 'assert_sees_one_of') {
        const buf = stripAnsi(client.buffer);
        const found = step.texts.some(t => buf.toLowerCase().includes(t.toLowerCase()));
        if (found) {
          result.transcript.push(`< ${step.player}: ✓ sees one of: ${step.texts.map(t => '"' + t + '"').join(', ')}`);
        } else {
          result.transcript.push(`< ${step.player}: ✗ expected one of: ${step.texts.map(t => '"' + t + '"').join(', ')} in "${buf.slice(-200).replace(/\n/g, '\\n')}"`);
          result.failures.push({
            step: i + 1,
            assertion: 'sees one of',
            player: step.player,
            expected: step.texts.join(' | '),
            actual: buf.slice(-300)
          });
          result.status = 'fail';
        }
      } else if (step.type === 'assert_not_sees') {
        const buf = stripAnsi(client.buffer);
        if (!buf.toLowerCase().includes(step.text.toLowerCase())) {
          result.transcript.push(`< ${step.player}: ✓ does not see "${step.text}"`);
        } else {
          result.transcript.push(`< ${step.player}: ✗ unexpectedly sees "${step.text}" in "${buf.slice(-200).replace(/\n/g, '\\n')}"`);
          result.failures.push({
            step: i + 1,
            assertion: 'does not see',
            player: step.player,
            expected: step.text,
            actual: buf.slice(-300)
          });
          result.status = 'fail';
        }
      }
    }
  } catch (err) {
    result.status = 'error';
    const errMsg = err instanceof Error ? (err.message || err.stack) : String(err);
    result.failures.push({ step: 0, error: errMsg });
    result.transcript.push(`[ERROR: ${errMsg}]`);
  } finally {
    for (const [playerName, client] of Object.entries(clients)) {
      client.disconnect();
      result.transcript.push(`[${playerName} disconnected]`);
    }
  }

  return result;
}

function resolveDefaultLogin(defaultSteps, playerName) {
  return defaultSteps.map(step => ({
    type: step.type,
    text: step.text.replace(/\{PlayerName\}/g, playerName)
  }));
}

function cleanSaveFiles(projectRoot) {
  const savesDir = path.join(projectRoot, 'data', 'saves', 'players');
  if (fs.existsSync(savesDir)) {
    for (const f of fs.readdirSync(savesDir)) {
      if (f.endsWith('.yaml') || f.endsWith('.tmp') || f.endsWith('.bak')) {
        fs.unlinkSync(path.join(savesDir, f));
      }
    }
  }
}

async function restartServer(projectRoot, port) {
  await killExistingServer();
  cleanSaveFiles(projectRoot);
  const serverProcess = startServer(projectRoot, port);
  await waitForPort(port);
  return serverProcess;
}

async function runScenarioFile(filePath, defaultsDir, port, delay) {
  const scenarios = parseScenarioFile(filePath);
  const defaultLoginSteps = parseDefaultLogin(defaultsDir);
  const results = [];

  for (let i = 0; i < scenarios.length; i++) {
    const scenario = scenarios[i];
    if (scenario.skip != null) {
      results.push({ name: scenario.name, status: 'skip', skipReason: scenario.skip, failures: [], transcript: [] });
      continue;
    }
    if (i > 0) {
      await new Promise(r => setTimeout(r, 1000));
    }
    const result = await runScenario(scenario, defaultLoginSteps, port, delay);
    results.push(result);
  }

  return {
    file: path.relative(process.cwd(), filePath),
    scenarios: results
  };
}

// ─── Reporter ──────────────────────────────────────────────────────

function writeTranscript(fileResult, resultsDir) {
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
  const baseName = path.basename(fileResult.file, '.md');
  const transcriptPath = path.join(resultsDir, `${timestamp}-${baseName}.md`);

  let content = `# ${baseName} — ${new Date().toISOString().slice(0, 19).replace('T', ' ')}\n\n`;

  for (const scenario of fileResult.scenarios) {
    content += `## Scenario: ${scenario.name}\n`;
    content += `**Status:** ${scenario.status}\n\n`;
    for (const line of scenario.transcript) {
      content += `${line}\n`;
    }
    if (scenario.failures.length > 0) {
      content += `\n**Failures:**\n`;
      for (const f of scenario.failures) {
        if (f.error) {
          content += `- Step ${f.step}: ${f.error}\n`;
        } else {
          content += `- Step ${f.step}: ${f.player} — ${f.assertion} "${f.expected}"\n`;
        }
      }
    }
    content += '\n---\n\n';
  }

  fs.mkdirSync(resultsDir, { recursive: true });
  fs.writeFileSync(transcriptPath, content);
  return transcriptPath;
}

function printSummary(allResults) {
  let totalScenarios = 0;
  let totalPassed = 0;
  let totalFailed = 0;
  let totalSkipped = 0;

  for (const fileResult of allResults) {
    for (const scenario of fileResult.scenarios) {
      totalScenarios++;
      if (scenario.status === 'pass') {
        totalPassed++;
      } else if (scenario.status === 'skip') {
        totalSkipped++;
      } else {
        totalFailed++;
      }
    }
  }

  const parts = [`${totalPassed} passed`, `${totalFailed} failed`];
  if (totalSkipped > 0) { parts.push(`${totalSkipped} skipped`); }
  parts.push(`${totalScenarios} total`);

  console.log(`\n${'='.repeat(50)}`);
  console.log(`Results: ${parts.join(', ')}`);
  console.log(`${'='.repeat(50)}`);

  for (const fileResult of allResults) {
    for (const scenario of fileResult.scenarios) {
      if (scenario.status === 'fail') {
        console.log(`\n✗ ${fileResult.file} > ${scenario.name}`);
        for (const f of scenario.failures) {
          if (f.error) {
            console.log(`  Step ${f.step}: ${f.error}`);
          } else {
            console.log(`  Step ${f.step}: ${f.player} — expected ${f.assertion} "${f.expected}"`);
            if (f.actual != null) {
              const cleaned = f.actual.trim().replace(/\r\n/g, '\n').split('\n').filter(l => l.trim());
              const lastLines = cleaned.slice(-8);
              console.log(`  Received (last ${lastLines.length} lines):`);
              for (const line of lastLines) {
                console.log(`    | ${line}`);
              }
            }
          }
        }
      } else if (scenario.status === 'skip') {
        console.log(`\n- ${fileResult.file} > ${scenario.name}`);
        console.log(`  Skipped: ${scenario.skipReason}`);
      }
    }
  }
}

// ─── CLI ───────────────────────────────────────────────────────────

function findScenarioFiles(targetPath) {
  const stat = fs.statSync(targetPath);
  if (stat.isFile() && targetPath.endsWith('.md')) {
    return [targetPath];
  }
  if (stat.isDirectory()) {
    const files = [];
    const walk = (dir) => {
      for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        if (entry.name.startsWith('_') || entry.name.startsWith('.') || entry.name === 'results') {
          continue;
        }
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          walk(fullPath);
        } else if (entry.name.endsWith('.md')) {
          files.push(fullPath);
        }
      }
    };
    walk(targetPath);
    return files;
  }
  return [];
}

function discoverAllScenarioFiles(projectRoot) {
  const allFiles = [];
  const seen = new Set();

  const addDir = (dir) => {
    if (!fs.existsSync(dir)) { return; }
    for (const f of findScenarioFiles(dir)) {
      if (!seen.has(f)) {
        seen.add(f);
        allFiles.push(f);
      }
    }
  };

  addDir(path.join(projectRoot, 'tests', 'scenarios'));

  const packsDir = path.join(projectRoot, 'packs');
  if (fs.existsSync(packsDir)) {
    for (const packName of fs.readdirSync(packsDir)) {
      addDir(path.join(packsDir, packName, 'tests'));
    }
  }

  return allFiles;
}

function getArg(flag, defaultValue) {
  const idx = process.argv.indexOf(flag);
  if (idx === -1 || idx + 1 >= process.argv.length) {
    return defaultValue;
  }
  const parsed = parseInt(process.argv[idx + 1], 10);
  return Number.isNaN(parsed) ? defaultValue : parsed;
}

// ─── Server Lifecycle ──────────────────────────────────────────────

function findProjectRoot() {
  let dir = __dirname;
  while (dir !== path.dirname(dir)) {
    if (fs.existsSync(path.join(dir, 'src', 'Tapestry.Server'))) {
      return dir;
    }
    dir = path.dirname(dir);
  }
  return null;
}

function killExistingServer() {
  try {
    if (process.platform === 'win32') {
      execSync('taskkill /F /IM Tapestry.Server.exe', { stdio: 'ignore', windowsHide: true });
    } else {
      execSync('pkill -f Tapestry.Server', { stdio: 'ignore' });
    }
  } catch (_) {
    // no server running — that's fine
  }
  // wait for port to be released
  return new Promise(resolve => {
    const check = () => {
      const sock = new net.Socket();
      sock.once('connect', () => {
        // port still in use — wait and retry
        sock.destroy();
        setTimeout(check, 200);
      });
      sock.once('error', () => {
        // port free
        sock.destroy();
        resolve();
      });
      sock.connect(4000, 'localhost');
    };
    // give the OS a moment before first check
    setTimeout(check, 300);
  });
}

function startServer(projectRoot, port) {
  const serverProj = path.join(projectRoot, 'src', 'Tapestry.Server');
  const configPath = path.join(projectRoot, 'server.test.yaml');
  const child = spawn('dotnet', ['run', '--project', serverProj, '--no-build', '--', configPath], {
    cwd: projectRoot,
    stdio: 'ignore',
    windowsHide: true
  });
  child.unref();
  return child;
}

function waitForPort(port, timeoutMs = 45000) {
  const start = Date.now();
  return new Promise((resolve, reject) => {
    const attempt = () => {
      const sock = new net.Socket();
      sock.once('connect', () => {
        sock.destroy();
        resolve(Date.now() - start);
      });
      sock.once('error', () => {
        sock.destroy();
        if (Date.now() - start > timeoutMs) {
          reject(new Error(`Server did not start within ${timeoutMs}ms`));
        } else {
          setTimeout(attempt, 200);
        }
      });
      sock.connect(port, 'localhost');
    };
    attempt();
  });
}

function printHelp() {
  console.log('telnet-runner -- integration test runner for Tapestry scenarios');
  console.log('');
  console.log('Usage:');
  console.log('  node telnet-runner.js <file-or-dir>  [options]   Run a scenario file or directory');
  console.log('  node telnet-runner.js --all-packs    [options]   Run all scenarios across core + every pack');
  console.log('  node telnet-runner.js --self-test               Run parser self-tests (no server needed)');
  console.log('  node telnet-runner.js --connect-test [--port N]  Test raw telnet connectivity');
  console.log('  node telnet-runner.js --help                     Show this help');
  console.log('');
  console.log('Options:');
  console.log('  --port N     Telnet port to connect to (default: 4000)');
  console.log('  --delay N    Settle delay in ms between steps (default: 500)');
  console.log('  --managed    Build, start a fresh server before tests, kill it after each file');
  console.log('  --clean      Delete old result files from the results/ dir before running');
  console.log('  --all-packs  Discover scenarios from tests/scenarios/ plus packs/*/tests/');
  console.log('  --json       Print results as JSON to stdout instead of human-readable summary');
  console.log('');
  console.log('Output:');
  console.log('  Transcripts  tests/scenarios/results/<timestamp>-<name>.md  (one per scenario file)');
  console.log('  JSON summary tests/scenarios/results/<timestamp>-results.json  (machine-readable, diff-friendly)');
  console.log('');
  console.log('Examples:');
  console.log('  node telnet-runner.js --all-packs --managed --clean');
  console.log('  node telnet-runner.js tests/scenarios/commands/say.md --port 4000');
  console.log('  node telnet-runner.js tests/scenarios/ --delay 200');
}

async function main() {
  const args = process.argv.slice(2);
  const flagsWithValues = new Set(['--port', '--delay']);
  const flags = [];
  const positional = [];
  for (let i = 0; i < args.length; i++) {
    if (args[i].startsWith('--')) {
      flags.push(args[i]);
      if (flagsWithValues.has(args[i]) && i + 1 < args.length) {
        i++; // skip the value
      }
    } else {
      positional.push(args[i]);
    }
  }

  const allPacks = flags.includes('--all-packs');

  if (flags.includes('--help') || flags.includes('-h')) {
    printHelp();
    process.exit(0);
  }

  if (positional.length === 0 && !allPacks) {
    printHelp();
    process.exit(1);
  }

  const port = getArg('--port', 4000);
  const delay = getArg('--delay', 500);
  const jsonOnly = flags.includes('--json');
  const managed = flags.includes('--managed');
  const clean = flags.includes('--clean');

  let projectRoot = null;
  if (managed) {
    projectRoot = findProjectRoot();
    if (!projectRoot) {
      console.error('Cannot find Tapestry.Server project. Run from within the repo.');
      process.exit(1);
    }
    // Build once up front so per-file restarts use --no-build
    console.log('Building server...');
    execSync(`dotnet build "${path.join(projectRoot, 'src', 'Tapestry.Server')}" -v q`, { stdio: 'inherit' });
  }

  const root = projectRoot || findProjectRoot() || process.cwd();
  const targets = allPacks ? [] : positional.map(p => path.resolve(p));

  const firstTarget = targets.length > 0 ? targets[0] : null;
  const scenariosBase = path.join(root, 'tests', 'scenarios');
  let defaultsDir = path.join(scenariosBase, '_defaults');
  if (!fs.existsSync(defaultsDir)) {
    if (firstTarget) {
      const scenariosIdx = firstTarget.indexOf(path.join('tests', 'scenarios'));
      if (scenariosIdx !== -1) {
        defaultsDir = path.join(firstTarget.slice(0, scenariosIdx), 'tests', 'scenarios', '_defaults');
      } else {
        let dir = path.dirname(firstTarget);
        while (dir !== path.dirname(dir)) {
          const candidate = path.join(dir, 'tests', 'scenarios', '_defaults');
          if (fs.existsSync(candidate)) {
            defaultsDir = candidate;
            break;
          }
          dir = path.dirname(dir);
        }
      }
    }
  }

  if (!defaultsDir || !fs.existsSync(defaultsDir)) {
    console.error('Warning: Could not find _defaults directory. Using empty login sequence.');
    defaultsDir = '';
  }

  const resultsDir = defaultsDir
    ? path.join(path.dirname(defaultsDir), 'results')
    : path.join(firstTarget ? path.dirname(firstTarget) : root, 'results');

  if (clean && fs.existsSync(resultsDir)) {
    const old = fs.readdirSync(resultsDir).filter(f => f.endsWith('.md'));
    for (const f of old) {
      fs.unlinkSync(path.join(resultsDir, f));
    }
    if (old.length > 0) {
      console.log(`Cleaned ${old.length} old result file(s) from ${resultsDir}`);
    }
  }

  const files = allPacks
    ? discoverAllScenarioFiles(root)
    : targets.flatMap(t => findScenarioFiles(t));
  if (files.length === 0) {
    console.error('No scenario files found at:', targets.join(', '));
    process.exit(1);
  }

  console.log(`Running ${files.length} scenario file(s) against localhost:${port}...\n`);

  const allResults = [];
  for (let fi = 0; fi < files.length; fi++) {
    let serverProcess = null;
    if (managed) {
      try {
        serverProcess = await restartServer(projectRoot, port);
      } catch (err) {
        console.error(`Failed to start server for file ${fi + 1}: ${err.message}`);
        process.exit(1);
      }
    } else if (fi > 0) {
      await new Promise(r => setTimeout(r, 1000));
    }

    const file = files[fi];
    if (!jsonOnly) {
      console.log(`▶ ${path.relative(process.cwd(), file)}`);
    }
    const fileResult = await runScenarioFile(file, defaultsDir || '', port, delay);
    allResults.push(fileResult);

    if (resultsDir) {
      const transcriptPath = writeTranscript(fileResult, resultsDir);
      if (!jsonOnly) {
        console.log(`  Transcript: ${path.relative(process.cwd(), transcriptPath)}`);
      }
    }

    if (serverProcess) {
      if (process.platform === 'win32') {
        try {
          execSync(`taskkill /F /T /PID ${serverProcess.pid}`, { stdio: 'ignore', windowsHide: true });
        } catch (_) {}
      } else {
        serverProcess.kill();
      }
    }
  }

  if (jsonOnly) {
    console.log(JSON.stringify(allResults, null, 2));
  } else {
    printSummary(allResults);
  }

  // Always write results.json for diffing between runs
  if (resultsDir) {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    const jsonPath = path.join(resultsDir, `${timestamp}-results.json`);
    const summary = allResults.flatMap(r =>
      r.scenarios.map(s => ({
        file: r.file,
        scenario: s.name,
        status: s.status,
        failures: s.failures.map(f => ({
          step: f.step,
          player: f.player,
          assertion: f.assertion,
          expected: f.expected,
          error: f.error
        }))
      }))
    );
    fs.mkdirSync(resultsDir, { recursive: true });
    fs.writeFileSync(jsonPath, JSON.stringify(summary, null, 2));
  }

  if (managed) {
    await killExistingServer();
  }

  const anyFailed = allResults.some(r =>
    r.scenarios.some(s => s.status === 'fail')
  );
  process.exit(anyFailed ? 1 : 0);
}

// ─── Self-Test ─────────────────────────────────────────────────────

function selfTest() {
  let passed = 0;
  let failed = 0;

  function assert(name, condition) {
    if (condition) {
      console.log(`  ✓ ${name}`);
      passed++;
    } else {
      console.log(`  ✗ ${name}`);
      failed++;
    }
  }

  console.log('Parser self-tests:');

  const commandScenario = `# say

## Scenario: Basic room message
- Players: Alice, Bob
- Room: same

### Steps
1. Alice: \`say Hello Bob!\`
2. Assert Alice sees: \`You say "Hello Bob!"\`
3. Assert Bob sees: \`Alice says "Hello Bob!"\`

## Scenario: Empty message
- Players: Alice

### Steps
1. Alice: \`say\`
2. Assert Alice sees: \`Say what?\`
`;

  const scenarios = parseScenarioContent(commandScenario);
  assert('parses two scenarios', scenarios.length === 2);
  assert('first scenario name', scenarios[0].name === 'Basic room message');
  assert('first scenario players', scenarios[0].players.length === 2);
  assert('first scenario room', scenarios[0].room === 'same');
  assert('first scenario steps', scenarios[0].steps.length === 3);
  assert('first step is command', scenarios[0].steps[0].type === 'command');
  assert('first step player', scenarios[0].steps[0].player === 'Alice');
  assert('first step text', scenarios[0].steps[0].text === 'say Hello Bob!');
  assert('second step is assert_sees', scenarios[0].steps[1].type === 'assert_sees');
  assert('second scenario single player', scenarios[1].players.length === 1);

  const negScenario = `# test

## Scenario: Negative
- Players: Alice, Bob
- Room: different

### Steps
1. Alice: \`say Hello?\`
2. Assert Bob does not see: \`Alice says\`
`;

  const negResult = parseScenarioContent(negScenario);
  assert('parses negative assertion', negResult[0].steps[1].type === 'assert_not_sees');
  assert('room is different', negResult[0].room === 'different');

  const smokeScenario = `# New Player Journey

## Setup
- Players: Wanderer

## Steps
1. Wanderer: \`look\`
2. Assert Wanderer sees: \`Town Square\`
`;

  const smokeResult = parseScenarioContent(smokeScenario);
  assert('parses smoke test', smokeResult.length === 1);
  assert('smoke test name from title', smokeResult[0].name === 'New Player Journey');
  assert('smoke test player', smokeResult[0].players[0] === 'Wanderer');

  const loginScenario = `# login test

## Scenario: Custom login
- Players: Alice

## Login
### Alice
1. Wait for: \`Enter your name:\`
2. Send: \`Alice\`
3. Wait for: \`Password:\`
4. Send: \`hunter2\`

### Steps
1. Alice: \`look\`
`;

  const loginResult = parseScenarioContent(loginScenario);
  assert('parses login override', Object.keys(loginResult[0].login).length === 1);
  assert('login has 4 steps', loginResult[0].login['Alice'].length === 4);
  assert('first login step is wait', loginResult[0].login['Alice'][0].type === 'wait');
  assert('second login step is send', loginResult[0].login['Alice'][1].type === 'send');

  const sendStep = parseLoginStep('1. Send: `Alice`');
  assert('parseLoginStep send', sendStep.type === 'send' && sendStep.text === 'Alice');
  const waitStep = parseLoginStep('2. Wait for: `Welcome`');
  assert('parseLoginStep wait', waitStep.type === 'wait' && waitStep.text === 'Welcome');
  const badStep = parseLoginStep('just some text');
  assert('parseLoginStep bad input', badStep === null);

  console.log(`\n${passed} passed, ${failed} failed`);
  return failed === 0;
}

// ─── Entry Point ───────────────────────────────────────────────────

if (process.argv.includes('--self-test')) {
  const ok = selfTest();
  process.exit(ok ? 0 : 1);
} else if (process.argv.includes('--connect-test')) {
  (async () => {
    const port = getArg('--port', 4000);
    console.log(`Connecting to localhost:${port}...`);
    const client = new TelnetClient('TestPlayer', port);
    try {
      await client.connect();
      console.log('✓ Connected');
      await client.settle(1000);
      console.log('Buffer after connect:', JSON.stringify(client.buffer.slice(0, 200)));
      client.send('TestPlayer');
      await client.settle(1000);
      console.log('Buffer after login:', JSON.stringify(client.buffer.slice(0, 500)));
      client.send('quit');
      await client.settle(500);
      console.log('Buffer after quit:', JSON.stringify(client.buffer.slice(0, 200)));
      client.disconnect();
      console.log('✓ Disconnected cleanly');
    } catch (err) {
      console.error('✗ Error:', err.message);
      process.exit(1);
    }
  })();
} else {
  main().catch(err => {
    console.error('Fatal error:', err.message);
    process.exit(1);
  });
}
