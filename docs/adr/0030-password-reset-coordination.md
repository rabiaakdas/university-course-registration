# ADR 0030 — Parola sıfırlama ve hesap düzeyinde koordinasyon

Durum: Kabul edildi; yalnız Gün30.

## İhtiyaç ve önceki sınır

Kullanıcı eski parolasını bilmiyorken süreli Identity izniyle yenileyebilmeli ve önceki
refresh aileleri kapanmalıdır. Mevcut e-posta provider'ının süresi2 saattir; bunu kısaltmak
confirmation sözleşmesini değiştirirdi. Yalnız aile kilidi, reset sırasında yeni aile
eklenmesi yarışını önlemiyordu. Parolayı önce commit edip aileleri sonra kapatmak ise
hata halinde eski yenileme haklarını bırakırdı.

## Seçim ve kazanç

Ayrı adlandırılmış Data Protection reset provider'ı15 dakika; UserManager.ResetPasswordAsync
ve tüm aktif ailelerin iptali aynı scoped EF context/açık transaction içinde. Reset,
refresh, revoke ve ilk üretim hesap satırında UPDLOCK/HOLDLOCK alır; ortak sıra hesap,
aile, token. İlk üretim kilitten sonra cookie stamp'ini doğrular. Hesap kilidi altındaki
ikinci reset güncel damgayı okuyarak eski token'ı reddeder. Güncel aile iptal zamanı
varsa korunur. Başarı yalnız commit sonrası204'tür; otomatik giriş yapılmaz.

Mevcut yerel sender isteğe bağlı konu parametresiyle kullanılır. Forgot uygun/missing/
unconfirmed hesaplarda aynı202; beklenen yerel yazma hatasında da bu yanıt ve yalnız
hata türü logu. Reset ortak güvenli400 ve parola için alan hatası sağlar. Callback
sunucu yapılandırmasıdır. Altyapı hatasını teslim veya kimlik reddi saymayız.

## Alternatif ve maliyet

E-posta sağlayıcısını ortak süreyle kullanmak daha az tür gerektirirdi fakat ayrı
geçerlilik ihtiyacını karşılamaz. Yalnız optimistic concurrency parola satırını koruyabilir,
ancak yeni aile eklenmesi/refresh sıralamasını tek başına açıklamaz. Hesap kilidi aynı
hesabın bağımsız ailelerini de sıraya alır ve SQL Server bağımlılığı getirir. İki küçük
provider/options türü ile üç mevcut yazma yolunun ortak kilit sözleşmesini sürdürme
maliyeti vardır. Generic locking framework, retry, yeni repository ve şema eklenmedi.

## Sınırlar ve kanıt

Gerçek SQL CHECK hatası Identity SaveChanges sonrasına enjekte edildi: parola/stamp/
aileler birlikte rollback. Kontrollü iki resetten bir204/bir400, reset–refresh ve
reset–ilk üretimin iki sırası gerçek ayrı HTTP/context'lerle sınandı. Diğer hesap
korundu. Cookie stamp aralığı30 dakika (bilet20 dakika); JWT anlık iptal olmaz.
Enumeration/timing/rate-limit, gerçek e-posta, ağ kaybı ve crash recovery tamamlanmış
sayılmaz. Data Protection anahtarlarının sürekliliği gereklidir.
