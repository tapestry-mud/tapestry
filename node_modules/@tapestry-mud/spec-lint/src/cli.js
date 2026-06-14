#!/usr/bin/env node
'use strict';

const path = require('path');
const { lint } = require('./engine');
const { loadConfig } = require('./config');
const { formatResults } = require('./formatter');
const { renderContract } = require('./renderer');

const argv = process.argv.slice(2);

const helpRequested = argv.includes('--help') || argv.includes('-h');
if (helpRequested) {
  console.log([
    'Usage: spec-lint [path] [--strict|--lenient] [--explain]',
    '',
    '  path       Directory containing specs/ (default: ./specs)',
    '  --strict   Force strict mode (violations = exit 1)',
    '  --lenient  Force lenient mode (violations = warnings, exit 0)',
    '  --explain  Print the effective contract for this repo and exit',
  ].join('\n'));
  process.exit(0);
}

const specsDir = path.resolve(argv.find(a => !a.startsWith('--')) || './specs');
const config = loadConfig(specsDir);

if (argv.includes('--explain')) {
  console.log(renderContract(config));
  process.exit(0);
}

let mode = config.mode || 'strict';
if (argv.includes('--strict')) { mode = 'strict'; }
if (argv.includes('--lenient')) { mode = 'lenient'; }

const lintResult = lint(specsDir, config);
const output = formatResults(specsDir, lintResult, mode);
console.log(output);

const hasViolations =
  Object.values(lintResult.fileResults).some(vs => vs.length > 0) ||
  lintResult.crossFileViolations.length > 0;

process.exit(mode === 'strict' && hasViolations ? 1 : 0);
