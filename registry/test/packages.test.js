const request = require('supertest');
const { createTestApp, cleanupTestApp, seedAccount, seedPackage } = require('./helpers');

let app, db, dataDir;
beforeEach(() => {
  ({ app, db, dataDir } = createTestApp());
  seedAccount(db);
  seedPackage(db, { scope: 'tapestry', name: 'weather', version: '1.0.0', ownerHandle: 'testuser', description: 'Dynamic weather' });
  seedPackage(db, { scope: 'tapestry', name: 'weather', version: '0.9.0', ownerHandle: 'testuser', description: 'Dynamic weather' });
  seedPackage(db, { scope: 'tapestry', name: 'sustenance', version: '1.0.0', ownerHandle: 'testuser', description: 'Hunger and thirst', keywords: ['hunger', 'survival'] });
});
afterEach(() => cleanupTestApp({ db, dataDir }));

describe('GET /v1/index.json', () => {
  test('returns all packages', async () => {
    const res = await request(app).get('/v1/index.json');
    expect(res.status).toBe(200);
    expect(res.body.packages).toHaveProperty('@tapestry/weather');
    expect(res.body.packages).toHaveProperty('@tapestry/sustenance');
    expect(res.body.updated).toBeDefined();
  });

  test('lists all versions for a package', async () => {
    const res = await request(app).get('/v1/index.json');
    const weather = res.body.packages['@tapestry/weather'];
    expect(weather.versions).toContain('1.0.0');
    expect(weather.versions).toContain('0.9.0');
  });

  test('includes description and keywords', async () => {
    const res = await request(app).get('/v1/index.json');
    const sustenance = res.body.packages['@tapestry/sustenance'];
    expect(sustenance.description).toBe('Hunger and thirst');
    expect(sustenance.keywords).toContain('hunger');
  });

  test('includes integrity hashes keyed by version', async () => {
    const res = await request(app).get('/v1/index.json');
    const weather = res.body.packages['@tapestry/weather'];
    expect(weather.integrity['1.0.0']).toBeDefined();
  });
});

describe('GET /v1/packages/@:scope/:name', () => {
  test('returns package metadata with versions', async () => {
    const res = await request(app).get('/v1/packages/@tapestry/weather');
    expect(res.status).toBe(200);
    expect(res.body.name).toBe('@tapestry/weather');
    expect(res.body.owner).toBe('testuser');
    expect(res.body.versions).toHaveLength(2);
  });

  test('includes parsed manifest in each version', async () => {
    const res = await request(app).get('/v1/packages/@tapestry/weather');
    const v = res.body.versions.find(v => v.version === '1.0.0');
    expect(v.manifest.description).toBe('Dynamic weather');
  });

  test('returns 404 for unknown package', async () => {
    const res = await request(app).get('/v1/packages/@tapestry/nonexistent');
    expect(res.status).toBe(404);
  });
});
