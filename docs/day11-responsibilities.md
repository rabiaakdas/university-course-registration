# 11. gün — Sorumluluk, composition ve SOLID'in sınırları

## Koddan tasarım değerlendirmesi

| Soru | Önce | Seçilen sınır |
|---|---|---|
| HTTP biçimi/durumları nerede? | EnrollmentEndpoints.CreateAsync ve BatchEnrollmentEndpoint.CreateAsync | Aynı endpoint'lerde; route, alan anahtarları, Problem Details, Location ve DTO zarfı korunur |
| İşlem adımları nerede? | Aynı metotlarda Any/Contains sorguları, Add/AddRange, SaveChanges | Application/EnrollmentOperations.CreateAsync ve CreateBatchAsync |
| Gerçek tekrar ne? | SaveChanges'te aynı dar SQL hata sınıflandırması ve başarı DTO alanları | Özel SaveAsync ve Response yardımcıları; yeni servis zinciri yok |
| Benzer ama farklı olan ne? | Teklide eksik iki başvuru birlikte 400; batch'te önce öğrenci 404, sonra seçimler 400 | Ayrı metot ve sonuç türleri; save FK hatasında batch de ilgili tekil alanla 400 |
| Hangi ayrım yararlı? | HTTP sonucu oluşturmadan işlem sonucunu değerlendirmek zor | İşlem yalnız başarı verisi veya gerekçeli ret döndürür; HTTP bağımsız çağrılabilir |
| Hangi katman yalnız aktarırdı? | DbContext zaten sorgu/takip/yazma sınırını sağlıyor | Repository/UoW, servis interface'i ve generic Result eklenmedi |

Endpoint, binding ve istek şekli doğrulamasını taşır: pozitif kimlikler, zorunlu
liste, tekrar kimlikler, 1–50 sınırı. Bunlar geçmeden DB sorgusu yoktur. İşlem
metotlarının ön koşulu bu biçim kontrolünden geçmiş girdidir; doğrudan çağıran
yeni bir giriş noktası da aynı sınırı sağlamalıdır. DB varlık/tekrar kontrolleri,
UTC üretimi, yalnız Enrollment eklenmesi ve kaydetme işlemin içindedir.

`EnrollmentResults.cs` iki ayrı kapalı başarı/ret türü tanımlar. Başarı mevcut
EnrollmentResponse değerlerini taşır (bu DTO'nun ASP.NET bağımlılığı yoktur),
ret yalnız işlemine özgü enum taşır. Başarı ve ret verilerini aynı nullable
torbaya doldurmaz. Batch'te ön kontroldeki StudentMissing ile yazma sırasında
StudentRemoved ayrıdır: birincisi 404, ikincisi studentId alanında 400 olmalıdır.
SQL sağlayıcı sınıflandırması `Data/RegistrationWriteErrors` içinde değişmeden
kalır; nesne adlarının SQL hata mesajından ayrıştırılması sınırı devam eder.

## İlkelerin somut karşılığı

- **SRP:** Tek metot demek değildir. İki kayıt akışı birlikte değişen kayıt
  koordinasyonunu taşır; HTTP biçimleri endpoint'lerde değişir. Sınıf sayısı hedef
  değildir. Okuma endpoint'lerini yeni sınıfa taşımak bugünkü ihtiyaca hizmet etmez.
- **Composition:** EnrollmentOperations, constructor ile aldığı DbContext'i
  kullanır; kalıtım hiyerarşisi kurulmaz. Program.cs nesneleri DI ile birleştirir.
- **DI / DIP:** Scoped DI nesneyi oluşturur ve yaşam süresini yönetir. Bu tek
  başına DIP ya da SQL Server/EF bağımsızlığı değildir. İşlem somut DbContext ve
  sağlayıcı hata sınıflandırmasına hâlâ bağımlıdır; bu bağımlılık gizlenmez.
- **OCP:** Mevcut kodu hiç değiştirmeme kuralı değildir. Kanıtlanmış değişkenlik
  olmadığı için Strategy, Factory veya genişleme kancası eklenmedi. 12. günün
  kayıt önceliği stratejisi bugün uygulanmadı.
- **LSP:** Bugün bir servis kalıtım ailesi kurulmadı. Korunacak davranış anlamı
  önemlidir: hata/iptal ret değildir; tekli 400 ile batch 404 farkı gizlenmez.
  Sorgu hatası bulunamayan öğrenciye çevrilmez. İptal token'ı aynen iletilir.
- **ISP:** Her metoda interface gerekmez. İlgisiz CRUD/rapor/kimlik işlemlerini
  toplayan büyük interface de kurulmadı. Gerçek ikinci uygulama ihtiyacı yoktur.

## Yazma ve yaşam süresi sınırları

Tekli Add → SaveAsync; batch AddRange → SaveAsync. Özel SaveAsync yalnız bir
SaveChangesAsync çağırır. Batch tekli metodu döngüyle çağırmaz; bütün seçimler bir
save sınırındadır. Başarı ancak save tamamlandıktan sonra oluşur; DTO sırası istek
sırasıdır. Ortak helper ön kontrolleri transaction içine almaz, yarış garantisi
vermez. Beklenmeyen SQL ve iptal hataları yayılır, merkezi güvenli handler korunur.
Başarısız context yeniden kaydedilmez, otomatik retry/BeginTransaction eklenmez.

`AddScoped<EnrollmentOperations>` ve AddDbContext aynı request yaşam süresini
kullanır. Aynı context'te sorgular sırayla await edilir; endpoint elle dispose
etmez. [Microsoft DbContext yaşam süresi belgesi](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
scoped varsayılanı ve paralel kullanmama gereğini açıklar.
[EF transaction belgesi](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
tek SaveChanges'in hata halinde tüm yazmaları geri alma davranışını açıklar.

## Doğrulama bağlantısı

`scripts/EnrollmentChecks.cs` ve `BatchEnrollmentChecks.cs` mevcut fault
interceptor'larını yeni işlem nesnesiyle endpoint'e verir; HTTP hata assertion'ları
gevşetilmez. Tekli save yarışında üç SELECT, 50 öğelik batch'te yine üç SELECT
beklenir. Batch'te başarılı ilk INSERT sonrası gerçek unique hatası ve rakip
commit'in korunması aynı deneyle doğrulanır. İptal kontrolü doğrudan işlemi çağırır;
aynı write senaryoları kopyalanmaz. Bu testler unit değil gerçek DB entegrasyonudur.
