'use strict';

const fs = require('fs');
const path = require('path');
const { readYaml, writeYaml } = require('../util/yaml');

const BOOT_FILE = 'tapestry-boot.yaml';

function readBoot(cwd) {
  const bootPath = path.join(cwd, BOOT_FILE);
  if (!fs.existsSync(bootPath)) {
    return { modules: [], packs: {} };
  }
  const data = readYaml(bootPath);
  return { modules: data.modules || [], packs: data.packs || {} };
}

function writeBoot(cwd, boot) {
  writeYaml(path.join(cwd, BOOT_FILE), boot);
}

function addPackageToBoot(cwd, packageName, manifest) {
  const boot = readBoot(cwd);

  if (!boot.packs[packageName]) {
    boot.packs[packageName] = { enabled: true };
  }

  if (manifest.module && manifest.module.class) {
    const alreadyPresent = boot.modules.some((m) => m.package === packageName);
    if (!alreadyPresent) {
      boot.modules.push({
        class: manifest.module.class,
        package: packageName,
        enabled: true,
      });
    }
  }

  boot.modules = topoSort(boot.modules);
  writeBoot(cwd, boot);
}

function removePackageFromBoot(cwd, packageName) {
  const boot = readBoot(cwd);
  delete boot.packs[packageName];
  boot.modules = boot.modules.filter((m) => m.package !== packageName);
  writeBoot(cwd, boot);
}

function enablePackage(cwd, packageName) {
  const boot = readBoot(cwd);
  if (!boot.packs[packageName]) {
    throw new Error(`${packageName} is not installed`);
  }
  boot.packs[packageName].enabled = true;
  for (const mod of boot.modules) {
    if (mod.package === packageName) {
      mod.enabled = true;
    }
  }
  writeBoot(cwd, boot);
}

function disablePackage(cwd, packageName) {
  const boot = readBoot(cwd);
  if (!boot.packs[packageName]) {
    throw new Error(`${packageName} is not installed`);
  }
  boot.packs[packageName].enabled = false;
  for (const mod of boot.modules) {
    if (mod.package === packageName) {
      mod.enabled = false;
    }
  }
  writeBoot(cwd, boot);
}

function topoSort(modules) {
  const byClass = {};
  for (const mod of modules) {
    byClass[mod.class] = mod;
  }

  const visited = new Set();
  const inStack = new Set();
  const result = [];

  function visit(mod) {
    if (inStack.has(mod.class)) {
      throw new Error(`Circular dependency in .NET module boot order involving: ${mod.class}`);
    }
    if (visited.has(mod.class)) {
      return;
    }
    inStack.add(mod.class);
    if (mod.after && byClass[mod.after]) {
      visit(byClass[mod.after]);
    }
    inStack.delete(mod.class);
    visited.add(mod.class);
    result.push(mod);
  }

  for (const mod of modules) {
    visit(mod);
  }

  return result;
}

module.exports = { readBoot, writeBoot, addPackageToBoot, removePackageFromBoot, enablePackage, disablePackage, topoSort };
