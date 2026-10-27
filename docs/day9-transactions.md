# Gün 9 — Transaction sınırını görmek

ACID'in bu uygulamadaki karşılıkları:

- **Atomicity:** Bir batch'in yeni kayıtları birlikte kalır veya birlikte geri alınır.
- **Consistency:** FK ve unique gibi tanımlı DB kuralları korunur. DB'ye hiç
  yazılmamış akademik kurallar transaction sayesinde kendiliğinden oluşmaz.
- **Isolation:** Eşzamanlı işlemler birbirinden etkilenir; ön kontrol başka isteği
  rezerve etmez. Bugün isolation seviyeleri karşılaştırılmadı ve kontenjan garantisi yok.
- **Durability:** Commit edilen kayıtlar kalıcı DB durumuna geçer. API restart
  kontrolü kayıtların kaldığını gösterir; bu çalışma elektrik/disk arızası testi değildir.

Üretim batch'i bir SaveChanges kullanır. Açık transaction varsa SaveChanges SQL'i
çalıştırır, fakat dış transaction'ı commit etmez. İki ayrı bağımsız save'de ilk
başarı ikinci hata yüzünden geri dönmez. Açık transaction içinde iki save için
son Commit veya tam Rollback ortak sınırı belirler.

Mevcut A/B deneyleri kopyalanmadan `scripts/RelatedWriteChecks.cs` Tracking metodu
C/D ile genişletildi. CHECK hatasından sonra ilk kayıt aynı transaction'da vardı;
tam rollback sonrası ayrı context'te yoktu. D'de commit sonrası iki kayıt vardı.
Commit öncesi başka bağlantıda boş okuma varsayılmadı; kilit beklemesi mümkündür.

EF açık transaction'da SaveChanges için savepoint kullanabilir. Savepoint ikinci
save'i geri alırken önceki save'i dış transaction'da bırakır. MARS bunu etkiler;
mevcut MARS kapalı ve savepoint desteği doğrulandı. Retrying execution strategy
etkin değildir. Ayarlar değiştirilmedi. [Microsoft transaction belgesi](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
varsayılan save sınırını, savepoint/MARS ve retry uyumunu açıklar.

Rollback DB durumunu değiştirir; C# nesnesini/tracker'ı temizlemez. Hatalı context
körlemesine tekrar kaydedilmez. Sequence tahsisleri boşluk bırakabilir. Rollback
gönderilmiş e-posta veya dış servis etkisini geri alamaz; bugün dış etki eklenmedi.
HTTP yanıtını alamamak commit olmadığı anlamına gelmez. Retry/idempotency çözümü
bu günün kapsamında değildir.

Kod: `Endpoints/BatchEnrollmentEndpoint.cs` yapı kontrolü, üç toplu sorgu,
AddRange/tek SaveChanges ve hata eşleme; `scripts/BatchEnrollmentChecks.cs` gerçek
HTTP retleri ile ayrı save-stage fault kanıtı. Yeni DTO yalnız items zarfıdır;
mevcut EnrollmentResponse tekrar kullanılır. Öğrenci/açılışlar yeniden Add edilmez.

Testte rollback temizliği iptal edilmiş request token'ına bağlı değildir. Cleanup
hatası ayrıca kaydedilir, asıl hata korunur; await using dispose sağlar. Cleanup
hatası simüle edilmedi. Kontrol sonrası sorgular yeni context'lerden yapılır.
