# ADR 0029 — Refresh ailesinde tek tüketim ve katı reuse

**İhtiyaç:** Aynı yenileme yetkisinin iki istek tarafından kullanılmasını koordine etmek,
kullanılmış token tekrarını tanımak ve hesaba kendi ailelerini yönetme imkânı vermek.

**Önceki sınır:** Gün28 yalnız güncel özeti tutuyordu; geçmiş kayboluyor, iki context
aynı token'dan iki başarı üretebiliyordu. Tek SaveChanges, okuma ile yazma arasını
koordine etmiyordu. In-process lock çok süreçli DB sınırını korumaz.

**Seçim/kazanç:** RefreshSession aile, RefreshTokenRecord özet geçmişi olur. Güncel
sütun taşınarak kaldırılır. Aynı aile satırında UPDLOCK/HOLDLOCK açık transaction boyunca
refresh ve revoke'u koordine eder. Koşullu tüketim, ardıl INSERT ve replacement birlikte
commit/rollback olur. Birleşik replacement FK farklı aileye bağlantıyı engeller.
Cookie-only liste ve CSRF'li revoke sahipliği doğrulanmış UserId ile sınırlar.

**Maliyet:** Geçmiş tablosu büyür; index/ilişki, migration veri taşıma ve kilit bekleme
maliyeti vardır. Kilit sırasında yalnız yerel hesap DB kontrolü ve token hesabı yapılır;
provider çağrısı/retry yoktur. EF tracker ve ExecuteUpdate sınırı açık tutulmalıdır.
Genel lock/repository/event framework'ü bu küçük akışı sadeleştirmeyeceğinden eklenmedi.

Katı reuse seçildi: kaybeden veya tekrarlayan istek aileyi iptal eder. Tolerans penceresi
kaybolan yanıt/çoklu sekme kullanımını kolaylaştırabilirdi; eski yetkinin yeniden
kabul edilmesini ve daha karmaşık yanıt saklama/tekrar politikalarını gerektirirdi.
Bugün belirsiz tekrarda yeniden giriş maliyeti kabul edilir. Masum tekrar saldırıdan
ayırt edilmez. Bilinmeyen token aile seçemez. Mevcut access JWT'leri iptal edilmez.

Eski token geçmişi migration ile üretilemez; yalnız gün28'in bilinen güncel özeti taşınır.
Downgrade geçmişi kaybettirir; otomatik tarih temizliği ve fiziksel cihaz tanımlama yoktur.
DB atomikliği HTTP yanıt teslimi garantisi değildir. Çökme/ağ kaybı denenmedi.
