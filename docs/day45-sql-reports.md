# Gün 45 — SQL kayıt raporları

## İhtiyaç ve seçim

Açılış doluluğu ve danışmana göre bekleyen kayıtlar mevcut katalog/özet
yanıtlarında yoktu. İki küçük DTO sorgusu eklendi. Öğrenci kredi yükü zaten
dönem özetindeydi: ikinci endpoint yerine aynı sözleşmenin toplamı SQL'e taşındı.
Mevcut scoped DbContext ve cookie politikaları yeterli; repository, rapor
framework'ü, paket, model/migration veya yeni mimari katman yok. Ayrı ADR
gerektiren mimari karar alınmadı.

## Granülarite ve kod konumları

- Application/RegistrationReports.cs: dolulukta bir satır bir açılıştır.
  CourseCode aynı olsa da farklı offeringId ayrı satırdır. Aktif enrollments
  önce CourseOfferingId ile gruplanır, açılışlara LEFT JOIN yapılır.
  Aktif sayı Submitted/Approved; Draft/Rejected/Dropped sayılmaz.
  OccupiedSeats saklanan sayaç, ActiveCount sorgulanan sayıdır.
  Yüzde 100m * ActiveCount / Capacity: ondalık bölme; boş açılış 0.
- Aynı dosyada PendingAsync: bir satır danışman kimliği grubudur, yalnız
  Submitted sayılır. AdvisorUserId NULL ayrı “atanmamış” grubudur. Hesap
  adı/e-posta veya öğrenci listesi verilmez. Sıfır bekleyen danışman satırı yoktur.
- Application/TermRegistrationSummaryFacade.cs: öğrenci/dönem toplamı,
  SUM öncesi nullable long dönüşümü (SQL bigint), COALESCE 0. İki ayrı
  üç kredili ders 6 eder. Aynı dersin iki şubesi iki aktif enrollment ise
  ayrı sayılır; SUM(DISTINCT Credits) kullanılmaz.
- Endpoints/RegistrationReportEndpoints.cs: yönetim raporları cookie-only
  ManageAcademicCatalog/StudentAffairs. Sayfa 1+, boyut 1..100, güvenli int offset.
  Pozitif bulunmayan dönem boş sayfa; ayrı varlık kontrolü sorgusu yok.
- Endpoints/TermRegistrationSummaryEndpoint.cs: mevcut kaynak bazlı erişim
  korunur (kendi Student veya atanmış Advisor). no-store eklendi.
  Mevcut öğrenci/dönem 404 ve girdi 400 sözleşmesi korunur.
- scripts/ReportsChecks.cs: elle hesaplanabilir fixture, gerçek HTTPS/SQL,
  parametreli SQL dosyalarını uygulama sonuçlarıyla karşılaştırma.
- scripts/SummaryChecks.cs: mevcut 4 ve 24 satırlı özet kontrolleri korunur;
  artık toplam için ek sabit sorgu nedeniyle 5 okuma, aktif saat yoksa 4.
- scripts/ResourceAccessChecks.cs ve CookieSessionChecks.cs: gerçek test
  hostuna aynı rapor endpoint'leri; rol ve bearer/cookie ayrımı.

## SQL kavramları

sql/app/report-occupancy-join.sql boş açılışları korur. Aktif durum koşulu
LEFT JOIN ON içindedir; WHERE e.Status yazmak boş satırları kaybettirirdi.
COUNT(e.Id) NULL sağ tarafı saymaz; COUNT(*) boş açılışın dış join satırını
sayardı. Önceden gruplanmış alt sorgu/CTE içindeki COUNT(*) ise gerçek
enrollment satırlarını saydığı için doğrudur.

report-occupancy-subquery.sql ve report-occupancy-cte.sql aynı hesabı iki
yazımla gösterir. CTE adlandırılmış sorgu ifadesidir; geçici tablo/materialization
veya performans garantisi değildir. Plan maliyeti burada benchmark edilmedi.

report-pending-having.sql: WHERE dönem/durumla giriş satırlarını, HAVING
minimum bekleyen sayısıyla oluşmuş grupları filtreler.
report-submitted-exists.sql: bir veya birçok Submitted olsa da açılışı bir
kez verir; EXISTS varlık sorusudur.
report-active-credits.sql: SUM öncesi bigint, boş kümede COALESCE.
Örnekler salt SELECT ve SQL parametreleri kullanır; üretim sorguları LINQ kalır.
Saat tablosu hiçbir toplam sorgusuna katılmaz; bire-çok join çoğalması yoktur.

## Erişim ve okuma zamanı

Yönetim raporları StudentAffairs gerektirir; rol/sahiplik denetimini frontend
yapmaz. Öğrenci özeti mevcut hesap→Student ilişkisini kullanır; istemci UserId
ile yetki seçemez. Advisor genel yönetim raporu göremez. Raporlar kişisel/yönetim
verisi olduğundan no-store. Public katalog değişmez.

Her yeni sayfa tek SQL komutuyla okunur. Bu, global transaction snapshot
garantisi değildir; mevcut SQL Server isolation davranışı korunur. Ayrı sayfa
istekleri arasında veri kayabilir. Özetin varlık, enrollment, toplam ve saat
sorguları farklı commit anlarını görebilir; yeni SQL toplamı önceki satır
sorgusundan farklı anı temsil edebilir. Canlı sayaç–sayım farkı tek gözlemle
kalıcı invariant bozukluğu sayılmaz. Yeni kilit/isolation ayarı yok.
Kontrollü, yazarsız fixture karşılaştırması canlı eşzamanlılık garantisi değildir.

## Doğrulama

README'deki reports/summary/resource-access/full komutlarını kullan.
reports fixture'ı: 2 dönem, 3 öğrenci, eşit kredili 2 ders, farklı şubeler,
bütün 5 enrollment durumu, üç saat satırı ve boş açılış. Beklenen doluluk
[2,1,1,0,0,0], kredi 6/6/0, danışman grupları 1/1. Bir ve altı sonuç için
doluluk sorgu sayısı 1; bütün projection'lar tracking oluşturmuyor.
Yeni context snapshot'ı okumaların fixture verisini değiştirmediğini sınar.
Mevcut summary testi Int32 üstü toplamı ve daha çok satırda sabit sorgu
sayısını ayrıca doğrular. SQL tam metin/alias snapshot'ı kullanılmaz.

Gerçek çalıştırma sonuçları ve sınırlar docs/progress.md Gün 45 kaydındadır.
Büyük veri performansı, rapor sayfalarının snapshot tutarlılığı ve farklı
açılış kredi/saat yarışları bu çalışmanın garantisi değildir.
