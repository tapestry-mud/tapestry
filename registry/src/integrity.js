const crypto = require('crypto');

function computeIntegrity(buffer) {
  const hash = crypto.createHash('sha256').update(buffer).digest('base64');
  return `sha256-${hash}`;
}

module.exports = { computeIntegrity };
