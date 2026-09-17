import { mkdirSync, rmSync, cpSync, renameSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..');
const runtime = process.argv[2] ?? 'win-x64';
const dotnet = process.env.DOTNET ?? join(repoRoot, '.dotnet', 'dotnet');
const publishDir = join(repoRoot, 'artifacts', 'backend', runtime);
const sidecarDir = join(repoRoot, 'frontend', 'src-tauri', 'bin');

const targetTriples = {
  'win-x64': 'x86_64-pc-windows-msvc',
  'osx-arm64': 'aarch64-apple-darwin',
  'osx-x64': 'x86_64-apple-darwin',
  'linux-x64': 'x86_64-unknown-linux-gnu',
};

const triple = targetTriples[runtime];
if (!triple) {
  throw new Error(`Unsupported runtime '${runtime}'.`);
}

rmSync(publishDir, { recursive: true, force: true });
mkdirSync(publishDir, { recursive: true });
mkdirSync(sidecarDir, { recursive: true });

const publish = spawnSync(dotnet, [
  'publish',
  join(repoRoot, 'src', 'RpaDevAssistant.Api', 'RpaDevAssistant.Api.csproj'),
  '-c',
  'Release',
  '-r',
  runtime,
  '--self-contained',
  'true',
  '--no-restore',
  '-p:PublishSingleFile=true',
  '-p:PublishTrimmed=false',
  '-p:UseSharedCompilation=false',
  '-p:EnableSourceControlManagerQueries=false',
  '-m:1',
  '/nr:false',
  '-o',
  publishDir,
], {
  cwd: repoRoot,
  stdio: 'inherit',
  env: {
    ...process.env,
    TMPDIR: join(repoRoot, 'tmp'),
    GIT_CONFIG_GLOBAL: '/dev/null',
  },
});

if (publish.status !== 0) {
  process.exit(publish.status ?? 1);
}

const executableName = runtime.startsWith('win') ? 'RpaDevAssistant.Api.exe' : 'RpaDevAssistant.Api';
const publishedExecutable = join(publishDir, executableName);
if (!existsSync(publishedExecutable)) {
  throw new Error(`Expected published executable was not found: ${publishedExecutable}`);
}

const sidecarName = runtime.startsWith('win')
  ? `RpaDevAssistant.Api-${triple}.exe`
  : `RpaDevAssistant.Api-${triple}`;
const sidecarPath = join(sidecarDir, sidecarName);
rmSync(sidecarPath, { force: true });
cpSync(publishedExecutable, sidecarPath);

const pdbPath = join(publishDir, 'RpaDevAssistant.Api.pdb');
if (existsSync(pdbPath)) {
  cpSync(pdbPath, join(sidecarDir, `RpaDevAssistant.Api-${triple}.pdb`));
}

console.log(`Backend sidecar published to ${sidecarPath}`);
