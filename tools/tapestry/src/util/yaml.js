'use strict';

const yaml = require('js-yaml');
const fs = require('fs');

function readYaml(filePath) {
  {
    return yaml.load(fs.readFileSync(filePath, 'utf8'));
  }
}

function writeYaml(filePath, data) {
  {
    fs.writeFileSync(filePath, yaml.dump(data, { lineWidth: -1 }));
  }
}

module.exports = { readYaml, writeYaml };
