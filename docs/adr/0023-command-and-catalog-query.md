# 0023 — Küçük komut/sorgu ayrımı, doğrudan çağrı

1. **İhtiyaç:** Kayıt yazma niyetiyle katalog okumasının HTTP dışında da açık,
   geçerli girişlerle çağrılabilmesi. Katalog sorgu kurma kodu endpoint içindeydi;
   kayıt koordinasyonu zaten EnrollmentOperations'ta doğru yerdeydi.
2. **Mevcut sınır:** Ham int parametreleri HTTP dışından hatalı kullanılabiliyordu.
   Katalog SQL'ini HTTP sonucundan bağımsız sınamak için endpoint çağrısı gerekiyordu.
   Kayıt koordinasyonunu ayrı handler'a kopyalamak ise fayda sağlamazdı.
3. **Seçim/kazanç:** Get-only CreateEnrollmentCommand mevcut CreateAsync tarafından
   kabul edilir. CourseCatalogQuery ve scoped CourseCatalogReader okuma akışını
   üstlenir. HTTP alan hataları ve constructor guard'ları aynı doğrulama kaynağını
   kullanır. SQL projection/sıralı iki sorgu ve mevcut DTO korunur. Doğrudan çağrı,
   aynı uygulama/DB; sadece bu iki akış düzenlendi.
4. **Maliyet:** Üç küçük üretim türü ve HTTP–uygulama eşlemesi bakım ister.
   DI kayıtlı reader Mediator değildir. Mediator üretimde somut fayda getirmediği
   için yalnız typed iki istekli öğrenme deneyinde kaldı; paket/framework yoktur.

Alternatif endpoint'te bırakmak daha az tür gerektirirdi fakat katalog okuma
sorumluluğunu HTTP'ye bağlı tutardı. Genel pipeline, repository veya bütün endpoint
ailesini dönüştürme seçilmedi. Yeni komut save/commit/publish eklemez: tekli/batch
sınırları ve onay sonrası olay sırası, hata/iptal sözleşmeleri aynen korunur.
Count/items snapshot ve kalıcı olay teslimi sınırları bu ayrımla çözülmüş değildir.
