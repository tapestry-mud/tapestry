const fs = require('fs');
const yaml = require('js-yaml');

const DEFAULTS = {
  limits: {
    default: {
      max_tarball_mb: 2,
      max_versions: 20,
      max_scope_mb: 50,
    },
    total_cap_gb: 10,
  },
  bypass: [],
  overrides: {},
};

function loadConfig(configPath) {
  if (!fs.existsSync(configPath)) {
    return structuredClone(DEFAULTS);
  }
  const raw = yaml.load(fs.readFileSync(configPath, 'utf8')) || {};
  return {
    limits: {
      default: { ...DEFAULTS.limits.default, ...(raw.limits?.default || {}) },
      total_cap_gb: raw.limits?.total_cap_gb ?? DEFAULTS.limits.total_cap_gb,
    },
    bypass: raw.bypass || [],
    overrides: raw.overrides || {},
  };
}

function getLimitsForScope(config, scope) {
  if (config.bypass.includes(scope)) {
    return null;
  }
  const override = config.overrides[scope];
  if (override) {
    return { ...config.limits.default, ...override };
  }
  return { ...config.limits.default };
}

module.exports = { loadConfig, getLimitsForScope, DEFAULTS };
