'use strict';

const readline = require('readline');
const fetch = require('node-fetch');
const { saveToken } = require('../lib/auth');
const { DEFAULT_REGISTRY } = require('../lib/registry-client');

async function promptRegistration() {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  return new Promise((resolve, reject) => {
    rl.on('error', reject);
    rl.question('Handle (lowercase, e.g. mallek): ', (handle) => {
      rl.question('Email: ', (email) => {
        rl.question('Password: ', (password) => {
          rl.close();
          resolve({ handle, email, password });
        });
      });
    });
  });
}

async function register({ handle, email, password } = {}, { registryUrl = DEFAULT_REGISTRY } = {}) {
  if (!handle || !email || !password) {
    ({ handle, email, password } = await promptRegistration());
  }

  const res = await fetch(`${registryUrl}/v1/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ handle, email, password }),
  });

  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body.error || `Registration failed (${res.status})`);
  }

  const { token } = await res.json();
  saveToken(token);
  console.log(`Registered as ${handle}. Logged in.`);
}

module.exports = { register };
