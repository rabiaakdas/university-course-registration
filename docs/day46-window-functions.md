# Gün 46 — Window functions ve bekleme pozisyonu

## İhtiyaç, seçim ve maliyet

Tek başvuru GET'i zaten COUNT ile bir pozisyon veriyordu. Bu basit yol korundu.
Toplu ihtiyaç için mevcut öğrenci waitlist kaynağına GET listesi eklendi;
ayrı public kişi listesi veya yönetim raporu oluşturulmadı. Öğrenci yalnız
kendi Waiting başvurularını görür. Toplam bekleyen, kendi açılışındaki sıra
bağlamını verir; başka öğrencilerin kimlikleri/kişisel verileri verilmez.

Application/WaitlistPositions.cs, EF'nin Database.SqlQuery<T> parametreli
unmapped DTO yolunu kullanır. Window SQL küçük ve görünür tutuldu; model,
repository, Dapper veya yeni paket gerekmedi. LINQ çevirisi varmış gibi
taklit edilmedi. Bu bir genel rapor altyapısı/mimari katman değişikliği değildir.

Maliyet: her ilgili açılışın Waiting kümesi sıralanıp sayılır; bir sayfada az
başvuru dönmesi az satır işlendiği garantisi değildir. Tekil COUNT her yerde
kaldırılmadı. İndeks/plan optimizasyonu ve büyük veri benchmark'ı yapılmadı;
48. günün konusu. CTE/derived table otomatik materialization veya hız garantisi
vermez.

## Sorgu sırası ve türler

1. Yalnız Waiting temel satırları alınır. Her satır bir WaitlistEntry'dir.
2. EXISTS yalnız çağıranın aktif başvurusu bulunan açılışları seçer; ilgili
   açılışın diğer öğrencilerini window girdisinden çıkartmaz.
3. ROW_NUMBER OVER(PARTITION BY CourseOfferingId ORDER BY QueueNumber)
   açılış başına 1'den başlayan kesintisiz konum verir. QueueNumber DB'de
   unique olduğundan ayrıca eşitlik bozucu gerekmez. Numara boşlukları geçerli.
4. COUNT_BIG(*) OVER(PARTITION BY CourseOfferingId) bütün Waiting sayısını
   her başvuruya ekler; ORDER BY eklenmediği için running count değildir.
5. Dış sorguda StudentId filtresi, açık CourseOfferingId/QueueNumber ORDER BY
   ve OFFSET/FETCH uygulanır. Önce öğrenci filtresi yapılsaydı herkes 1 olurdu;
   önce sayfalansaydı global konumlar kaybolabilirdi.

ROW_NUMBER ve COUNT_BIG SQL bigint döndürür; WaitingPosition.Position ve
TotalWaiting C# long'dur. Id/QueueNumber da long. Parametreler gerçek SQL
parametreleridir, değerlerden SQL metni birleştirilmez. DTO EF tracker'a entity
eklemez. Saat/başka bire-çok JOIN yok; sayı çoğaltılmaz. GROUP BY satırları
gruba indirgerken window fonksiyonları temel satırları korur.

## Öğrenme SQL'i

sql/app/window-functions-example.sql üretim tablosu eklemeden iki CTE cohort'u
ve altı satırla çalışır. A puanları90/90/80/70 için:

| Fonksiyon | Sonuç |
|---|---|
| ROW_NUMBER (Score DESC, Id) | 1,2,3,4 |
| RANK (yalnız Score DESC) | 1,1,3,4 |
| DENSE_RANK (yalnız Score DESC) | 1,1,2,3 |
| ROWS birikimli Amount | 10,30,60,100 |
| RANGE birikimli Amount | 30,30,60,100 |

ROW_NUMBER sırası Id ile deterministiktir. RANK/DENSE_RANK'e Id eklemek puan
eşitliğini bozardı. ROWS açıkça UNBOUNDED PRECEDING ... CURRENT ROW kullanır;
eşit puanlı satırların kendi arası sırası Id ile sabittir. RANGE yalnız Score
sırasını kullanarak eşit puan grubunu birlikte kapsar. ROWS'a Id eklemek
fiziksel adımları sabitler; RANGE'e aynı unique Id eklense peer farkı kaybolurdu.
LAG/LEAD aynı deterministik sırada önceki/sonraki Amount'u okur; ilk/son satırda
NULL döner, bölüm sınırını aşmaz. B cohort'u hesapları bağımsız yeniden başlar.

## Erişim ve sınırlar

Endpoints/WaitlistEndpoints.cs içindeki yeni GET mevcut
WithStudentAccess(Manage) sınırını kullanır: cookie + Student + kendi UserId
bağlantısı. Advisor/StudentAffairs bypass yok; bearer-only 401. Kaynak yok veya
başkasına aitse404. Join/cancel POST, CSRF, açılış kilidi ve FIFO yerleştirme
kodu değişmedi. Tekil GET yanıtı korunup no-store eklendi; yeni liste de no-store.

Bu hesap yer ayırmaz ve concurrency koruması değildir. Bir sonraki istek veya
sayfa başka commit anını görebilir; konum iptal/yerleştirme ile değişebilir.
QueueNumber tıklama/commit sırası garantisi değildir. Farklı açılış kredi/saat
ve danışman TOCTOU açıkları bu çalışmayla çözülmez.

## Doğrulama ve kod konumları

- scripts/WindowFunctionChecks.cs: iki açılış, Waiting/Cancelled/Placed/Ineligible,
  sıra boşlukları ve üç saat satırı. Her açılışta pozisyon1/2/3, toplam3.
  Hedef öğrencide gerçek konum2/1; sayfa2 ilk açılışı filtreleyince ikinci
  konum1 aynen korunur. Diğer öğrenciye erişim/rol/no-store/girdi sınırları.
- Gerçek SQL interceptor: ROW_NUMBER/COUNT_BIG/PARTITION/OFFSET, bağlı student
  parametresi; bir veya iki sonuçta tek sorgu, tracking yok. HTTP kaynak
  kontrolü ayrıca sabit bir projection sorgusu yapar; öğrenci/açılış döngüsü yok.
- Öğrenme dosyasının altı satırında ranking, ROWS/RANGE, LAG/LEAD beklenen
  değerleri tek tek SqlDataReader Int64/int/null olarak kontrol edilir.
- Yeni context snapshot'ıyla fixture değişmemesi; finally FK sırasıyla temizlik.
  Kapalı başvurular okuma fixture'ıdır, gerçek yerleştirme deneyi diye sunulmaz.
- scripts/ResourceAccessChecks.cs: yeni liste için sahip/başkası/Advisor/rolsüz/
  anonymous ve geçerli bearer-only retleri mevcut HTTPS hostunda.
- scripts/verify-course-writes.ps1: window-functions modu; full aynı testleri
  yeniden kullanır. Mevcut waitlist ve reports regresyonları korunur.

Gerçek çalıştırmalar docs/progress.md Gün46 kaydında; komutlar README'dedir.
Browser, büyük veri performansı ve bütün eşzamanlı okuma interleaving'leri
bu testlerin kapsamı değildir.
