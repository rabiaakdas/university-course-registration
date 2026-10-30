# Gün 18 — Bildirim Adapter deneyi

## Roller ve sınırlar

`scripts/NotificationAdapterDemo.cs` içindeki **target** INotificationSender,
NotificationMessage ve NotificationSendResult sözleşmesidir. **Adaptee**'ler
FakeMailProvider ve FakePublishProvider; **adapter**'lar MailNotificationAdapter
ve PublishNotificationAdapter'dır. NotificationDemoCaller yalnız target'ı bilir;
ikisi de deney kurulumunda açıkça oluşturulur. DI/factory/registry gerekmez.
Bütün örnek `scripts/**/*.cs` üretim derlemesi dışlama kuralına tabidir.

Mesaj kurucusu her alanın null/boş/yalnız boşluk olmasını reddeder; hata ilgili
parametreyi taşır. E-posta regex'i veya kanal doğrulaması yoktur. Baş/son boşluk,
Türkçe karakter ve satır sonları dahil geçerli metin aynen aktarılır. Sonuç türünün
private kurucusu ve get-only alanları başarısız sonuçla referansı birleştirmeyi
engeller. Accepted'ın referansı null olabilir; non-null referans yalnız boşluk
olamaz. Kabul, teslim veya okunma anlamına gelmez.

## Dönüşümler

| Target alanı/sonucu | Mail API | Publish API |
|---|---|---|
| Recipient | recipient | Address |
| Subject | subject | Heading |
| Body | body | Content |
| CancellationToken | Aynı token | Aynı token |
| Accepted | Dönen zorunlu referans | Accepted=true, ErrorCode=null; isteğe bağlı Reference |
| RecipientRejected | FakeRecipientRejectedException | INVALID_ADDRESS |
| ProviderUnavailable | FakeProviderUnavailableException | SERVICE_UNAVAILABLE |

Mail adapter **await** içinde yalnız iki bilinen exception türünü yakalar.
Task'ı await etmeden try'dan döndürmek, Task daha sonra fault olduğunda catch'in
çalışmamasına neden olurdu. Diğer exception'lar aynı nesne olarak ilerler;
OperationCanceledException kesinti/alıcı reddi değildir. Retry/fallback yoktur.

Publish adapter await edilen cevabı önce tutarlılık açısından değerlendirir:
accepted+hata kodu, başarısız+hata kodu yok, başarısız+herhangi non-null referans,
null cevap veya boşluk referans bozuk protokoldür. Tanınmayan kod da
InvalidOperationException üretir; kodun ham içeriği hata metnine eklenmez.
Başarıda null referans desteklenir, uydurulmaz; diğer geçerli referanslar kayıpsızdır.
Mail'in zorunlu referansı null/boş gelirse yine protokol hatasıdır.

## Davranış kontrolleri

`scripts/AdapterChecks.cs` ortak harness ile iki adapter'a aynı kabul/ret/kesinti,
alan/token, tek çağrı, null mesaj, beklenmeyen exception ve cancellation
beklentilerini uygular. Fake delegate davranışları alıcı adresindeki sihirli
kelimelere dayanmaz. TaskCompletionSource henüz tamamlanmamışken çağrıyı gözler;
sonra başarılı/exception/cancel tamamlar. Sabit gecikme veya süre beklentisi yoktur.
Mail'in bilinen exception'ları da çağrı döndükten sonra Task tamamlanırken üretilir.
Publish'in bozuk kombinasyonları ayrı tablolu kontrollerdir. Web assembly'sine
örnek türlerinin dahil edilmediği de kontrol edilir. Bunlar gerçek SDK/dış
sağlayıcı entegrasyon testi veya teslimat testi değildir.

## Decorator'dan fark ve güvenilirlik

17. gün Decorator aynı evaluator sözleşmesini koruyup çevresine süre gözlemi
ekliyordu. Burada farklı alan/hata protokolleri target sözleşmesine **uyarlanır**.
İkisi composition kullanabilir; aynı desen değildir. Adapter hata anlamını
silmemeli, bilinmeyen cevabı başarı saymamalıdır.

DB transaction'ı dış servisin daha önce kabul ettiği gönderimi geri alamaz.
ProviderUnavailable güvenli retry demek değildir; yanıt kaybında istek alınmış
olabilir. Bugün bu belirsizlikler çözülmez; dış gönderim zaten yoktur. Kayıt
onayına bağlamak, DB/dış servis atomikliği ve güvenilirlik kararlarını erken
ürüne taşırdı. Bu nedenle üretim endpoint'leri, entity'ler ve migration değişmedi.
