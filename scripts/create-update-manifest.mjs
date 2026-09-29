import { readdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const repository = process.env.GITHUB_REPOSITORY;
const tag = process.env.GITHUB_REF_NAME;
if (!repository || !tag || !tag.startsWith('v')) {
  throw new Error('GITHUB_REPOSITORY and a v-prefixed GITHUB_REF_NAME are required.');
}

const bundleDirectory = path.resolve('frontend/src-tauri/target/release/bundle/nsis');
const files = await readdir(bundleDirectory);
const updaterName = files.find((file) => file.endsWith('-setup.exe') && files.includes(`${file}.sig`))
  ?? files.find((file) => file.endsWith('.nsis.zip') && files.includes(`${file}.sig`));
if (!updaterName) {
  throw new Error('A signed NSIS updater and signature were not produced.');
}

const signature = (await readFile(path.join(bundleDirectory, `${updaterName}.sig`), 'utf8')).trim();
if (!signature) throw new Error('Updater signature is empty.');

// GitHub release assets normalize whitespace in uploaded file names to dots.
const releaseAssetName = updaterName.replace(/\s+/g, '.');
const encodedAsset = releaseAssetName.split('/').map(encodeURIComponent).join('/');
const manifest = {
  version: tag.slice(1),
  notes: `RPA Dev Assistant ${tag}`,
  pub_date: new Date().toISOString(),
  platforms: {
    'windows-x86_64': {
      signature,
      url: `https://github.com/${repository}/releases/download/${encodeURIComponent(tag)}/${encodedAsset}`,
    },
  },
};

await writeFile(path.join(bundleDirectory, 'latest.json'), `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
