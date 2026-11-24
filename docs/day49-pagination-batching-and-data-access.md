# Gün 49 — pagination, parçalı okuma ve veri erişimi

## Üretim sınırı

`CourseCatalogReader` içindeki ortak Filter, offset ve keyset tarafından
kullanılır. `CourseKeysetQuery` mevcut CourseCatalogQuery giriş/trim/kredi/
pageSize korumasını kullanır; afterId ayrıca pozitif olmalıdır.
`CourseCatalogEndpoint.GetKeysetAsync` ve CourseEndpoints yeni public
`/courses/catalog/keyset` GET'ini eşler. Mevcut offset sözleşmesi, erişim ve
collation korunur. Yeni route authentication/CSRF yazma yolu değildir.

Id benzersiz artan sıralama; Id>afterId ve TOP(pageSize+1) SQL'de.
Fazladan satır çıkartılarak hasMore hesaplanır. Devam varsa son dönen Id,
yoksa null; toplam COUNT yok. Silinmiş devam satırı aranmaz. Gönderilen boş,
bozuk, negatif veya int sınırını aşan afterId400. DTO yalnız id/code/name.
Offset rastgele sayfaya atlamaya/toplam göstermeye uygundur; keyset yalnız
ileri devamı kolaylaştırır, rastgele sayfa numarası sağlamaz.

## Deney alanı ve sahiplik

`scripts/PaginationChecks.cs` / `pagination` modu:6000 ders, bilinen kimlik
boşluğu,73'lük parçalar. Sonuçlar bütün liste halinde biriktirilmez; count,
long Id toplamı, son Id ve kesin artış kontrolü tutulur. Fixture hazırlama
listesi bundan ayrıdır. Son parça, boş sonuç ve iptal edilmiş token kontrolü
vardır. EF yazma batching'i ise bir SaveChanges içindeki yazmaların komut
paketlerine bölünmesidir; okuma parçasının boyutuyla aynı kavram değildir.
Kalıcı checkpoint/job/retry yok.

Doluluk raporu: üretimde `RegistrationReports.OccupancyAsync` LINQ kalır.
Deneyde aynı alan/filtre/sıra için EF `SqlQueryRaw<OfferingOccupancy>` +
SqlParameter ve Dapper `QueryAsync` + parametre nesnesi kullanılır. İki açılış:
4 kapasitede Submitted+Approved=2/%50; Draft bulunan diğerinde aktif0/%0.
LEFT JOIN boş aggregate'ı COALESCE ile0 yapar; iki saat kaydı sayıyı çoğaltmaz.
Olmayan dönem üç yolda da boş. Üç çıktı birbirine değil aynı bağımsız iki
DTO'ya eşitlik assertion'ıyla doğrulanır. Ham SQL ondalıklı çarpma/bölme yapar; ek yuvarlama uygulamaz.

Dapper2.1.66 yalnız `verify-course-writes.ps1` tarafından üretilen deney csproj'una
referanstır. Üretim csproj'una eklenmez; framework veya repository yok.
[Resmi paket](https://www.nuget.org/packages/Dapper/2.1.66) net8+/netstandard
uyumluluğunu, [proje](https://github.com/DapperLib/Dapper) parametreli sorgu ve
buffering kullanımını açıklar. En yeni sürüme yükseltme amaçlanmadı.

Her rapor tamamen okunur; Dapper buffered, EF ToListAsync. Bu karşılaştırma
unbuffered üstünlüğü iddiası değildir. EF kendi context/bağlantısını yönetir;
GetDbConnection ayrıca dispose edilmez. Dapper ayrı SqlConnection'ını await
using ile sahiplenir. Rapor command timeout30s ve iki dakikalık deney token'ı
iletir; DB arızası/iptal başarıya çevrilmez. Aynı context'te paralellik yok.

## Gerçek ölçümler — 24 Kasım 2026

Son hedefli koşu: izole LocalDB ve sertifikası sınırlandırılmış gerçek HTTPS.
Her yol bir ısınma, üç tamamlanmış okuma. Stopwatch sonucu tam materyalizasyon
ve assertion tamamlanmasını kapsar. EF reader kapanışında IO bilgi mesajları
alınamadığından gerçekten yürütülen SQL ve typed parametreler interceptor ile
yakalanıp ayrıca tekrar yürütülür; tüm result set'ler tüketilir. EF için IO ve
actual XML plan bu tekrarındır, uygulama süresine dahil değildir. Dapper IO
asıl çağrıdan alınır. Bu ayrım araç kıyasının sınırıdır.

Derin offset5000; aynı filtre/sıralama/pageSize20. Boundary, fixture'ın
sıralanmış4999 indeksli satırından hazırlıkta bulunur; Id=offset varsayılmaz.
Bu ön hazırlık maliyeti süreye dahil değil; gerçek istemci önceki sayfanın
nextAfterId'sini taşır. Offset sayfa sorgusu ölçülür, endpoint'in ayrı COUNT'u
bu karşılaştırmaya dahil değildir. Keyset hasMore için21 satır okur,20 döner.

| Okuma | Logical reads | Tamamlanma ms (üç tekrar) | Plan |
| --- | --- | --- | --- |
| Derin offset |56/56/56|6.19 /6.46 /8.69|Top, clustered scan|
| Keyset |3/3/3|1.53 /1.88 /1.56|Top, clustered seek|
| Rapor LINQ |10/10/10|2.00 /2.15 /2.48|aggregate, joins, Top|
| Rapor EF raw |10/10/10|1.16 /0.99 /1.20|aggregate, joins, Top|
| Rapor Dapper |10/10/10|0.66 /0.53 /0.54|ayrı Dapper XML planı alınmadı|

Rapor IO toplamı: Enrollments4 + Courses4 + CourseOfferings2. XML planın
runtime sayaçları doğrulandı, geçici XML/fixture commit edilmedi. Global cache
boşaltma, hint veya süre eşiği yok. Bu küçük, sıcak, tek ortam ölçümü genel
ORM hız sıralaması veya üretim SLA garantisi değildir.

İlk/son/boş sayfa, maksimum sayfa, geçersiz girdiler, trim+arama+kredi,
silinmiş cursor, eksiksiz/tekrarsız parçalı okuma, cancellation ve no tracking
geçti. Katalog okumasından sonra yeni context tüm ders alanlarını karşılaştırdı.
Rapor sonrası yeni context ders/enrollment/açılış snapshot'ı değişmedi.

Kontrollü mutation: ilk sayfadan önceki bir satır silinince offset sonraki
hayatta kalan satırı atladı; keyset son Id'den doğru devam etti. Sonradan
büyük Id ile eklenen ders sonraki keyset sayfasında göründü. Bu snapshot değildir;
özellikle filtre alanı değişimleri ve bütün eşzamanlı yazma kombinasyonları
ölçülmedi. Büyük Id eklemesi offset son bölümünde de görünür; başa ekleme/Id değiştirme deneyi yapılmadı.

Restore/build0 uyarı/0 hata; hedefli pagination başarılı. Boş cursor/snapshot
kontrolleri ve eksik EF IO yakalaması tamamlandığı için hedefli koşu yenilendi;
sırf başarılı testi tekrar etmek için yapılmadı. Model/snapshot uyumlu, pending
migration yok; şema/index/migration değişmedi. Full bir kez çalıştı, exit0. Son test derlemesi full executable kilidinde kısa süre MSB3026 yeniden denemeleri yaptı; full bittikten sonra derleme ve hedefli koşu başarılı tamamlandı.
Fixture FK sırasıyla temizlenir, API finally'de kapatılır; app/lab hedef değildir.

## Sınırlar

Kayıt/yerleştirme/idempotency/transaction koduna dokunulmadı. Yeni sayfalama
rotası diğer public katalog gibi salt okunur. Filtreler devamda değişirse yeni
bir gezinme anlamına gelir. Ortak snapshot, sabit checkpoint ve yetkili kişisel
veri listesi sağlanmaz. Sıradaki gün50; kapsam sonraki görevde belirlenecek.
