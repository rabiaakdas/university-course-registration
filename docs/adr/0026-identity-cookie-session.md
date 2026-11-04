# 0026 — Identity cookie girişi ve açık CSRF sınırı

1. **İhtiyaç:** Doğrulanmış hesabın kimliğini HTTP istekleri arasında taşımak ve
   küçük bir hesap okumasını korumak. 24–25. gün hesap/confirmation verisi vardı,
   giriş ve istek kimliği yoktu.
2. **Seçim:** Mevcut AddIdentityCore/store üzerine SignInManager ve Identity cookie
   şemaları. Elle cookie/parola kodu veya JWT yerine aynı site ders akışına uygun
   __Host-University.Account, HttpOnly/Secure/Strict, 20 dakika sabit bilet ve
   nonpersistent giriş. API redirect event'leri 401/403; middleware sırası açık.
3. **Kazanç:** Identity parola/confirmation/lockout kararlarını yeniden kullanır;
   me/logout kimlik gerektirir. Küçük GET antiforgery ve login/logout açık doğrulaması,
   kimlik değiştikten sonra yeni token, tarayıcının otomatik cookie taşıma riskini
   karşılar. SameSite tek başına CSRF çözümü kabul edilmez.
4. **Maliyet/sınır:** HTTPS ve Data Protection anahtar yönetimi; istemcide cookie jar
   ve iki token alma adımı. Beş hata/beş dakika lockout kapsamlı saldırı koruması
   değildir. Logout kopya bileti anında iptal etmez. Profil sahipliği, akademik
   endpoint yetkisi, cihaz oturumları, 2FA ve bearer/JWT akışı yoktur. Identity'nin
   yardımcı cookie şemalarının kaydı bu özelliklerin ürün olarak sunulması değildir.

Şema değişmez; wrapper/interface/paket eklenmedi. Testte yalnız koşuya ait
sertifikaya pinning ve CookieAuthenticationOptions.TimeProvider kullanıldı.
Önceki register/confirm davranışı ve akademik transaction/observer sınırları korunur.
