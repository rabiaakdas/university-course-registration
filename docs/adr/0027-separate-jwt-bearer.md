# 0027 — Cookie yanında ayrı JWT doğrulaması

1. **İhtiyaç:** İmza/issuer/audience/süre ve claim doğrulamasını gerçek HTTPS üzerinde
   göstermek; cookie akışını bozmadan ayrı bearer kaynağına erişmek.
2. **Önceki sınır:** Identity cookie biletleri bu JWT deneyini sağlamıyordu. Default
   şemayı bearer yapmak me/logout'u değiştirirdi. Tam OAuth sunucusu veya otomatik
   scheme forwarding bugünkü ihtiyacı aşardı.
3. **Seçim/kazanç:** Adlandırılmış UniversityBearer + yalnız o şemayı kullanan policy.
   Default cookie korunur. Development + açık opt-in dev-token, Identity cookie,
   taze CSRF ve sunucu hesap koşullarıyla on dakikalık RS256 üretir. Üretimde public
   key doğrulaması vardır; üretim token endpoint'i yoktur. Mevcut SignInManager ve
   HTTPS fixture tekrar kullanılır; tek uyumlu JwtBearer paketi eklenir.
4. **Maliyet/sınır:** Repo dışı PEM/ACL yönetimi, explicit key yaşam süresi, ayrı
   scheme/claims ve ret matrisi bakımı. Anahtar rotasyonu/JWKS yok. Onaylanmış hesabın
   sonraki değişimleri ve cookie logout JWT'yi iptal etmez; jti replay önlemez.
   Refresh/rotation, OAuth/OIDC, role/ownership ve tam güvenlik sunucusu yoktur.

Özel ve açık RSA nesneleri ayrı, container-owned singleton'dır. Başlangıçta ayar/
anahtar hataları anlaşılır fakat secretsiz reddedilir. SameSite cookie güvenliği ve
CSRF önceki akışta korunur; bearer endpoint'i DB hesabı kontrol etmez. Şema/migration
ve akademik transaction/observer davranışı değişmez. Generic token service veya
repository eklenmedi.
