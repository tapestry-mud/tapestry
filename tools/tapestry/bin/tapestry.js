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

program.parse(process.argv);
