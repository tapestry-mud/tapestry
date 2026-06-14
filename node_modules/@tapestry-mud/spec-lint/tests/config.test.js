'use strict';

const { BASE_CONFIG } = require('../src/base-config');

describe('BASE_CONFIG', () => {
  test('has required sections array with four entries', () => {
    expect(BASE_CONFIG.sections).toEqual([
      'Overview',
      'Behavior',
      'Rejected and Reverted',
      'Change Log',
    ]);
  });

  test('anchorRegex matches valid anchors', () => {
    const re = new RegExp(BASE_CONFIG.anchorRegex);
    expect(re.test('(src/Tapestry.Engine/CommandRouter.cs:26-64)')).toBe(true);
    expect(re.test('(src/engine.js:10)')).toBe(true);
    expect(re.test('(config/settings.yaml)')).toBe(true);
    expect(re.test('(@tapestry/core/index.js:5)')).toBe(true);
    expect(re.test('(README.md)')).toBe(false);
    expect(re.test('(src/plain-text.txt)')).toBe(false);
  });

  test('sentinelText is exact sentinel string', () => {
    expect(BASE_CONFIG.sentinelText).toBe('- None on record.');
  });

  test('defaultMode is strict', () => {
    expect(BASE_CONFIG.defaultMode).toBe('strict');
  });
});

const path = require('path');
const { loadConfig } = require('../src/config');

describe('loadConfig', () => {
  test('returns base config when no lint.config.json exists', () => {
    const specsDir = path.join(__dirname, 'fixtures/good/complete/specs');
    const cfg = loadConfig(specsDir);
    expect(cfg.sections).toEqual(BASE_CONFIG.sections);
    expect(cfg.mode).toBe('strict');
  });

  test('per-repo mode overrides default', () => {
    const os = require('os');
    const fs = require('fs');
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'sl-'));
    fs.writeFileSync(path.join(tmp, 'lint.config.json'), JSON.stringify({ mode: 'lenient' }));
    const cfg = loadConfig(tmp);
    expect(cfg.mode).toBe('lenient');
    fs.rmSync(tmp, { recursive: true });
  });

  test('extra sections from config are appended', () => {
    const os = require('os');
    const fs = require('fs');
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'sl-'));
    fs.writeFileSync(path.join(tmp, 'lint.config.json'), JSON.stringify({
      sections: { extra: ['Security'] }
    }));
    const cfg = loadConfig(tmp);
    expect(cfg.sections).toContain('Security');
    expect(cfg.sections).toContain('Overview');
    fs.rmSync(tmp, { recursive: true });
  });
});
