# Gün 25 — Hesap, parola ve yerel e-posta doğrulama

## Kod ve sorumluluklar

| Kod | Sorumluluk |
|---|---|
| `Accounts/AccountSetup.cs` | Identity politika/EF store/token provider, Development kontrolü ve Data Protection |
| `Endpoints/AccountEndpoints.cs` | İstek sınırı, UserManager çağrıları, güvenli alan hataları, register/confirm HTTP sonuçları |
| `Accounts/LocalAccountConfirmationSender.cs` | Kullanıcı girdisinden yol üretmeden tek mesajı yerel dosyaya yazma |
| `Contracts/AccountRequests.cs` | Entity yerine küçük request/response sözleşmeleri |
| `Data/RegistrationWriteErrors.cs` | Yalnız AspNetUsers/UserNameIndex/2601 kaydetme hatası için yeni dar sınıflandırma |
| `scripts/AccountsChecks.cs` | Gerçek HTTP/LocalDB/Identity/token, dosya kutusu ve süreç restart kontrolleri |

`UserManager.CreateAsync(user, password)` parola doğrulayıcılarını, hasher'ı ve
hesap doğrulamasını kullanır. Bu sürüm parola hatasında kullanıcı doğrulamasına
geçmeden dönebilir; bütün Identity hatalarının tek çağrıda toplandığı iddia edilmez.
HTTP sınırı eksik/boş ve üst uzunluk hatalarını birlikte verir. Parola hiç Trim
edilmez. `UserName=Email=trimmedEmail`; normalized alanları manager hazırlar.
Varsayılan normalizer Unicode normalizasyonu ve invariant büyük harf uygular.
Nokta ve + etiketleri kaldırılmaz. ASCII kullanıcı adı whitelist'i boş bırakıldı;
e-posta biçimi Identity'nin standart email doğrulayıcısına aittir.

RequireUniqueEmail bir uygulama sorgusudur. Mevcut EmailIndex unique değildir.
Bu akış e-postayı kullanıcı adı da yaptığından mevcut UserNameIndex yarış anında
aynı normalize adı engeller. Bunun bütün olası dış hesap yazmalarını e-posta
bakımından benzersiz yaptığı iddia edilmez. Ön doğrulama ve gerçek index hatası
aynı `400 / account_creation_failed / errors.email` sonucuna çevrilir. Diğer
unique/PK/altyapı hataları kullanıcı hatası yapılmaz. SQL Server nesne adları
yapılandırılmış exception alanı olmadığı için mevcut mesaj biçimine dar bağımlılık
sürer; tanınmayan biçim teknik hata olarak kalır.

## Hash, koruma, kodlama

Hash parola geri çözümü için değildir. Identity'nin salt ve maliyetli parola
türetme algoritması kullanılır; aynı parola iki hesapta farklı saklanan hash üretir.
Salt, hesaplar arası aynı parola için aynı çıktıyı önler; work factor hesaplama
maliyetini artırır. Burada özel hash algoritması veya work-factor ayarı eklenmedi.
Kesin güvenlik maliyeti/performans garantisi çıkarılmaz.

Data Protection token'ın içeriğini korur; Base64Url yalnız UTF8 token'ın URL'de
taşınmasını sağlar, şifreleme değildir. Email confirmation token'ı kullanıcı,
amaç, oluşturma zamanı ve security stamp ile ilişkilidir. Token DB'ye ayrıca
kaydedilmez. Başarılı doğrulama login, öğrenci hesabı sahipliği veya yetki vermez.

## Gözlenen süre ve yeniden kullanım

Üretim sağlayıcısının süresi iki saattir. GenerateEmailConfirmationTokenAsync yeni
token üretince eski token geçerli kalabildi. ConfirmEmailAsync başarılı olduktan
sonra aynı token yeniden kabul edildi; yeni token da kabul edildi. Keyfi token,
hesap zaten doğrulanmışken bile reddedildi. Güvenlik damgası değiştirilen test
hesabında eski token geçersiz oldu. Bu sonuçlar tek kullanımlılık garantisi değildir.

Sağlayıcı DateTimeOffset.UtcNow kullanır, enjekte TimeProvider kullanmaz. Süre
testinde daha önce üretilmiş gerçek token, sadece test DI seçenekleri geçici olarak
1 tick pozitif ömre indirildiğinde reddedildi; sonra iki saat geri yüklendi. İki
saat gerçek zaman beklenmedi, production koduna saat/test anahtarı eklenmedi.
Paylaşılan DataProtectionTokenProviderOptions gelecekte aynı sağlayıcıyla açılacak
diğer token amaçlarını da etkileyebilir; bugün reset/2FA endpoint'i yoktur.

## Commit, gönderim ve iptal

Hesap CreateAsync ile kaydedilir, sonra token üretilip sender beklenir. Arada
transaction bağı yoktur. Sender arızasında hesap silinmez: 201 ve `failed`.
Çağıran token iptal edilmişse `cancelled`; token iptal değilken gelen OCE teknik
sender arızasıdır. Güvenli log yalnız kararlı EventId2501 ve exception türünü içerir.
Logger arızası için genel fallback yoktur. UserManager'ın bu API'leri dışarıdan
CancellationToken almaz; varsayılan manager iç token'ı None'dır. Endpoint sınırda
iptali kontrol eder, sender'a token iletir; devam eden Identity DB çağrısının request
iptaliyle duracağını iddia etmez. Commit sonrası yanıtın kaybı hesabın yokluğunu
kanıtlamaz. Kalıcı retry/resend/recovery/Outbox bugün yoktur.

## Yerel anahtar ve posta kutusu

Content root altında `.local/accounts/keys` ve `messages` Git dışındadır ve
wwwroot altında değildir. Windows anahtarları DPAPI ile korunur; aynı kullanıcı,
uygun anahtarlar ve uygulama adı restart doğrulaması için gerekir. Dosya erişim
izinleri ayrıca önemlidir. Windows dışı key-at-rest koruması burada tasarlanmadı.
Testler ayrı content root, key ring ve uygulama ayrımı kullanır; child süreçler
kendi izole content root'unda aynı uygulama adıyla restart edilir. Test dizini
yalnız üretilen obj/accounts-checks/run kimliği doğrulandıktan sonra temizlenir.

Mesaj adı rastgele Guid'dir, recipient yolun parçası değildir. CreateNew/pending
dosyası tamamlanınca json adına taşınır; paralel çağrılar aynı dosyayı paylaşmaz.
Kısmi dosya temizliği best-effort'tur. Dosyada token bağlantısı bir secret'tır;
terminalde paylaşmayın. Kutunun gerçek e-posta kutusu erişimini veya teslimi
kanıtladığı söylenemez. Yerel modda hosting request URL bilgi logları kapatılır;
token'lı adresleri başka proxy/terminal loglarına taşımayın.

## Resmi kaynaklar (10.0.14 / ASP.NET Core 10)

- [UserManager kaynak kodu](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/Extensions.Core/src/UserManager.cs): CreateAsync, ConfirmEmailAsync ve cancellation sınırı.
- [UserValidator](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/Extensions.Core/src/UserValidator.cs) ve [normalizer](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/Extensions.Core/src/UpperInvariantLookupNormalizer.cs): doğrulama, RequireUniqueEmail ve normalized alanlar.
- [PasswordHasher](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/Extensions.Core/src/PasswordHasher.cs): salt ve PBKDF2 tabanlı sürümlü Identity hash'i; özel SHA256 depolaması yok.
- [DataProtectorTokenProvider](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/src/Identity/Core/src/DataProtectorTokenProvider.cs): zaman/user/purpose/stamp kontrolleri ve UTC saat kullanımı.
- [Hesap doğrulama](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0): token üretme/confirm ve UTF8 + Base64Url taşıma.
- [Data Protection yapılandırması](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0): kalıcı anahtar alanı, uygulama izolasyonu, DPAPI ve erişim izinleri.
