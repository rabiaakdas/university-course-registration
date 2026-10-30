# Gün 21 — Onay olayını gözlemlemek

Command yapılması istenen işlemdir: ApproveAsync onaylamayı dener ve reddedilebilir.
EnrollmentApproved gerçekleşmiş, kaydedilmiş onay geçişini bildirir. Immutable olay
yalnız kimlikler ve uygulamada oluşturulma zamanını taşır; entity/DbContext, HTTP
veya kişisel veri taşımaz. OccurredAtUtc kesin SQL commit timestamp'i değildir.
EventId yalnız olay kimliğidir; teslim veya dedup garantisi sağlamaz.

Observer rolleri: EnrollmentOperations olayı üretir; EnrollmentApprovedPublisher
kayıtlı IEnrollmentApprovedObserver nesnelerini sırayla await eder; ApprovalLogObserver
kritik olmayan tepki verir. Ön koşul/kredi/program kontrolleri observer'a taşınmadı.
Onay için zorunlu adımlar hâlâ koordinasyondadır. Üretimde yalnız log observer
vardır; çoklu dağıtım ve arıza gözlemcileri scripts/ObserverChecks.cs içindedir.

Bu, uygulama içinde kullanılan domain olayıdır. Integration event ayrı süreçlere
yayımlanan, sürümleme/teslim/sözleşme kararları gerektiren bir sınırdır; bugün broker
ve integration yayını yoktur. Adapter deneyleri de onaya bağlanmadı.

## Commit sınırı

Program.cs SQL Server scoped context kullanır; EnableRetryOnFailure veya dış
transaction yoktur. AdvanceAsync yalnız onay için mevcut/ambient/enlisted dış
transaction varsa değişiklik yapmadan hata verir. Bağımsız SaveChangesAsync
tamamlandıktan sonra olay oluşturulup yayınlanır. Tek SQL UPDATE'in kendi transaction'ı
olabileceğinden her SaveChanges için ayrıca BEGIN/COMMIT komutu beklenmez.
EF'nin dış transaction içindeki SaveChanges'i dış transaction'ı commit etmez.
[Microsoft: transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

Commit öncesi yayın, rollback olacak işlem için yanlış tepki oluşturabilirdi.
Commit sonrası yayın kaydedilmiş durumu gözlemletir, fakat commit ile yayın
arasında süreç kapanırsa olay kaybolabilir. Kısmen dağıtılmış olay kalan gözlemcilere
ulaşmayabilir. Log kalıcı teslim kaydı değildir. Retry, dedup, exactly-once veya
shutdown sonrasında devam garantisi yoktur. DB transaction'ı e-posta gibi dış etkiyi
geri alamaz. İleride Outbox/queue bu boşluğu ele alacak; bugün eklenmedi.

## Hata ve iptal

Publisher bir dağıtımda her kayıtlı observer'ı en fazla bir kez çağırır; zorunlu iş
sırası oluşturmaz. Beklenmeyen exception (çağıran token iptal değilken OCE dahil)
EventId, EnrollmentId, ObserverType ve ExceptionType ile loglanır; ham exception
mesajı/stack veya bağlantı kaydedilmez. Sonraki bağımsız observer devam eder.
Onay geri alınmaz ve bu tepki hatası HTTP başarısını 500'e dönüştürmez.

Çağıran token aynen iletilir. Token iptalse yeni observer başlatılmaz; işbirlikçi
OCE teknik arıza logu üretmez. Publisher Cancelled bildirir; EnrollmentOperations
onayın zaten kesinleştiğini gözeterek mevcut Updated sonucunu korur. İstemci bağlantı
kapandığında yanıtı alacağı garanti değildir. Fire-and-forget/Task.Run yoktur.
Logger sağlayıcısının kendisi hata verirse akış bozulabilir; genel fallback eklenmedi.
İki context'in eşzamanlı onay yarışı ve mükerrer olay üretimi hâlâ çözülmedi.

## Kod ve doğrulama

- Application/EnrollmentApproved.cs: immutable veri ve observer sözleşmesi.
- Application/EnrollmentApprovedPublisher.cs: sıralı await, hata/iptal politikası.
- Application/ApprovalLogObserver.cs: EventId 2101, EnrollmentApprovalObserved.
- Application/EnrollmentOperations.cs / AdvanceAsync: save → olay → await → Updated.
- Program.cs: scoped publisher ve observer composition.
- scripts/ObserverChecks.cs: saf dağıtım; gerçek endpoint + test DI/Kestrel; yeni
  context ile commit kanıtı, SQL CHECK yazma arızası, postcommit iptal ve sorgu sayısı.

Saf kontroller SQL/HTTP kanıtı değildir. Entegrasyon testleri yeni, sahipliği
doğrulanmış izole LocalDB'de çalışır. Gerçek endpoint mapping yeniden kullanılır;
test observer'ları yalnız test host'una eklenir. Üretim endpoint'ine fault anahtarı
eklenmez. Ana süreçteki state/full kontrolleri gerçek Program DI kaydını da kullanır.
