# 0014 — Doğrudan ön koşul ve dönemlik kredi specification'ları

1. **İhtiyaç:** Kayıt öncesi tamamlanmış ön koşulları ve dönemlik kredi yükünü
   değerlendirmek. Önceki akış yalnız varlık/mükerrer çift doğruluyordu;
   Enrollment başarı bilgisi taşımadığından akademik uygunluğu gösteremiyordu.
2. **Seçim:** İki ilişki tablosu (birleşik PK/FK/NO ACTION ve doğrudan self CHECK),
   iki küçük somut specification ve kontrollü girdiler. Veriyi EnrollmentOperations
   toplu sorgularla hazırlar; specification EF/HTTP bilmez. Doğrudan if/özel metot
   daha az tür gerektirirdi; ayrı saf koşulların sınır/test sözleşmesi bu kapsamda
   anlamlıdır. Generic specification, expression birleşimi veya rule engine yok.
3. **Kazanç:** Başarı ile kayıt ayrılır; kurallar DB'siz sınanır. Sunucudaki açık
   MaxCreditsPerTerm ayarı başlangıçta doğrulanır. Tekli ve batch aynı akademik
   değerlendirmeyi kullanır; batch seçimleri birlikte ve dönemlere ayrı toplanır.
4. **Maliyet:** İki tablo/migration ve başarı verisini güvenilir hazırlama ihtiyacı;
   başarılı yolda üç ek toplu SELECT (toplam6). Güncel Course.Credits değişiklikleri
   geçmiş kayıt yükünü etkiler. Doğrudan FK/CHECK dolaylı döngüyü çözmez.

Fail-fast seçildi: önce istek sırasındaki ön koşullar, sonra ilk görünme sırasındaki
dönemler. Sonuç yalnız ilgili kimlik ve ret türünü taşır; bütün retleri toplamak
15. gündür. AcademicRejectionHttp bunu400/code'a çevirir; eski retler korunur.

Tek SaveChanges ve eski batch atomikliği korunur; ek transaction yok. Ön kontrol
ile save arasındaki yarış aralığı sürer. Kredi aşımını eşzamanlı kesin engelleme,
kontenjan, transkript, danışman override veya tarihsel snapshot eklenmedi.

Varsayılan seed yeni ön koşul/başarı yazmaz. Opsiyonel SQL sadece işaretli izole
DB'de sentetik kayıtlarla rollback örneğidir. Migration yalnız app DB'sine açık
komutla uygulanır; lab korunur. Down veriyi yedeklemez, ayrıca çalıştırılmadı.
