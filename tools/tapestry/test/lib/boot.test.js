'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const {
  readBoot,
  writeBoot,
  addPackageToBoot,
  removePackageFromBoot,
  enablePackage,
  disablePackage,
  topoSort,
} = require('../../src/lib/boot');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-boot-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('readBoot', () => {
  it('returns empty structure when no boot file exists', () => {
    const boot = readBoot(tmpDir);
    expect(boot).toEqual({ modules: [], packs: {} });
  });

  it('reads existing boot file', () => {
    writeBoot(tmpDir, { modules: [], packs: { '@tapestry/core': { enabled: true } } });
    const boot = readBoot(tmpDir);
    expect(boot.packs['@tapestry/core'].enabled).toBe(true);
  });
});

describe('addPackageToBoot', () => {
  it('adds a pack-only package to boot packs', () => {
    addPackageToBoot(tmpDir, '@tapestry/weather', { name: '@tapestry/weather' });
    const boot = readBoot(tmpDir);
    expect(boot.packs['@tapestry/weather']).toEqual({ enabled: true });
    expect(boot.modules).toHaveLength(0);
  });

  it('adds a module package to both packs and modules', () => {
    const manifest = {
      name: '@tapestry/combat',
      module: { class: 'Tapestry.Modules.CombatModule', assembly: 'Combat.dll' },
    };
    addPackageToBoot(tmpDir, '@tapestry/combat', manifest);
    const boot = readBoot(tmpDir);
    expect(boot.packs['@tapestry/combat']).toEqual({ enabled: true });
    expect(boot.modules[0].class).toBe('Tapestry.Modules.CombatModule');
    expect(boot.modules[0].enabled).toBe(true);
  });

  it('does not duplicate if called twice', () => {
    const manifest = { name: '@tapestry/combat', module: { class: 'Tapestry.Modules.CombatModule' } };
    addPackageToBoot(tmpDir, '@tapestry/combat', manifest);
    addPackageToBoot(tmpDir, '@tapestry/combat', manifest);
    const boot = readBoot(tmpDir);
    expect(boot.modules).toHaveLength(1);
  });
});

describe('removePackageFromBoot', () => {
  it('removes pack and any modules for that package', () => {
    const manifest = { name: '@tapestry/combat', module: { class: 'Tapestry.Modules.CombatModule' } };
    addPackageToBoot(tmpDir, '@tapestry/combat', manifest);
    removePackageFromBoot(tmpDir, '@tapestry/combat');
    const boot = readBoot(tmpDir);
    expect(boot.packs['@tapestry/combat']).toBeUndefined();
    expect(boot.modules).toHaveLength(0);
  });
});

describe('enablePackage / disablePackage', () => {
  it('disables a pack-only package', () => {
    addPackageToBoot(tmpDir, '@tapestry/weather', { name: '@tapestry/weather' });
    disablePackage(tmpDir, '@tapestry/weather');
    expect(readBoot(tmpDir).packs['@tapestry/weather'].enabled).toBe(false);
  });

  it('re-enables a disabled package', () => {
    addPackageToBoot(tmpDir, '@tapestry/weather', { name: '@tapestry/weather' });
    disablePackage(tmpDir, '@tapestry/weather');
    enablePackage(tmpDir, '@tapestry/weather');
    expect(readBoot(tmpDir).packs['@tapestry/weather'].enabled).toBe(true);
  });

  it('disables all modules for the package', () => {
    const manifest = { name: '@tapestry/combat', module: { class: 'Tapestry.Modules.CombatModule' } };
    addPackageToBoot(tmpDir, '@tapestry/combat', manifest);
    disablePackage(tmpDir, '@tapestry/combat');
    const boot = readBoot(tmpDir);
    expect(boot.modules[0].enabled).toBe(false);
  });

  it('throws when enabling a package not in boot file', () => {
    expect(() => enablePackage(tmpDir, '@tapestry/missing')).toThrow('@tapestry/missing is not installed');
  });

  it('throws when disabling a package not in boot file', () => {
    expect(() => disablePackage(tmpDir, '@tapestry/missing')).toThrow('@tapestry/missing is not installed');
  });
});

describe('topoSort', () => {
  it('returns modules in dependency order', () => {
    const modules = [
      { class: 'ModB', package: '@pkg/b', enabled: true, after: 'ModA' },
      { class: 'ModA', package: '@pkg/a', enabled: true },
    ];
    const sorted = topoSort(modules);
    expect(sorted.map((m) => m.class)).toEqual(['ModA', 'ModB']);
  });

  it('throws on circular dependency', () => {
    const modules = [
      { class: 'ModA', package: '@pkg/a', enabled: true, after: 'ModB' },
      { class: 'ModB', package: '@pkg/b', enabled: true, after: 'ModA' },
    ];
    expect(() => topoSort(modules)).toThrow('Circular dependency');
  });

  it('handles modules with no after constraint', () => {
    const modules = [
      { class: 'ModA', package: '@pkg/a', enabled: true },
      { class: 'ModB', package: '@pkg/b', enabled: true },
    ];
    expect(topoSort(modules)).toHaveLength(2);
  });
});
