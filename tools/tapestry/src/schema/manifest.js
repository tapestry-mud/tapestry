'use strict';

const { z } = require('zod');

const SCOPED_NAME = /^@[a-z0-9-]+\/[a-z0-9-]+$/;

const PackageManifestSchema = z.object({
  name: z.string().regex(SCOPED_NAME, 'name must be @scope/package-name'),
  version: z.string().min(1),
  type: z.enum(['core', 'module', 'world']),
  display_name: z.string().min(1),
  description: z.string().min(1),
  author: z.union([
    z.string().min(1),
    z.object({
      name: z.string().min(1),
      handle: z.string().min(1),
    }),
  ]),
  license: z.string().min(1),
  engine: z.string().min(1),
  tag_validation: z.enum(['strict', 'lenient']),
  dependencies: z.record(z.string()).optional(),
  peerDependencies: z.record(z.string()).optional(),
  provides: z.array(z.string()).optional(),
  tags: z.string().optional(),
  module: z.object({
    assembly: z.string(),
    class: z.string(),
    implements: z.string(),
    after: z.string().optional(),
  }).optional(),
  content: z.record(z.string()).optional(),
  client: z.object({
    manifest: z.string(),
    assets: z.string(),
    min_client_version: z.string(),
  }).optional(),
  meta: z.object({
    commands: z.array(z.string()).optional(),
    properties: z.number().optional(),
    keywords: z.array(z.string()).optional(),
  }).optional(),
});

const ProjectManifestSchema = z.object({
  name: z.string().min(1),
  engine: z.union([
    z.string().min(1),
    z.object({
      version: z.string().min(1),
      mode: z.enum(['docker', 'binary', 'source']),
      image: z.string().optional(),
    }),
  ]),
  dependencies: z.record(z.string()).optional(),
  packs: z.array(z.string()).optional(),
  tag_validation: z.enum(['strict', 'lenient']).optional(),
});

function validatePackageManifest(data) {
  return PackageManifestSchema.safeParse(data);
}

function validateProjectManifest(data) {
  return ProjectManifestSchema.safeParse(data);
}

module.exports = {
  PackageManifestSchema,
  ProjectManifestSchema,
  validatePackageManifest,
  validateProjectManifest,
};
