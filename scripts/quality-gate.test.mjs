import assert from 'node:assert/strict';
import test from 'node:test';
import { evaluateQualityGate } from './quality-gate.mjs';

test('passes when score and severities satisfy the policy', () => {
  const result = evaluateQualityGate({ qualityScore: { score: 91 }, analysis: { totalFindings: 2, criticalCount: 0, errorCount: 0 } }, { minimumScore: 80 });
  assert.equal(result.passed, true);
});

test('fails below the minimum score', () => {
  const result = evaluateQualityGate({ qualityScore: { score: 79 }, analysis: { criticalCount: 0, errorCount: 0 } }, { minimumScore: 80 });
  assert.equal(result.passed, false);
  assert.match(result.failures[0], /below the minimum/);
});

test('fails on Error and Critical findings by default', () => {
  const result = evaluateQualityGate({ qualityScore: { score: 100 }, analysis: { criticalCount: 1, errorCount: 2 } });
  assert.equal(result.passed, false);
  assert.equal(result.failures.length, 2);
});

test('allows severity gates to be disabled explicitly', () => {
  const result = evaluateQualityGate({ qualityScore: { score: 100 }, analysis: { findings: [{ severity: 'Critical' }, { severity: 'Error' }] } }, { failOnCritical: false, failOnError: false });
  assert.equal(result.passed, true);
  assert.equal(result.criticalCount, 1);
  assert.equal(result.errorCount, 1);
});
