import { chromium } from "playwright-core";
import fs from "node:fs";
const input = JSON.parse(fs.readFileSync(0, "utf8"));
let browser, step = "launch";
const check = (condition, label) => { if (!condition) throw new Error(label); };
const edge = process.env.WEB_TEST_EDGE ?? "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
try {
    browser = await chromium.launchPersistentContext(input.profile, { executablePath: edge, headless: true,
        args: [`--ignore-certificate-errors-spki-list=${input.pin}`] });
    const context = browser; // No global ignoreHTTPSErrors / certificate bypass.
    if (!input.labOnly) {
    const page = await context.newPage();
    const consoleLines = [], urls = [], secrets = [input.password];
    page.on("console", m => consoleLines.push(m.text())); page.on("pageerror", e => consoleLines.push(e.message));
    page.on("request", r => urls.push(r.url()));
    const waitText = (id, text) => page.waitForFunction(([id, text]) => document.getElementById(id).textContent.includes(text), [id, text]);
    const ready = () => page.waitForFunction(() => !document.getElementById("check-session").disabled);
    const fill = async password => { await page.locator("#email").fill(input.email); await page.locator("#password").fill(password); };
    const fetchStatus = (path, options = {}) => page.evaluate(async ([path, options]) => (await fetch(path, { credentials: "same-origin", ...options })).status, [path, options]);
    step = "anonymous/static/CSP";
    const html = await page.goto(input.address); await waitText("session", "Giriş yapılmamış"); await ready();
    const csp = html.headers()["content-security-policy"];
    check(csp.includes("script-src 'self'") && csp.includes("frame-ancestors 'none'") && !csp.includes("unsafe-"), "CSP");
    check(html.headers()["cache-control"] === "no-store", "static no-store");
    check(await page.locator("#external-form").isHidden(), "disabled provider hidden; local login remains available");
    await page.waitForFunction(() => document.querySelectorAll("#courses li").length > 0);
    check(await page.locator("#courses").textContent().then(t => t.includes("<img src=x onerror=window.web34Xss=1>")), "literal DB fixture");
    check(await page.locator("#courses img, #courses script").count() === 0 && await page.evaluate(() => !window.web34Xss), "safe DOM independently of CSP execution blocking");
    for (const path of ["/docs/web34-canary.txt", "/scripts/web34-canary.txt", "/sql/web34-canary.txt", "/.local/accounts/messages/web34-canary.txt", "/.local/accounts/keys/web34-canary.txt", "/docs/", "/open", "/closed"])
        check(await fetchStatus(path) === 404, "private/static/lab route absent");
    await page.screenshot({ path: input.screenshot, fullPage: true });
    step = "wrong password/401";
    await fill("wrong-web34-password"); await page.locator("#login").click(); await ready();
    check((await page.locator("#account-message").textContent()).includes("Giriş yapılamadı"), "common401 UI");
    check(!(await context.cookies()).some(c => c.name === "__Host-University.Account"), "failure no auth cookie");
    step = "login/single-flight/new CSRF";
    let loginCount = 0, release, reached;
    const pending = new Promise(resolve => release = resolve), reachedLogin = new Promise(resolve => reached = resolve);
    await page.route("**/accounts/login", async route => { loginCount++; reached(); await pending; await route.continue(); });
    const csrfValues = [];
    page.on("response", async response => { if (response.url().endsWith("/accounts/antiforgery") && response.status() === 200) { const value = (await response.json()).requestToken; csrfValues.push(value); secrets.push(value); } });
    await fill(input.password);
    await page.evaluate(() => { const f = document.getElementById("login-form"); f.dispatchEvent(new Event("submit", { cancelable: true })); f.dispatchEvent(new Event("submit", { cancelable: true })); });
    await reachedLogin; check(loginCount === 1 && await page.locator("#login").isDisabled(), "single flight guard"); release();
    await waitText("session", "Giriş yapıldı"); await ready(); await page.unroute("**/accounts/login");
    check(await page.locator("#account").textContent() === input.email && await page.locator("#password").inputValue() === "", "account and cleared password");
    check(csrfValues.length === 2 && csrfValues[0] !== csrfValues[1], "new identity-bound request token after login");
    const authCookie = (await context.cookies()).find(c => c.name === "__Host-University.Account");
    const csrfCookie = (await context.cookies()).find(c => c.name === "__Host-University.Csrf");
    for (const cookie of [authCookie, csrfCookie]) { check(cookie && cookie.httpOnly && cookie.secure && cookie.sameSite === "Strict" && cookie.path === "/" && !cookie.domain.startsWith("."), "cookie flags/scope"); secrets.push(cookie.value); }
    check(await page.evaluate(() => !document.cookie.includes("__Host-University.Account") && !document.cookie.includes("__Host-University.Csrf")), "HttpOnly invisible to JS");
    check(await fetchStatus("/accounts/me") === 200, "fetch carries HttpOnly cookie");
    await page.reload(); await waitText("session", "Giriş yapıldı"); await ready();
    step = "CSRF and backend authorization";
    check(await fetchStatus("/accounts/logout", { method: "POST" }) === 400, "missing CSRF logout");
    check(await fetchStatus("/accounts/logout", { method: "POST", headers: { "X-CSRF-TOKEN": "broken" } }) === 400, "bad CSRF logout");
    check(await fetchStatus("/accounts/me") === 200, "invalid logout preserved session");
    check(await fetchStatus("/courses", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: "NO-WRITE", name: "Denied", credits: 3 }) }) === 403, "direct HTTP role denial");
    check(await fetchStatus("/students/2147483647/enrollments") === 403, "resource role denial outside UI");
    step = "network/500/403 UI";
    await page.route("**/accounts/me", route => route.fulfill({ status: 500, contentType: "application/problem+json", body: '{"title":"internal"}' }));
    await page.locator("#check-session").click(); await ready(); await waitText("session", "doğrulanamadı");
    check(await page.locator("#account").textContent() === "", "500 clears stale display without false logout"); await page.unroute("**/accounts/me");
    await page.route("**/accounts/me", route => route.abort()); await page.locator("#check-session").click(); await ready();
    check((await page.locator("#account-message").textContent()).includes("Bağlantı kurulamadı"), "network is not logout"); await page.unroute("**/accounts/me");
    await page.locator("#check-session").click(); await ready(); await waitText("session", "Giriş yapıldı");
    let logoutHeader;
    page.on("request", r => { if (r.url().endsWith("/accounts/logout")) logoutHeader = r.headers()["x-csrf-token"]; });
    await page.locator("#logout").click(); await ready(); await waitText("session", "Giriş yapılmamış");
    check(logoutHeader && logoutHeader !== csrfValues[0], "logout current token");
    check(await page.locator("#account").textContent() === "" && await fetchStatus("/accounts/me") === 401, "logout cleared data and cookie");
    step = "safe error text/rate-limit UI";
    const payload = '<img src=x onerror="window.web34Error=1">';
    for (const [status, title, expected] of [[400, payload, payload], [403, "", "yetkiniz"], [429, "", "7 saniye"], [500, "", "ulaşılamıyor"]]) {
        await page.route("**/accounts/login", route => route.fulfill({ status, headers: { "Content-Type": "application/problem+json", ...(status === 429 ? { "Retry-After": "7" } : {}) }, body: JSON.stringify({ title }) }));
        await fill(input.password); await page.locator("#login").click(); await ready();
        check((await page.locator("#account-message").textContent()).includes(expected), "status-specific UI");
        check(await page.locator("#account-message img").count() === 0 && await page.evaluate(() => !window.web34Error), "error stays text");
        check(await page.locator("#password").inputValue() === "", "failure clears password"); await page.unroute("**/accounts/login");
    }
    step = "real browser rate limit";
    await page.evaluate(async password => {
        const token = (await (await fetch("/accounts/antiforgery", { credentials: "same-origin" })).json()).requestToken;
        for (let i = 0; i < 10; i++) await fetch("/accounts/login", { method: "POST", credentials: "same-origin", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token }, body: JSON.stringify({ email: "absent-web34-limit@example.test", password }) });
    }, input.password);
    await fill(input.password);
    const limited = page.waitForResponse(r => r.url().endsWith("/accounts/login"));
    await page.locator("#login").click(); check((await limited).status() === 429, "real endpoint returns429");
    await ready(); check((await page.locator("#account-message").textContent()).includes("Çok fazla deneme"), "real429 rendered, not success");
    check(!(await context.cookies()).some(c => c.name === "__Host-University.Account"), "rate limit no auth cookie");
    step = "storage/secret boundaries";
    check(await page.evaluate(async () => localStorage.length === 0 && sessionStorage.length === 0 && (await indexedDB.databases()).length === 0), "no persistent app storage");
    check(!urls.some(u => /dev-token|refresh|password=|token=/.test(u)), "no token API or credentials in URLs");
    check(consoleLines.every(line => secrets.every(secret => !line.includes(secret))) && urls.every(url => secrets.every(secret => !url.includes(secret))), "no secret console/URL leak");
    await page.setViewportSize({ width: 390, height: 844 });
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), "mobile no horizontal overflow");
    }
    step = "real browser CORS";
    const lab = await context.newPage(); await lab.goto(input.allowed);
    const fetchLab = (url, preflight = false) => lab.evaluate(async ([url, preflight]) => { try { const r = await fetch(url, { credentials: "omit", ...(preflight ? { headers: { "X-Lab": "test" } } : {}) }); return r.ok; } catch (e) { return e instanceof TypeError ? false : "unexpected"; } }, [url, preflight]);
    check(await fetchLab(input.address + "/courses") === false, "main app no CORS read");
    check(await fetchLab(input.api + "/closed") === false, "simple GET blocked from reading");
    check(await fetchLab(input.api + "/closed", true) === false, "closed preflight fails");
    check(await fetchLab(input.api + "/open", true) === true, "allowlisted origin preflight and GET");
    await lab.goto(input.denied); check(await fetchLab(input.api + "/open") === false, "other origin not allowed");
    if (!input.labOnly) console.log("PASS: Edge real browser anonymous/login/reload/logout, single-flight, fresh CSRF and failed logout, HttpOnly/Secure/Strict/path, safe literal DB/error DOM, no token storage/console/URL, static boundaries, mobile layout; real UI429 and controlled UI403/500/network");
    console.log("PASS: browser enforces no-CORS and exact-origin CORS with distinct preflight; ports are cross-origin, not a cross-site test");
    await context.close();
} catch (error) { console.error(`FAIL: web browser step '${step}' (${error.name}); secret-bearing details suppressed`); process.exitCode = 1; }
finally { if (browser) await browser.close(); }
