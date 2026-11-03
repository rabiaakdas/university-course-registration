# Gün 24 — Hesap ve öğrenci profili

Authentication, isteği yapanın kimliğini doğrular; authorization, o kimliğin hangi
işlemi yapabileceğini değerlendirir. Bugün ikisi de HTTP akışına bağlanmadı.
Identity verisinin eklenmesi mevcut endpoint'leri güvenli self-servis yapmaz.

## Sorumluluklar ve ilişki

| Kod | Sorumluluk |
|---|---|
| `Models/ApplicationUser.cs` | IdentityUser'ın string kimlikli hesap modeli; akademik alan taşımaz |
| `Models/Student.cs`, `TryLinkAccount` | Öğrenci profili ve bir kez bağlanabilen, private setter'lı UserId |
| `Data/UniversityDbContext.cs`, `OnModelCreating` | Önce Identity temel eşlemesi, sonra mevcut akademik eşlemeler ve hesap FK/index'i |
| `Program.cs` | Scoped context ve AddIdentityCore + EF store kaydı |
| `scripts/IdentityModelChecks.cs` | Parolasız UserManager oluşturma, yeni scope okuma, SQL kısıtları ve ayrı kaydetme deneyi |

`UserManager`, hesap işlemlerini ve IdentityResult doğrulamalarını koordine eder.
EF store bu işlemleri context üzerinden kalıcılaştırır. IdentityDbContext
Identity tablolarını eşler; bir HTTP giriş akışı veya yetkilendirme kararı değildir.
Öğrenci profili olmadan hesap, hesap olmadan birden çok öğrenci bulunabilir.
Enrollment ve başarı ilişkileri hâlâ değişmeyen sayısal Student.Id'ye bağlıdır.

Student.UserId nullable `nvarchar(450)` ve AspNetUsers.Id'ye NO ACTION FK'dir.
`UX_Students_UserId`, yalnız `[UserId] IS NOT NULL` satırlarında unique'dir:
çok sayıda NULL kabul edilir, dolu bir hesap kimliği iki profile bağlanamaz.
Her profilde tek UserId vardır. Hesap bağlıyken fiziksel silme engellenir;
akademik kayıtlar cascade ile silinmez. Identity'nin kendi claim/token/role
ilişkilerinin varsayılan cascade eşlemesi bundan ayrıdır.

`TryLinkAccount` null/boş/whitespace ve önceden bağlı profili false ile reddeder.
Aynı kimlikle tekrar çağrı da false; önceki değer korunur. Başarılı değer aynen
saklanır, Trim/case dönüşümü yapılmaz. Metot hesap varlığını, başka profile
bağlılığı veya çağıranın yetkisini kanıtlamaz. FK/unique ilk ikisinin DB korumasıdır;
sahiplik ve yetki ileride ayrıca tasarlanmalıdır. Ad/e-posta/numara ile eşleştirme,
public bağlama, unlink/relink endpoint'i yoktur.

## Tek context, iki ayrı kaydetme sınırı

Mevcut context'i genişletmek akademik mapping ve migration geçmişini korur.
Bedeli Identity ile iş verisinin aynı EF modelini ve migration yaşam döngüsünü
paylaşmasıdır. Identity store'un varsayılan `AutoSaveChanges=true` davranışında
`UserManager.CreateAsync(user)` kendi SaveChanges'ini yapar. Aynı context'teki
başka bekleyen değişiklikler de bu SaveChanges'e katılabilir; hesap deneyi önce
temiz context üzerinde hesabı oluşturur.

Deneyde parolasız hesap başarıyla kaydedilir; ardından aynı context'te bu hesaba
bağlı yeni profil, mevcut öğrenci numarasını tekrarladığı için
`UX_Students_StudentNumber`/2601 hatası alır. Yeni scope'ta hesap vardır, profil
yoktur. Aynı context kullanmak bütün akışı tek transaction yapmaz. Bu bir ürün
provisioning akışı değildir. Test, başarısız context'i yeniden kaydetmez; oluşturduğu
hesap kimliklerini önceden takip eder ve finally içinde yalnız kendi verisini temizler.

## Paket, temel model ve resmi kaynaklar

EF SqlServer/Design ve yerel dotnet-ef 10.0.14 korunarak yalnız
[Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.14](https://www.nuget.org/packages/Microsoft.AspNetCore.Identity.EntityFrameworkCore/10.0.14)
eklendi; kararlı paket net10.0 hedefler.
[Identity model belgesi](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)
temel OnModelCreating çağrısını ve IdentityDbContext'in rol içeren modelini açıklar.
Seçilen varsayılan model bu migration'da yedi AspNet tablosu üretir; rol tabloları
bulunur ama [AddIdentityCore](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.identityservicecollectionextensions.addidentitycore?view=aspnetcore-10.0)
varsayılan olarak rol servislerini eklemez. AddRoles/RoleManager akışı eklenmedi.
SchemaVersion/passkey veya UI ayarı yapılmadı; migration'da passkey tablosu yoktur.

[EF index belgesi](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-filter)
SQL Server nullable unique index filtre davranışını açıklar. Filtre burada açıkça
eşlendi ve gerçek SQL metadata'sında doğrulandı.
[10.0.14 UserOnlyStore kaynağı](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/EntityFrameworkCore/src/UserOnlyStore.cs)
CreateAsync → SaveChanges ve AutoSaveChanges varsayılanını gösterir; EF user-only
store için de aynı kaydetme sözleşmesi gerçek DI deneyiyle doğrulandı.
[SQL Server SET QUOTED_IDENTIFIER](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-quoted-identifier-transact-sql?view=sql-server-ver17)
filtreli index'in SET gereksinimini açıklar. Eski rollback-only SQL örneğine ON
eklendi; global sunucu ayarı değiştirilmedi.

## Güvenlik ve doğrulama sınırları

Test hesabı parolasızdır; bu parola ile giriş yapılabilirlik değildir. Cookie,
JWT, SignInManager, token provider, Identity HTTP endpoint'i, rol atama ve gerçek
kullanıcı hesabı seed'i yoktur. Identity entity'si HTTP yanıtı olarak sunulmaz.
Migration yalnız şemayı değiştirir; eski Student.UserId değerleri NULL kalır.
Down kaynakları incelendi, uygulanmadı: hesap verisini ve bağlantıları kaldırır,
yedek veya geri kazanım garantisi değildir. Üretim provisioning/hesap silme,
yarışan bağlama ve auth güvenliği bu günün kanıtı değildir.
