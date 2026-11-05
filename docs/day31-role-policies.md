# Gün 31 — Claim, rol ve policy

Claim principal üzerindeki tür/değer bilgisidir; rol görev üyeliğidir; policy kabul
edilen şema, kimlik ve rol gereksinimlerini birleştirir. Bir claim adı kendi başına
ayrıcalık vermez: doğrulanmış kaynaktan gelmeli ve sunucu policy'since yorumlanmalıdır.
Frontend butonunu gizlemek doğrudan HTTP çağrısını engellemez.

`AccountSetup.AddRoles<IdentityRole>` rol store'ları ve
`UserClaimsPrincipalFactory<ApplicationUser,IdentityRole>` ile cookie'ye üyelikleri
aktarır. Manuel cookie role claim kopyalama yoktur. UserId, StudentId değildir;
Student profili bağlantısı veya EmailConfirmed ayrıcalıklı rol sağlamaz. Register
request'inde rol alanı yoktur; gerçek HTTP'de ek role/roles/permission gönderimi
başarılı hesaba hiçbir üyelik kazandırmadı.

`TokenSessions.CreatePairAsync` kilit/transaction içindeki mevcut üretim yerinde,
UserManager'dan güncel rol üyeliklerini okur. RS256/issuer/audience/lifetime/sub
kontrolleri aynen kalır. `UniversityJwtSetup` RoleClaimType=`role`, MapInboundClaims=false.
Rol dizisi JWT işleyicisi tarafından ayrı role claim'leri olarak yorumlanır. Eski
cookie/JWT'den üyelik kopyalanmaz. Her token üretimine bir rol sorgusu eklenir;
bearer-me doğrulamasına DB sorgusu eklenmez.

`ManageAcademicCatalog` yalnız cookie şeması, authenticated user ve StudentAffairs
ister. Authorization middleware handler çalışmadan401/403 verir. Yetkili istek
`CatalogAntiforgeryFilter` üzerinden yerleşik antiforgery doğrulamasından geçer;
sonra eski alan/iş kuralı/DB hata davranışı çalışır. Katalog GET'leri public kalır.
Bu gün dört katalog yazma route'uyla sınırlıdır; ownership/atama sonraki gündür.

Policy mantığı: `RequireRole(A,B)` VEYA; iki ayrı `RequireRole(A).RequireRole(B)` VE.
`RolesChecks` hem cookie hem bearer için ayrı role politikaları, Advisor OR
StudentAffairs ve Advisor AND Instructor deneyleri kullanır. Test route'ları
production derlemesinde yoktur; sabit başarı dışında veri taşımazlar.

Rol değişikliği damga değişikliği değildir. Gerçek manager add/remove sonrasında
stamp aynı kaldı; eski cookie/JWT yetkisi sürdü. Yeni login/mint/refresh rolü yeniden
okudu. Kontrollü saatle mevcut20 dakika fixed cookie19'da eski rolü taşıdı,21'de401
verdi. Varsayılan30 dakika stamp doğrulamasından önce bilet biter. Stamp doğrulaması
başarılı olduğunda factory yeniden principal üretir; rol kaldırıldı diye cookie'nin
anında reddedildiğini veya JWT'nin değiştiğini söyleyemeyiz.

Bakım komutu `Program.cs` içinde app.Build sonrasında Run'dan önce ayrılır; Development
kontrolü, sabit izin listesi, RoleManager/UserManager ve exit code kullanır. Normal
startup rol oluşturmaz, ilk kullanıcıyı yönetici yapmaz. Yerel operatörün yetkili
olması varsayımı vardır; Production provisioning/panel yoktur.

## Kaynak ve kod bağlantıları

- [Microsoft rol yetkilendirme](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0): AddRoles, rol politikaları ve VE/VEYA.
- [Microsoft claim eşleme](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-10.0): RoleClaimType ve isim eşleme.
- [Principal factory v10.0.14](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Extensions.Core/src/UserClaimsPrincipalFactory.cs): kimlik/stamp ve rol üyelikleri.
- [Stamp validator v10.0.14](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Core/src/SecurityStampValidator.cs): aralık sonrası principal yenilemesi, fixed expiry korunumu.

Üretim: Accounts/UniversityRoles.cs, AccountSetup.cs, TokenSessions.cs,
UniversityJwtSetup.cs; Endpoints/CourseEndpoints.cs, AcademicTermEndpoints.cs,
CourseOfferingEndpoints.cs. Test: RolesChecks.cs ve CatalogTestIdentity.cs;
CookieSessionChecks'in gerçek HTTPS/key/log altyapısı yeniden kullanılır.
