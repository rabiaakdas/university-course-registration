# 0019 — Dönem kayıt özeti için küçük Facade

1. **İhtiyaç:** İstemci bir öğrencinin dönem kayıtları, durum sayıları, aktif
   kredisi ve programını birlikte görmek istiyor.
2. **Önceki sınır/alternatif:** Kayıt listesini, dersleri ve saatleri istemcide
   birleştirmek aktif durum, kredi ve sıralama kararlarını istemcilere dağıtır;
   gereksiz çağrı/satır çoğalması riski doğurur.
3. **Seçim/kazanç:** Somut scoped TermRegistrationSummaryFacade doğrudan mevcut
   DbContext üzerinden sınırlı scalar sorgularla özet hazırlar. Küçük ayrık sonuç
   HTTP'den bağımsızdır. Endpoint kimlik doğrulama ve HTTP eşlemesini üstlenir.
   Her enrollment tek satır; aktif saatler ayrı toplu sorgu, no N+1. Sayılar/kredi
   aynı kayıt projection'ından, long/checked toplamla türetilir.
4. **Maliyet:** Bir facade, endpoint, sonuç/DTO ve test senaryolarının bakımı.
   Okuma sorgularıyla aktif durum ve güncel kredi politikasının tutarlılığı
   korunmalıdır. Büyük raporlama sistemi veya hız garantisi değildir.

Yapay student/term/schedule servisleri, interface/repository/mediator eklenmedi;
mevcut ihtiyacın değişkenlik noktası yok. İç HTTP yerine doğrudan sorgu kullanılır.
Scalar projection tracking oluşturmaz, SaveChanges ve diğer mutasyonlar yoktur.
Gün sırası Pazartesi başlangıçlı, TimeOnly/time(7) hassasiyeti korunur.

Birkaç sorgu tek snapshot değildir; saat ve kayıt bilgisi farklı anlarda okunabilir.
Bugün transaction/isolation/kilit eklenmedi. Saatsiz aktif açılışlar açıkça bildirilir;
program eksiksizliği, uygunluk veya rezervasyon garantisi verilmez. Yetkilendirme
ve eşzamanlı yük garantisi önceki geliştirme sınırları olarak kalır. Yeni şema,
index, cache veya sonraki günün Builder altyapısı yoktur.
