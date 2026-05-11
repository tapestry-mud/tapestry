const request = require('supertest');
const { createTestApp, cleanupTestApp, seedAccount, seedPackage } = require('./helpers');

let app, db, dataDir;
beforeEach(() => {
  ({ app, db, dataDir } = createTestApp());
  seedAccount(db);
  seedPackage(db, { scope: 'tapestry', name: 'weather', description: 'Dynamic weather patterns', keywords: ['weather', 'climate'] });
  seedPackage(db, { scope: 'tapestry', name: 'combat-skills', description: 'Ability-based combat', keywords: ['combat', 'skills'] });
  seedPackage(db, { scope: 'tapestry', name: 'sustenance', description: 'Hunger and thirst mechanics', keywords: ['survival'] });
});
afterEach(() => cleanupTestApp({ db, dataDir }));

test('finds package by name fragment', async () => {
  const res = await request(app).get('/v1/search?q=weather');
  expect(res.status).toBe(200);
  expect(res.body.results.some(r => r.name === '@tapestry/weather')).toBe(true);
  expect(res.body.results.some(r => r.name === '@tapestry/sustenance')).toBe(false);
});

test('finds package by description word', async () => {
  const res = await request(app).get('/v1/search?q=hunger');
  expect(res.body.results.some(r => r.name === '@tapestry/sustenance')).toBe(true);
});

test('finds package by keyword', async () => {
  const res = await request(app).get('/v1/search?q=combat');
  expect(res.body.results.some(r => r.name === '@tapestry/combat-skills')).toBe(true);
  expect(res.body.results.some(r => r.name === '@tapestry/weather')).toBe(false);
});

test('returns empty results for no match', async () => {
  const res = await request(app).get('/v1/search?q=nonexistent');
  expect(res.status).toBe(200);
  expect(res.body.results).toHaveLength(0);
});

test('returns empty results for missing q param', async () => {
  const res = await request(app).get('/v1/search');
  expect(res.status).toBe(200);
  expect(res.body.results).toHaveLength(0);
});

test('each result includes name, version, description', async () => {
  const res = await request(app).get('/v1/search?q=weather');
  const result = res.body.results[0];
  expect(result.name).toBeDefined();
  expect(result.version).toBeDefined();
  expect(result.description).toBeDefined();
});
