# Gün 34 — Aynı origin web istemcisi

XSS, güvenilmeyen verinin çalıştırılabilir içerik olmasıdır; CSRF, tarayıcının
kendiliğinden taşıdığı kimlikle istenmeyen işlem yaptırılmasıdır. `site.js` hesap,
ders ve hata metnini textContent/createElement ile yazar. HTML birleştirme, eval,
inline olay kodu ve üçüncü taraf script yoktur. CSP ek savunmadır; veri HTML
elemanına çevrilmediği ayrıca DOM assertion'ıyla ölçülür. CSP yalnız statik HTML
cevabında self script/style/connect, object-src none ve frame-ancestors none uygular.
API politikalarını global değiştirmez.

Secure cookie yalnız HTTPS'te gönderilir. HttpOnly, JS'nin cookie değerini okumasını
engeller; fetch'e eklenmesini ve XSS'nin kullanıcı adına işlem yapmasını engellemez.
SameSite Strict bu aynı site akışına uygundur ama CSRF doğrulamasının yerine geçmez.
Her iki __Host cookie Path=/, Domain belirtilmemiş, Secure/HttpOnly/Strict'tir.
Path güçlü güvenlik izolasyonu değildir. Auth cookie tarayıcı tarafından taşınır;
CSRF request token JSON'dan alınıp yalnız JS belleğinde X-CSRF-TOKEN header'ına konur.
Kimlik değişince token yenilenir; login öncesi token logout için saklanmaz.

Origin scheme+host+port'tur. Site kavramı scheme ve kayıtlı alan adıyla ilişkilidir;
farklı port cross-origin olabilir ama cross-site değildir. CORS tarayıcının yanıtı
okumasını denetler; sunucuda kimlik veya CSRF kontrolü değildir. Zararsız basit GET
sunucuya ulaşabilir ve yanıt okunamaz. Özel header preflight OPTIONS doğurur; izin
verilmezse asıl GET gönderilmez. Laboratuvar bu ikisini tarayıcı sonucu ve sunucu
sayaçlarıyla ayırır. Ana uygulamada CORS yoktur. Test hostu yalnız tek açık origin,
GET ve X-Lab header'ına izin verir; credentials açmaz.

localStorage kalıcı, sessionStorage sekme ömürlüdür; ikisi de aynı origin JS'sine
açıktır. IndexedDB de script erişimli depodur. Bellek sayfa kapanınca kaybolur ama
XSS'ye karşı kasa değildir. HttpOnly cookie JS okumasını sınırlar ancak CSRF ve
kullanıcı adına XSS işlemleri hâlâ önemlidir. Sayfa JWT/refresh almaz, saklamaz;
parolayı yalnız giriş isteğinde kullanır ve alanı sonunda temizler. Bu bellekten
güvenli silme değildir; tarayıcının parola yöneticisi engellenmez.

BFF istemciye özel backend'dir. OAuth tasarımında token'ları sunucuda tutup tarayıcıya
cookie oturumu sunabilir; XSS/CSRF'yi yok etmez. Bugün aynı uygulama/API ile doğrudan
cookie kullanıyoruz; ayrı BFF, proxy veya OAuth implementasyonu yoktur.

Kod konumları:
- `wwwroot/index.html`, `site.css`, `site.js`: erişilebilir form, bekleme/ret/oturum
  durumu, tek istek koruması, güvenli metin ve göreli fetch adresleri.
- `Endpoints/WebClientSetup.cs`, `Program.cs`: yalnız web kökü, HTML CSP ve no-store.
- `scripts/WebClientChecks.cs`: mevcut LocalDB/Identity fixture'ı ve geçici CORS hostları.
- `scripts/browser/checks.mjs`: gerçek Edge kontrolü; test sertifikasının SPKI izin
  listesi. Genel sertifika bypass yok. Browser profili owned fixture altında temizlenir.

Kanıt sınırı: gerçek aynı-origin Strict cookie akışı test edildi. Farklı portlarla
CORS test edildi; ayrı site adlarıyla cross-site cookie deneyi yapılmadı. UI500/
ağ hataları kontrollü browser intercept yanıtlarıdır; gerçek limiter/DB hata davranışı
ayrı auth-abuse/full HTTP testidir. Browser butonuna bağlı olmadan doğrudan403 de ölçülür;
tam sahiplik matrisi backend resource-access regresyonundadır. Firefox/Safari denenmedi.

Kaynaklar:
- [Microsoft .NET10 static files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
- [Microsoft antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [MDN Set-Cookie](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Set-Cookie)
- [MDN CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CORS)
- [MDN CSP](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy)

Browser UI429 ayrıca gerçek login kotası (10/60s) doldurularak doğrulanır; test hostu
forgot kotası3/60s, diğer backend regresyonları10000/60s kullanır.
