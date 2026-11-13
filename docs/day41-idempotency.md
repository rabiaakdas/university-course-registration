# Gün 41 — Tek enrollment oluşturma için kalıcı idempotency

`POST /enrollments` artık tek, boş olmayan D biçiminde UUID `Idempotency-Key`
header'ı ister. Eksik/bozuk/çoklu/boş UUID için alan bazlı400 döner. Mevcut
cookie-only Student rolü, Student.UserId sahipliği ve CSRF kontrolleri korunur.
Kimlik/rol/kaynak retleri header doğrulamasından önce gerçekleşebilir; yetkisiz
istemciye iş kuralı bilgisi açılmaz. Replay için de aynı kontroller çalışır.
Ayrıca hesap/e-posta uygunluğu ve güncel DB Student üyeliği yeniden kontrol edilir;
eski cookie'de kalmış rol tek başına replay yetkisi vermez.

```powershell
dotnet tool run dotnet-ef database update
dotnet build --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode idempotency
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode core
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-course-writes.ps1 -Mode full
```

Mevcut cookie/antiforgery oturumuyla her yeni mantıksal create için bir kez
`[Guid]::NewGuid().ToString('D')` üretin ve tekrarlarında aynı anahtarı gönderin.
Gövdede yalnız mevcut `studentId` ve `courseOfferingId` kullanılır. Örnek header:
`Idempotency-Key: <aynı-mantıksal-isteğin-UUID-değeri>`; gerçek cookie/CSRF veya
istek header'larını loglamayın. Batch, submit, approve ve reject'e anahtar eklenmedi.

## Sözleşme

- İlk başarı201; yalnız ilk enrollment JSON'u ve yerel `/enrollments/{id}` Location
  saklanır. Aynı kapsam/anahtar/anlamlı içerik tam aynı JSON/Location/201 döndürür.
- Aynı anahtar farklı içerik409 `idempotency_key_reused`.
- Oluşumdan24 saatlik sabit süresi biten mevcut anahtar409
  `idempotency_key_expired`; yeni oluşturma veya iş kuralı yürütümü değildir.
- Kapsam: doğrulanmış AccountId + sunucunun sabit `enrollment.create.v1` işlem adı
  + Guid. Hesap kimliği gövdeden alınmaz. İki hesabın aynı UUID'si ayrı kapsamlardır;
  mevcut ownership sınırları geçerlidir.
- RequestHash: işlem sürümü ve doğrulanmış iki pozitif integer alanın invariant
  gösteriminden SHA-256. JSON property sırası, boşlukları ve bilinmeyen alanlar
  semantik özeti değiştirmez. Değişen iş anlamı gelecekte yeni işlem sürümü ister.
- Enrollment daha sonra Submitted olabilir: replay eski Draft201 gövdesini verir,
  GET detay güncel durumu gösterir. Replay yeni bir durum sorgusu değildir.
- İş reddi veya DB hatası saklanmaz. Yeniden deneme aynı anahtarla güncel iş
  kurallarından geçebilir. Başka anahtarla aynı öğrenci/açılış mevcut UNIQUE
  kısıtından409 alır; idempotency bu kısıtın yerine geçmez.

## Transaction ve veri modeli

`IdempotentEnrollmentCreation` önce kesinleşmiş sonuç arar. Yoksa açık transaction
başlatır, UNIQUE `(AccountId,Operation,Key)` rezervasyon INSERT'ini **iş yazmasından
önce** SaveChanges ile gönderir. Ardından mevcut `EnrollmentOperations.CreateAsync`
çağrılır; iş koordinasyonu kopyalanmaz. Bu metottaki SaveChanges dış transaction'a
katılır. Son JSON/201/Location ve EnrollmentId güncellemesi de aynı transaction'da
kaydedilip commit edilir. İş reddinde açık rollback, istisnada transaction dispose
ile rollback gerçekleşir. Commit öncesi yanıt verilmez.

Rezervasyon sırasında sonuç alanları null olabilir; yalnız bütün sonuç alanları
birlikte dolu olan başarı yolu commit eder. DB CHECK all-null veya tamamlanmış201
biçimini korur; DB tek başına pending satırın commit edilmesini yasaklayan deferred
constraint sunmaz. Bu nedenle uygulamanın tek commit noktası önemlidir. Üretim
kodunda pending kaydı tek başına commit eden yol yoktur; genel bakım yazmaları
bu sözleşmeyi korumalıdır.

Model hesap ve enrollment FK'larını NO ACTION ile korur; JSON2000, Location100,
işlem40, hesap450 karakter; RequestHash binary(32). Yanıt kaydı kişisel akademik
kimlikleri içerir; token/cookie/header/parola saklamaz. Migration yalnız yeni tablo,
CHECK/FK ve indeksleri ekler; eski başarılar için geriye dönük anahtar üretilmez.

Eşzamanlı INSERT'i UNIQUE koordine eder. Yalnız ilgili tablo/indeksin2601 hatası
`RegistrationWriteErrors` tarafından sınıflandırılır. Diğer unique/FK/DB hataları
bu çakışma sayılmaz. Kaybeden rollback ve dispose sonrası tracker'ı temizler;
yeni DbContext ile kesinleşmiş yanıtı okur. Sonucun keyfi eksikliği başarıya
çevrilmez. Otomatik retry yoktur; cancellation da iş reddi değildir.

## Süre, temizlik ve garantiler

24 saat saklama süresi otomatik silme anlamına gelmez. Bu ders expired kaydı tutar
ve açık ret verir; erken fiziksel silme aynı anahtarın yeniden sahiplenilmesine
izin vereceği için bu semantiği değiştirir. Arşiv/tombstone/saklama politikası ve
zamanlanmış cleanup eklenmedi. FK'lar hesap/enrollment fiziksel silmesini de
engelleyebilir; ürün silme akışı ayrıca tasarlanmalıdır. Test temizliği yalnız
hedef enrollment'ların receipt'lerini önce siler, mevcut FK'ları kapatmaz.

Commit ile HTTP teslimi farklıdır. Başarılı yanıt istemciye ulaşmamış olsa bile
saklanan sonucu tekrar almak mümkündür; burada gerçek ağ kopması enjekte edilmedi.
Test yalnız sonucu kullanmadan tekrar ve süreç restart'ı yaptı. Bu sınırlı DB
koordinasyonu genel exactly-once teslim veya harici yan etki garantisi değildir.
Onay observer'ı/create dışındaki olay sınırları değişmedi. Yetki kontrolü ile
transaction arasında sahiplik/rol değişimi TOCTOU sınırı bu dersle çözülmedi.

## Kod ve doğrulama

- `Endpoints/EnrollmentEndpoints.cs`: HTTP anahtar doğrulaması, güncel erişim ve
  saklanan yanıt. Eski HTTP dışı CreateAsync yardımcısı güvenilen doğrudan test
  yoludur; route yalnız yeni idempotent metoda bağlıdır.
- `Application/IdempotentEnrollmentCreation.cs`: claim→mevcut işlem→sonuç→commit;
  çakışmadan sonra yeni context. `Models/EnrollmentCreationReceipt.cs` ve
  `Data/UniversityDbContext.cs`: kalıcı model/eşleme.
- `Data/RegistrationWriteErrors.cs`: dar SQL indeks sınıflandırması.
- `scripts/IdempotencyChecks.cs`: gerçek SQL/HTTPS; eski migration yükseltmesi,
  sıralı/eşzamanlı replay, restart, farklı içerik/hesap, gerçek UNIQUE kaybı için
  iki context'i INSERT öncesi TCS ile buluşturma, sonuç UPDATE'inde gerçek DB
  hatası ve tam rollback, iş reddi/expiry/CSRF/anonymous/rol/sahiplik retleri.
- Eski tek-create HTTP fixture'ları yeni UUID verir; test hostları servisi kaydeder.
  Enrollment cleanup predicate'leri korunarak yalnız bağlı receipt'ler silinir.
  Saf uygulama/domain testlerine HTTP kimliği zorlanmadı.

Hedefli iki koşu, core, hedefli Observer ve son full başarılıdır. İki eski test
altyapısı uyumsuzluğu dar düzeltmelerle giderildi; ayrıntılar progress kaydındadır.
Migration app DB'ye uygulandı; mevcut tablo içerikleri korundu, ikinci update
no-op ve model/snapshot uyumludur. İzole yükseltme de başarılıdır. Crash recovery, gerçek ağ kaybı ve çok-instance
HTTP yük testi yapılmadı; iki SQL context/bağlantı ve paralel gerçek HTTP istekleri
kullanıldı. Testler HTTPS sertifikasını yalnız fixture sertifikasıyla doğrular.
