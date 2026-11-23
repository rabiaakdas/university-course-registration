# Gün 48 — birleşik index ve gerçek execution plan

## Hedef ve kod

`Application/EnrollmentOperations.cs / PlaceWaitingAsync` içindeki aday sayfası:
CourseOfferingId eşitliği, Waiting, QueueNumber > after, QueueNumber artan,
TOP 32. Yerleştirme ve yetkilendirme kodu değiştirilmedi.
`Data/UniversityDbContext.cs` index eşlemeleri aynen korundu.
`IndexPerformanceChecks.cs` aynı LINQ ifadesini gerçek EF sağlayıcısında yürütür;
private yerleştirme işlemini tetiklemez. Kaynak ifadesinin değişmesine karşı
küçük kontrol vardır; uygulama sorgusu değişirse deney de gözden geçirilmelidir.
Gerçek çalışmış command interceptor'la SQL ve typed parametreler yakalanır;
ToQueryString tek başına ölçüm olarak kullanılmaz.

Gerçek EF çıktısı (fixture parametreleri sır değildir):

```sql
SELECT TOP(@p) [w].[Id], [w].[ClosedAtUtc], [w].[ClosureReason],
 [w].[CourseOfferingId], [w].[JoinedAtUtc], [w].[QueueNumber],
 [w].[Status], [w].[StudentId]
FROM [WaitlistEntries] AS [w]
WHERE [w].[CourseOfferingId] = @offeringId AND [w].[Status] = 0
 AND [w].[QueueNumber] > @after
ORDER BY [w].[QueueNumber]
```

@p SQL int=32; @offeringId int; @after bigint (başlangıçta0, son sayfa
örneğinde6002). EF sabit Waiting enum'unu0 literal üretir; diğer parametreler
ADO.NET SqlParameter olarak aynen klonlanır. Ham string ile değer eklenmez.

## Mevcut index ve aday

- Clustered PK: Id.
- Unique QueueNumber: genel sıra numarası benzersizliği.
- Filtered unique (StudentId, CourseOfferingId), WHERE Status=0:
  aynı öğrencinin ikinci Waiting başvurusunu engeller; açılışın FIFO sayfası
  için baştaki StudentId uygun erişim yolu değildir.
- Nonunique (CourseOfferingId, Status, QueueNumber): iki eşitlikten sonra
  aralık ve sıralama. QueueNumber'ı başa almak açılış/durum daraltmasını
  aynı şekilde sağlamaz; key sırası yalnız sütunların varlığı değildir.

İzole aday, son index'i DROP_EXISTING ile aynı anahtar sırasıyla değiştirir:
INCLUDE(StudentId, JoinedAtUtc, ClosedAtUtc, ClosureReason).
Id clustered anahtardan zaten taşınır. Aynı ön eke ikinci index eklenmez.
Aday filtered değildir; yeni filtered index kullanılabilirliği iddia edilmiyor.
Özgün filtered unique index ve bütün kısıtlar deney boyunca korunur.

## Yöntem ve sonuçlar — 23 Kasım 2026

Yeni izole, migration'ları uygulanmış SQL Server LocalDB. Aynı6000 öğrenci,
üç açılış:4 Waiting;6000 Waiting;6000 satırın6'sı Waiting,5994'ü Cancelled.
Son sayfa parametresi yoğun açılışın son2 kaydını seçer. Aynı veri ve
parametreler önce/sonra kullanıldı. Fixture'dan bağımsız hesaplanan beklenen
Id/StudentId/FIFO sırası hem EF hem ölçüm SQL'i sonuçlarıyla karşılaştırıldı.

Fixture sonrası yalnız test tablosunda FULLSCAN statistics. Her varyant ve
parametre için bir ısınma, üç ölçüm. Global cache temizliği, index hint,
FORCESEEK veya RECOMPILE yok. SET STATISTICS IO/TIME/XML ile gerçek çalışma;
XML'de RunTimeCountersPerThread varlığı doğrulanır. Plan XML dosyaları geçici,
commit edilmez. Beklenen index veya plan şeklini zorlayan assertion yok.

| Parametre | Dönen satır | Önce logical reads (üç koşu) | Kapsayıcı (üç koşu) |
| --- | ---: | --- | --- |
| Küçük | 4 | 10/10/10 | 2/2/2 |
| Yoğun | 32 | 66/66/66 | 2/2/2 |
| Seyrek Waiting | 6 | 48/48/48 | 2/2/2 |
| Kuyruk sonu | 2 | 6/6/6 | 2/2/2 |

Son koşuda tüm physical reads0. Önce CPU0ms, elapsed0–1ms; aday CPU çoğunlukla
0ms, iki örnekte15/16ms, elapsed0–1ms. İlk başarılı koşuda elapsed0–3ms idi.
Bu küçük süreler ve sayaç çözünürlüğü hız garantisi veya CPU kazancı değildir.
STATISTICS araçlarının ek maliyeti de ölçüme dahildir. Ham çıktı commit edilmez.

Önce: Top → Nested Loops → mevcut birleşik Index Seek + PK Clustered Index
Seek ile eksik alanların tamamlanması. XML'in gösterdiği fiziksel ad Clustered
Index Seek'tir; bunu genel anlamda lookup erişimi olarak yorumluyoruz.
Sonra: Top → aynı isimli kapsayıcı Index Seek; PK erişimi yok. Her ikisinde
Sort veya Scan operatörü yok. IO mesajındaki Scan count=1, planın Scan
operatörü kullandığı anlamına gelmez.

Top tahmini her parametrede2.83031, gerçek4/32/6/2. Küçük parametreden sonra
aynı parametreli planın yeniden kullanımıyla tutarlı bu fark, tek tahminin
bütün dağılımları temsil etmediğini gösterir; yalnız bu veriden kök nedeni
kesinleştirmiyoruz. PK dalındaki EstimateRows=1 per-execution tahminidir;
raporlanan ActualRows toplamını birebir aynı granülarite sanmamak gerekir.
Yalnız seek bulunması veya maliyet yüzdesi başarı ölçütü yapılmadı.

## Karar, maliyet ve sınırlar

Kapsayıcı adayın IO kazancı gerçektir; kazanç yok diye sunulmuyor. Buna rağmen
üretim index'i değişmedi: mevcut erişim32 satırla sınırlı, sıralı seek var;
ölçülen gecikme çok küçük ve yazma/depolama/gerçek çağrı sıklığı ölçülmedi.
ClosureReason gibi kapanış alanlarını leaf'e taşımak daha geniş index ve
kapanışlarda ek bakım gerektirir. Yoğun gerçek iş yükü kanıtıyla tekrar
incelenebilir. Ayrıntı: `adr/0048-retain-waitlist-index.md`.

Restore/build başarılı (0 uyarı/0 hata). İlk test projesi derlemesindeki
CleanupFailure yapıcı uyumsuzluğu düzeltildi. Hedefli mod geçti; index adı
izini ve koşuya özel plan klasörünü ekledikten sonraki son koşu da exit0.
Yeni context bütün waitlist alanlarının snapshot'ını, durum/satır sayılarını,
boş enrollment ve0 OccupiedSeats değerlerini doğruladı. Tracking0, model/snapshot
uyumlu. Üretim sorgusu/şema değişmedi; migration ve full/HTTP regresyonu
çalıştırılmadı. Yeni index altında üretim lifecycle yazmaları ölçülmedi.
Yetki/CSRF/FIFO koordinasyonuna dokunulmadı; bu laboratuvar HTTP güvenlik testi
veya gerçek yerleştirme transaction süresi ölçümü değildir.

İki başarılı koşunun fixture'ları temizlendi, özgün index geri geldi, geçici
planlar silindi; yalnız bu koşuların DB'leri ayrıca kaldırıldı. App/lab veya
önceden mevcut DB'lere dokunulmadı. Sıradaki gün49; kapsamı sonraki görevde
belirlenecek, burada önceki günler yeniden tamamlandı sayılmıyor.
