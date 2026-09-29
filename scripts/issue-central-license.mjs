import { readFileSync, writeFileSync } from 'node:fs';
import { constants, createPrivateKey, sign } from 'node:crypto';

const args = Object.fromEntries(process.argv.slice(2).map((item) => {
  const [key, ...value] = item.replace(/^--/, '').split('=');
  return [key, value.join('=')];
}));

for (const required of ['private-key', 'output', 'license-id', 'customer-id', 'expires']) {
  if (!args[required]) throw new Error(`--${required}=... is required.`);
}

const issuedAtUtc = new Date().toISOString();
const expiresAtUtc = new Date(args.expires).toISOString();
if (new Date(expiresAtUtc) <= new Date(issuedAtUtc)) throw new Error('--expires must be in the future.');
const payload = {
  licenseId: args['license-id'],
  customerId: args['customer-id'],
  plan: args.plan ?? 'Team',
  issuedAtUtc,
  expiresAtUtc,
  maxTenants: Number(args['max-tenants'] ?? 1),
  maxMonthlyAnalyses: Number(args['max-monthly-analyses'] ?? 1000),
};
if (!Number.isInteger(payload.maxTenants) || payload.maxTenants <= 0 || !Number.isInteger(payload.maxMonthlyAnalyses) || payload.maxMonthlyAnalyses <= 0) {
  throw new Error('License limits must be positive integers.');
}
const payloadJson = JSON.stringify(payload);
const signature = sign('sha256', Buffer.from(payloadJson), {
  key: createPrivateKey(readFileSync(args['private-key'], 'utf8')),
  padding: constants.RSA_PKCS1_PSS_PADDING,
  saltLength: 32,
});
writeFileSync(args.output, JSON.stringify({ payload, signature: signature.toString('base64') }, null, 2));
console.log(`Signed central license written to ${args.output}.`);
