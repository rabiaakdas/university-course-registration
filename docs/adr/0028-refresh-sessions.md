# ADR 0028 — Güncel özetli kalıcı refresh oturumu

## İhtiyaç ve önceki sınır
Kısa süreli JWT bittiğinde cookie üretim endpoint'ini tekrar kullanmak yerine ayrı
refresh yetkisiyle yenileme gerekir. Önceki JWT doğrulaması süreyi denetliyordu;
kalıcı yenileme yetkisi ve sıralı rotation yoktu.

## Seçim ve kazanç
Identity hesabına bağlı somut RefreshSession; binary(32) SHA-256 güncel özet,
UNIQUE ve NO ACTION. Ham token 32 kriptografik rastgele bayt/kanonik Base64Url'dir.
TokenSessions mevcut JWT üretimini tek yerde sürdürür; dev-token ilk kaydı oluşturur,
refresh aynı satırın özetini değiştirir. SaveChanges başarısından önce çift dönmez.
Sabit yedi günlük son tarih uzatılmaz; access token kalan süreyle sınırlanır.

Tam token geçmişi/family yerine bugünkü dar model seçildi. Genel servis/interface,
repository, retry veya token framework'ü eklenmedi. Cookie+CSRF giriş kapısı korunur;
refresh JSON secret'ı ile anonim çalışır. Üretim public-only doğrulama davranışını
korumak için refresh de açık Development üretici opt-in'ine bağlıdır. Genel OAuth
sunucusu veya üretim oturum yönetimi olduğu iddia edilmez.

## Maliyet ve sınırlar
Yeni tablo/migration, secret taşıyan yanıt, hesap öncesi oturum temizliği ve ayrı
rotasyon durumunu yönetme maliyeti vardır. Hesap silme NO ACTION nedeniyle engellenir.
Hash arama ve güncelleme arasında concurrency koruması yoktur; iki yenilemede tek başarı
ve reuse tespiti vaat edilmez. Başka bir çağrıda eski token401 olur; bu saldırı tespiti
anlamına gelmez. Yanıt kaybında eski token tekrar kullanılamayabilir. Access token
cookie logout/oturum iptaliyle geri alınmaz. HTTP teslimini DB transaction'ına dahil
etme, kilit, otomatik retry veya denylist alternatifleri bugünün kapsamı dışındadır.

Geçmiş veriye dokunmayan tek migration seçildi. İzole önceki şema yükseltmesi ve gerçek
UNIQUE yazma hatası, yeni context'ten kalıcı durumla doğrulanır; Down zorunlu değildir.
