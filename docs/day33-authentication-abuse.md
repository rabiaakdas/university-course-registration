# Gün 33 — Hesap keşfi ve kötüye kullanım sınırları

Enumeration, yanıt veya süre farkından hesap varlığının çıkarılmasıdır. Brute force
bir hesaba çok parola; password spraying çok hesaba az sayıda yaygın parola dener.
Ortak 401 gereksiz bilgiyi azaltır; bildirim/DB/precheck sürelerini eşitlemez.

Lockout hesap durumudur; rate limiting istek kaynağı kotasıdır. Aynı IP'deki farklı
hesaplar tek kotayı paylaşırken aynı hesap farklı IP'lerden denenebilir. Lockout
başkasının hesabını engellemek için de kötüye kullanılabilir. İkisi birbirinin yerine geçmez.

Fixed window pencere başına kota verir ve sınır çevresinde kısa yoğunluk oluşabilir.
Sliding window zamanı alt aralıklara böler; token bucket zamana göre izin biriktirir.
Concurrency limiter aynı anda çalışan iş sayısını sınırlar; istek/dakika kotası değildir.
Bu ihtiyaçta iki ayrı fixed window, QueueLimit=0 ve güvenilir IP partition yeterlidir.

`DummyPasswordVerification`, mevcut Identity hasher'ıyla startup'ta tek hash hazırlar.
Gerçek hesaba veya DB'ye bağlı değildir. Her olmayan hesap denemesi aynı yapılandırılmış
hasher ile doğrulanır; sonuç giriş sağlamaz. Gerçek kullanıcıda UserManager'ın
normalize eden FindByNameAsync sorgusu ve SignInManager'ın kullanıcı overload'u
kullanılır; ikinci sorgu veya ikinci hash doğrulaması eklenmez. Scoped hasher singleton
field'ına tutulmaz. Maliyet artışı bilinçlidir; limiter bu işten önce çalışır.
Kilitli/doğrulanmamış hesap ön kontrolleri hâlâ erken dönebilir. Sabit süre garantisi yoktur.

Secret loglamamak yalnız başarılı akış için değildir: SQL arızası, bildirim hatası,
429 ve test raporları da parola/hash/cookie/token taşımamalıdır. Debug event + TraceId
operasyon türünü gözlemlemeye yeter; e-posta/IP ve tam gövde eklenmez.

Kod konumları:
- `Accounts/AuthenticationAbuse.cs`: yapılandırma, partition, dummy hash, 429.
- `Endpoints/AccountSessionEndpoints.cs`: ortak 401 ve gerçek SignInManager yolu.
- `Endpoints/PasswordResetEndpoints.cs`: ayrı forgot kotası, değişmeyen ortak kabul.
- `Program.cs`: middleware sırası; `Accounts/AccountSetup.cs`: mevcut lockout5/5m.
- `scripts/AuthAbuseChecks.cs`: gerçek hasher/provider gözlemi, kontrollü SQL hatası,
  HTTPS lockout/kota/süre gözlemi; `AuthAbuseTestSettings.cs`: test kotası ayrımı.

Testte aynı sunucuda kısa pencere gerçekten yenilenir. Kontrollü Connection IP'si
public header'a güvenmez; gerçek farklı ağ çıkışları denenmiş değildir. Lockout5m
DB'de gözlenir, sonrası için fixture geçmişe alınır. Isınma sonrası beşer HTTPS süre
örneği gözlemdir; benchmark veya timing açığı yok kanıtı değildir.

Resmî kaynaklar (kullanılan 10.0.14 davranışı):
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [SignInManager kaynak kodu](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/Core/src/SignInManager.cs): precheck, kullanıcı adı araması, başarısızlık ve sayaç reseti.
- [PasswordHasher kaynak kodu](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/Extensions.Core/src/PasswordHasher.cs): yapılandırılmış doğrulama; uygulama ayrı algoritma yazmaz.

Süreç belleği, ortak IP, proxy güveni, rol/sahiplik TOCTOU ve verilmiş token iptal
sınırları çözülmüş değildir. Sonraki gün: XSS/CSRF, cookie flags, token storage,
CORS/BFF ve küçük web istemcisi.
