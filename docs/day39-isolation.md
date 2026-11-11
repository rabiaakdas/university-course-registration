# Gün 39 — SQL Server isolation laboratuvarı

Üretim davranışı değişmez. `scripts/IsolationLabChecks.cs` yalnız yeni ve adı
sunucuda üretilen `university_registration_tests_day39_<guid>` veritabanını ve
`dbo.IsolationRows` tablosunu kullanır. Migration, akademik veri, uygulama veya
`university_registration_lab` ayarları bu deneyin hedefi değildir.

```powershell
dotnet build --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode isolation-lab
# Hata yayılımı deneyi: aşağıdaki komutun beklenen exit code'u 1'dir.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode isolation-lab -ProbeFailure
# Mevcut yarış laboratuvarının smoke kontrolü
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode concurrency-lab
```

LocalDB çalışır durumda olmalı; test hesabı kendi DB'sini oluşturup kaldırabilmeli
ve diğer test bağlantısının DMV bekleme bilgisini görebilmelidir. DMV erişimi yoksa
veya bekleme kanıtı alınamazsa test başarılı sayılmaz. Çalıştırıcı hatayı sıfır
olmayan exit code ile iletir; kısa süre tamamlanmama kilit kanıtı değildir.

## Gerçek sonuçlar (11 Kasım 2026)

| Deney | Gözlem ve doğrulanan kalıcı sonuç |
| --- | --- |
| READ UNCOMMITTED | Yazarın commit etmediği 99 okundu; rollback sonrası yeni bağlantı 10 okudu. |
| READ COMMITTED, RCSI OFF | Okuyucu yazar üzerinde `LCK_M_S` bekledi. Yazar rollback sonrası okuyucu 10 aldı; dirty read olmadı. |
| READ COMMITTED, iki sorgu | Aynı transaction'da 10, başka bağlantının commit'inden sonra 20 okundu. Yeni transaction ve kalıcı değer 20. |
| REPEATABLE READ, satır | Okunan satıra UPDATE `LCK_M_X` bekledi. Okuyucu commit edince UPDATE tamamlandı; kalıcı değer 20. |
| REPEATABLE READ, phantom | Aynı koşulun sayısı başka bağlantıdaki INSERT commit'i sonrası aynı transaction'da 1→2 oldu. Kalıcı sayı 2. |
| SERIALIZABLE | Aynı koşula uyan INSERT `LCK_M_RIn_NL` bekledi. Okuyucu commit edince INSERT tamamlandı; kalıcı sayı 2. |
| RCSI | Açık, commit edilmemiş 99 yazmasına rağmen okuma 10 ile tamamlandı. Rollback sonrası 10 kaldı. Ayrı deneyde aynı RC transaction'ın iki sorgusu aradaki commit ile 10→20 gördü. |
| SNAPSHOT | İlk veri erişimi 10; başka bağlantı 20 commit etmesine rağmen ikinci okuma 10. Yeni transaction ve kalıcı değer 20. |

`dotnet build`: 0 uyarı/0 hata. `isolation-lab`: exit0.
`-ProbeFailure`: bütün deneylerin ardından kontrollü assertion, exit1; finally
temizliği başarılı. `concurrency-lab`: exit0; sıralı kayıt, kontrollü kapasite
aşımı ve kayıp artış beklenen sonuçları verdi. Smoke'un bu koşuda oluşturduğu
veritabanı ayrıca kaldırıldı. Her iki isolation DB'si finally içinde kaldırıldı.
Hiçbir bağlantı zorla sonlandırılmadı. Üretim değişmediğinden full/HTTP paketleri,
app migration ve model/snapshot komutları yeniden çalıştırılmadı.

## Kavramlar ve maliyet

Dirty read, sonradan rollback edilebilen veriyi okumaktır. Non-repeatable read,
aynı satırın iki okumasında farklı commit edilmiş değer görmektir. Phantom ise
aynı sorgu koşuluna uyan satır kümesinin değişmesidir; yalnız satır güncellemesi
değildir. Tek tablo/indeksli bu deneyler bütün sorgu planları için aynı kilit türü
veya kilit granülerliği garantisi vermez.

RCSI, READ COMMITTED sorgularına statement düzeyinde sürüm görünümü verir;
SNAPSHOT transaction görünümünü korur. Kod üç ayrı yapılandırmayı
`sys.databases` üzerinden doğrular: OFF/OFF, RCSI ON/SNAPSHOT OFF ve
RCSI OFF/SNAPSHOT ON. Ayarlar bütün deney bağlantıları kapalıyken master üzerinden
değiştirilir; `ROLLBACK IMMEDIATE` kullanılmaz. Bu davranışların temeli
[SQL Server isolation düzeyleri belgesidir](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-transaction-isolation-level-transact-sql?view=sql-server-ver17).

Kilitler bekleme, düşük eşzamanlılık ve deadlock maliyeti doğurabilir. Sürümlü
okumalar sürüm saklama alanı ve uzun transaction maliyeti getirir; yazma
çatışmalarını kendiliğinden ortadan kaldırmaz. Bu laboratuvar throughput, deadlock,
SNAPSHOT update conflict veya çökme kurtarma ölçümü değildir. Gün 38'deki SQL
`rowversion` sütunu ise eski istemci yazmasını tespit eden concurrency değeridir;
RCSI/SNAPSHOT okuma sürümlerini açan bir ayar değildir.

## Koordinasyon ve kod konumları

- `DirtyRead`, `TwoReads`, `RcsiUncommitted`: ayrı açık bağlantılar; tamamlanmış SQL
  adımlarını await ederek belirli sıra. SNAPSHOT görünümü ilk tablo okumasında kurulur.
- `Blocking` / `ObserveLock`: ayrı sahip, bekleyen ve gözlemci bağlantısı.
  `sys.dm_exec_requests` içinde tam bekleyen/engelleyen session çiftinin `LCK_M_*`
  beklemesi aranır; SQL metni veya bağlantı sırları okunmaz.
  [DMV alanları ve izinleri](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-objects/sys-dm-exec-requests-transact-sql?view=sql-server-ver17).
  20 ms PeriodicTimer yalnız DMV örneklemesini sınırlar; zaman geçmesi test kararı
  değildir. Okuyucu, kendisini bekleyen yazarın tamamlanmasını commit öncesi beklemez.
- Komutlar 15 saniye, DMV gözlemi 8 saniye, genel çalışma 3 dakika ile sınırlı.
  Hata yolunda bekleyen komut iptal edilir, sahip transaction rollback edilir,
  başlatılan task sonlandırılmadan metottan çıkılmaz. Ayrı cleanup süresi vardır.
- `Final`: yeni bağlantıyla kalıcı değer/sayı kontrolü. SqlClient scalar sorguları
  EF tracking içermez; SQL int/CLR int eşleşmesi açık, `@@SPID` int'e çevrilir.
- Veri değerleri parametreli; yalnız doğrulanan, bu koşuya ait DB tanımlayıcısı
  `QuoteIdentifier` ile SQL'e eklenir. Pooling kapalıdır; DROP yalnız bu koşunun
  oluşturduğu DB için yapılır. Hatalı cleanup exit3 üretir.
- `CourseWriteChecks.cs` ve `verify-course-writes.ps1`: tek `isolation-lab` modu,
  HTTP fixture'ına girmeden çıkış, test kaynaklarının web derlemesinden ayrılması.

Yeni mimari karar veya üretim isolation seçimi yapılmadığı için ADR eklenmedi.
