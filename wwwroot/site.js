"use strict";
(() => {
    const byId = id => document.getElementById(id);
    const form = byId("login-form"), password = byId("password"), account = byId("account");
    const message = byId("account-message"), session = byId("session");
    let csrf = null, busy = false, courseBusy = false, providerEnabled = false, externalStarting = false;
    const externalFailure = new URLSearchParams(location.search).get("external") === "failed";
    if (externalFailure) history.replaceState(null, "", location.pathname);
    const request = (path, options = {}) => fetch(path, { ...options, credentials: "same-origin", cache: "no-store" });
    async function failure(response) {
        if (response.status === 401) return "Giriş yapılamadı veya oturumunuz artık geçerli değil.";
        if (response.status === 403) return "Bu işlem için yetkiniz bulunmuyor.";
        if (response.status === 429) {
            const seconds = response.headers.get("Retry-After");
            return /^\d{1,6}$/.test(seconds ?? "") ? `Çok fazla deneme. Yaklaşık ${seconds} saniye sonra yeniden deneyebilirsiniz.` : "Çok fazla deneme. Lütfen daha sonra yeniden deneyin.";
        }
        if (response.status >= 500) return "Hizmete şu anda ulaşılamıyor. İşlemin sonucunu yeniden kontrol edin.";
        let body; try { body = await response.json(); } catch { /* No HTML error rendering. */ }
        return typeof body?.title === "string" ? body.title : "İstek tamamlanamadı. Bilgilerinizi kontrol edin.";
    }
    async function token() {
        const r = await request("/accounts/antiforgery");
        if (!r.ok) throw new Error(await failure(r));
        csrf = (await r.json()).requestToken;
        if (typeof csrf !== "string" || !csrf) throw new Error("İstek doğrulaması hazırlanamadı.");
    }
    function showAnonymous() {
        account.textContent = ""; account.hidden = true; form.hidden = false;
        byId("external-form").hidden = !providerEnabled;
        byId("logout").hidden = true; session.textContent = "Giriş yapılmamış.";
    }
    async function me() {
        account.textContent = ""; account.hidden = true;
        session.textContent = "Oturum kontrol ediliyor…";
        let r;
        try { r = await request("/accounts/me"); }
        catch (error) { session.textContent = "Oturum durumu doğrulanamadı."; throw error; }
        if (r.status === 401) { showAnonymous(); return; }
        if (!r.ok) {
            account.textContent = ""; account.hidden = true;
            session.textContent = "Oturum durumu doğrulanamadı.";
            throw new Error(await failure(r));
        }
        const user = await r.json();
        account.textContent = typeof user.email === "string" ? user.email : "Hesabınız";
        account.hidden = false; form.hidden = true; byId("external-form").hidden = true; byId("logout").hidden = false;
        session.textContent = "Giriş yapıldı.";
    }
    async function action(work) {
        if (busy) return;
        busy = true; message.textContent = "İşlem sürüyor…";
        for (const id of ["login", "logout", "check-session", "external-login"]) byId(id).disabled = true;
        try { await work(); message.textContent = ""; }
        catch (error) { message.textContent = error instanceof TypeError ? "Bağlantı kurulamadı. Oturum durumunu yeniden kontrol edin." : error.message; }
        finally { busy = false; for (const id of ["login", "logout", "check-session", "external-login"]) byId(id).disabled = false; }
    }
    form.addEventListener("submit", event => {
        event.preventDefault();
        void action(async () => {
            try {
                await token();
                const r = await request("/accounts/login", { method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf }, body: JSON.stringify({ email: byId("email").value, password: password.value }) });
                if (!r.ok) throw new Error(await failure(r));
                csrf = null; await me(); await token();
            } finally { password.value = ""; }
        });
    });
    byId("external-form").addEventListener("submit", event => {
        event.preventDefault();
        if (externalStarting) return;
        void action(async () => {
            await token(); byId("external-csrf").value = csrf; password.value = "";
            externalStarting = true; HTMLFormElement.prototype.submit.call(byId("external-form"));
        });
    });
    byId("logout").addEventListener("click", () => void action(async () => {
        if (!csrf) await token();
        const r = await request("/accounts/logout", { method: "POST", headers: { "X-CSRF-TOKEN": csrf } });
        if (!r.ok) throw new Error(await failure(r));
        csrf = null; showAnonymous(); password.value = ""; byId("email").value = "";
        await me(); await token();
    }));
    byId("check-session").addEventListener("click", () => void action(async () => { csrf = null; await me(); await token(); }));
    async function courses() {
        if (courseBusy) return;
        courseBusy = true; byId("reload-courses").disabled = true;
        byId("course-message").textContent = "Dersler yükleniyor…";
        try {
            const r = await request("/courses"); if (!r.ok) throw new Error(await failure(r));
            const data = await r.json(); const list = document.createDocumentFragment();
            for (const course of data) {
                const row = document.createElement("li"), code = document.createElement("span"), name = document.createElement("span");
                code.className = "course-code"; name.className = "course-name";
                code.textContent = course.code; name.textContent = course.name;
                row.append(code, name); list.append(row);
            }
            byId("courses").replaceChildren(list);
            byId("course-message").textContent = data.length ? `${data.length} ders listeleniyor.` : "Henüz ders bulunmuyor.";
        } catch (error) { byId("course-message").textContent = error instanceof TypeError ? "Derslere ulaşılamadı. Yeniden deneyebilirsiniz." : error.message; }
        finally { courseBusy = false; byId("reload-courses").disabled = false; }
    }
    byId("reload-courses").addEventListener("click", () => void courses());
    void action(async () => {
        const options = await request("/accounts/external/options");
        if (options.ok) { const provider = await options.json(); providerEnabled = provider.enabled === true; byId("external-login").textContent = provider.displayName; }
        await me(); await token();
        if (externalFailure) throw new Error("Harici giriş tamamlanamadı. Yerel hesabınızı veya sağlayıcı erişimini kontrol edin.");
    }); void courses();
})();
