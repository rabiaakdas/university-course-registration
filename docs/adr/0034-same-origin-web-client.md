# ADR 0034 — Aynı origin cookie istemcisi

## 1. İhtiyaç
Mevcut login/logout/me ve public ders API'sini tarayıcıda kullanmak; HTTP testinin
kanıtlamadığı DOM, cookie erişimi ve CORS davranışını gerçek browser'da gözlemek.

## 2. Önceki çözümün sınırı
API ve PowerShell araçları kullanıcı sayfası sunmuyordu. HTTP istemcisi tarayıcının
CORS/SameSite kurallarını uygulamaz. JWT üretmek mevcut aynı-origin sayfa için gerekli değil.

## 3. Seçim ve kazanç
Üç küçük wwwroot dosyası, göreli fetch, mevcut cookie ve CSRF akışı. Yalnız web-root
static provider, HTML'e dar CSP, no-store, güvenli DOM yazımı. JWT/refresh sayfaya
bağlanmaz; public katalog açık kalır. Framework/build sistemi ve ayrı BFF gereksizdir.
Mevcut auth/role/ownership backend'de kalır. Laboratuvar yalnız test hostlarındadır.

## 4. Maliyet ve sınırlar
JS durum yönetimi, kimlik değişiminde CSRF yenileme ve ağ hatalarının belirsizliğini
anlatma maliyeti vardır. No-store statik cache verimini azaltır; bu küçük örnekte
basit ve açık tercihtir. HttpOnly/CSP tam XSS/CSRF çözümü değildir. Cookie seçimi
bütün istemciler için zorunlu değildir. Test-only playwright-core ve kurulu Edge
bağımlılığı eklenir; production NuGet/framework büyümez. Browser dosyaları publish
content'inden dışlanır. Ayrı cross-site ortamı ve diğer browser motorları denenmedi.
