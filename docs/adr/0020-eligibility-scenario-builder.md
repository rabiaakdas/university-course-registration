# 0020 — Saf akademik test hazırlığı için küçük Builder

1. **İhtiyaç:** Çok açılışlı uygunluk testinde dönem/kredi, program ve ön koşul
   nesneleri farklı koleksiyonlarda iç içe kuruluyor; aynı senaryonun bağlamı zor
   izleniyor. Beklenen retler açıkça yazılmaya devam etmeli.
2. **Alternatif:** Doğrudan constructor/object initializer küçük girdiler için
   yeterli ve korunuyor. Karmaşık fixture'larda ilişkili hazırlığı adlandırmak
   anlamlı; bütün DB fixture'larını dönüştürmek bakım maliyetini büyütürdü.
3. **Seçim/kazanç:** Yalnız scripts içinde EligibilityScenarioBuilder; gerçek
   OfferingEligibility/OfferingSchedule/PrerequisiteInput türleriyle hazırlık.
   Açık kimlik/dönem/kredi/status/limit; boş başlangıç koleksiyonları. Build
   bağımsız, salt okunur sonuç verir, reset/akademik karar/veri düzeltme yapmaz.
   Sonuçları test bekler, gerçek evaluator hesaplar.
4. **Maliyet:** Builder API'si, mutable hazırlık alanı, yapısal hata ve kopyalama
   kontrolleri. Satır sayısı kazancı hedef değil. İki temsilî saf fixture alanıyla
   sınırlı; basit constructor ve DB setup alternatifleri bırakılır.

Dış koleksiyonlar ekleme anında kopyalanır; Build nested üretim nesnelerini yeniden
oluşturur. Immutable slot/FrozenSet paylaşımı güvenli; read-only arayüz tek başına
kanıt değildir, sahip olunan kopya ve sarmalayıcı kullanılır. Birebir tekrar slot ve
açılış tanımı açık hatadır; geçerli örtüşme/eksik ön koşul/kredi aşımı saklanır.

Bir builder tek öğrenciye hazırlanmış bağlamı temsil eder; production imzasında
olmayan StudentId veya paralel model yoktur. Existing Submitted/Approved aktif
veriye dönüşür; Draft/Rejected sessizce başka duruma geçirilmez. Genel entity
builder, Director, clone framework, thread-safety, DB fixture yönetimi ve üretim
model değişikliği eklenmedi. Paket/migration ve sonraki günün Observer altyapısı yok.
