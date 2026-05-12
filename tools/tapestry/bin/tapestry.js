#!/usr/bin/env node
'use strict';

const { Command } = require('commander');
const { init } = require('../src/commands/init');
const { createPack } = require('../src/commands/create-pack');
const { install } = require('../src/commands/install');
const { uninstall } = require('../src/commands/uninstall');
const { update } = require('../src/commands/update');
const { enable } = require('../src/commands/enable');
const { disable } = require('../src/commands/disable');
const { login } = require('../src/commands/login');
const { register } = require('../src/commands/register');
const { validate } = require('../src/commands/validate');
const { pack } = require('../src/commands/pack');
const { publish } = require('../src/commands/publish');
const { search } = require('../src/commands/search');
const { info } = require('../src/commands/info');
const { list } = require('../src/commands/list');
const { outdated } = require('../src/commands/outdated');
const { engineInstall, engineUpdate, engineInfo } = require('../src/commands/engine');
const { startCmd } = require('../src/commands/start');
const { stopCmd } = require('../src/commands/stop');

const program = new Command();

program
  .name('tapestry')
  .description('Tapestry Package Manager')
  .version('0.1.0');

program
  .command('init')
  .description('Initialize a new Tapestry game project in the current directory')
  .action(() => {
    try {
      init();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

const createCmd = program.command('create');

createCmd
  .command('pack <name>')
  .description('Scaffold a new pack with annotated example content')
  .action((name) => {
    try {
      createPack(name);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('install [package]')
  .description('Install a package or all dependencies from tapestry.yaml')
  .action(async (pkg) => {
    try {
      await install(pkg || undefined);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('uninstall <package>')
  .description('Remove an installed package')
  .action(async (pkg) => {
    try {
      await uninstall(pkg);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('update [package]')
  .description('Update a package or all packages to latest compatible versions')
  .action(async (pkg) => {
    try {
      await update(pkg || undefined);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('enable <package>')
  .description('Activate a package in the engine boot order')
  .action(async (pkg) => {
    try {
      await enable(pkg);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('disable <package>')
  .description('Remove a package from the engine boot order without deleting files')
  .action(async (pkg) => {
    try {
      await disable(pkg);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('login')
  .description('Authenticate with the registry and store token in ~/.tapestryrc')
  .action(async () => {
    try {
      await login();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('register')
  .description('Create an account on the registry')
  .action(async () => {
    try {
      await register();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('validate')
  .description('Validate tapestry.yaml in the current directory')
  .action(() => {
    try {
      validate();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('pack')
  .description('Build a tarball from the current pack directory for local inspection')
  .action(async () => {
    try {
      await pack();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('publish')
  .description('Build and upload the current pack to the registry')
  .action(async () => {
    try {
      await publish();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('search <query>')
  .description('Search the registry by keyword')
  .action(async (query) => {
    try {
      await search(query);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('info <package>')
  .description('Show details for a registry package')
  .action(async (pkg) => {
    try {
      await info(pkg);
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('list')
  .description('Show installed packages with version, type, and enabled/disabled status')
  .action(async () => {
    try {
      await list();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('outdated')
  .description('Show installed packages with newer versions available in the registry')
  .action(async () => {
    try {
      await outdated();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

const engineCmd = program.command('engine').description('Manage the Tapestry engine');

engineCmd
  .command('install')
  .description('Fetch the engine artifact for the configured mode (docker/binary/source)')
  .action(async () => {
    try {
      await engineInstall();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

engineCmd
  .command('update')
  .description('Update the engine to the configured version')
  .action(async () => {
    try {
      await engineUpdate();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

engineCmd
  .command('info')
  .description('Show installed engine version, mode, and image or path')
  .action(() => {
    try {
      engineInfo();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('start')
  .description('Launch the Tapestry engine')
  .action(async () => {
    try {
      await startCmd();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program
  .command('stop')
  .description('Stop the running Tapestry engine')
  .action(async () => {
    try {
      await stopCmd();
    } catch (e) {
      console.error(`error: ${e.message}`);
      process.exit(1);
    }
  });

program.parse(process.argv);
