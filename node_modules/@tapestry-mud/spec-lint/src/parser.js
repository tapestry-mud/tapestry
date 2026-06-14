'use strict';

function parseFrontmatter(content) {
  const match = content.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!match) { return null; }
  const result = {};
  for (const line of match[1].split(/\r?\n/)) {
    const listMatch = line.match(/^([\w-]+):\s*\[([^\]]*)\]$/);
    if (listMatch) {
      result[listMatch[1]] = listMatch[2].split(',').map(s => s.trim()).filter(Boolean);
      continue;
    }
    const scalarMatch = line.match(/^([\w-]+):\s*(.+)$/);
    if (scalarMatch) {
      result[scalarMatch[1]] = scalarMatch[2].trim();
    }
  }
  return result;
}

function parseSections(content) {
  const body = content.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n?/, '');
  const sections = {};
  let currentName = null;
  const currentLines = [];

  for (const line of body.split(/\r?\n/)) {
    const h2 = line.match(/^## (.+)$/);
    if (h2) {
      if (currentName !== null) {
        sections[currentName] = currentLines.join('\n').trim();
        currentLines.length = 0;
      }
      currentName = h2[1];
    } else if (line === '---') {
      // skip thematic breaks (horizontal rules); they are visual dividers, not content
    } else if (currentName !== null) {
      currentLines.push(line);
    }
  }
  if (currentName !== null) {
    sections[currentName] = currentLines.join('\n').trim();
  }
  return sections;
}

module.exports = { parseFrontmatter, parseSections };
