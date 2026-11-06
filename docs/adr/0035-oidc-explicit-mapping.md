# ADR 0035 — Önceden bağlanmış hesaplarla OIDC

Durum: Kabul edildi; yalnız açıkça etkinleştirilen Development test sağlayıcısı.

1. İhtiyaç: Yerel parola göndermeden harici kimlik doğrulamasını mevcut yerel hesap,
   rol ve cookie kurallarıyla birleştirmek; Code+PKCE güven sınırlarını gözlemek.
2. Önceki sınır: Yerel login ve kendi JWT doğrulamamız discovery, code değişimi,
   correlation/nonce veya harici kimlik eşleştirmesi sağlamıyordu. JWT decode etmek
   ya da e-postası aynı hesabı bağlamak bu ihtiyacı güvenle çözmez.
3. Seçim/kazanç: ASP.NET Core10.0.14 OIDC handler, Identity.External ara cookie ve
   SignInManager external giriş yolu. Tek doğrulanmış issuer'ın sub'ı önceden
   UserManager.AddLoginAsync ile bağlanır. Local roller tek yetki kaynağıdır.
   Issuer'a bağlı LoginProvider adı Authority değişiminde anahtarların yanlış
   yeniden yorumlanmasını önler. Otomatik e-posta birleştirme/provisioning yoktur.
4. Maliyet: Yeni paket, dış sağlayıcı erişimi, geçici cookie'ler, callback ve secret
   yapılandırması; hesap bağlantılarının güvenilen yönetim işlemiyle hazırlanması.
   Issuer değişikliği açık yeniden bağlantı gerektirir. Yerel logout sağlayıcıyı
   kapatmaz; reset/giriş yarışı için anlık iptal garantisi eklenmez.

Alternatifler: Başarılı principal döndüren sahte endpoint protokol kanıtı olmadığı
 için seçilmedi. Public tenant/ücretli hesap/Docker yerine MIT lisanslı, bakımı yapılan
 OpenID Certified oidc-provider9.13.3 yalnız test alanında kullanıldı. In-memory
 adapter ve fixture giriş ekranı üretim IdP değildir. Üretime OAuth sunucusu/BFF,
 generic auth wrapper veya federated logout eklenmedi.

Default authenticate/challenge cookie olarak kaldı; UniversityBearerOnly ayrı.
OIDC yalnız açık POST challenge ile başlar. Başlatma aynı login IP kotasına katılır,
CSRF gerekir; protokol callback framework kontrollerine tabidir. SaveTokens=false,
openid scope, form_post, RS256 ve HTTPS zorunludur. PAR bu ders için kapalıdır.
CSP yalnız form hedefinde yapılandırılmış sağlayıcı origin'i kadar genişletildi.
AspNetUserLogins zaten var: model veya migration değişikliği gerekmedi.
