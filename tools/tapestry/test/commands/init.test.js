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

test('creates tapestry.yaml with project name derived from directory', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    const manifest = readYaml(path.join(projectDir, 'tapestry.yaml'));
    expect(manifest.name).toBe('my-game');
  }
});

test('tapestry.yaml includes engine config and empty dependencies', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    const manifest = readYaml(path.join(projectDir, 'tapestry.yaml'));
    expect(manifest.engine).toMatchObject({
      version: '0.0.1',
      mode: 'docker',
      image: 'ghcr.io/tapestry-mud/tapestry',
    });
    expect(manifest.dependencies).toBeDefined();
    expect(Object.keys(manifest.dependencies)).toHaveLength(0);
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

test('creates .gitignore with packs/ and .tapestry-engine/ entries', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    init(projectDir);
    const gitignore = fs.readFileSync(path.join(projectDir, '.gitignore'), 'utf8');
    expect(gitignore).toContain('packs/');
    expect(gitignore).toContain('.tapestry-engine/');
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

test('throws if tapestry.yaml already exists', () => {
  {
    const projectDir = path.join(tmpDir, 'my-game');
    fs.mkdirSync(projectDir);
    fs.writeFileSync(path.join(projectDir, 'tapestry.yaml'), 'name: my-game\n');
    expect(() => { init(projectDir); }).toThrow('tapestry.yaml already exists');
  }
});
