# Gün 29 — Token geçmişi, aile koordinasyonu ve sahiplik

RefreshSession bir mantıksal oturum ailesidir; fiziksel cihaz değildir. Her dev-token
çağrısı bağımsız aile yaratır. RefreshTokenRecord her token'ın binary(32) özetini,
CreatedAtUtc/UsedAtUtc ve ReplacedById bağlantısını tutar. Ham token DB'ye girmez.
Eski güncel özet sütunu kaldırıldı; çift güncel durum kaynağı yoktur.

`Models/RefreshTokenRecord.cs`, `Data/UniversityDbContext.cs` ve
`20261105103704_AddRefreshTokenHistory` tabloyu tanımlar. TokenHash UNIQUE;
(SessionId, ReplacedById) → (SessionId, Id) birleşik FK ardılın aynı ailede olmasını
korur. Replacement için UsedAtUtc zorunludur; kendi kendine bağlantı ve kullanımın
oluşturulmadan önce olması CHECK ile reddedilir. FK'ler NO ACTION. Genel döngü
algoritması yoktur; uygulama yalnız yeni ardıl yaratır. Geçmiş için otomatik temizlik yok.
Erken silmek reuse tespit kapsamını azaltır; saklama/temizlik politikası ileride gerekir.

Migration eski her ailenin tek bilinen güncel özetini yeni token satırına taşır, sonra
eski index/sütunu kaldırır. Aile kimliği, hesap, created/expiry/revoked korunur. Gün28'in
atılmış geçmişi geri getirilemez; o token'lar bilinmeyen401 olur, reuse tespiti yapılamaz.
Geçişte eski/yeni uygulama yazmaları durdurulmalıdır. Down geçmişi kaybeder ve aile başına
son kullanılmamış (yoksa en son) özeti seçer; iptal durumu korunur. Down çalıştırılmadı.

`Accounts/TokenSessions.cs` önce yalnız değişmeyen SessionId'yi özetten bulur, açık
transaction başlatır, aileyi parametreli SELECT WITH (UPDLOCK,HOLDLOCK) ile alır.
Önceki durumlara güvenmez; aile ve token kullanımını yeniden okur. Kilit transaction
sonuna kadar tutulur. Hesap uygunluğu aynı DB'den okunur, harici servis çağrısı yoktur.
Koşullu UsedAtUtc IS NULL ExecuteUpdate sonucu tam1 olmalıdır. Ardıl Add/SaveChanges,
eski satırın ReplacedById güncellemesi ve Commit aynı transaction/bağlantıdadır.
SaveChanges tek başına bu çok adımlı akışı atomik yapmaz. Hata/erken dönüşte dispose
transaction'ı bırakır; başarısız context yeniden kaydedilmez. Otomatik retry yoktur.

ExecuteUpdate tracker'ı güncellemez. Bu nedenle aile/eski token AsNoTracking okunur;
yalnız yeni token izlenir. Eski izlenen değerin SaveChanges ile geri yazılması önlenir.
`RevokeAsync` aynı aile kilidini kullanır, sahipliği kontrol eder; iptal edilmiş kendi
ailesi204 olur ve timestamp korunur. Liste yalnız kullanıcı filtresi/projection ile
CreatedAtUtc, Id sırasındadır. Endpoint claim'i hesapId'dir, StudentId değildir.

Bilinen kullanılmış token, geçerli ailede yeniden gelirse iptal güncellenir ve COMMIT
edildikten sonra401 döner. Bu ret exception değildir; rollback ile iptal kaybedilmez.
Bilinmeyen token başka aileye dokunmaz. Süresi dolmuş/iptal aile açılmaz. İki refresh
aynı token'ı sunarsa biri tüketir; kilidi sonra alan kullanım izini görüp aileyi iptal
eder. Böylece normal tamamlanan yarış200/401, tek ardıl, iptal aile üretir. Kazananın
refresh token'ı da reddedilir. İstemcide single-flight yararlıdır fakat sunucu garantisi
yerine geçmez. Kaybolan yanıt/iki sekme masum tekrar üretebilir; saldırgan kimliği
kesinleşmiş sayılmaz. Tolerans penceresi bugün seçilmedi.

Cookie logout, aile iptali ve access token kabulü ayrı davranışlardır. JWT validator
DB sorgulamadığından verilmiş JWT exp (+mevcut30s skew) sınırına kadar çalışabilir.
Session revoke cookie'yi silmez. İptal400/401 veya DB500 ile karıştırılmaz; cancellation
aynı anlamıyla taşınır. Cookie-only sessions GET ve CSRF'li POST revoke,
`Endpoints/UniversityJwtEndpoints.cs` içindedir. Development üretici opt-in sınırı
korunur; üretim OAuth/OIDC servisi iddiası yoktur.

`scripts/RefreshChecks.cs` gerçek HTTPS/SQL testlerini genişletir. Test-only interceptor
iki ayrı bağlantıyı aile kilidinden önce buluşturur; ordered deney ilk SQL kilidi
alındıktan sonra ikinci isteği başlatır ve ilkini serbest bırakır. Rastgele delay yok.
Gerçek ardıl INSERT UNIQUE hatası tüketimi de geri alır. Salt yanıt sayısı yerine yeni
context'ten geçmiş, kullanım, replacement ve revoke durumu kontrol edilir. Cookie
host'u sertifikayı yalnız o koşunun sertifikasıyla eşleştirir. Fiziksel süreç çökmesi,
ağ kopması ve multi-node deployment denenmemiştir.

Kaynaklar:

- [RFC9700 §4.14](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14): rotation ile
  eski ilişki bilgisini tutup tekrar kullanımda aktif yetkiyi iptal etme yaklaşımı.
  Bu uygulama tam OAuth uygulaması değildir; politika seçimi her sağlayıcının aynı
  davranacağı anlamına gelmez.
- [SQL Server table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17): UPDLOCK/HOLDLOCK kapsamı.
- [EF ExecuteUpdate](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete): tracker güncellenmez; birden çok komut için açık transaction gerekir.
