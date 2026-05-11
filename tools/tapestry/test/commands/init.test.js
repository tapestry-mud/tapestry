'use strict';

const { init } = require('../../src/commands/init');
const { readYaml } = require('../../src/util/yaml');
const fs = require('fs');
const path = require('path');
const os = require('os');

let tmpDir;

beforeEach(() => {
  {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-init-'));
  }
});

afterEach(() => {
  {
    fs.rmSync(tmpDir, { recursive: true });
  }
});

test('creates tpm.yaml with project name derived from directory', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    const manifest = readYaml(path.join(projectDir, 'tpm.yaml'));
    expect(manifest.name).toBe('my-game');
  }
});

test('tpm.yaml includes @tapestry/core dependency and engine config', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    const manifest = readYaml(path.join(projectDir, 'tpm.yaml'));
    expect(manifest.engine).toBeDefined();
    expect(manifest.dependencies['@tapestry/core']).toBe('^1.0.0');
  }
});

test('creates server.yaml', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    expect(fs.existsSync(path.join(projectDir, 'server.yaml'))).toBe(true);
  }
});

test('creates packs/ directory', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    expect(fs.existsSync(path.join(projectDir, 'packs'))).toBe(true);
  }
});

test('creates .gitignore with packs/ entry', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    const gitignore = fs.readFileSync(path.join(projectDir, '.gitignore'), 'utf8');
    expect(gitignore).toContain('packs/');
  }
});

test('logs git hint when no .git directory exists', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    const log = jest.spyOn(console, 'log').mockImplementation();
    init(projectDir);
    const output = log.mock.calls.map(c => c[0]).join('\n');
    expect(output).toContain('git init');
    log.mockRestore();
  }
});

test('does not log git hint when .git directory exists', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    fs.mkdirSync(path.join(projectDir, '.git'));
    const log = jest.spyOn(console, 'log').mockImplementation();
    init(projectDir);
    const output = log.mock.calls.map(c => c[0]).join('\n');
    expect(output).not.toContain('git init');
    log.mockRestore();
  }
});

test('throws if tpm.yaml already exists', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    fs.writeFileSync(path.join(projectDir, 'tpm.yaml'), 'name: my-game\n');
    expect(() => { init(projectDir); }).toThrow('tpm.yaml already exists');
  }
});
