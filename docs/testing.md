# Test kılavuzu — 10. gün

## Kısa test haritası

| Davranış | Dosya / seçim | Sınır ve önemli assertion | Önceki eksik / karar |
|---|---|---|---|
| Kredi, boş metin, normalizasyon, birlikte alan hataları | `CoreBehaviorChecks.Unit`, `-Mode unit` | Saf `CourseInput.Validate`, alan anahtarları ve sonuç değerleri | Önceden HTTP üzerinden dolaylıydı; DB'siz seçim eklendi |
| Başarılı / olmayan açılış / mükerrer kayıt | `CoreBehaviorChecks.Run`, `-Mode core` | Gerçek HTTP + ayrı EF context: tam 201/400/409, Location, FK değerleri, sayı farkı, eski kayıtların bütün alanları | Önceden geniş senaryoya bağlıydı; bağımsız fixture ve korunma assertion'ları eklendi |
| Öğrenci ve çoktan çoğa kayıt, yarış, gerçek save hatası, restart | `EnrollmentChecks.cs`, `-Mode full` | LocalDB/HTTP ve handler entegrasyonu; unique/FK sınıflandırması, tek çift, JOIN | Mevcut kapsam korunur; çekirdek mod bunların yerine geçmez |
| Ders yazma, güvenli hata yanıtı, migration/seed | `CourseWriteChecks.cs`, `-Mode full` | HTTP, SQL Server, gerçek constraint | Yeniden kullanılır |
| Dönem/açılış, tracking, dört transaction sınırı | `RelatedWriteChecks.cs`, `-Mode full` | EF + LocalDB; yeni context'te commit/rollback sonucu | Yeniden kullanılır |
| Katalog filtre/sayfa, gerçek SQL, tracking karşılaştırması | `CatalogChecks.cs`, `-Mode full` | HTTP + interceptor ile SQL yürütme | Yeniden kullanılır |
| Atomik batch, ön kontrol ve gerçek yazma hatası | `BatchEnrollmentChecks.cs`, `-Mode full` | HTTP/DB, koordineli interceptor; rakip commit korunur | Yeniden kullanılır |
| Üç liste sözleşmesi | `verify-http.ps1` (full içinden de çağrılır) | Gerçek HTTP; seed alanları, ek kayıtlara izin | Yeniden kullanılır |

## Komutlar

Repo kökünde .NET SDK 10.0.401 gerekir. DB kontrolleri ayrıca Windows LocalDB
`MSSQLLocalDB`, SQL Server 17.0.4025.3, `sqlcmd` ve PowerShell 7 (`pwsh`) kullanır.
EF paket/araç 10.0.14 değiştirilmedi. Yeni paket, test framework'ü veya ürün
projesi eklenmedi. Mevcut konsol projesi `obj/day5-checks` altında üretilir;
web projesinin `Compile Remove="scripts/**/*.cs"` kuralı testleri dışarıda tutar.

```powershell
dotnet restore
dotnet build --no-restore
# Saf birim kontrolleri: LocalDB bağlantısı/API süreci oluşturulmaz.
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
# Tek komut: birim + üç bağımsız gerçek HTTP/DB senaryosu.
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
# Sıra bağımsızlığı:
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode core -Reverse
# Önceki günlerin geniş regresyonları (varsayılan seçim):
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
# Bilinçli çalıştırıcı kontrolü: beklenen exit code 1, normal test komutu değildir.
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit -ProbeFailure
```

Core her koşuda GUID son ekli `university_registration_tests_day10_*` DB seçer.
İsim/LocalDB hedefi ve DB'nin daha önce bulunmadığı mutasyondan önce kontrol edilir.
App/lab hedeflenmez. Migration'lar yeni DB'ye uygulanır; sabit seed gerekmez.
Her senaryo kendi çalışma etiketli ders/dönem/öğrenci/açılışını kurar ve üretilen
kimlikleri kullanır. Olmayan kimlik istekten önce DB'de gerçekten aranır.
Senaryolar seri çalışır; her biri kendi başlangıç kaydını hazırlar. Diğer
senaryonun başarılı olmasına dayanmaz. API bounded readiness polling ile beklenir.

Finally Enrollment → açılış → öğrenci/dönem/ders sırasıyla yalnız fixture
kimliklerini temizler. Ayrı context doğrulaması sonunda beş tablo boş olmalıdır.
API process kill/wait/dispose edilir. Test DB şeması ve migration geçmişi inceleme
amacıyla tutulur; DB silinmez, sequence sıfırlanmaz. Full modun eski day6 isimli
DB yaklaşımı ve seed tutma davranışı korunur. Core, önceki fault/concurrency
kontrollerini kopyalamaz.

Assertion hatası senaryo, beklenen/gerçekleşen sonuç ve gereken üretilmiş kimlikleri
bildirir (exit 1). Ortam/çalıştırma engelleri tip adıyla ayrı raporlanır (exit 2);
bağlantı veya SQL hata metni basılmaz. Temizlik hatası ayrıca `CLEANUP` olarak
bildirilir; önceki hata varsa onu örtmez. Alt dotnet komutunun başarısız kodu
PowerShell tarafından üst çağırana iletilir. Atlanan bir DB kontrolü PASS değildir.
Eski full mod raporları kendi PASS/exception düzenini korur. Temizlik hata yoluna
ayrıca fault enjekte edilmedi.

## Öğrenme notu

Unit kontrolü gerçek `CourseInput.Validate` imzasını çağırır; DB/HTTP kurmaz.
Integration kontrolleri HTTP binding/endpoint/EF/SQL Server sınırını birlikte
çalıştırır. Dosyanın `.cs` olması onu unit test yapmaz. Arrange fixture'ı kurar,
Act isteği gönderir, Assert durum koduyla birlikte DB sonucunu doğrular.
Sadece herhangi bir exception ya da “201 gelmedi” beklentisi yeterli değildir.

Birim kontrolü SQL benzersizliği, transaction veya HTTP sözleşmesini kanıtlamaz;
DbSet mock'u veya sahte DB de SQL Server kısıtlarının karşılığı değildir.
Yeni context okumaları izlenen eski nesneyi sonuç sanmayı engeller. Zaman UTC
ve istek öncesi/sonrası aralıkla doğrulanır; tam saat eşitliği aranmaz. Beklenen
hatalar metnin Türkçe yazımına değil status/alan anahtarına dayanır.

İzolasyon, deterministik beklenti ve finally temizliği tekrar çalıştırmayı sağlar.
İki geçiş bütün olası hataların yokluğunu kanıtlamaz. Kontenjan, akademik uygunluk,
yetkilendirme, genel idempotency ve altyapı arızaları bu çekirdek takımın güvencesi
değildir. Ürün davranışı ve DB şeması değiştirilmedi.

## 11. gün bağlantı güncellemesi

Kayıt fault kontrolleri aynı interceptor/context'i `EnrollmentOperations` ile
endpoint'e sağlar. Senaryolar ve HTTP beklentileri korunur. Tekli kayıt save
hatalarında üç ön kontrol SELECT'i ayrıca doğrulanır; batch'in 50 öğede üç sorgu
kontrolü korunur. Mevcut batch dosyasında işlemin iptal token'ını ret sonucuna
çevirmeden yaydığı ve DB'yi değiştirmediği doğrudan kontrol edilir. Runner ve test
framework'ü değişmedi. [Sorumluluk notu](day11-responsibilities.md).

## 12. gün — Saf Strategy ve DB'siz HTTP

`PriorityChecks.Unit` mevcut unit/core moduna eklenmiştir: graduation eşiklerinin
iki tarafı, kredi aşımı, completed puanı, kurucu parametre hataları, null,
determinizm ve Int32 uçları. Framework/paket eklenmedi.

`powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode preview`
aynı konsol altyapısında yalnız saf testler + gerçek preview HTTP kontrollerini
çalıştırır. Bir process, rezerve edilmiş dinlemeyen loopback SQL portuyla başlar;
connection değeri loglanmaz. Readiness sınırlı polling ile 404 route üzerinden
beklenir. Preview 200, DB gerektiren /courses 500 ve ardından preview yine 200
beklenir. DB oluşturulmaz; süreç/socket/client finally/using ile kapatılır.

Bu erişilebilir DB gerekmemesi kanıtıdır; bağlantı girişimlerini sayan interceptor
kullanılmadı. Kodda preview DB bağımlılığı yoktur. Full mod aynı HTTP kontrollerini
çalıştırır, ardından önceki gerçek LocalDB kayıt/batch regresyonlarını sürdürür.
Hatalar üst çalıştırıcıya nonzero exit code ile taşınır; süreç temizliği hatası
ayrı bildirilir ve asıl hatayı gizlemez.

## 13. gün — Factory deneyleri

`FactoryExperiments.Run` DB'siz davranış kontrolüdür: Simple Factory başarı/
false-null/yeni nesne sözleşmesi, Factory Method 100/225, Abstract Factory uyumlu
puan/açıklama ailesi. Web assembly'sinde deney türünün bulunmadığı ayrıca sınanır.
Unit/core/full bağlantıları mevcut geçici konsol projesine küçük Compile Include
ile eklenir; yeni proje/paket/framework yoktur.

```powershell
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode factories
# Yalnız çalıştırıcı hata yayılımı deneyi; beklenen başarısız exit code 1:
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode factories -ProbeFailure
```

Factories modu DB bağlantısı veya API süreci oluşturulmadan döner. Probe kalıcı
bozuk test değildir; açıkça seçildiğinde kontrollü assertion hatası üretir.
Önceki preview DB bağımsızlığı ve core/full güvenli LocalDB temizliği korunur.

## 14. gün — Akademik specification kontrolleri

`SpecificationChecks.Unit` unit/core moduna saf koşulları ekler; `-Mode specifications`
birim + başlangıç ayarı + yeni izole DB'de gerçek HTTP/kısıt/SQL kontrollerini
çalıştırır. Full bunları mevcut regresyonlarla birlikte yeniden kullanır.

`PrerequisiteInput` dış koleksiyon değişikliğine karşı kopya, null/kimlik
koşulları; CreditLoad negatif/sıfır sınırları ve long uçları sınanır. Başlangıç
testleri boş content root ve özel süreç ortamıyla eksik/boş/bozuk/0/negatif/taşan
ayarları sınar; gerçek ayar dosyası değişmez, hata çıktısı loglanmaz.

HTTP testleri Enrollment≠başarı, başka öğrenci başarısı, batch seçimi≠tamamlama,
fail-fast sırası, dönem ayrımı, 24+6 kabul/24+7 ret, iki4kredi batch reddi ve
çok dönemli kısmi yazmama durumlarını yeni context üzerinden doğrular. SQL capture
başarılı yolda altı SELECT ve WHERE/JOIN/GROUP BY/SUM(bigint) kanıtını bellekte
inceler. Büyük mevcut yük Int32 üstünde de denenir. PK/FK/selfCHECK retleri tam
SQL numarası ve nesne adıyla sınanır; dolaylı döngünün kabulü sınırı gösterilir.

Eski 50 öğelik batch fixture'ı on yeni test dönemine beşer 4 kredi açılış dağıtır;
kredi politikası 30 kalır. Tekli/batch fault kontrolleri üç yerine altı toplu
sorgudan sonra aynı gerçek yazma hatalarına ulaşır. Cleanup yeni ilişki tablolarını
üst kayıtlardan önce kaldırır; yalnız üretilen test kimlikleri hedeflenir. Varsayılan
seed değişmedi. Full içindeki rollback-only akademik SQL örneği tekrar güvenlidir.

## Gün 15 — Uygunluk ve saatler

- `-Mode unit`: önceki saf kontroller + WeeklyTimeSlot yarı açık aralık, simetri,
  geçersiz gün/saat, tüm retlerin sırası/tekilleştirme, savunmacı sonuç kopyaları.
- `-Mode eligibility`: yeni GUID LocalDB + gerçek API; üç akademik ret birlikte,
  yeni–mevcut/yeni–yeni, dönem/gün/öğrenci ayrımı, sınır saat ve boş program,
  preview sonrası program değişikliği, fresh-context DB değişmezliği,
  time(7) tick/min/max gidiş-dönüşü ve gerçek PK/FK/NOT NULL/CHECK/UNIQUE.
- `-Mode specifications`: önceki ön koşul/kredi kontrolleri korunur; artık
  failures listesindeki belirli ilk neden kontrol edilir. Tam liste/sıra yeni
  EligibilityChecks'te sınanır; aynı testler kopyalanmaz.
- `-Mode core/full`: mevcut davranışlar. Full save-stage unique/FK ve batch ilk
  INSERT sonrası rollback/rakip kayıt korunmasını aynen çalıştırır. SQL interceptor
  tekli/50'li batch/önizlemede sekiz toplu SELECT'i; saat sorgularında WHERE ve
  ilgili öğrenci/dönem JOIN'lerini doğrular. INSERT batching sayısı sabitlenmez.

`pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode eligibility`
komutu bağımsız tekrar çalışır. Mutasyon öncesi LocalDB/test adı ve DB yokluğu
kontrol edilir; sahipliği belirsiz DB kullanılmaz. Saat kayıtları açılışlardan
önce, fixture kimlikleriyle finally temizlenir; başarısız temizlik ayrıca raporlanır.
İzole DB şemaları korunur, sequence geri sarılmaz; geçici API kapatılır.
`eligibility/core/specifications` sonunda sekiz uygulama tablosu boş olmalıdır;
full sonunda yalnız başlangıç seed'i kalır. Uygulama/laboratuvar DB'si değişmez.
Migration uygulama komutu ayrı ve yalnız app hedeflidir.

Gerçek SQL metadata: `sql/app/verify-time-slots.sql`; optional rollback örneği:
`sql/app/time-slot-example.sql` (yalnız işaretli full test DB'si). Önizlemede
interceptor yalnız sekiz SELECT yakalar; satır sayısının değişmemesinden tek başına
"sorgu yapılmadı" sonucu çıkarılmaz. Bu önizleme DB okur, rezervasyon yapmaz.

## Gün 16 — Enum ve State karşılaştırması

- `unit`: `StateChecks.Unit` aynı matris/ret gerekçesi beklentilerini üretim
  Enrollment ve `EnrollmentStateExperiment` için uygular. DB/HTTP gerektirmez;
  deney ana web derlemesine dahil değildir.
- `state`: `StateChecks.Upgrade` izole DB'yi önce15.gün şemasına getirir, eski
  biçimde ilişki ekler;16.gün migration'ından sonra Id/FK/zamanı koruyarak Approved
  olduğunu gerçek context ile okur. App DB'si geri alınmaz. Yeni API satırlarının
  Draft olması, submit/approve/reject, hata sırası, private setter yüklenmesi,
  restart, durum CHECK/gerekçe NULL/boşluk/uzunluk ve aktif yük politikası sınanır.
- Önceki `SpecificationChecks`/`EligibilityChecks` başarı fixture'larını HTTP ile
  submit eder; olağandışı eski yük fixture'ları test yardımcısıyla açık Approved
  hazırlanır. Yeni filtre kapatılmaz, kredi sınırı değiştirilmez. Eski akademik
  kontroller ve yeni durum kontrolleri ayrı amaçlıdır.
- `core/full` yeni status/rejectionReason alanlarını doğrular. Full gerçek
  unique/FK ve ilk INSERT sonrası rollback/rakip kayıt korunmasını yeniden
  kullanır. Gönderme/onaylama her defasında ortak kuralları okur; akademik ret
  halinde yeni context ile durum/gerekçenin değişmediği kontrol edilir.

Komut: `pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode state`.
Mutasyon öncesi mevcut test hedefi kontrolü korunur. Yalnız fixture kimlikleriyle
Enrollment ve ilişkiler, ardından üst kayıtlar finally temizlenir; süreç kapatılır.
State modunda sekiz tablo boş kalır; full sonunda seed dışında veri kalmamalıdır.
Hata exit code'u üst çalıştırıcıya taşınır. `sql/app/verify-enrollment-lifecycle.sql`
yeni kolon/default/CHECK metadata kontrolüdür. Down ve eşzamanlı geçiş garantisi
sınanmış değildir; ikinci garanti ürün kapsamında da yoktur.

## Gün 17 — Decorator gözlemi

| Sınır | Kontrol / komut | Kanıt |
|---|---|---|
| Saf birim | `-Mode unit`, `DecoratorChecks.Run` | Aynı sonuç referansı/ret sırası, tek çağrı; Event1701, outcome/count/süre; aynı exception/stack |
| Üretim dışı deney | Aynı unit veya `-Mode decorator` | TCS ile await/result/fault/cancel; iki sarma sırası ve finally |
| Gerçek HTTP + LocalDB + DI | `-Mode decorator` | EligibilityChecks ve StateChecks yeniden kullanılır; başarılı/retli preview, create/batch/submit/approve logları; erken retlerde log yok; JSON değişmez |
| SQL/fault regresyonu | `-Mode full` | Mevcut unique/FK, batch ilk INSERT sonrası rollback ve rakip kayıt korunması; doğrudan işlem kontrolleri de timing üzerinden |

Yeni test framework'ü yoktur. Runner sadece iki test kaynağını geçici console
projesine dahil eder. Saf mod DB istemez; decorator modu diğer gerçek kontrollerle
aynı yeni ve adı doğrulanmış LocalDB yaklaşımını kullanır. Eligibility/state
fixture'ları sonunda sekiz tablo boş; full yalnız seed bırakır. App/lab hedeflenmez.
SQL sayacı artık sarmalanmış evaluator kullanan işlemde yine sekiz hazırlık
SELECT'i bekler; yeni ölçüm DB erişmez. SQL logu dosyaya yazılmaz.

Gerçek HTTP log gözlemcisi test API'sinde JSON console formatter açar; yalnız
Event1701 alanlarını ve istek-bitme sayaçlarını bellekte tutar. RequestFinished
sıra bariyeri ile serial senaryoları kontrol eder; eşzamanlı full senaryolarında
bu serial gözlemci etkin değildir. Logger sağlayıcısı arızası simüle edilmez.
Alt komut hata kodu üst PowerShell'e taşınır; `unit -ProbeFailure` kontrollü
başarısızlık denemesidir, kalıcı bozuk test yoktur.

## Gün 18 — Sahte sağlayıcı adapter kontrolleri

`pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode adapter`
DB/ağ/HTTP olmadan `AdapterChecks.Run` çalıştırır. Aynı kontroller unit/full
modlarına dahil; test framework'ü veya yeni proje yoktur. Ortak harness iki
adapter için alan, token, tek çağrı, kabul/ret/kesinti, referans, null mesaj,
beklenmeyen exception ve cancellation'ı yeniden kullanır. TaskCompletionSource
ile gecikmeli exception/cancel kontrol edilir; keyfi Task.Delay yoktur.
Publish protokolünün bozuk/bilinmeyen cevapları ve referans kombinasyonları ayrıca
sınanır. Bu gerçek sağlayıcı veya teslim testi değildir.

`-Mode adapter -ProbeFailure` kontrollü assertion üretir; üst PowerShell exit1
vermelidir. Normal adapter çalışması exit0. Deney türleri web assembly'sinde
bulunmamalıdır. Kaynaklar yalnız geçici `obj/day5-checks/Checks.csproj` içine açıkça
dahil edilir. Veri/süreç oluşturmazlar; core/full gerçek DB regresyonları mevcut
izole LocalDB ve finally temizliğiyle devam eder. App/lab hedeflenmez.

## Gün 19 — Dönem özeti

`pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode summary` mevcut
izole LocalDB/gerçek HTTP çalıştırıcısıyla SummaryChecks'i çağırır. Full de aynı
kontrolü kullanır; kopya test/proje/framework yoktur. Fixture durumları kontrollü
entity metotlarıyla hazırlanır; okuma testi akademik kayıt kabulüne bağlanmaz.

Kontroller:4 statü, aktif kredi9, çok saatli derste tek kredi sayımı, öğrenci/dönem
ayrımı, tüm alanlar, aktif program/saatsiz kimlikler, Pazartesi→Pazar ve eşit
saatlerde kimlik sırası, time(7) JSON tick dönüşü, boş/pasif sonuç,404/400, tekrarlı
GET ve fresh context'te değişmeyen fixture içeriği. Gerçek SQL interceptor4 ve24
kayıtta4 SELECT, boş/pasif yolda3 SELECT; filtre/projection ve tracker boşluğu.
Yüksek kredi fixture'ı Int32 üstü toplamı ve aynı dersin farklı şubelerinin ayrı
sayımını sınar. SQL alias/parametre adı/süre snapshot'ı yoktur.

`-Mode summary -ProbeFailure` kontrollerden sonra bilerek assertion üretir;
finally fixture temizliği ve süreç kapatması çalışırken üst komut exit1 vermelidir.
Yalnız oluşturulan öğrenci/açılış/saat/kayıt/dönem/ders kimlikleri FK sırasıyla
silinir. App/lab kullanılmaz, test DB şeması tutulur, sequence sıfırlanmaz.

## Gün 20 — Bellek içi uygunluk senaryosu Builder

`EligibilityScenarioBuilder` yalnız test derlemesindedir. Kullanım yerleri:
- `EligibilityChecks.Unit`: yeni–mevcut ve yeni–yeni saat çakışması, üç ret türünün
  birlikte/sıralı dönmesi ve farklı dönem dışlamasını zaten birlikte sınayan fixture.
  Aynı beklenen ret türleri/kimlikleri/sırası korundu; testler kopyalanmadı.
- `DecoratorChecks.Run`: boş ve kredi+ön koşul retli gerçek evaluator eşdeğerlik
  girdileri. Beklenen davranış karşılaştırması aynı kaldı.
- `BuilderChecks.Run`: builder'ın kopyalama ve yapısal sözleşmesi.

Kredi sınırı kurucuda zorunlu; kimlik/dönem/kredi/durum ve saatler açık verilir.
Başlangıçta koleksiyonlar boştur: ön koşul/tamamlanan ders/saat/kayıt yoktur.
Bir builder, verisi zaten tek öğrenciye daraltılmış senaryo içindir; saf evaluator
StudentId kullanmadığı için sahte öğrenci entity'si/kimliği eklenmez.
`Request`, `Existing`, `WithSlots`, `Requires`, `Completed`, `Build` adımları vardır.
Existing durumları değiştirilmez; yalnız Submitted/Approved, üretim değerlendirme
imzasının beklediği aktif kredi ve mevcut program girdisine hazırlanır.

Her Build yeni dış listeler/dictionary ve nested schedule/prerequisite değerleri
üretir; gizli reset yoktur. Dış koleksiyonlar ekleme anında kopyalanır. Listeler
salt okunur sarmalanır, kredi dictionary'si ayrıca kopyalanır; prerequisite kümeleri
mevcut FrozenSet, slot elemanları mevcut sealed/get-only WeeklyTimeSlot'tır.
Bu immutable elemanları paylaşmak güvenlidir; keyfi mutable entity'ler kabul edilmez.
Builder değiştirilebilirdir, testler arasında paylaşılmaz; thread-safe değildir.

Pozitif kimlik/kredi/sınır, geçerli enum, tanımlı hedef ve slot constructor kuralları
korunur. Aynı açılışı tekrar tanımlamak (aynı veya çelişkili değerlerle) hata verir;
birebir aynı saat tekrarı da hata verir, sessizce tekilleştirilmez. Farklı ama
örtüşen saatler kabul edilir. Gerekli/tamamlanan dersler üretim girdisi gibi kümedir.
Hatalı ekleme mevcut hazırlığı değiştirmez. Build uygunluk hesaplamaz, DB/HTTP
çağırmaz, ders tamamlamaz veya kredi/saat düzeltmez. DB fixture temizliği yönetmez.

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode builder
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode eligibility
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode specifications
```

Builder modu saf sözleşme kontrolleri ve refactor edilen EligibilityChecks.Unit'i
DB olmadan çalıştırır; unit/full de builder sözleşmesini içerir.
`-Mode builder -ProbeFailure` kontrollü assertion ile üst çalıştırıcıya exit1
taşınmasını sınar. Yeni framework/proje yok; iki kaynak mevcut geçici console
projesine açık dahil edilir, web derlemesinden dışlanır.

## Gün 21 — Observer kontrolleri

`-Mode unit` saf dağıtımı DB olmadan içerir. `-Mode observer`, aynı saf kontrollerin
ardından mevcut güvenli adlandırma/sahiplik kontrolüyle yeni izole LocalDB oluşturur.
Gerçek EnrollmentEndpoints, scoped DI ve geçici Kestrel host'unda test observer'ları
kullanılır. `-Mode full` bu kontrolleri mevcut regresyonlarla birlikte çalıştırır.
`-Mode observer -ProbeFailure` DB oluşturmadan assertion üretir, üst runner exit1 döner.

ObserverChecks: sıfır/tek/iki observer, aynı olay/token, bir çağrı, TCS ile sıralı await,
null, güvenli log alanları, beklenmeyen hata ve token iptal değilken OCE, işbirlikçi
iptal. Gerçek SQL/HTTP: bağımsız context Approved okur; 200/409/404/akademik400,
SQL CHECK ile gerçek UPDATE hatasında500 ve olay yok; observer hatasında200;
uygulama sınırında kontrollü commit sonrası iptal; dış transaction reddi.
Üretim log observer'ı ile onay SELECT sayısı önce/sonra7 olarak karşılaştırılır.

Oluşturma/submit/reject olay üretmez. Test CHECK'i kendi yeni DB'sinde geçici olarak
kurulur ve kaldırılır. FK sırasıyla yalnız fixture kayıtları finally temizlenir;
host ve API süreçleri kapatılır. Temizlik hatası ayrı raporlanır, asıl assertion'ı
gizlemez. SQL/log çıktıları yalnız bellektedir. DB şeması test sonunda korunur;
sequence sıfırlanmaz. App/lab hedeflenmez. Ağ kullanımı yalnız yerel HTTP'dir;
gerçek bildirim gönderilmez. Kalıcı teslim/çökme kurtarma/eşzamanlı onay garantisi
bu testlerle kanıtlanmaz.

## Gün 22 — Direct EF / dar repository karşılaştırması

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode repository
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode unit
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```

Repository modu gerçek LocalDB gerektirir; birim testi değildir. Mevcut runner her
çalışmada yeni, ad/sahiplik kontrolü yapılmış izole DB oluşturur. Üretim app/lab
DB'leri hedeflenmez. EnrollmentRepositoryExperiment ve RepositoryChecks yalnız
geçici test projesine açık dahil edilir; web derlemesinde bulunmadıkları sınanır.

Tek harness iki erişim yoluyla Find/null/tracking, Add/Added, ayrı context'ten save
öncesi/sonrası görünürlük, gerçek FK hatasında tek save rollback, cancellation ve
kapalı context yaşam süresini kontrol eder. Mevcut SQL capture filtre/parametre ve
projection'ı gerçek yürütülmüş komutta inceler; log dosyası üretmez. İki küçük bileşen
ortak context'e ekler, SaveChanges'i çağıran çalıştırır. Repository context'i
oluşturmaz veya dispose etmez. Başarısız context sonraki doğrulamada kullanılmaz.

Full, mevcut RelatedWriteChecks.Tracking deneyini de çalıştırır: bağımsız iki
SaveChanges artık ayrı context'lerde gerçekleştirilir; ikinci gerçek CHECK hatası
ilk kesinleşmiş kaydı geri almaz. A/B/C/D deneyleri kopyalanmadı. Batch INSERT sonrası
rollback ve commit sonrası observer kontrolleri mevcut testlerden yeniden kullanılır.

`-Mode repository -ProbeFailure` fixture sonunda kontrollü assertion üretir;
finally temizliği çalışır, runner exit1 döner. Yalnız testin kimlikleri FK sırasıyla
silinir, sequence sıfırlanmaz, test DB şeması tutulur. Temizlik hatası ayrıca
raporlanır, asıl hatayı gizlemez. Context'ler await using, API süreçleri runner finally
ile kapatılır. GetAll/bellekte Where örneği yalnız belgede açıklanır; gerçek katalog
SQL davranışı mevcut full kontrolleriyle sınanır. Sahte repository SQL kanıtı değildir.

## Gün 23 — CQRS ve Mediator sınırları

`-Mode cqrs`: üretim komut/query constructor sınırları, paylaşılan alan doğrulaması,
varsayılan/Trim/Int32 offset, null uygulama çağrısı; üretim dışı typed Mediator'da
komut ve sorgu yönlendirmesi, doğrudan eşdeğerlik, tek handler/tek çağrı, aynı token,
sonuç nesnesi, TCS ile bekleme/hata/cancellation ve eksik handler. İki sabit typed
constructor slot'u olduğundan duplicate registration temsil edilemez. DB gerekmez.
`-Mode cqrs -ProbeFailure` DB oluşturmadan kontrollü assertion ve exit1 üretir.
Deney kaynakları yalnız geçici test projesinde; üretim assembly'sinden dışlanır.

Mevcut CatalogChecks.SqlEvidence artık doğrudan CourseCatalogReader'ı çalıştırır;
WHERE/parametre/COUNT/ORDER BY/OFFSET-FETCH/yalnız DTO sütunları ve boş tracker
beklentileri korunur. Invalid HTTP query DB sorgulamaz. Katalog HTTP senaryoları
aynı kalır; fixture kimlik/kod/ad/kredilerinin değişmediği yeni context ile ayrıca
karşılaştırılır. Katalog ve kayıt senaryoları kopyalanmadı. `-Mode full` bu gerçek
SQL/HTTP kontrollerini, akademik/state/observer ve batch yazma hatası regresyonlarını
çalıştırır; `-Mode unit` saf CQRS deneyini de içerir. Temizlik/izolasyon değişmedi.

```powershell
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode cqrs
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode core
pwsh -NoProfile -File scripts/verify-course-writes.ps1 -Mode full
```
