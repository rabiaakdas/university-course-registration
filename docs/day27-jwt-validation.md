# Gün 27 — Ayrı JWT bearer doğrulaması

`Accounts/UniversityJwtSetup.cs`: Jwt:Enabled ve DevTokenEnabled guards, ayrı şema/
policy, açık doğrulama parametreleri ve sub denetimi. `UniversityJwtKeys.cs`: PEM
okuma, eşleşme, public/private ayrımı ve DI dispose. `Endpoints/UniversityJwtEndpoints.cs`:
mevcut cookie principal'dan kontrollü Development token üretimi ve bearer-me.
`CookieSessionChecks.cs` HTTPS/cookie hazırlığını; `JwtChecks.cs` JWT HTTP matrisini
paylaşır. Test fixture algoritması üretim doğrulamasının yerine geçmez.

Decode, Base64Url JSON'u açıp okumaktır; doğrulama değildir. İmza payload'ın güvenilen
özel anahtarla üretildiğini ve değiştirilmediğini denetler; şifreleme değildir.
JWT içeriği okunabilir. Parola/hash/stamp/email/rol/öğrenci bilgisi konulmaz.
Private RSA yalnız Development imzasında kullanılır. Ayrı public RSA nesnesi
ValidateIssuerSigningKey ile doğrulamaya verilir; private parametreler taşınmaz.
ImportFromPem geçersiz/çoklu/şifreli PEM'i reddedebilir. Kurulum PKCS8 private ve
SubjectPublicKeyInfo public PEM üretir; dosyalar şifreli değildir, kullanıcıya özel
ACL ve repo dışı konum gerekir. İçerik veya dosya yolu başlangıç hata metnine eklenmez.

Singleton key sahibi DI factory ile oluşturulur ve host kapanışında Dispose edilir;
her istek/başlangıç yeni kalıcı anahtar üretmez. Public/private anahtar eşleşmesi ve
minimum 2048-bit RSA kontrol edilir; kurulum script'i 3072 bit üretir. Production
validasyonu yalnız public dosya gerektirir; private fallback yoktur. Açık Jwt:Enabled
ayarı yoksa JWT şeması/endpoint'leri kapalıdır. DevTokenEnabled yalnız Development
ve Enabled ile mümkündür; Production yanlış ayarı başlangıçta reddedilir.

iss güvenilen üretici, aud hedef API, nbf ilk kullanım, exp son kullanım, iat üretim
zamanıdır. Süre on dakika; expiresIn 600. Otuz saniye skew küçük saat farkını tolere
eder, token'a ilave garanti vermez. RequireExpirationTime/RequireSignedTokens açık;
yalnız RS256 kabul edilir. Audience trailing slash farkı da kabul edilmez. Token
header alg/jku/x5u değerleri güven kaynağı değildir; Authority/JWKS/remote key resolver
kurulmadı. Varsayılan JwtBearer yalnız Authorization Bearer header'ını okur; query
access_token alınmaz. Başarısızlıkta IncludeErrorDetails=false, JWT handler logu ve
JWT açıkken URL logu kapalıdır; harici proxy log politikası ayrıca ele alınmalıdır.

MapInboundClaims=false: sub olduğu gibi kalır. OnTokenValidated içinde tam bir dolu
sub claim'i ve JSON'da tek string sub alanı gerekir; array veya yinelenen JSON alanı
reddedilir. Buradaki payload incelemesi ancak imza/issuer/audience/lifetime geçtikten
sonradır. Ham ReadJwtToken sonucundan kimlik türetilmez. sub Identity hesap kimliğidir,
Student.Id değildir; öğrenci profili olmayan hesap da geçebilir.

Varsayılan Identity application cookie değişmedi. UniversityBearerOnly yalnız
UniversityBearer, dev-token ise açık UniversityCookieOnly policy'sini kullanır.
Bearer me, cookie me değildir; cookie tek başına bearer policy'sini karşılamaz.
Cookie principal'dan alınan hesap yeniden okunur; CanSignInAsync/lockout ve CSRF
geçmeden token verilmez. İstemci kimlik, rol veya süre seçemez; POST gövdesi model
olarak bağlanmaz. JSON'daki saldırgan userId göz ardı edilir. Bu standart OAuth/OIDC
token endpoint'i değil, Development'ta oturum üzerinden kontrollü eğitim deneyidir.

Bearer doğrulaması otomatik hesap/stamp/oturum DB sorgusu yapmaz. Aynı jti tekrar
kullanılabilir; jti yalnız tanımlayıcıdır. Cookie çıkışı, parola veya hesap değişimi
önceden imzalanmış JWT'yi otomatik iptal etmez. Denylist, refresh/rotation, replay
cache ve tüm cihazlardan çıkış yoktur. Akademik endpoint'ler topluca korunmadı.

Testler gerçek HTTPS ve izole LocalDB kullanır. Sertifika pinning/cookie/CSRF hazırlığı
26. günden yeniden kullanılır. Doğrulayıcı testte üretilen, repo dışı geçici RSA
anahtarlarını alır. SqlCounter önce login sorgusunu gördüğünü kanıtlar, ardından
bearer-me çağrılarında artmadığını kontrol eder. Geçmiş/gelecek imzalı fixture'lar
HTTP'ye gönderilir: skew içinde exp=-2 saniye/nbf=+10 saniye kabul, dışarıda ±120
saniye ret. Gerçek saat ve geniş marj kullanılır; tam 30.saniye sınırında benchmark
veya uzun bekleme yoktur. Bütün ret fixture'ları gerçekten API'ye gönderilmiştir.

## Resmi kaynaklar

- [ASP.NET Core 10 JWT bearer doğrulaması](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Uyumlu kararlı JwtBearer 10.0.14 paketi](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer/10.0.14)
- [Şema seçimi](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/limitingidentitybyscheme?view=aspnetcore-10.0)
- [JwtBearerOptions: MapInboundClaims, IncludeErrorDetails](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbeareroptions?view=aspnetcore-10.0)
- [TokenValidationParameters](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters)
- [RSA.ImportFromPem](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsa.importfrompem?view=net-10.0)
