#!/usr/bin/env node

import { pathToFileURL } from 'node:url';

export function evaluateQualityGate(response, options = {}) {
  const minimumScore = Number.isFinite(options.minimumScore) ? options.minimumScore : 70;
  const failOnError = options.failOnError ?? true;
  const failOnCritical = options.failOnCritical ?? true;
  const score = Number(response?.qualityScore?.score);
  const analysis = response?.analysis ?? {};
  const criticalCount = countSeverity(analysis, 'critical');
  const errorCount = countSeverity(analysis, 'error');
  const failures = [];

  if (!Number.isFinite(score)) {
    failures.push('Analysis response does not contain a numeric quality score.');
  } else if (score < minimumScore) {
    failures.push(`Quality score ${score} is below the minimum ${minimumScore}.`);
  }
  if (failOnCritical && criticalCount > 0) failures.push(`${criticalCount} Critical finding(s) detected.`);
  if (failOnError && errorCount > 0) failures.push(`${errorCount} Error finding(s) detected.`);

  return {
    passed: failures.length === 0,
    score: Number.isFinite(score) ? score : null,
    minimumScore,
    criticalCount,
    errorCount,
    totalFindings: Number(analysis.totalFindings ?? analysis.findings?.length ?? 0),
    failures,
  };
}

function countSeverity(analysis, severity) {
  const property = `${severity}Count`;
  if (Number.isFinite(Number(analysis?.[property]))) return Number(analysis[property]);
  return (analysis?.findings ?? []).filter(
    (finding) => String(finding?.severity ?? '').toLowerCase() === severity,
  ).length;
}

function parseArguments(argv) {
  const values = new Map();
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (!argument.startsWith('--')) continue;
    const [name, inlineValue] = argument.slice(2).split('=', 2);
    if (inlineValue !== undefined) values.set(name, inlineValue);
    else if (argv[index + 1] && !argv[index + 1].startsWith('--')) values.set(name, argv[++index]);
    else values.set(name, 'true');
  }
  return values;
}

function booleanValue(value, fallback) {
  if (value === undefined) return fallback;
  return !['false', '0', 'no'].includes(String(value).toLowerCase());
}

async function run() {
  const args = parseArguments(process.argv.slice(2));
  const projectPath = args.get('project') ?? process.env.UIPATH_PROJECT_PATH;
  const apiUrl = (args.get('api-url') ?? process.env.RPA_API_URL ?? 'http://127.0.0.1:5123').replace(/\/$/, '');
  const profileId = args.get('profile') ?? process.env.RPA_PROFILE_ID ?? 'default';
  const minimumScore = Number(args.get('minimum-score') ?? process.env.RPA_MINIMUM_SCORE ?? 70);

  if (!projectPath) throw new Error('UiPath project path is required. Use --project or UIPATH_PROJECT_PATH.');
  if (!Number.isFinite(minimumScore) || minimumScore < 0 || minimumScore > 100) {
    throw new Error('Minimum score must be between 0 and 100.');
  }

  const response = await fetch(`${apiUrl}/api/uipath/projects/analyze`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ projectPath, profileId, locale: 'en' }),
  });
  if (!response.ok) throw new Error(`Analysis API returned HTTP ${response.status}: ${await response.text()}`);

  const analysis = await response.json();
  const result = evaluateQualityGate(analysis, {
    minimumScore,
    failOnError: booleanValue(args.get('fail-on-error') ?? process.env.RPA_FAIL_ON_ERROR, true),
    failOnCritical: booleanValue(args.get('fail-on-critical') ?? process.env.RPA_FAIL_ON_CRITICAL, true),
  });
  console.log(JSON.stringify({ projectName: analysis.projectName, profileId, ...result }, null, 2));
  if (!result.passed) process.exitCode = 2;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  run().catch((error) => {
    console.error(`RPA Dev Assistant quality gate failed: ${error.message}`);
    process.exitCode = 1;
  });
}
