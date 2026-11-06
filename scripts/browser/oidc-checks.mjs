import https from 'node:https';
import { generateKeyPairSync } from 'node:crypto';
import { createInterface } from 'node:readline';
import Provider from 'oidc-provider';
import { chromium } from 'playwright-core';

// Isolated, in-memory teaching provider. No production imports, persistent accounts or public tenant.
const input = createInterface({ input: process.stdin });
const queue = []; const waiters = [];
input.on('line', line => { const value = JSON.parse(line); if (waiters.length) waiters.shift()(value); else queue.push(value); });
const next = () => queue.length ? Promise.resolve(queue.shift()) : new Promise(resolve => waiters.push(resolve));
const emit = value => process.stdout.write('OIDC35:' + JSON.stringify(value) + '\n');
const config = await next();
let fixture = { email: 'same@example.test' };
let fault = ''; let provider; let browser; let step = 'provider startup';
const counts = { token: 0, authorization: 0, verifier: 0, privilegedClaim: false, sameEmail: false };
const signing = generateKeyPairSync('rsa', { modulusLength: 2048 });
const jwk = signing.privateKey.export({ format: 'jwk' }); jwk.kid = 'test35'; jwk.alg = 'RS256'; jwk.use = 'sig';
const server = https.createServer({ cert: config.cert, key: config.key }, async (req, res) => {
  try {
    if (req.url.startsWith('/interaction/')) {
      const details = await provider.interactionDetails(req, res);
      if (req.method === 'POST') {
        let body = ''; for await (const chunk of req) body += chunk;
        const subject = new URLSearchParams(body).get('subject');
        if (subject === 'cancel') return await provider.interactionFinished(req, res, { error: 'access_denied', error_description: 'Test cancellation' }, { mergeWithLastSubmission: false });
        if (!['linked','unlinked','pending','locked','mfa'].includes(subject)) { res.writeHead(400); return res.end(); }
        return await provider.interactionFinished(req, res, { login: { accountId: subject } }, { mergeWithLastSubmission: false });
      }
      if (details.prompt.name === 'consent') {
        const grant = new provider.Grant({ accountId: details.session.accountId, clientId: details.params.client_id });
        if (details.prompt.details.missingOIDCScope) grant.addOIDCScope(details.prompt.details.missingOIDCScope.join(' '));
        if (details.prompt.details.missingOIDCClaims) grant.addOIDCClaims(details.prompt.details.missingOIDCClaims);
        const grantId = await grant.save();
        return await provider.interactionFinished(req, res, { consent: { grantId } }, { mergeWithLastSubmission: true });
      }
      res.setHeader('content-type', 'text/html'); res.setHeader('cache-control', 'no-store');
      return res.end('<!doctype html><title>Isolated OIDC provider</title><h1>Test identity provider</h1><form method="post">' + ['linked','unlinked','pending','locked','mfa','cancel'].map(s => `<button name="subject" value="${s}">${s}</button>`).join('') + '</form>');
    }
    return provider.callback()(req, res);
  } catch { res.writeHead(500); res.end('Test provider failure'); }
});
await new Promise(resolve => server.listen(config.port ?? 0, '127.0.0.1', resolve));
const authority = `https://127.0.0.1:${server.address().port}`;
provider = new Provider(authority, {
  clients: [{ client_id: 'university-test', client_secret: config.secret, redirect_uris: [config.address + '/signin-oidc'], response_types: ['code'], grant_types: ['authorization_code'], token_endpoint_auth_method: 'client_secret_post', id_token_signed_response_alg: 'RS256' }],
  jwks: { keys: [jwk] }, cookies: { keys: [config.secret] },
  features: { devInteractions: { enabled: false } }, pkce: { required: () => true },
  interactions: { url: (_ctx, interaction) => '/interaction/' + interaction.uid },
  claims: { openid: ['sub', 'email', 'role'] },
  findAccount: async (_ctx, id) => ({ accountId: id, claims: async () => ({ sub: id, email: fixture.email, role: 'StudentAffairs' }) })
});
provider.use(async (ctx, nextMiddleware) => {
  if (ctx.path === '/auth') counts.authorization++;
  if (ctx.path === '/token') { counts.token++; if (fault === 'unavailable') { ctx.status = 503; ctx.body = { error: 'temporarily_unavailable' }; return; } }
  await nextMiddleware();
  if (ctx.path === '/token' && ctx.body?.id_token) { const { decodeJwt } = await import('jose'); const original = decodeJwt(ctx.body.id_token); counts.privilegedClaim = original.role === 'StudentAffairs'; counts.sameEmail = original.email === fixture.email; }
  if (ctx.path === '/token' && ctx.body?.id_token && ['nonce','missing-nonce','issuer','audience','signature','expired','sub-empty','sub-multiple'].includes(fault)) {
    // Negative protocol fixtures only: successful E2E uses the provider's unmodified signed token.
    const { SignJWT, decodeJwt, importJWK } = await import('jose');
    const claims = decodeJwt(ctx.body.id_token);
    if (fault === 'sub-empty') claims.sub = '';
    if (fault === 'sub-multiple') claims.sub = ['linked', 'unlinked'];
    if (fault === 'nonce') claims.nonce = 'wrong-nonce';
    if (fault === 'missing-nonce') delete claims.nonce;
    if (fault === 'issuer') claims.iss = 'https://other.example.test';
    if (fault === 'audience') claims.aud = 'other-client';
    if (fault === 'expired') { claims.exp = Math.floor(Date.now()/1000)-300; claims.iat = claims.exp-300; }
    const key = fault === 'signature' ? generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey : await importJWK(jwk, 'RS256');
    ctx.body.id_token = await new SignJWT(claims).setProtectedHeader({ alg: 'RS256', kid: jwk.kid }).sign(key);
  }
});
emit({ ready: authority });
if (process.argv.includes("--serve")) await new Promise(() => {}); // Explicit manual lab; stop with Ctrl+C.
fixture = await next();
if (fixture.run !== true) throw new Error('Expected isolated run');
const check = (ok, name) => { if (!ok) { step += ':' + name; throw new Error('check'); } };
async function label(name) { step = name; fault = name; emit({ test: name }); await next(); }
async function context() { return await browser.newContext(); }
async function me(page) { return await page.evaluate(async () => { const r = await fetch('/accounts/me'); return { status: r.status, body: r.ok ? await r.json() : null }; }); }
async function begin(page) {
  await page.goto(config.address); await page.locator('#external-login').waitFor({ state: 'visible' });
  await page.waitForFunction(() => !document.querySelector('#external-login').disabled);
  await page.locator('#external-login').click();
}
async function flow(name, subject = 'linked', success = false) {
  await label(name); const ctx = await context(); const page = await ctx.newPage();
  let codeFlow = false; let browserToken = false;
  page.on('request', r => { const u = new URL(r.url()); if (u.origin === authority && u.pathname === '/auth') codeFlow = u.searchParams.get('response_type') === 'code' && u.searchParams.get('code_challenge_method') === 'S256' && !!u.searchParams.get('code_challenge'); if (u.origin === authority && u.pathname === '/token') browserToken = true; });
  if (name === 'state' || name === 'correlation') await page.route(config.address + '/signin-oidc', async route => {
    if (name === 'state') { const data = new URLSearchParams(route.request().postData()); data.set('state', 'invalid-state'); await route.continue({ postData: data.toString() }); }
    else { await ctx.clearCookies({ name: /Correlation/ }); const headers = await route.request().allHeaders(); headers.cookie = (headers.cookie ?? '').split(';').filter(x => !x.includes('Correlation')).join(';'); await route.continue({ headers }); }
  });
  await begin(page); step = name + ':provider-page'; await page.getByRole('button', { name: subject, exact: true }).click();
  step = name + ':callback'; await page.waitForURL(u => u.origin === config.address && u.pathname === '/');
  step = name + ':outcome'; const result = await me(page); check(result.status === (success ? 200 : 401), 'local authentication outcome');
  const cookies = await ctx.cookies(config.address); check(!cookies.some(c => c.name === '__Host-University.External'), 'external cookie cleaned');
  check(success || !cookies.some(c => c.name === '__Host-University.Account'), 'no application cookie on failure');
  check(codeFlow && !browserToken, 'code S256 and backend exchange');
  if (success) {
    check(result.body.id === fixture.accountId, 'mapped local account');
    check(counts.privilegedClaim && counts.sameEmail, 'provider actually issued email and elevated role fixture');
    const conflict = await page.evaluate(async () => { const t = await (await fetch('/accounts/antiforgery')).json(); return (await fetch('/accounts/external/login', { method: 'POST', headers: { 'X-CSRF-TOKEN': t.requestToken }, body: new URLSearchParams({ returnUrl: '/' }) })).status; });
    check(conflict === 409 && (await me(page)).status === 200, 'existing session not switched');
    const proof = await page.evaluate(async () => (await fetch('/test/oidc-local')).json());
    check(proof.instructor && !proof.studentAffairs && !proof.savedTokens, 'local roles and no saved tokens');
    check(await page.evaluate(() => localStorage.length === 0 && sessionStorage.length === 0), 'no browser token storage');
    // Local logout leaves the provider session; a second challenge completes without provider login.
    const before = (await ctx.cookies(authority)).filter(c => c.name.includes('session'));
    await page.locator('#logout').waitFor({ state: 'visible' }); await page.locator('#logout').click();
    await page.waitForFunction(() => !document.querySelector('#login-form').hidden && !document.querySelector('#logout').disabled);
    check((await me(page)).status === 401 && before.length > 0, 'local logout only');
    await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === '/accounts/external/callback'), begin(page)]); await page.waitForURL(u => u.origin === config.address && u.pathname === '/');
    check((await me(page)).status === 200, 'provider session survives local logout');
  }
  await ctx.close(); emit({ pass: name });
}
try {
  browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true, args: ['--ignore-certificate-errors-spki-list=' + config.pin] });
  await flow('success', 'linked', true);
  for (const [name, subject] of [['unlinked','unlinked'],['pending','pending'],['locked','locked'],['mfa','mfa'],['cancel','cancel']]) await flow(name, subject);
  for (const name of ['state','correlation','nonce','missing-nonce','issuer','audience','signature','expired','sub-empty','sub-multiple','verifier','replay','unavailable']) await flow(name);
  await label('return-url');
  const ctx = await context(); const page = await ctx.newPage(); await page.goto(config.address);
  for (const token of [null, 'invalid']) {
    const status = await page.evaluate(async token => (await fetch('/accounts/external/login', { method: 'POST', headers: token ? { 'X-CSRF-TOKEN': token } : {}, body: new URLSearchParams({ returnUrl: '/' }) })).status, token);
    check(status === 400, 'start CSRF rejected');
  }
  for (const path of ['https://other.example.test','//other.example.test','/not-allowed']) {
    const status = await page.evaluate(async path => { const t = await (await fetch('/accounts/antiforgery')).json(); return (await fetch('/accounts/external/login', { method: 'POST', headers: { 'X-CSRF-TOKEN': t.requestToken }, body: new URLSearchParams({ returnUrl: path }) })).status; }, path);
    check(status === 400, 'return URL rejected');
  }
  check((await me(page)).status === 401, 'no returnURL login');
  const badRedirect = new URL('/auth', authority);
  badRedirect.search = new URLSearchParams({ client_id: 'university-test', response_type: 'code', scope: 'openid', redirect_uri: 'https://other.example.test/callback', code_challenge_method: 'S256', code_challenge: 'x'.repeat(43), state: 'test-state', nonce: 'test-nonce' });
  const deniedRedirect = await page.goto(badRedirect.href);
  check(deniedRedirect.status() === 400 && new URL(page.url()).origin === authority, 'provider rejects unregistered redirect URI');
  check(!(await ctx.cookies(config.address)).some(c => c.name === '__Host-University.Account'), 'redirect denial no application cookie'); await ctx.close(); emit({ pass: 'return-url' });
  emit({ done: true, tokenCalls: counts.token });
} catch { emit({ failure: step }); process.exitCode = 1; }
finally { if (browser) await browser.close(); server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); input.close(); process.stdin.destroy(); }
