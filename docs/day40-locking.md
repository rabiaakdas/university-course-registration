# Gün 40 — Blocking, deadlock ve write skew

`scripts/LockingLabChecks.cs`, üretim koduna kilit/retry eklemeden gerçek SQL
Server LocalDB üzerinde altı deney çalıştırır. Veritabanı adı yalnız bu koşu için
üretilir: `university_registration_tests_day40_<guid>`. Resources, Marks,
Departments ve Advisors tabloları test verisidir; gerçek danışman atama modeli
değildir. App/lab DB, production isolation ve migration değişmez.

```powershell
dotnet build --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode locking-lab
# Beklenen exit1: deneylerden sonra kasıtlı assertion ve finally temizliği.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode locking-lab -ProbeFailure
# Yeniden kullanılan gün39 SQL/DMV yardımcılarının smoke kontrolü.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode isolation-lab
```

## Gerçek sonuçlar — 12 Kasım 2026

| Deney | Kanıt ve yeni bağlantıdaki sonuç |
| --- | --- |
| Blocking | A X üzerinde yazma kilidi tutarken B için gerçek `LCK_M_X` gözlendi. A commit sonrası B ilerledi; X=2, Y=0. |
| Lock timeout | B önce Marks'a yazdı, sonra kilitli X için `LOCK_TIMEOUT 1000` ile gerçek SqlException 1222 aldı. XACT_STATE=1; açık rollback sonrası @@TRANCOUNT=0. B'nin işareti yok, yalnız A yazması kalıcı: X=1, Y=0. |
| Deadlock | A X→Y, B Y→X; ikisi ilk yazma ve işaret INSERT'ini tamamladıktan sonra ikinci kaynağa geçti. Tam bir 1205 ve bir commit. Kurbanın hem ilk satır artışı hem işareti yok; iki kaynakta yalnız kazananın katkısı var. |
| Tutarlı sıra | A ve B X→Y. B ilk kilitte `LCK_M_X` beklerken A Y'yi de yazıp commit eder; sonra B tamamlar. X=2, Y=2. |
| Write skew — beklenen kusur | İki SNAPSHOT transaction aktif sayısını2 okudu. Kendi satırının gerçek8 bayt rowversion'ını UPDATE koşuluna koyan iki işlem farklı satırları pasif yaptı. İkisi commit, aktif=0: ortak kural bozuldu. |
| Ortak bölüm kilidi — düzeltilmiş karşılaştırma | READ COMMITTED, RCSI OFF. Her işlem önce aynı bölüm satırını UPDLOCK,HOLDLOCK ile alır. B için `LCK_M_U` gözlendi. Kilit sonrası sayılar2/1; yalnız A pasif yaptı, kalıcı aktif=1 (B). |

Build restore dahil başarılı, 0 uyarı/0 hata. İlk hedefli koşu timeout kontrolünde
`XACT_STATE()` SQL smallint/CLR Int16 sonucunun ortak int scalar yardımcısına
uyuşmamasıyla başarısız oldu. Sorgu açık `CAST(XACT_STATE() AS int)` olarak
düzeltildi; başarısız koşunun DB'si de temizlendi. Son hedefli koşu exit0.
Hata probu bütün deneylerden sonra beklenen assertion/exit1 ve başarılı cleanup
verdi. Normal koşuda kurban B, probda kurban A oldu: test kurbanı önceden seçmez.
Gün39 isolation smoke da exit0; paylaşılan yardımcıların davranışı korundu.

## Kavramlar, kararlar ve sınırlar

Blocking döngü gerektirmez: kilit sahibi tamamlanınca bekleyen işlem ilerleyebilir.
Deadlock karşılıklı bağımlılık döngüsüdür; SQL Server'ın seçtiği kurban1205 alır ve
transaction'ı geri alınır. Test yalnız1205'i deadlock kanıtı kabul eder; client
timeout (-2), cancellation veya1222 aynı başarı değildir. Burada deadlock graph
ve Extended Events kaydı toplanmadı; iki ilk kaynağın edinilmesi, gerçek1205 ve
kalıcı rollback/survivor sonucu kanıttır.

1222 kilit bekleme süresinin dolmasıdır. Bu deneyde transaction hâlâ açıktı;
uygulama açık rollback ile daha önceki yazmayı da geri aldı. Dispose hata
yollarında ek güvence sağlar. Bağlantı unpooled olduğundan LOCK_TIMEOUT ayarı
başka bir işe taşınmaz. Lock timeout ile deadlock monitor farklı mekanizmalardır.

Tutarlı X→Y sırası bu deneydeki döngüyü önler; bütün iş yüklerinde deadlock olmaz
garantisi değildir. Daha fazla kaynak, indeks, sorgu planı ve farklı kilit
yükseltmeleri yeni döngüler yaratabilir. Üretime genel kilit altyapısı eklenmedi.

Write skew aynı satırın eski değerinin üzerine yazılması değildir: iki işlem
farklı satırları değiştirirken aynı çok-satırlı kurala güvenmiştir. Rowversion
koşulları iki ayrı satırda da geçerli kaldı. Ortak bölüm satırı üzerinden bütün
ilgili işlemler aynı koordinasyona katılırsa bu deneyin ortak kuralı korunur.
Koordinasyona katılmayan yazma yolu için garanti yoktur. Sayı kilit alındıktan
sonra READ COMMITTED altında tekrar okunur; SNAPSHOT'tan kalmış sayı kullanılmaz.
Maliyet bölüm başına seri çalışma ve kilit beklemesidir; üretime aktarılmadı.

Retry yapılmadı.1205 sonrasında yeniden deneme düşünülecekse yalnız son komutu
değil bütün transaction'ın iş kuralını yeniden değerlendirmek gerekir. Sınırlı
deneme, idempotency ve dış yan etki/yanıt kaybı ayrı tasarım konularıdır. Her SQL
hatasını retry etmek doğru değildir; bu laboratuvar güvenli retry sağlamaz.

## Koordinasyon, SQL ve temizlik

- `Ordered` ve `CoordinatedRule`: üç ayrı bağlantı (sahip, bekleyen, DMV
  gözlemcisi). Bekleme DMV'deki session/engelleyen çiftinden doğrulanınca sahip
  commit eder. Bekleyen ilk kilide ulaşamadan ortak bariyer beklenmez.
- `Pair` / `Rendezvous`: TaskCompletionSource ve15 saniyelik rendezvous; iki ilk
  kaynak veya iki snapshot okuması tamamlanmadan sonraki aşama başlamaz. Worker
  hatası eşini iptal eder; WhenAll bütün task/transaction'lar kapanana kadar döner.
- Deadlock ikinci komutuna35 saniye, worker çiftine45 saniye, bütün koşuya3 dakika
  tanınır. Normal komutlar15 saniye, DMV gözlemi8 saniye ile sınırlıdır. Rastgele
  Task.Delay yok;20ms timer yalnız gerçek DMV gözlemini örnekler, kanıt değildir.
- `IsolationLabChecks` içindeki mevcut Command/Scalar/Execute/Open/ObserveLock
  yardımcıları test assembly'sinde internal olarak yeniden kullanılır. SQL int
  sorguları CLR int'e, Version binary(8) parametresine eşlenir; EF tracking yoktur.
- Dinamik DB adı doğrulanır ve QuoteIdentifier ile quote edilir; veri parametreli,
  DDL fixture sabitleri güvenilirdir. RCSI OFF ve ALLOW_SNAPSHOT_ISOLATION ON
  deneylerden önce sys.databases üzerinden doğrulanır. Ayarlar yalnız yeni test
  DB'sine uygulanır; diğer bağlantıları sonlandıran seçenek kullanılmaz.
- DMV okuma izni ve LocalDB DB oluşturma/silme yetkisi gerekir. Bu koşuda DMV
  gözlemleri alınabildi. İzin yoksa bekleme testi başarılı sayılmaz.
- `Run` finally içinde yalnız kendi oluşturduğu DB'yi dört tabloyla birlikte
  kaldırır, DB_ID yokluğunu kontrol eder. Cleanup hatası mevcut exit3 yoluna gider;
  assertion exit1, beklenmeyen yürütme hatası mevcut hata yoluna gider. Üst script
  sıfır olmayan sonucu korur. Üç gün40 koşusu ve smoke DB'si temizlendi.

Full/browser/HTTP tekrar çalıştırılmadı; üretim, paket ve model değişmediği için
migration yok. Performans, deadlock graph, bağlantı kopması, crash recovery ve
üretim danışman atama yarışları bu deneyde test edilmedi. Yeni mimari karar
alınmadığından ADR eklenmedi.
