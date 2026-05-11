#!/usr/bin/env node
'use strict';

const { Command } = require('commander');
const { init } = require('../src/commands/init');
const { createPack } = require('../src/commands/create-pack');

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

program.parse(process.argv);
