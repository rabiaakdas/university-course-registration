# Gün 35 — OIDC Code + PKCE ve açık hesap bağlantısı

OAuth yetki devridir; OIDC bunun üzerinde kimlik doğrulama protokolüdür. JWT bir veri
biçimidir. ID token bu istemcideki giriş içindir; access token API erişimi içindir.
Bu uygulama sağlayıcının access token'ını kendi UniversityBearer token'ı saymaz.

`Accounts/UniversityOidc.cs`: tek Development sağlayıcısı, `UniversityOidc` şeması,
code/form_post, PKCE S256, yalnız openid, HTTPS discovery/JWKS, RS256, sabit issuer,
client audience, süre ve 30 saniye skew. PAR bu dar ders akışında kapalıdır.
Framework state/correlation ile giriş talebini; nonce ile ID token'ın o talebe
bağını doğrular. PKCE authorization code'u backend'deki verifier'a bağlar.
Bunlar birbirinin yerine geçmez. İmza şifreleme değildir.

İki callback farklıdır: `/signin-oidc` protokol yanıtını doğrulayan handler yoludur;
`/accounts/external/callback` geçici Identity.External sonucunu yerel hesaba bağlar.
`Endpoints/ExternalLoginEndpoints.cs` başlangıç POST'unda antiforgery doğrular.
Protokol dönüşünde form antiforgery yerine OIDC correlation/state/nonce kullanılır.
ReturnUrl yalnız `/` veya `/index.html`; giriş yapmış hesap başlangıçta409 alır.

Doğrulanmış tek, boş olmayan sub'dan NameIdentifier üretilir. E-posta ve sağlayıcı
rolleri geçici principal'a bile taşınmaz. Identity LoginProvider değeri sabit şema
ve doğrulanacak Authority'nin SHA-256 kimliğinden oluşur; ProviderKey sub'dır.
Böylece Authority değiştirildiğinde eski bağlantı başka issuer'a yorumlanmaz.
Bu hash sır/parola değildir; issuer ad alanını kararlı biçimde sınırlar.

`external link <accountId> <verifiedSubject>` yalnız Development'ta UserManager ile
önceden mevcut hesaba bağlantı kurar. Subject güvenilen yönetici girdisidir; public
self-link yoktur. Aynı bağlantı tekrarında başarı, başka hesaba taşımada ret vardır.
E-posta benzerliği sahiplik kanıtı değildir. Otomatik hesap/Student/rol oluşturulmaz.

`ExternalLoginSignInAsync(..., false, false)` yerel uygunluk, doğrulanmış e-posta ve
lockout kontrolünü kullanır; yerel principal Identity role factory'den gelir.
Desteklenmeyen iki aşamalı hesap ayrıca reddedilir; MFA bypass yapılmaz. Harici
başarısızlıklar parola denemesi değildir, AccessFailedCount artırılmaz.

Application cookie mevcut Secure/HttpOnly/Strict ve20dakika politikasını korur.
External cookie Secure/HttpOnly/Lax,5dakika; nonce/correlation Secure/None'dır.
SaveTokens=false: sağlayıcı token'ları application cookie'ye/browser storage'a
konmaz. UI gerçek form navigasyonu yapar; fetch redirect'i JSON diye okumaz.
CSP form-action yalnız self ve yapılandırılmış sağlayıcı origin'ine izin verir;
script/connect kaynakları genişletilmez. Yerel logout sağlayıcı oturumunu kapatmaz.

Reset/security-stamp ile giriş arasında atomik hesap kilidi eklenmedi. Uygunluk
kontrolü ile cookie yazılması arasındaki yarış çözülmüş değildir; mevcut stamp
kontrol aralığı/anlık iptal sınırı sürer. Federated logout, MFA, provisioning ve
Production harici sağlayıcı işletimi bu dersin kapsamı değildir.

## Gerçek test ile protokol fixture ayrımı

`scripts/OidcChecks.cs` + `scripts/browser/oidc-checks.mjs`: izole LocalDB, gerçek
HTTPS/Edge ve MIT lisanslı OpenID Certified oidc-provider9.13.3. Başarı akışında
kütüphanenin discovery/JWKS/code/token/PKCE/tek-kullanımlık code işlemleri kullanılır.
Test hesabı seçme ekranı yalnız kontrollü fixture kimliklerini sunar; gerçek hesap
veya dış tenant yoktur. Üretim uygulaması sağlayıcı içermez.

Yanlış nonce/issuer/audience/imza/süre için yalnız test sağlayıcısının token
cevabı kontrollü yeniden imzalanır. Bunlar normal sağlayıcı başarısı olarak
sunulmaz. Test, nonce hatasının signature/issuer/audience kabulünden sonra oluştuğunu
ve exception türünü kontrol eder. Verifier ve code replay, gerçek sağlayıcının token
endpoint'inde400 üretir. Backend backchannel yalnız bu testin sertifikasına güvenir;
Edge yalnız aynı sertifikanın SPKI anahtarına izin verir. Genel TLS bypass yoktur.

## Kaynaklar

- [Microsoft OIDC yapılandırması (.NET10)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0)
- [SignInManager10.0.14 kaynak kodu](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Core/src/SignInManager.cs): external login, PreSignInCheck, ProviderKey seçimi.
- [OIDC Core Code Flow](https://openid.net/specs/openid-connect-core-1_0.html#CodeFlowAuth)
- [RFC7636 PKCE](https://www.rfc-editor.org/rfc/rfc7636.html)
- [oidc-provider kaynak/kurulum/lisans](https://github.com/panva/node-oidc-provider): MIT, Node destek aralığı ve geliştirme adapter sınırları.
