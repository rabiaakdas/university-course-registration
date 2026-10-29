# 0015 — Ortak uygunluk değerlendirmesi ve haftalık saatler

1. **İhtiyaç:** Ön koşul, dönemlik kredi ve saat sorunlarını tek yanıtta açıklamak;
   yazmadan önizlemek. Önceki fail-fast uygulama yalnız ilk nedeni bildiriyordu,
   saat verisi ve önizleme yoktu.
2. **Seçim:** Bir saat tablosu ve kontrollü WeeklyTimeSlot; saf AcademicEligibility
   tüm bağımsız nedenleri toplar. Veri hazırlığı EnrollmentOperations'ta kalır.
   Ayrık ret tipleri ve kopyalanmış salt okunur liste; IsEligible türetilir.
   Önizleme ile batch aynı varlık/mükerrer kontrolü ve değerlendirmeyi kullanır.
3. **Kazanç:** HTTP ve DB bağımlılığı olmayan sınanabilir hesap; tüm nedenlerin
   kararlı sırada görünmesi; saat çifti başına tek ret. Kullanıcı yeni bir sorun
   görmek için ilkini düzeltip tekrar denemek zorunda kalmaz. Tekli/batch başarı
   ve yazma anı hata sözleşmeleri, batch'in tek SaveChanges atomikliği korunur.
4. **Maliyet:** Yeni tablo/migration, iki ek SELECT (başarılı hazırlık toplam8),
   saf veri hazırlama türleri ve bilinçli API değişikliği: tek code yerine failures.
   Ön koşul başarısızken de kredi/saatler okunur; liste tüm retleri üretir.

Fail-fast daha az hesap/sorgu yapabilirdi; açıklanabilir ortak önizleme için
seçilmedi. Generic composite specification/rule engine ve servis aktarım
zinciri bu üç somut koşul için gereksizdir. HTTP sonuçları evaluator'a taşınmadı.

Saatler aynı gün, yerel, haftalık; time(7) TimeOnly tick'ini korur. Gün enum/int,
CHECK0..6; FK NO ACTION. Unique tam tekrarı önler, örtüşmeyi değil. Yeni–mevcut ve
yeni–yeni çiftler aynı yarı açık aralık formülünü kullanır; çift tekilleştirilir,
çıktı rolü korunur. Saat yokluğu program tamamlandı iddiası oluşturmaz.

Önizleme sonucu saklanmaz; gerçek kayıt yeniden değerlendirir. Yeni transaction,
kilit/retry veya concurrency garantisi yoktur. Yazmadan önce değişen program
yeniden okunabilir; değerlendirmeden sonra değişen program için garanti verilmez.
Migration yalnız app DB'sine açık komutla; lab ve mevcut açılış saatleri korunur.
Örnek seed otomatik saat atamaz. Down kaynakları incelenir, veri yedeği değildir.
