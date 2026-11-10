# Gün 38 — Ders düzenlemede optimistic concurrency

## Sürüm ve eski istemci
İki istemci aynı ders detayını okuyabilir. Son yazanın öncekinin değişikliğini
sessizce ezmesi yerine SQL Server her UPDATE'te yeni 8 baytlık rowversion üretir.
Bu değer bir tarih/saat değildir; istemcinin seçtiği bir sayaç veya erişim yetkisi
olarak kullanılamaz. JSON'daki Base64 yalnız taşıma kodlamasıdır.

## Kod konumları ve karar sırası
- `Models/Course.cs`: DB üretimli `byte[] RowVersion`, private setter.
- `Data/UniversityDbContext.cs`: `IsRowVersion()` concurrency token ve generated
  on add/update eşlemesi. Migration/Designer/snapshot EF aracıyla üretildi.
- `Contracts/CourseRequests.cs`: PUT'un string RowVersion girdisi ve CourseDetailDto.
  DTO byte[] çıktısı JSON'da Base64 olur; entity dışarı verilmez.
- `Endpoints/CourseEndpoints.cs`: yetki ve CSRF'den sonra mevcut ders yüklenir;
  bulunamadıysa404. Normal alanlar ile sürüm birlikte doğrulanır. RowVersion standart
  kanonik Base64/12 karakter/8 bayt olmalıdır, boşluklar normalize edilmez.
  İstemci değeri `Entry(course).Property(...).OriginalValue` olur; böylece UPDATE'in
  WHERE koşulu Id ve istemci sürümünü birlikte kullanır. Etkilenen satır yoksa EF'nin
  DbUpdateConcurrencyException'ı409 `course_version_conflict` olarak karşılanır.
  Okumadan sonra silinen ders de bu yazma çakışmasına girer; başlangıçtaki yokluk404'tür.
- Aynı içerikli PUT'ta da Name IsModified işaretlenir: SaveChanges no-op yapıp eski
  sürümü kabul etmez. Bunun maliyeti aynı içerikte bile UPDATE ve yeni sürümdür.
- Kod UNIQUE hatası ayrı409 kalır; diğer DB hataları genel güvenli hata yönetimine
  gider. Otomatik retry/merge/ETag eklenmez. Public liste DTO'ları değişmez.

## Test ve yükseltme
`scripts/CourseVersionChecks.cs` iki ayrı cookie jar ve yalnız fixture sertifikasını
kabul eden HTTPS istemcileri kullanır. Aynı sürümü okur, ilk yazmayı kesinleştirir,
sonra stale PUT'u gönderir: zamanlama için delay gerekmez. Yeni DbContext ilk değeri
ve sürümünü doğrular. Güncel sürümle ikinci yazma, stale aynı içerik, eksik/bozuk
sürüm400, başlangıç yokluğu404, anonymous401/Student403, eksik CSRF400, normalizasyon,
kredi ve UNIQUE kontrolleri vardır. Liste alanları ayrıca doğrulanır.

Eski PUT fixture'ları detay GET'inden sürüm alır; hedefli stale test bu yardımcıyı
kullanmaz. Eski migration fixture'larında açık sütunlu Course INSERT kullanılır:
yeni modelin henüz bulunmayan RowVersion'ı OUTPUT ile istemesi önlenir. Seat-capacity
upgrade testi artık kendi migration'ını isimle bulur; son migration olmasını varsaymaz.
Yeni migration testi eski dersin Id/code/name/credits korunmasını, 8 baytlık üretimi,
ikinci update ve model uyumunu denetler. SQL şema kontrolü timestamp/8 baytı kapsar.

## Maliyet ve sınırlar
409 sonrasında kör retry, güncel sürümü alıp başka kullanıcının değişikliğini yine
silebilir. Kullanıcı iki içeriği değerlendirip yeni bir yazma kararı vermelidir.
Rowversion concurrency token'ı sahiplik/rol/CSRF'nin yerine geçmez. Bu çözüm yalnız
Course PUT yolunu korur; farklı açılış kredi/saat, danışman atama, bütün entity'lerde
lost update, genel transaction veya otomatik merge çözümü değildir. Doğrudan SQL ve
ExecuteUpdate kullanan başka yazma yolları bu endpoint sözleşmesini uygulamaz; SQL
Server yine rowversion üretir. Down ve ayrı browser deneyi çalıştırılmadı.
Gerçek sonuçlar `docs/progress.md` Gün38 bölümündedir.
