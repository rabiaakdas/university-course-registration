# Gün 28 — Access/refresh ayrımı

Access token kısa ömürlü, imzalı API yetki belgesidir. Refresh token yeni access token
isteme yetkisidir; opak ve en az 256 bit güvenli rastgelelik taşır. JWT payload'ı değildir.
`Accounts/TokenSessions.cs` RNG.GetBytes(32), Base64Url ve SHA256.HashData kullanır.
43 karakter kanonik biçim korunur; girilen token normalize edilmez. Özet binary(32)
olduğundan SQL byte eşitliğiyle arama yapılır; UNIQUE index tek güncel özeti korur.

İnsan parolaları tahmin edilebilir olduğundan hızlı SHA-256 parola saklama yöntemi değildir.
Burada özetlenen yüksek entropili rastgele secret'tır; Identity parola hasher'ı değişmedi.
Resmi API kaynakları: [RNG.GetBytes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes?view=net-10.0),
[SHA256.HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata?view=net-10.0).

`Models/RefreshSession.cs` yalnız güncel özet ve hesap/tarihleri taşır.
`Data/UniversityDbContext.cs` ve `20261105101325_AddRefreshSessions` PK/unique/FK/date
korumalarını kurar. UserId hesap kimliğidir. RevokedAtUtc alanı fixture üzerinden
sınanır; bugün ürün iptal/silme endpoint'i yoktur. NO ACTION hesap silmeyi engeller.

`Endpoints/UniversityJwtEndpoints.cs` cookie+CSRF ilk üretimini korur; anonim refresh
HTTPS JSON alır. `TokenSessions` hesap varlığı, CanSignInAsync ve lockout kontrolü yapar.
Identity UserManager metotlarında olmayan CancellationToken parametresi icat edilmedi;
EF okuma/yazmalarına request token aktarılır. İptal401'e çevrilmez. Kayıt başarısı
sonrasında çift döner; mevcut JWT kuralları ve ayrı şema seçimi değişmez.

İlk üretim Add+SaveChanges, yenileme tek hash değişikliği+SaveChanges yapar. EF'nin
[SaveChanges transaction davranışı](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
DB yazmasını korur; HTTP yanıt teslimini kapsamaz. Başarısız context yeniden kaydedilmez.
Test interceptor'ı gerçek UNIQUE hatası oluşturur:500, eski özet kalır, eski token
çalışır; kaydedilmeyen yeni özet bulunmaz. Ham yeni token dışarı verilmediğinden test
onu bir istemci gibi yeniden gönderdiğini iddia etmez. Bağlantı kopması simüle edilmedi.

Sabit yedi günlük oturum sonu rotation'da uzamaz. Access exp min(şimdi+600, oturumsonu)
olarak tam saniyeye aşağı yuvarlanır; expiresIn aynı NumericDate farkıdır. JWT validator'ın
30 saniyelik toleransı exp sonrasında sınırlı kabul anlamına gelebilir; refresh ise
oturum sonunu UTC now ile denetler. Sıralı test concurrency garantisi değildir.
İki context aynı özeti okuyup ikisi de başarılı yazabilir; bir yanıtın token'ı son
hash olmayabilir. History/family/reuse tespiti, kilit/retry/concurrency token yoktur.

Özetin değişmesi eski JWT'yi iptal etmez. Cookie logout da access token iptali değildir.
Revoked refresh oturumu yalnız sonraki refresh'i engeller. Commit ile yanıt arasında
kayıp olursa yeniden giriş gerekebilir. Kalıcı kayıt, kalıcı HTTP teslimi anlamına gelmez.

`UniversityJwtSetup.cs` süreleri doğrular; üretici ve refresh yalnız açık Development
opt-in ile kayıtlıdır. Production public-key-only kalır. `scripts/RefreshChecks.cs`
yükseltme/rotation/SQL/fault deneyleri; `JwtChecks` ve `CookieSessionChecks` mevcut
HTTPS şema/CSRF/Identity davranışlarını birlikte yeniden kullanır. Sertifika sabitlemesi,
geçici anahtarlar ve sahipliği bilinen hesap temizliği korunur; full ortak regresyonları
çalıştırır. Yeni paket, genel token framework'ü veya browser storage tasarımı yoktur.
