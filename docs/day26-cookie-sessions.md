# Gün 26 — Cookie ile HTTP kimliği ve CSRF

Üretim kodu: `Accounts/AccountSetup.cs` servis/şema ve politika kaydı;
`Endpoints/AccountSessionEndpoints.cs` HTTP sınırı; `Program.cs` açık authentication/
authorization middleware sırası. Deney: `scripts/CookieSessionChecks.cs`.

UserManager hesap, parola hash'i, normalizasyon ve e-posta doğrulama işlerini yürütür.
SignInManager giriş ön koşullarını, parolayı, lockout'u ve Identity cookie üretimini
birleştirir. Elle parola kontrolü ardından cookie yazılmadı. AddIdentityCore üzerine
AddSignInManager ve AddIdentityCookies eklendi. Identity'nin application/external/
two-factor yardımcı şemaları kayıtlıdır; harici giriş veya 2FA endpoint'i açılmadı.
Yalnız Succeeded giriş başarısıdır; diğer sonuçlar aynı 401 olur.

Authentication kimliği belirler; authorization kaynağa erişim kararını verir.
Bugün yalnız me/logout RequireAuthorization kullanır. Hesap kimliği Student.Id
olmaz; profilsiz hesap da me çağırabilir. Eksik kimlik claim'i veya silinmiş hesap
401 olur. Akademik endpoint'ler halen herkese açıktır; öğrenci self-servisi değildir.

Cookie tarayıcı tarafından uygun isteklere otomatik taşınır. Bearer ise taşıyanın
kullanabildiği kimlik bilgisidir; mutlaka JWT değildir. Cookie de kopyalandığında
kullanılabilir, bu nedenle HTTPS ve gizlilik gerekir. HttpOnly JS okumayı kısıtlar;
XSS'i tek başına çözmez. Strict SameSite aynı site ders akışına uygundur; ileride
harici giriş/cross-site istemci ihtiyaçları yeniden değerlendirilmelidir.

Biletin korunan ExpiresUtc değeri 20 dakikadır; sliding kapalıdır. isPersistent:false
nedeniyle cookie'de kalıcı Expires/Max-Age yoktur. Tarayıcı session restore davranışı
sunucu bileti süresini uzatmaz. Çıkış cookie silme talimatıdır; çalınmış/kopyalanmış
bileti anında iptal eden sunucu oturum tablosu değildir. Identity'nin varsayılan
security-stamp kontrol aralığı 30 dakikadır; bunu her istekte anlık iptal diye sunmuyoruz.

RequireConfirmedEmail=true. Yanlış parolada lockoutOnFailure:true, yeni hesaplarda
lockout açık, beş başarısız denemede beş dakika kilit. Identity ön kontrolüyle
reddedilen doğrulanmamış hesaplar parola hatası gibi sayılmaz. Bu ayarlar IP tabanlı
rate limiting veya tam enumeration koruması sağlamaz. Aynı 401 metni zamanlama farkını
otomatik kapatmaz. UserManager/SignInManager bu API'lerde request token parametresi
almaz; iptal edilmiş istemcinin sonucu alması veya DB işleminin durması garantisi yoktur.

401 geçerli kimlik bulunmadığını/giriş kabul edilmediğini; 403 kimlik olsa da erişim
izni verilmediğini ifade eder. ASP.NET Core 10 bilinen API endpoint'lerinde redirect
yerine 401/403 kullanır. Uygulamanın HTML login sayfası olmadığından cookie redirect
event'leri de açıkça 401/403'e ayarlandı; mevcut OnValidatePrincipal korunur.
Bugün yapay rol/403 senaryosu eklenmedi.

CSRF token'ı kimlik cookie'si değildir. GET /accounts/antiforgery, HttpOnly/Secure
CSRF cookie'si ve JSON requestToken sağlar; response no-store'dur. Login/logout
JSON POST olduğundan IAntiforgery.ValidateRequestAsync açıkça çağrılır; yalnız servis
kaydı yapmak yetmez. Token X-CSRF-TOKEN header'ında cookie ile birlikte gönderilir.
Giriş kimliği değiştirdiğinden çıkış için yeni token alınır. Eksik/bozuk veya eski
kimliğe bağlı token 400 ve işlem yoktur. Logout'ta authorization önce çalışır;
anonim istek 401 olur. Geçerli JSON binding ardından CSRF, sonra alan kontrolü ve
giriş değerlendirmesi gelir. SameSite/CORS token doğrulamasının yerine geçmez.
Register/confirm ve akademik POST'lar bugünkü CSRF değişikliğine dahil değildir;
bunların kalan güvenlik kapsamı ileride ayrıca değerlendirilecektir.

HTTPS testi her koşuda yalnız kendi sertifikasını üretir, Windows Schannel için
bellekte PFX olarak yeniden yükler; istemci yalnız aynı DER sertifikayı kabul eder.
Global TLS bypass, trust-store değişimi veya repoya sertifika yazımı yoktur.
Cookie options TimeProvider testte kontrollü ilerletilir: 11. dakikada başarılı me
ve Set-Cookie yokluğu, 21. dakikada 401. Üretim süresi değiştirilmez, uzun bekleme yok.
Data Protection app adı/anahtar dizini testte ayrıdır. Fixture hesapları/profili,
anahtar dizini ve HTTPS host finally temizliğindedir. Assertion/temizlik hataları
ayrıdır; log testleri yalnız yakalanan uygulama çıktısını kapsar, dış proxy'leri değil.

## Resmi kaynaklar

- [10.0.14 SignInManager: PasswordSignInAsync, ön kontroller, lockout ve SignOutAsync](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Core/src/SignInManager.cs)
- [Identity cookie şemaları ve security stamp doğrulaması](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Core/src/IdentityCookiesBuilderExtensions.cs)
- [AddSignInManager bağımlılıkları](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Core/src/IdentityBuilderExtensions.cs)
- [Cookie süresi ve kalıcılık](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [ASP.NET Core 10 API 401/403 davranışı](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0)
- [Minimal API açık antiforgery doğrulaması](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0#antiforgery-with-minimal-apis)
