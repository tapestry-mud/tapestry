'use strict';

const {
  validatePackageManifest,
  validateProjectManifest,
} = require('../../src/schema/manifest');

const validPackage = {
  name: '@author/my-pack',
  version: '1.0.0',
  type: 'module',
  display_name: 'My Pack',
  description: 'A test pack',
  author: { name: 'Test Author', handle: 'author' },
  license: 'MIT',
  engine: '>=3.0.0',
  tag_validation: 'strict',
};

test('accepts a valid package manifest', () => {
  const result = validatePackageManifest(validPackage);
  expect(result.success).toBe(true);
});

test('rejects a manifest missing required field version', () => {
  const { version, ...without } = validPackage;
  const result = validatePackageManifest(without);
  expect(result.success).toBe(false);
});

test('rejects an unscoped package name', () => {
  const result = validatePackageManifest({ ...validPackage, name: 'my-pack' });
  expect(result.success).toBe(false);
});

test('rejects an invalid type', () => {
  const result = validatePackageManifest({ ...validPackage, type: 'plugin' });
  expect(result.success).toBe(false);
});

test('rejects an invalid tag_validation value', () => {
  const result = validatePackageManifest({ ...validPackage, tag_validation: 'off' });
  expect(result.success).toBe(false);
});

test('accepts optional dependencies field', () => {
  const result = validatePackageManifest({
    ...validPackage,
    dependencies: { '@tapestry/core': '^1.0.0' },
  });
  expect(result.success).toBe(true);
});

test('accepts optional module section', () => {
  const result = validatePackageManifest({
    ...validPackage,
    module: { assembly: 'MyModule.dll', class: 'My.Module', implements: 'IGameModule' },
  });
  expect(result.success).toBe(true);
});

const validProject = {
  name: 'my-game',
  engine: { version: '3.1.0', mode: 'docker' },
};

test('accepts a valid project manifest with object engine', () => {
  const result = validateProjectManifest(validProject);
  expect(result.success).toBe(true);
});

test('accepts a project manifest with string engine', () => {
  const result = validateProjectManifest({ name: 'my-game', engine: '>=3.0.0' });
  expect(result.success).toBe(true);
});

test('rejects a project manifest missing name', () => {
  const { name, ...without } = validProject;
  const result = validateProjectManifest(without);
  expect(result.success).toBe(false);
});
