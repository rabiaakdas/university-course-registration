# Gün 30 — Identity parola sıfırlama

ChangePassword eski parolayı gerektirir; ResetPassword süreli kurtarma iznini doğrular.
HTTP gövdesindeki userId bir yetki kanıtı değildir. UserManager token provider/purpose,
hesap ve security stamp doğrulamasından sonra parola politikasını çalıştırır, Identity
hash'ini ve damgayı günceller. Parola/hash/stamp üretimini uygulama yeniden yazmaz.

`PasswordResetTokenProvider` yalnız adlandırılmış seçenekler ve Data Protection
koruma adını ayırır; kriptografiyi miras alınan sağlayıcı yapar. Reset15 dakika,
confirmation2 saat. UTF8/Base64Url taşıma kodlaması şifreleme değildir. Yeni token
üretmek eski token'ı iptal etmez; başarılı resetin damga değişimi önceki token'ları
geçersiz kılar. Purpose binding nedeniyle confirmation token reset izni değildir.

`PasswordResetOperations.ResetAsync` açık transaction sahibidir. EF Identity store'un
SaveChanges'i bu transaction'ı commit etmez. Reset başarılıysa yalnız henüz iptal
edilmemiş aileler ExecuteUpdate ile iptal edilir, sonra commit olur. Başarısız sonuçta
veya exception'da dispose rollback yapar. Bellekteki değişmiş entity rollback kanıtı
olamaz; testler yeni context ile hash/stamp/aileleri okur. EF store concurrency kaybını
IdentityResult.ConcurrencyFailure olarak verir; endpoint bunu başarı saymaz.

Yalnız aile kilidi yeni aile eklenmesini engellemez. `AccountWriteLock.AcquireAsync`
parametreli UPDLOCK/HOLDLOCK ile hesabı ortak koordinasyon noktası yapar. Reset ve
`TokenSessions.CreateAsync/RefreshAsync/RevokeAsync` hesap → aile → token sırasını
izler. Kimliği bulmak için önce okunan yönlendirme alanlarına karşılık durum kilit
sonrasında yeniden okunur. Cookie doğrulaması hesabı zaten izlemiş olabileceğinden
Reload yapılır. İlk token üretimi kilit altında güncel stamp ile principal'ı karşılaştırır.
Böylece resetten önceki cookie ile reset sonrasında yeni aktif aile oluşturulmaz.
Bu yolun maliyeti aynı hesabın bağımsız ailelerinin de kısa süre sıraya girmesidir.

Forgot akışı veri değiştirmez ve kilit altında mesaj yazmaz. Güvenilen callback ayarı
Host'tan bağımsızdır; bağlantı GET'i işlem yapmaz. Yerel JSON mesajı gerçek e-posta
veya kalıcı teslim değildir. Aynı202 yanıtı timing/enumeration korumasını tamamlamaz.

Cookie security stamp kontrolü periyodiktir (mevcut varsayılan30 dakika); sabit bilet
20 dakika sürer. Test yalnız kendi host'unda stamp aralığını0 yapıp zamanı ilerletir.
JWT'nin imza/süre doğrulaması aynen kalır; parola değişimi JWT'yi anında iptal etmez.

## Kaynaklar ve gözlemler

- [UserManager v10.0.14](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Extensions.Core/src/UserManager.cs): GeneratePasswordResetTokenAsync/ResetPasswordAsync, ResetPassword purpose ve stamp güncellemesi.
- [DataProtectorTokenProvider v10.0.14](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Core/src/DataProtectorTokenProvider.cs): oluşturulma zamanı, userId, purpose, stamp ve süre doğrulaması; doğrudan UtcNow kullanır.
- [EF UserStore v10.0.14](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/EntityFrameworkCore/src/UserStore.cs): aynı context'te AutoSaveChanges, concurrency sonucunun korunması.
- [SecurityStampValidatorOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.securitystampvalidatoroptions.validationinterval?view=aspnetcore-10.0): varsayılan30 dakika.

Kaynaklar uygulama sonucu yerine geçmez. `scripts/PasswordResetChecks.cs` gerçek
HTTPS/LocalDB ile purpose/reuse/yarış ve rollback'i doğrular. Provider TimeProvider
almadığı için süre kontrolü yalnız izole testte10ms geçerlilik ve30ms bekleme kullanır;
üretim15 dakikayı uzun süre bekleyerek ölçmez. Test token'ı elle üretmez/değiştirmez.
Host/cleanup/secret taraması `CookieSessionChecks`, kilit koordinasyonu `RefreshChecks`
üzerinden yeniden kullanılır. Ağ kopması/çökme/timing saldırısı garantisi ölçülmedi.
