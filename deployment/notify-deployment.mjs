// Generated from get-it-done.deployment/templates. No token is logged.
import { readFileSync } from 'node:fs';
const target = JSON.parse(readFileSync(new URL('./uat.json', import.meta.url), 'utf8'));
const token = process.env.UAT_DEPLOY_DISPATCH_TOKEN?.trim();
if (!token) throw Error('Set UAT_DEPLOY_DISPATCH_TOKEN: a fine-grained PAT for get-it-done.deployment with Contents write permission.');
const payload = { event_type: 'deploy-uat', client_payload: { repository: 'mikaelarey/' + target.repository, sourceCommit: process.env.GITHUB_SHA, runId: process.env.GITHUB_RUN_ID, runAttempt: process.env.GITHUB_RUN_ATTEMPT } };
const response = await fetch('https://api.github.com/repos/mikaelarey/get-it-done.deployment/dispatches', { method: 'POST', headers: { Accept: 'application/vnd.github+json', 'Content-Type': 'application/json', Authorization: 'Bearer ' + token, 'X-GitHub-Api-Version': '2022-11-28' }, body: JSON.stringify(payload) });
if (response.status !== 204) throw Error(`Central deployment notification failed (HTTP ${response.status}). Check the token's selected repository and permissions.`);
console.log('Requested deployment in mikaelarey/get-it-done.deployment. Track the live result in that repository’s Actions page.');
