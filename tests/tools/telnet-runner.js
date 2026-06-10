'use strict';

const fs = require('fs');
const path = require('path');
const { spawn, execSync } = require('child_process');
const net = require('net');
const os = require('os');

// ─── Tunables ──────────────────────────────────────────────────────
// Every wait in the runner is event-driven with a bounded timeout: fast when
// green, bounded when red. There are no unconditional sleeps in the step path.

const LOGIN_WAIT_MS = 15000;     // per login "Wait for:" step (CI cold start)
const SYNC_WAIT_MS = 10000;      // sentinel echo barrier after each command
const ASSERT_WAIT_MS = 2000;     // positive "Assert sees" grace window
const GMCP_WAIT_MS = 5000;       // GMCP packet arrival window
const WAIT_FOR_SEES_MS = 30000;  // explicit "Wait for ... sees" steps
const BOOT_TIMEOUT_MS = 60000;   // managed server boot (build excluded)
const BANNER_PROBE_MS = 10000;   // login banner probe before any scenario
const DEFAULT_SCENARIO_TIMEOUT_S = 120;
const DEFAULT_SUITE_TIMEOUT_S = 600;

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
  const gmcpWithMatch = line.match(/^\d+\.\s*Assert (\w+) receives GMCP:\s*`(.+?)`\s+with\s+(.+)/);
  if (gmcpWithMatch) {
    const fields = {};
    for (const pair of gmcpWithMatch[3].split(',')) {
      const [k, v] = pair.split('=').map(s => s.trim().replace(/^"|"$/g, ''));
      if (k && v !== undefined) {
        fields[k] = v;
      }
    }
    return { type: 'assert_gmcp_field', player: gmcpWithMatch[1], package: gmcpWithMatch[2], fields };
  }
  const gmcpMatch = line.match(/^\d+\.\s*Assert (\w+) receives GMCP:\s*`(.+)`/);
  if (gmcpMatch) {
    return { type: 'assert_gmcp', player: gmcpMatch[1], package: gmcpMatch[2] };
  }
  const gmcpOrderMatch = line.match(/^\d+\.\s*Assert `(.+?)` packet index is less than `(.+?)` packet index/);
  if (gmcpOrderMatch) {
    return { type: 'assert_gmcp_order', first: gmcpOrderMatch[1], second: gmcpOrderMatch[2] };
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

// ─── Text Normalization ────────────────────────────────────────────
// All matching happens on normalized text: ANSI stripped, CR removed (the
// server emits CRLF; Linux vs Windows must behave identically — tapestry #90),
// case-insensitive contains.

function stripAnsi(text) {
  return text.replace(/\x1b\[[0-9;]*m/g, '');
}

function normalize(text) {
  return stripAnsi(text).replace(/\r/g, '');
}

function containsText(haystack, needle) {
  return normalize(haystack).toLowerCase().includes(normalize(needle).toLowerCase());
}

// AggregateError (e.g. dual-stack connect failures) has an empty .message;
// always render something diagnosable.
function describeError(err) {
  if (err && err.errors && Array.isArray(err.errors)) {
    return err.errors.map(e => e.message || String(e)).join('; ') || String(err);
  }
  return (err && err.message) || String(err);
}

// Sentinel sync lines (`help __sync_N__` echoes / responses) are runner
// plumbing — strip them out of any buffer used for assertions or transcripts
// so a scenario can never accidentally match against them.
function filterSentinels(text) {
  return text.split('\n').filter(l => !l.includes(SYNC_PREFIX)).join('\n');
}

// ─── Telnet Constants ──────────────────────────────────────────────

const IAC  = 0xFF;
const SB   = 0xFA;
const SE   = 0xF0;
const WILL = 0xFB;
const WONT = 0xFC;
const DO   = 0xFD;
const DONT = 0xFE;

const OPT_ECHO  = 1;
const OPT_GMCP  = 201;

// ─── Sync Sentinel ─────────────────────────────────────────────────
// The engine processes each session's input FIFO on the game-loop tick, and
// `help <unknown>` responds "No help found for '<term>'." — so a unique
// sentinel help lookup queued AFTER a command is a completion barrier: when
// the sentinel echo arrives, every line the command produced (including room
// broadcasts queued to OTHER sessions during that tick) has already been
// written to the respective sockets.

const SYNC_PREFIX = '__sync_';
let syncCounter = 0;

function nextSyncToken() {
  syncCounter++;
  return `${SYNC_PREFIX}${syncCounter}__`;
}

// ─── Telnet Client ─────────────────────────────────────────────────

class TelnetClient {
  // 127.0.0.1, not localhost: dual-stack resolution makes Node race ::1 and
  // 127.0.0.1 and surface failures as AggregateError with an empty message.
  constructor(name, port = 4000, host = '127.0.0.1') {
    this.name = name;
    this.port = port;
    this.host = host;
    this.socket = null;
    this.buffer = '';
    this.connected = false;
    this._resolve = null;
    this._rawBuf = Buffer.alloc(0);
    this.gmcpPackets = [];
    this.gmcpEnabled = false;
    // True when no output can be in flight for this client (post-sentinel).
    // Cleared whenever any client sends a command (its tick may broadcast
    // output to us).
    this.synced = false;
  }

  connect() {
    return new Promise((resolve, reject) => {
      this.socket = new net.Socket();

      this.socket.on('data', (chunk) => {
        this._rawBuf = Buffer.concat([this._rawBuf, chunk]);
        this._parseIac();
        if (this._gmcpNegotiated && !this._gmcpSupportsSent) {
          this._gmcpSupportsSent = true;
          process.nextTick(() => this._sendGmcpSupports());
        }
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
        // Wake any pending waiter so it can fail fast instead of timing out.
        if (this._resolve) {
          const fn = this._resolve;
          this._resolve = null;
          fn();
        }
      });

      this.socket.connect(this.port, this.host, () => {
        this.connected = true;
        resolve();
      });
    });
  }

  _parseIac() {
    let text = '';
    let i = 0;
    const buf = this._rawBuf;

    while (i < buf.length) {
      if (buf[i] !== IAC) {
        text += String.fromCharCode(buf[i]);
        i++;
        continue;
      }

      if (i + 1 >= buf.length) {
        break;
      }

      const cmd = buf[i + 1];

      if (cmd === IAC) {
        text += String.fromCharCode(0xFF);
        i += 2;
        continue;
      }

      if (cmd === WILL || cmd === WONT || cmd === DO || cmd === DONT) {
        if (i + 2 >= buf.length) {
          break;
        }
        const opt = buf[i + 2];
        this._handleNegotiation(cmd, opt);
        i += 3;
        continue;
      }

      if (cmd === SB) {
        const seIdx = this._findSubnegEnd(buf, i + 2);
        if (seIdx === -1) {
          break;
        }
        const subData = buf.slice(i + 2, seIdx);
        this._handleSubnegotiation(subData);
        i = seIdx + 2;
        continue;
      }

      i += 2;
    }

    this._rawBuf = buf.slice(i);
    if (text.length > 0) {
      this.buffer += text;
    }
  }

  _findSubnegEnd(buf, start) {
    for (let j = start; j < buf.length - 1; j++) {
      if (buf[j] === IAC && buf[j + 1] === SE) {
        return j;
      }
    }
    return -1;
  }

  _handleNegotiation(cmd, opt) {
    if (cmd === WILL && opt === OPT_GMCP) {
      this.socket.write(Buffer.from([IAC, DO, OPT_GMCP]));
      this.gmcpEnabled = true;
      this._gmcpNegotiated = true;
    } else if (cmd === WILL && opt === OPT_ECHO) {
      this.socket.write(Buffer.from([IAC, DO, OPT_ECHO]));
    } else if (cmd === WILL) {
      this.socket.write(Buffer.from([IAC, DONT, opt]));
    } else if (cmd === DO) {
      this.socket.write(Buffer.from([IAC, WONT, opt]));
    }
  }

  _sendGmcpSupports() {
    const packages = [
      'Char 1', 'Char.Login 1', 'Room 1', 'World 1',
      'Comm 1', 'Response 1', 'Core 1'
    ];
    const payload = 'Core.Supports.Set ' + JSON.stringify(packages);
    const payloadBytes = Buffer.from(payload, 'utf-8');
    const frame = Buffer.alloc(payloadBytes.length + 5);
    frame[0] = IAC;
    frame[1] = SB;
    frame[2] = OPT_GMCP;
    payloadBytes.copy(frame, 3);
    frame[payloadBytes.length + 3] = IAC;
    frame[payloadBytes.length + 4] = SE;
    this.socket.write(frame);
  }

  _handleSubnegotiation(data) {
    if (data.length === 0) {
      return;
    }
    const opt = data[0];
    if (opt === OPT_GMCP) {
      const text = data.slice(1).toString('utf-8').trim();
      const spaceIdx = text.indexOf(' ');
      const pkg = spaceIdx < 0 ? text : text.slice(0, spaceIdx);
      let payload = null;
      if (spaceIdx >= 0) {
        try {
          payload = JSON.parse(text.slice(spaceIdx + 1));
        } catch (_) {
          payload = text.slice(spaceIdx + 1);
        }
      }
      this.gmcpPackets.push({ package: pkg, data: payload });
    }
  }

  send(text) {
    if (!this.socket || !this.connected) {
      throw new Error(`${this.name}: not connected`);
    }
    this.socket.write(text + '\n');
  }

  // The assertion-safe view of everything this client has seen since the last
  // clearBuffer(): ANSI-stripped, CR-stripped, sentinel plumbing removed.
  view() {
    return filterSentinels(normalize(this.buffer));
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
        if (containsText(this.buffer, text)) {
          done(resolve, true);
          return;
        }
        if (!this.connected) {
          done(reject, new Error(
            `${this.name}: connection closed while waiting for "${text}" — buffer:\n${normalize(this.buffer).slice(-500)}`
          ));
          return;
        }
        this._resolve = check;
      };

      timer = setTimeout(() => {
        done(reject, new Error(
          `${this.name}: timeout (${timeoutMs}ms) waiting for "${text}" — buffer:\n${normalize(this.buffer).slice(-500)}`
        ));
      }, timeoutMs);

      check();
    });
  }

  // Completion barrier: queue a sentinel help lookup and wait for its echo.
  // When this returns, all output triggered by this client's earlier commands
  // has been delivered (to this client AND broadcast to others' sockets).
  // A connection closed by the scenario itself (e.g. `quit`) counts as synced.
  async sync(timeoutMs = SYNC_WAIT_MS) {
    if (!this.connected) {
      this.synced = true;
      return;
    }
    const token = nextSyncToken();
    try {
      this.send(`help ${token}`);
      await this.waitFor(`No help found for '${token}'`, timeoutMs);
    } catch (err) {
      if (!this.connected) {
        // Closed mid-sync (quit/server shutdown): whatever was sent before the
        // close is already in the buffer; that's as synced as it gets.
        this.synced = true;
        return;
      }
      throw err;
    }
    this.synced = true;
  }

  // Poll for a GMCP packet matching `predicate` — bounded, event-paced.
  waitForGmcp(predicate, timeoutMs = GMCP_WAIT_MS) {
    return new Promise((resolve) => {
      const deadline = Date.now() + timeoutMs;
      const poll = () => {
        if (this.gmcpPackets.some(predicate)) {
          resolve(true);
          return;
        }
        if (Date.now() >= deadline || !this.connected) {
          resolve(false);
          return;
        }
        setTimeout(poll, 50);
      };
      poll();
    });
  }

  clearBuffer() {
    this.buffer = '';
  }

  clearGmcpPackets() {
    this.gmcpPackets = [];
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

class ScenarioTimeoutError extends Error {}

function makeDeadline(seconds) {
  const end = Date.now() + seconds * 1000;
  return {
    check(label) {
      if (Date.now() > end) {
        throw new ScenarioTimeoutError(
          `scenario exceeded ${seconds}s wall-clock cap (at: ${label})`
        );
      }
    },
    remainingMs() {
      return Math.max(0, end - Date.now());
    },
    clamp(timeoutMs) {
      return Math.max(1, Math.min(timeoutMs, this.remainingMs()));
    }
  };
}

async function runScenario(scenario, defaultLoginSteps, opts) {
  const { port, delay, adminPlayer, scenarioTimeoutS } = opts;
  const result = {
    name: scenario.name,
    status: 'pass',
    steps: scenario.steps.length,
    failures: [],
    transcript: []
  };

  const clients = {};
  const deadline = makeDeadline(scenarioTimeoutS);

  // Mark every client un-synced: `actor` just ran a command whose tick may
  // have broadcast output to anyone; re-sync before trusting their buffers.
  const markAllUnsynced = () => {
    for (const c of Object.values(clients)) {
      c.synced = false;
    }
  };

  // Barrier for assertions: guarantee no in-flight output for this player.
  const ensureSynced = async (client) => {
    if (!client.synced) {
      await client.sync(deadline.clamp(SYNC_WAIT_MS));
    }
  };

  try {
    for (const playerName of scenario.players) {
      deadline.check(`connect ${playerName}`);
      const client = new TelnetClient(playerName, port);
      await client.connect();
      clients[playerName] = client;
      result.transcript.push(`[${playerName} connected]`);
    }

    for (const playerName of scenario.players) {
      deadline.check(`login ${playerName}`);
      const client = clients[playerName];
      const loginSteps = scenario.login[playerName]
        || resolveDefaultLogin(defaultLoginSteps, playerName);

      for (const step of loginSteps) {
        if (step.type === 'wait') {
          await client.waitFor(step.text, deadline.clamp(LOGIN_WAIT_MS));
        } else if (step.type === 'send') {
          client.send(step.text);
        }
      }

      // "Welcome" arrives while the login flow still owns the session; input
      // sent before handoff is consumed by the flow, not the command router.
      // The first prompt flush ("[HP]: ...") only happens once the session is
      // actually playing — wait for it before issuing any command.
      await client.waitFor('[HP]:', deadline.clamp(LOGIN_WAIT_MS));

      // Reset position to spawn point, then barrier + clean slate.
      client.send('recall');
      await client.sync(deadline.clamp(SYNC_WAIT_MS));
      client.clearBuffer();
    }

    if (scenario.room === 'different' && scenario.players.length > 1) {
      for (let i = 1; i < scenario.players.length; i++) {
        deadline.check(`room placement ${scenario.players[i]}`);
        const client = clients[scenario.players[i]];
        client.send('north');
        await client.sync(deadline.clamp(SYNC_WAIT_MS));
        client.clearBuffer();
      }
      // The mover's departure was broadcast to player 1's room — flush it.
      await clients[scenario.players[0]].sync(deadline.clamp(SYNC_WAIT_MS));
      clients[scenario.players[0]].clearBuffer();
    }

    if (scenario.room && scenario.room !== 'same' && scenario.room !== 'different') {
      deadline.check('admin room placement');
      const adminClient = new TelnetClient(adminPlayer, port);
      await adminClient.connect();
      const adminLoginSteps = resolveDefaultLogin(defaultLoginSteps, adminPlayer);
      for (const step of adminLoginSteps) {
        if (step.type === 'wait') {
          await adminClient.waitFor(step.text, deadline.clamp(LOGIN_WAIT_MS));
        } else if (step.type === 'send') {
          adminClient.send(step.text);
        }
      }
      await adminClient.waitFor('[HP]:', deadline.clamp(LOGIN_WAIT_MS));
      for (const playerName of scenario.players) {
        adminClient.send(`teleport ${playerName} ${scenario.room}`);
      }
      await adminClient.sync(deadline.clamp(SYNC_WAIT_MS));
      adminClient.disconnect();
      for (const playerName of scenario.players) {
        const c = clients[playerName];
        await c.sync(deadline.clamp(SYNC_WAIT_MS));
        c.clearBuffer();
      }
    }

    for (let i = 0; i < scenario.steps.length; i++) {
      const step = scenario.steps[i];
      deadline.check(`step ${i + 1}`);
      const client = step.player ? clients[step.player] : null;

      if (!client && step.type !== 'assert_gmcp_order') {
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
        markAllUnsynced();
        await client.sync(deadline.clamp(SYNC_WAIT_MS));
        if (delay > 0) {
          await client.settle(delay);
        }
      } else if (step.type === 'assert_sees') {
        // Positive assert: bounded wait, instant when already present.
        let seen = client.view().toLowerCase().includes(normalize(step.text).toLowerCase());
        if (!seen) {
          try {
            await client.waitFor(step.text, deadline.clamp(ASSERT_WAIT_MS));
            seen = true;
          } catch (_) {
            seen = client.view().toLowerCase().includes(normalize(step.text).toLowerCase());
          }
        }
        if (seen) {
          result.transcript.push(`< ${step.player}: ✓ sees "${step.text}"`);
        } else {
          const buf = client.view();
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
          await client.waitFor(step.text, deadline.clamp(WAIT_FOR_SEES_MS));
          result.transcript.push(`< ${step.player}: ✓ waited and sees "${step.text}"`);
        } catch (err) {
          const buf = client.view();
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
        await ensureSynced(client);
        const buf = client.view();
        const found = step.texts.some(t => buf.toLowerCase().includes(normalize(t).toLowerCase()));
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
        // Negative assert: barrier first — only meaningful on a quiesced buffer.
        await ensureSynced(client);
        const buf = client.view();
        if (!buf.toLowerCase().includes(normalize(step.text).toLowerCase())) {
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
      } else if (step.type === 'assert_gmcp') {
        const found = await client.waitForGmcp(
          p => p.package.toLowerCase() === step.package.toLowerCase(),
          deadline.clamp(GMCP_WAIT_MS)
        );
        if (found) {
          result.transcript.push(`< ${step.player}: ✓ received GMCP "${step.package}"`);
        } else {
          const received = client.gmcpPackets.map(p => p.package).join(', ') || '(none)';
          result.transcript.push(`< ${step.player}: ✗ expected GMCP "${step.package}" — received: ${received}`);
          result.failures.push({
            step: i + 1,
            assertion: 'receives GMCP',
            player: step.player,
            expected: step.package,
            actual: received
          });
          result.status = 'fail';
        }
      } else if (step.type === 'assert_gmcp_field') {
        await client.waitForGmcp(
          p => p.package.toLowerCase() === step.package.toLowerCase(),
          deadline.clamp(GMCP_WAIT_MS)
        );
        const pkt = client.gmcpPackets.find(p =>
          p.package.toLowerCase() === step.package.toLowerCase()
        );
        if (!pkt) {
          const received = client.gmcpPackets.map(p => p.package).join(', ') || '(none)';
          result.transcript.push(`< ${step.player}: ✗ expected GMCP "${step.package}" — received: ${received}`);
          result.failures.push({
            step: i + 1,
            assertion: 'receives GMCP with fields',
            player: step.player,
            expected: step.package,
            actual: received
          });
          result.status = 'fail';
        } else {
          let allMatch = true;
          for (const [k, v] of Object.entries(step.fields)) {
            const actual = pkt.data && typeof pkt.data === 'object' ? String(pkt.data[k] ?? '') : '';
            if (actual.toLowerCase() !== v.toLowerCase()) {
              result.transcript.push(`< ${step.player}: ✗ GMCP "${step.package}" field ${k}: expected "${v}", got "${actual}"`);
              result.failures.push({
                step: i + 1,
                assertion: `GMCP ${step.package} field ${k}`,
                player: step.player,
                expected: v,
                actual: actual
              });
              result.status = 'fail';
              allMatch = false;
            }
          }
          if (allMatch) {
            result.transcript.push(`< ${step.player}: ✓ received GMCP "${step.package}" with matching fields`);
          }
        }
      } else if (step.type === 'assert_gmcp_order') {
        const firstClient = Object.values(clients)[0];
        if (!firstClient) {
          result.failures.push({ step: i + 1, error: 'No client for GMCP order check' });
          result.status = 'fail';
        } else {
          // Make sure both packets have had a chance to arrive before judging order.
          await firstClient.waitForGmcp(
            p => p.package.toLowerCase() === step.second.toLowerCase(),
            deadline.clamp(GMCP_WAIT_MS)
          );
          const firstIdx = firstClient.gmcpPackets.findIndex(p =>
            p.package.toLowerCase() === step.first.toLowerCase()
          );
          const secondIdx = firstClient.gmcpPackets.findIndex(p =>
            p.package.toLowerCase() === step.second.toLowerCase()
          );
          if (firstIdx === -1 || secondIdx === -1) {
            const received = firstClient.gmcpPackets.map(p => p.package).join(', ') || '(none)';
            result.transcript.push(`< ✗ GMCP order: missing packet(s) — received: ${received}`);
            result.failures.push({
              step: i + 1,
              assertion: 'GMCP order',
              expected: `${step.first} before ${step.second}`,
              actual: received
            });
            result.status = 'fail';
          } else if (firstIdx < secondIdx) {
            result.transcript.push(`< ✓ GMCP order: "${step.first}" (${firstIdx}) before "${step.second}" (${secondIdx})`);
          } else {
            result.transcript.push(`< ✗ GMCP order: "${step.first}" (${firstIdx}) NOT before "${step.second}" (${secondIdx})`);
            result.failures.push({
              step: i + 1,
              assertion: 'GMCP order',
              expected: `${step.first} before ${step.second}`,
              actual: `${step.first} at ${firstIdx}, ${step.second} at ${secondIdx}`
            });
            result.status = 'fail';
          }
        }
      }
    }
  } catch (err) {
    result.status = 'error';
    const errMsg = err instanceof Error ? (err.message || err.stack) : String(err);
    result.failures.push({ step: 0, error: errMsg });
    result.transcript.push(`[ERROR: ${errMsg}]`);
    // Dump every player's buffer — an error is exactly when you need them.
    for (const [playerName, client] of Object.entries(clients)) {
      const tail = client.view().slice(-1000);
      result.transcript.push(`[${playerName} buffer tail]\n${tail}`);
    }
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

async function runScenarioFile(filePath, defaultsDir, opts) {
  const scenarios = parseScenarioFile(filePath);
  const defaultLoginSteps = parseDefaultLogin(defaultsDir);
  const results = [];

  for (const scenario of scenarios) {
    if (scenario.skip != null) {
      results.push({ name: scenario.name, status: 'skip', skipReason: scenario.skip, failures: [], transcript: [] });
      continue;
    }
    const result = await runScenario(scenario, defaultLoginSteps, opts);
    results.push(result);
  }

  return {
    file: path.relative(process.cwd(), filePath),
    scenarios: results
  };
}

// ─── Server Lifecycle (managed mode) ───────────────────────────────

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

// Locate the built server dll for the given configuration. Running the dll
// directly (instead of `dotnet run`) gives us a single child process we own:
// no wrapper tree to orphan, one PID to kill.
function findServerDll(projectRoot, configuration) {
  const binDir = path.join(projectRoot, 'src', 'Tapestry.Server', 'bin', configuration);
  if (!fs.existsSync(binDir)) {
    return null;
  }
  const tfms = fs.readdirSync(binDir).filter(d => d.startsWith('net'));
  for (const tfm of tfms.sort().reverse()) {
    const dll = path.join(binDir, tfm, 'Tapestry.Server.dll');
    if (fs.existsSync(dll)) {
      return dll;
    }
  }
  return null;
}

function findFreePorts(count) {
  return new Promise((resolve, reject) => {
    const servers = [];
    const ports = [];
    const next = () => {
      if (ports.length === count) {
        for (const s of servers) {
          s.close();
        }
        resolve(ports);
        return;
      }
      const srv = net.createServer();
      srv.once('error', reject);
      srv.listen(0, '127.0.0.1', () => {
        servers.push(srv);
        ports.push(srv.address().port);
        next();
      });
    };
    next();
  });
}

// Isolated, ephemeral config for a managed run: a temp dir holding a copy of
// server.test.yaml with the ports rewritten to free ones. The server resolves
// the relative save_path (./data/saves) against the config's own directory,
// so persistence lands inside tmpDir — every run boots a virgin save store on
// a port nothing else is using. The caller removes tmpDir when the run ends.
function rewriteConfigPorts(yaml, telnetPort, websocketPort) {
  return yaml
    .replace(/^(\s*telnet_port:\s*)\d+/m, `$1${telnetPort}`)
    .replace(/^(\s*websocket_port:\s*)\d+/m, `$1${websocketPort}`);
}

function countConfigPacks(yaml) {
  const lines = yaml.split('\n');
  let inPacks = false;
  let count = 0;
  for (const line of lines) {
    if (/^packs:\s*$/.test(line)) {
      inPacks = true;
      continue;
    }
    if (inPacks) {
      if (/^\s+-\s+\S/.test(line)) {
        count++;
      } else if (line.trim() !== '' && !line.trim().startsWith('#')) {
        break;
      }
    }
  }
  return count;
}

function createManagedConfig(projectRoot, telnetPort, websocketPort) {
  const baseConfig = path.join(projectRoot, 'server.test.yaml');
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-test-'));
  const yaml = fs.readFileSync(baseConfig, 'utf-8');
  const rewritten = rewriteConfigPorts(yaml, telnetPort, websocketPort);
  const configPath = path.join(tmpDir, 'server.test.yaml');
  fs.writeFileSync(configPath, rewritten);
  return { tmpDir, configPath, expectedPacks: countConfigPacks(yaml) };
}

// Link (junction on Windows — works without elevation; symlink elsewhere) with
// a copy fallback, so staging works on locked-down filesystems too.
function linkOrCopyDir(target, linkPath) {
  try {
    fs.symlinkSync(target, linkPath, process.platform === 'win32' ? 'junction' : 'dir');
  } catch (_) {
    fs.cpSync(target, linkPath, { recursive: true });
  }
}

// Stage a merged pack corpus into tmpDir: per-pack links to the real corpus,
// then the engine-repo scenario fixtures overlaid. The fixtures pack
// (@tapestry/test-fixtures — seed accounts, test arena) deliberately lives
// under tests/ in the engine repo so it can NEVER be published or packaged;
// merging at run time is what lets the managed server still load it.
function stageCorpus(tmpDir, realPacksDir, fixturesDir) {
  const corpusDir = path.join(tmpDir, 'packs');
  fs.mkdirSync(corpusDir, { recursive: true });

  const overlay = (sourceRoot) => {
    if (!sourceRoot || !fs.existsSync(sourceRoot)) {
      return;
    }
    for (const entry of fs.readdirSync(sourceRoot, { withFileTypes: true })) {
      if (!entry.isDirectory()) {
        continue;
      }
      const sourcePath = path.join(sourceRoot, entry.name);
      if (entry.name.startsWith('@')) {
        const scopeDir = path.join(corpusDir, entry.name);
        fs.mkdirSync(scopeDir, { recursive: true });
        for (const child of fs.readdirSync(sourcePath, { withFileTypes: true })) {
          if (!child.isDirectory()) {
            continue;
          }
          const dest = path.join(scopeDir, child.name);
          if (!fs.existsSync(dest)) {
            linkOrCopyDir(path.join(sourcePath, child.name), dest);
          }
        }
      } else {
        const dest = path.join(corpusDir, entry.name);
        if (!fs.existsSync(dest)) {
          linkOrCopyDir(sourcePath, dest);
        }
      }
    }
  };

  overlay(realPacksDir);
  overlay(fixturesDir);
  return corpusDir;
}

class ManagedServer {
  constructor(projectRoot, configuration, packsDir) {
    this.projectRoot = projectRoot;
    this.configuration = configuration;
    this.packsDir = packsDir;
    this.child = null;
    this.exited = false;
    this.exitCode = null;
    this.logPath = null;
    this.config = null;
    this.port = null;
  }

  async start() {
    const dll = findServerDll(this.projectRoot, this.configuration);
    if (!dll) {
      throw new Error(
        `Tapestry.Server.dll not found for configuration "${this.configuration}". Build first (dotnet build src/Tapestry.Server -c ${this.configuration}).`
      );
    }

    const [telnetPort, websocketPort] = await findFreePorts(2);
    this.port = telnetPort;
    this.config = createManagedConfig(this.projectRoot, telnetPort, websocketPort);
    this.logPath = path.join(this.config.tmpDir, 'server.log');

    // Merged corpus: the real packs plus the never-published scenario fixtures.
    const fixturesDir = path.join(this.projectRoot, 'tests', 'fixtures', 'scenario-packs');
    const stagedPacksDir = stageCorpus(this.config.tmpDir, this.packsDir, fixturesDir);
    this.stagedPacksDir = stagedPacksDir;

    const logFd = fs.openSync(this.logPath, 'a');
    this.child = spawn('dotnet', [dll, '--config', this.config.configPath, '--packs', stagedPacksDir], {
      cwd: this.projectRoot,
      stdio: ['ignore', logFd, logFd],
      windowsHide: true
    });
    fs.closeSync(logFd);

    this.child.on('exit', (code) => {
      this.exited = true;
      this.exitCode = code;
    });

    await this._waitForBoot();
  }

  readLog() {
    try {
      return fs.readFileSync(this.logPath, 'utf-8');
    } catch (_) {
      return '';
    }
  }

  logTail(lines = 25) {
    const all = this.readLog().split('\n').filter(l => l.trim());
    return all.slice(-lines).join('\n');
  }

  // Boot gate: the server must (a) stay alive, (b) log one "Loaded pack:" per
  // configured pack — an empty/missing packs dir is a SILENT no-op in the
  // engine (ContentLoadingModule), so a world that failed to load would
  // otherwise boot "fine" and hang every scenario — and (c) start the game
  // loop and accept a TCP connection that shows the login banner.
  async _waitForBoot() {
    const start = Date.now();
    const expected = Math.max(1, this.config.expectedPacks);

    while (true) {
      if (this.exited) {
        throw new Error(
          `Server exited during boot (code ${this.exitCode}).\n--- server log tail ---\n${this.logTail()}`
        );
      }
      if (Date.now() - start > BOOT_TIMEOUT_MS) {
        throw new Error(
          `Server did not finish booting within ${BOOT_TIMEOUT_MS}ms.\n--- server log tail ---\n${this.logTail()}`
        );
      }

      const log = this.readLog();
      const loadedPacks = (log.match(/Loaded pack:/g) || []).length;
      const loopStarted = log.includes('Game loop starting');
      // The telnet listener binds AFTER the game loop starts — probing before
      // this line appears races the bind and sees ECONNREFUSED.
      const listening = log.includes(`Telnet server listening on port ${this.port}`);

      if (loopStarted && listening) {
        if (loadedPacks < expected) {
          throw new Error(
            `World is empty or incomplete: game loop started with ${loadedPacks}/${expected} configured packs loaded. ` +
            `Check --packs (${this.packsDir}).\n--- server log tail ---\n${this.logTail()}`
          );
        }
        break;
      }
      if (loopStarted && !listening && loadedPacks < expected) {
        // Don't wait the full boot timeout to report a bad world.
        throw new Error(
          `World is empty or incomplete: game loop started with ${loadedPacks}/${expected} configured packs loaded. ` +
          `Check --packs (${this.packsDir}).\n--- server log tail ---\n${this.logTail()}`
        );
      }
      await new Promise(r => setTimeout(r, 200));
    }

    // Banner probe: TCP up is not enough — prove the login flow answers.
    const probe = new TelnetClient('BootProbe', this.port);
    try {
      await probe.connect();
      await probe.waitFor('Speak your name', BANNER_PROBE_MS);
    } catch (err) {
      throw new Error(
        `Server booted but the login banner never arrived: ${describeError(err)}\n--- server log tail ---\n${this.logTail()}`
      );
    } finally {
      probe.disconnect();
    }

    console.log(`Managed server up on port ${this.port} (${this.config.expectedPacks} packs, save store: ${this.config.tmpDir})`);
  }

  async stop() {
    if (this.child && !this.exited) {
      this.child.kill();
      // Bounded wait for exit; force-kill is the fallback, never a hang.
      const start = Date.now();
      while (!this.exited && Date.now() - start < 5000) {
        await new Promise(r => setTimeout(r, 100));
      }
      if (!this.exited) {
        try {
          this.child.kill('SIGKILL');
        } catch (_) { /* already gone */ }
      }
    }
  }

  cleanup(keepLogs) {
    if (this.config && fs.existsSync(this.config.tmpDir)) {
      if (keepLogs) {
        console.error(`Server log kept at: ${this.logPath}`);
        return;
      }
      fs.rmSync(this.config.tmpDir, { recursive: true, force: true });
    }
  }
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
  let totalErrored = 0;
  let totalSkipped = 0;

  for (const fileResult of allResults) {
    for (const scenario of fileResult.scenarios) {
      totalScenarios++;
      if (scenario.status === 'pass') {
        totalPassed++;
      } else if (scenario.status === 'skip') {
        totalSkipped++;
      } else if (scenario.status === 'error') {
        totalErrored++;
      } else {
        totalFailed++;
      }
    }
  }

  const parts = [`${totalPassed} passed`, `${totalFailed} failed`];
  if (totalErrored > 0) { parts.push(`${totalErrored} errored`); }
  if (totalSkipped > 0) { parts.push(`${totalSkipped} skipped`); }
  parts.push(`${totalScenarios} total`);

  console.log(`\n${'='.repeat(50)}`);
  console.log(`Results: ${parts.join(', ')}`);
  console.log(`${'='.repeat(50)}`);

  for (const fileResult of allResults) {
    for (const scenario of fileResult.scenarios) {
      if (scenario.status === 'fail' || scenario.status === 'error') {
        console.log(`\n✗ ${fileResult.file} > ${scenario.name} [${scenario.status}]`);
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

function writeResultsJson(allResults, resultsDir) {
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
  return jsonPath;
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

function discoverAllScenarioFiles(projectRoot, packsDir) {
  const allFiles = [];
  const seen = new Set();

  const addDir = (dir) => {
    if (!fs.existsSync(dir)) { return; }
    for (const f of findScenarioFiles(dir)) {
      // Dedup on real path: locally the packs dir is often a link into the
      // packs repo, so the same scenario is reachable via two paths.
      let key = f;
      try {
        key = fs.realpathSync(f);
      } catch (_) { /* fall back to the literal path */ }
      if (!seen.has(key)) {
        seen.add(key);
        allFiles.push(f);
      }
    }
  };

  addDir(path.join(projectRoot, 'tests', 'scenarios'));

  // Pack-owned scenarios: <pack>/tests/ inside the packs corpus, including
  // scoped layouts (@tapestry/<pack>/tests/). Honors --packs, so CI finds
  // them in the cloned corpus too.
  const addPackTests = (dir) => {
    if (!fs.existsSync(dir)) { return; }
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (!entry.isDirectory()) { continue; }
      const entryPath = path.join(dir, entry.name);
      if (entry.name.startsWith('@')) {
        for (const sub of fs.readdirSync(entryPath, { withFileTypes: true })) {
          if (sub.isDirectory()) {
            addDir(path.join(entryPath, sub.name, 'tests'));
          }
        }
      } else {
        addDir(path.join(entryPath, 'tests'));
      }
    }
  };

  addPackTests(path.join(projectRoot, 'packs'));
  if (packsDir && path.resolve(packsDir) !== path.resolve(path.join(projectRoot, 'packs'))) {
    addPackTests(packsDir);
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

function getStringArg(flag, defaultValue) {
  const idx = process.argv.indexOf(flag);
  if (idx === -1 || idx + 1 >= process.argv.length) {
    return defaultValue;
  }
  return process.argv[idx + 1];
}

function printHelp() {
  console.log('telnet-runner -- integration test runner for Tapestry scenarios');
  console.log('');
  console.log('Usage:');
  console.log('  node telnet-runner.js <file-or-dir>  [options]   Run a scenario file or directory');
  console.log('  node telnet-runner.js --all-packs    [options]   Run all scenarios across core + every pack');
  console.log('  node telnet-runner.js --self-test                Run parser/normalizer self-tests (no server)');
  console.log('  node telnet-runner.js --connect-test [--port N]  Test raw telnet connectivity');
  console.log('  node telnet-runner.js --help                     Show this help');
  console.log('');
  console.log('Options:');
  console.log('  --managed               Boot a fresh server on a free port with an isolated,');
  console.log('                          ephemeral save store; verify the world actually loaded');
  console.log('                          (pack count + game loop + login banner) before running.');
  console.log('  --configuration C      Build configuration for --managed (Debug|Release, default Debug).');
  console.log('                          The server must already be built; the runner does not build.');
  console.log('  --packs DIR            Packs directory for --managed (default: <repo>/packs).');
  console.log('  --port N               Telnet port for non-managed runs (default: 4000).');
  console.log('  --admin-player NAME    Seeded admin used for room placement (default: Gamemaster).');
  console.log('  --scenario-timeout S   Per-scenario wall-clock cap in seconds (default: ' + DEFAULT_SCENARIO_TIMEOUT_S + ').');
  console.log('  --suite-timeout S      Whole-run hard cap in seconds (default: ' + DEFAULT_SUITE_TIMEOUT_S + '). On expiry the');
  console.log('                          runner dumps what it has, kills the server, exits 1. It cannot hang.');
  console.log('  --delay N              EXTRA settle ms after each command (default 0; sync is');
  console.log('                          deterministic via a sentinel barrier, no sleeps needed).');
  console.log('  --all-packs            Discover scenarios from tests/scenarios/ plus packs/*/tests/.');
  console.log('  --clean                Delete old result files from the results/ dir before running.');
  console.log('  --json                 Print results as JSON to stdout instead of human-readable summary.');
  console.log('');
  console.log('Output:');
  console.log('  Transcripts  tests/scenarios/results/<timestamp>-<name>.md  (one per scenario file)');
  console.log('  JSON summary tests/scenarios/results/<timestamp>-results.json');
  console.log('');
  console.log('Exit code: 0 only when every scenario passed. Failures, errors, timeouts,');
  console.log('and boot problems all exit nonzero.');
  console.log('');
  console.log('Examples:');
  console.log('  dotnet build src/Tapestry.Server -v q && node tests/tools/telnet-runner.js --all-packs --managed --clean');
  console.log('  node tests/tools/telnet-runner.js tests/scenarios/smoke/new-player.md --managed');
  console.log('  node tests/tools/telnet-runner.js tests/scenarios/ --port 4000');
}

async function main() {
  const args = process.argv.slice(2);
  const flagsWithValues = new Set([
    '--port', '--delay', '--configuration', '--packs', '--admin-player',
    '--scenario-timeout', '--suite-timeout'
  ]);
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

  const delay = getArg('--delay', 0);
  const jsonOnly = flags.includes('--json');
  const managed = flags.includes('--managed');
  const clean = flags.includes('--clean');
  const configuration = getStringArg('--configuration', 'Debug');
  const adminPlayer = getStringArg('--admin-player', 'Gamemaster');
  const scenarioTimeoutS = getArg('--scenario-timeout', DEFAULT_SCENARIO_TIMEOUT_S);
  const suiteTimeoutS = getArg('--suite-timeout', DEFAULT_SUITE_TIMEOUT_S);

  const projectRoot = findProjectRoot();
  if (managed && !projectRoot) {
    console.error('Cannot find Tapestry.Server project. Run from within the repo.');
    process.exit(1);
  }
  const root = projectRoot || process.cwd();
  const packsDir = path.resolve(getStringArg('--packs', path.join(root, 'packs')));

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
    const old = fs.readdirSync(resultsDir).filter(f => f.endsWith('.md') || f.endsWith('.json'));
    for (const f of old) {
      fs.unlinkSync(path.join(resultsDir, f));
    }
    if (old.length > 0) {
      console.log(`Cleaned ${old.length} old result file(s) from ${resultsDir}`);
    }
  }

  const files = allPacks
    ? discoverAllScenarioFiles(root, packsDir)
    : targets.flatMap(t => findScenarioFiles(t));
  if (files.length === 0) {
    console.error('No scenario files found at:', targets.join(', '));
    process.exit(1);
  }

  // ── Suite watchdog: this process CANNOT outlive the cap. ──
  const allResults = [];
  let server = null;
  const watchdog = setTimeout(async () => {
    console.error(`\nSUITE TIMEOUT: exceeded ${suiteTimeoutS}s — aborting.`);
    try {
      if (allResults.length > 0 && resultsDir) {
        writeResultsJson(allResults, resultsDir);
      }
      printSummary(allResults);
      if (server) {
        console.error(`--- server log tail ---\n${server.logTail()}`);
        await server.stop();
        server.cleanup(true);
      }
    } catch (_) { /* abort path: best effort */ }
    process.exit(1);
  }, suiteTimeoutS * 1000);
  watchdog.unref();

  let port = getArg('--port', 4000);

  if (managed) {
    server = new ManagedServer(projectRoot, configuration, packsDir);
    try {
      await server.start();
    } catch (err) {
      console.error(`BOOT FAILURE: ${err.message}`);
      await server.stop();
      server.cleanup(true);
      process.exit(1);
    }
    port = server.port;
  } else {
    // Non-managed: prove the target server is alive before running anything.
    const probe = new TelnetClient('BootProbe', port);
    try {
      await probe.connect();
      await probe.waitFor('Speak your name', BANNER_PROBE_MS);
    } catch (err) {
      console.error(`Target server on port ${port} is not answering logins: ${describeError(err)}`);
      process.exit(1);
    } finally {
      probe.disconnect();
    }
  }

  console.log(`Running ${files.length} scenario file(s) against localhost:${port}...\n`);

  const opts = { port, delay, adminPlayer, scenarioTimeoutS };
  const suiteStart = Date.now();

  for (const file of files) {
    if (!jsonOnly) {
      console.log(`▶ ${path.relative(process.cwd(), file)}`);
    }
    const fileResult = await runScenarioFile(file, defaultsDir || '', opts);
    allResults.push(fileResult);

    if (resultsDir) {
      const transcriptPath = writeTranscript(fileResult, resultsDir);
      if (!jsonOnly) {
        console.log(`  Transcript: ${path.relative(process.cwd(), transcriptPath)}`);
      }
    }
  }

  if (jsonOnly) {
    console.log(JSON.stringify(allResults, null, 2));
  } else {
    printSummary(allResults);
    console.log(`\nSuite wall-clock: ${((Date.now() - suiteStart) / 1000).toFixed(1)}s`);
  }

  if (resultsDir) {
    writeResultsJson(allResults, resultsDir);
  }

  const anyBad = allResults.some(r =>
    r.scenarios.some(s => s.status === 'fail' || s.status === 'error')
  );

  if (server) {
    if (anyBad) {
      console.error(`--- server log tail ---\n${server.logTail()}`);
    }
    await server.stop();
    server.cleanup(anyBad);
  }

  process.exit(anyBad ? 1 : 0);
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

  console.log('Normalizer self-tests:');

  assert('stripAnsi removes color codes',
    stripAnsi('\x1b[31mred\x1b[0m text') === 'red text');
  assert('normalize removes CR (CRLF vs LF parity)',
    normalize('line one\r\nline two\r\n') === 'line one\nline two\n');
  assert('containsText matches across CRLF/ANSI/case',
    containsText('\x1b[32mThe \x1b[1mNexus\x1b[0m\r\n', 'the nexus'));
  assert('containsText negative',
    !containsText('hello world', 'farewell'));
  assert('filterSentinels drops sync lines',
    filterSentinels(`look output\nNo help found for '${SYNC_PREFIX}9__'.\nmore output`) === 'look output\nmore output');
  assert('filterSentinels keeps everything else',
    filterSentinels('a\nb\nc') === 'a\nb\nc');

  console.log('Managed-config self-tests:');

  const sampleYaml = 'server:\n  name: "X"\n  telnet_port: 4000\n  websocket_port: 4001\n\npacks:\n  - tapestry-core\n  - tapestry-biomes\n  - tapestry-cooking\n  - example-pack\n';
  const rewritten = rewriteConfigPorts(sampleYaml, 41234, 41235);
  assert('rewrites telnet_port', rewritten.includes('telnet_port: 41234'));
  assert('rewrites websocket_port', rewritten.includes('websocket_port: 41235'));
  assert('counts config packs', countConfigPacks(sampleYaml) === 4);
  assert('counts zero packs when absent', countConfigPacks('server:\n  name: x\n') === 0);
  assert('counts packs across comment lines',
    countConfigPacks('packs:\n  - a\n  # comment\n  - b\n') === 2);

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
    console.error('Fatal error:', err.stack || err.message);
    process.exit(1);
  });
}
