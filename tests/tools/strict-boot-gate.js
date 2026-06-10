'use strict';

// ─── Strict-Boot Composition Gate ───────────────────────────────────
//
// Boots the engine to seal against the published @tapestry/* packs at their
// `stable` dist-tags — the exact composition prod meets when an engine is
// promoted (promote = `tapestry stop && start`; installed packs do NOT
// update). This is the CI mechanization of the manual Windows strict-boot
// harness that caught the Plan 2 / v0.1.24 breakage after static analysis
// missed it twice.
//
// The gate FAILS when:
//   - the server process exits before boot completes (seal/HelpSeal throws,
//     manifest errors, anything fatal),
//   - `Pack validation complete: N issue(s) found` reports N > 0,
//   - fewer packs load than were staged (a silently skipped pack),
//   - the server answers the boot sentinels but dies within the settle
//     window (late post-seal crash),
//   - boot does not complete within BOOT_TIMEOUT_MS.
//
// Usage:
//   node tests/tools/strict-boot-gate.js [--configuration Debug|Release]
//        [--workdir DIR] [--corpus DIR]...
//
//   --corpus DIR   Overlay additional local packs (packs-dir layout, i.e.
//                  DIR/@scope/pack/...) over the downloaded corpus. Used by
//                  gate proofs and for pre-release checks of unpublished
//                  packs against the rest of the stable set.
//   --workdir DIR  Stage corpus + config + server.log here (default: a temp
//                  dir). CI passes a repo-relative dir so the log can be
//                  uploaded as an artifact on failure.
//
// No npm dependencies; node builtins + system `tar` only.

const { spawn, spawnSync } = require('child_process');
const crypto = require('crypto');
const fs = require('fs');
const https = require('https');
const net = require('net');
const os = require('os');
const path = require('path');

const REGISTRY = 'https://registry.tapestryengine.com';
const SCOPE = '@tapestry/';
const BOOT_TIMEOUT_MS = 120000;
const SETTLE_MS = 2500;

// ─── HTTP ───────────────────────────────────────────────────────────

function httpsGet(url, asBuffer) {
  return new Promise((resolve, reject) => {
    const req = https.get(url, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        res.resume();
        resolve(httpsGet(new URL(res.headers.location, url).toString(), asBuffer));
        return;
      }
      if (res.statusCode !== 200) {
        res.resume();
        reject(new Error(`GET ${url} -> HTTP ${res.statusCode}`));
        return;
      }
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => {
        const buf = Buffer.concat(chunks);
        try {
          resolve(asBuffer ? buf : JSON.parse(buf.toString('utf-8')));
        } catch (err) {
          reject(new Error(`GET ${url} -> unparseable JSON: ${err.message}`));
        }
      });
    });
    req.on('error', reject);
    req.setTimeout(30000, () => req.destroy(new Error(`GET ${url} -> timeout`)));
  });
}

async function withRetry(label, fn, attempts = 3) {
  for (let i = 1; ; i++) {
    try {
      return await fn();
    } catch (err) {
      if (i >= attempts) {
        throw err;
      }
      console.warn(`${label} failed (attempt ${i}/${attempts}): ${err.message}; retrying...`);
      await new Promise((r) => setTimeout(r, 2000 * i));
    }
  }
}

// ─── Corpus ─────────────────────────────────────────────────────────

async function resolveCorpus() {
  const index = await withRetry('fetch catalog', () => httpsGet(`${REGISTRY}/v1/index.json`));
  const names = Object.keys(index.packages || {}).filter((n) => n.startsWith(SCOPE)).sort();
  if (names.length === 0) {
    throw new Error(`registry catalog lists no ${SCOPE}* packages`);
  }
  const corpus = [];
  for (const name of names) {
    const tags = await withRetry(`dist-tags ${name}`, () =>
      httpsGet(`${REGISTRY}/v1/packages/${name}/dist-tags`));
    if (!tags.stable) {
      console.warn(`SKIP ${name}: no stable dist-tag (not on the prod surface)`);
      continue;
    }
    corpus.push({
      name,
      version: tags.stable,
      integrity: (index.packages[name].integrity || {})[tags.stable] || null
    });
  }
  if (corpus.length === 0) {
    throw new Error('no packages with a stable dist-tag - empty corpus');
  }
  return corpus;
}

async function stagePack(pack, packsDir) {
  const url = `${REGISTRY}/v1/packages/${pack.name}/${pack.version}.tgz`;
  const buf = await withRetry(`download ${pack.name}@${pack.version}`, () => httpsGet(url, true));
  if (pack.integrity) {
    const got = `sha256-${crypto.createHash('sha256').update(buf).digest('base64')}`;
    if (got !== pack.integrity) {
      throw new Error(
        `integrity mismatch for ${pack.name}@${pack.version}\n  expected: ${pack.integrity}\n  got:      ${got}`);
    }
  } else {
    console.warn(`WARN ${pack.name}@${pack.version}: no integrity hash in catalog`);
  }
  const dest = path.join(packsDir, ...pack.name.split('/'));
  fs.mkdirSync(dest, { recursive: true });
  const tgz = path.join(packsDir, `${pack.name.replace(/[@/]/g, '_')}.tgz`);
  fs.writeFileSync(tgz, buf);
  // Tarballs have a single root folder (npm-style); system tar exists on
  // ubuntu runners and on Windows 10+. On Windows, pin to the System32
  // bsdtar — a MINGW GNU tar earlier on PATH parses "C:\..." as a remote
  // host ("Cannot connect to C") and dies.
  const tarBin = process.platform === 'win32'
    ? path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'tar.exe')
    : 'tar';
  const tar = spawnSync(tarBin, ['-xzf', tgz, '-C', dest, '--strip-components=1'], { stdio: 'inherit' });
  if (tar.status !== 0) {
    throw new Error(`tar extract failed for ${pack.name}@${pack.version} (exit ${tar.status})`);
  }
  fs.rmSync(tgz);
}

// Overlay a local packs-dir-shaped tree (DIR/@scope/pack/... or DIR/pack/...)
// over the staged corpus. Copies (not links) so the staged corpus is
// self-contained. Returns the number of packs added (pre-existing dirs are
// overwritten in place and not double-counted).
function overlayCorpusDir(sourceRoot, packsDir) {
  if (!fs.existsSync(sourceRoot)) {
    throw new Error(`--corpus dir not found: ${sourceRoot}`);
  }
  let added = 0;
  for (const entry of fs.readdirSync(sourceRoot, { withFileTypes: true })) {
    if (!entry.isDirectory()) {
      continue;
    }
    const sourcePath = path.join(sourceRoot, entry.name);
    if (entry.name.startsWith('@')) {
      for (const child of fs.readdirSync(sourcePath, { withFileTypes: true })) {
        if (!child.isDirectory()) {
          continue;
        }
        const dest = path.join(packsDir, entry.name, child.name);
        if (!fs.existsSync(dest)) {
          added++;
        }
        fs.cpSync(path.join(sourcePath, child.name), dest, { recursive: true });
      }
    } else {
      const dest = path.join(packsDir, entry.name);
      if (!fs.existsSync(dest)) {
        added++;
      }
      fs.cpSync(sourcePath, dest, { recursive: true });
    }
  }
  return added;
}

// ─── Server plumbing (patterns from tests/tools/telnet-runner.js) ───

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

function findServerDll(projectRoot, configuration) {
  const binDir = path.join(projectRoot, 'src', 'Tapestry.Server', 'bin', configuration);
  if (!fs.existsSync(binDir)) {
    return null;
  }
  const tfms = fs.readdirSync(binDir).filter((d) => d.startsWith('net'));
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

// Minimal ephemeral config. No `packs:` list — the engine loads every pack
// it discovers in --packs (ContentLoadingModule filters only when the list
// is non-empty), so the staged corpus IS the assertion. save_path resolves
// against the config's own directory, keeping persistence inside workdir.
function writeConfig(workdir, telnetPort, websocketPort) {
  const yaml = [
    'server:',
    '  name: "Strict Boot Gate"',
    `  telnet_port: ${telnetPort}`,
    `  websocket_port: ${websocketPort}`,
    '  tick_rate_ms: 100',
    '',
    'llm:',
    '  provider: none',
    '',
    'logging:',
    '  level: Information',
    '',
    'telemetry:',
    '  enabled: false',
    '',
    'persistence:',
    '  save_path: "./data/saves"',
    '  autosave_interval: 3000',
    '  password_min_length: 6',
    '  max_login_attempts: 5',
    ''
  ].join('\n');
  const configPath = path.join(workdir, 'server.gate.yaml');
  fs.writeFileSync(configPath, yaml);
  return configPath;
}

// ─── The gate ───────────────────────────────────────────────────────

function logTail(logPath, lines = 40) {
  let log = '';
  try {
    log = fs.readFileSync(logPath, 'utf-8');
  } catch (_) {
    return '(no server log)';
  }
  return log.split('\n').filter((l) => l.trim()).slice(-lines).join('\n');
}

async function runGate(opts) {
  const projectRoot = findProjectRoot();
  if (!projectRoot) {
    throw new Error('could not locate the repo root (src/Tapestry.Server)');
  }
  const dll = findServerDll(projectRoot, opts.configuration);
  if (!dll) {
    throw new Error(
      `Tapestry.Server.dll not found for configuration "${opts.configuration}". ` +
      `Build first (dotnet build src/Tapestry.Server -c ${opts.configuration}).`);
  }

  const workdir = opts.workdir
    ? path.resolve(opts.workdir)
    : fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-strict-boot-'));
  fs.mkdirSync(workdir, { recursive: true });
  const packsDir = path.join(workdir, 'packs');
  fs.mkdirSync(packsDir, { recursive: true });

  console.log('Resolving the published stable corpus...');
  const corpus = await resolveCorpus();
  for (const pack of corpus) {
    console.log(`  staging ${pack.name}@${pack.version}`);
    await stagePack(pack, packsDir);
  }
  let staged = corpus.length;
  for (const dir of opts.corpusDirs) {
    const added = overlayCorpusDir(dir, packsDir);
    staged += added;
    console.log(`  overlaid ${added} pack(s) from ${dir}`);
  }

  const [telnetPort, websocketPort] = await findFreePorts(2);
  const configPath = writeConfig(workdir, telnetPort, websocketPort);
  const logPath = path.join(workdir, 'server.log');

  console.log(`Booting ${path.basename(dll)} with ${staged} pack(s) on port ${telnetPort}...`);
  const logFd = fs.openSync(logPath, 'a');
  const child = spawn('dotnet', [dll, '--config', configPath, '--packs', packsDir], {
    cwd: projectRoot,
    stdio: ['ignore', logFd, logFd],
    windowsHide: true
  });
  fs.closeSync(logFd);

  let exited = false;
  let exitCode = null;
  child.on('exit', (code) => {
    exited = true;
    exitCode = code;
  });

  const failWithTail = (msg) => {
    console.error(`\nSTRICT-BOOT GATE FAILED: ${msg}`);
    console.error(`--- server log tail (${logPath}) ---`);
    console.error(logTail(logPath));
    return new Error(msg);
  };

  const kill = async () => {
    if (!exited) {
      child.kill();
      const start = Date.now();
      while (!exited && Date.now() - start < 5000) {
        await new Promise((r) => setTimeout(r, 100));
      }
      if (!exited) {
        try {
          child.kill('SIGKILL');
        } catch (_) { /* already gone */ }
      }
    }
  };

  try {
    const start = Date.now();
    while (true) {
      if (exited) {
        throw failWithTail(`server exited during boot (code ${exitCode}) - seal/HelpSeal or fatal boot error`);
      }
      if (Date.now() - start > BOOT_TIMEOUT_MS) {
        throw failWithTail(`server did not finish booting within ${BOOT_TIMEOUT_MS}ms`);
      }

      const log = fs.existsSync(logPath) ? fs.readFileSync(logPath, 'utf-8') : '';
      const validation = log.match(/Pack validation complete: (\d+) issue\(s\) found/);
      if (validation && Number(validation[1]) > 0) {
        throw failWithTail(`pack validation reported ${validation[1]} issue(s); the gate requires 0`);
      }
      const loopStarted = log.includes('Game loop starting');
      const listening = log.includes(`Telnet server listening on port ${telnetPort}`);

      if (loopStarted && listening) {
        // Settle window: a post-sentinel crash (late HelpSeal/startup task)
        // must still fail the gate.
        await new Promise((r) => setTimeout(r, SETTLE_MS));
        if (exited) {
          throw failWithTail(`server died ${SETTLE_MS}ms after boot sentinels (code ${exitCode})`);
        }
        const settled = fs.readFileSync(logPath, 'utf-8');
        const finalValidation = settled.match(/Pack validation complete: (\d+) issue\(s\) found/);
        if (!finalValidation) {
          throw failWithTail('boot completed but the pack-validation line never appeared');
        }
        if (Number(finalValidation[1]) > 0) {
          throw failWithTail(`pack validation reported ${finalValidation[1]} issue(s); the gate requires 0`);
        }
        const loaded = (settled.match(/Loaded pack:/g) || []).length;
        if (loaded !== staged) {
          throw failWithTail(`staged ${staged} pack(s) but the engine loaded ${loaded} - a pack was silently skipped`);
        }
        console.log('\nSTRICT-BOOT GATE PASSED');
        console.log(`  corpus: ${corpus.map((p) => `${p.name.slice(SCOPE.length)}@${p.version}`).join(', ')}`);
        if (staged > corpus.length) {
          console.log(`  + ${staged - corpus.length} overlaid local pack(s)`);
        }
        console.log(`  packs loaded: ${loaded}/${staged}, validation issues: 0`);
        return;
      }
      await new Promise((r) => setTimeout(r, 200));
    }
  } finally {
    await kill();
  }
}

// ─── CLI ────────────────────────────────────────────────────────────

function parseArgs(argv) {
  const opts = { configuration: 'Debug', workdir: null, corpusDirs: [] };
  for (let i = 0; i < argv.length; i++) {
    switch (argv[i]) {
      case '--configuration':
        opts.configuration = argv[++i];
        break;
      case '--workdir':
        opts.workdir = argv[++i];
        break;
      case '--corpus':
        opts.corpusDirs.push(argv[++i]);
        break;
      default:
        console.error(`Unknown argument: ${argv[i]}`);
        console.error('Usage: node tests/tools/strict-boot-gate.js [--configuration C] [--workdir DIR] [--corpus DIR]...');
        process.exit(2);
    }
  }
  return opts;
}

runGate(parseArgs(process.argv.slice(2))).then(
  () => process.exit(0),
  (err) => {
    console.error(`\n${err.message}`);
    process.exit(1);
  }
);
