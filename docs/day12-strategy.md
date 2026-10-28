# 12. gün — Strategy ile kayıt önceliği önizlemesi

`Priority/IRegistrationPriorityStrategy.cs` aynı geçerli, null olmayan
PriorityInput için deterministik `int Calculate` sözleşmesini tanımlar. İki
somut strateji hesaplamayı uygular; kullanan `PriorityPreviewEndpoint.Preview`
isteği doğrular, stratejiyi seçer ve sonucu HTTP'ye çevirir. DB/dış servis/yazma
bu sözleşmede yoktur. Null iki stratejide de `ArgumentNullException(input)` verir.

`PriorityInput` sealed sınıfının get-only özellikleri ve kurucusu ortak koşulları
korur: completed >= 0, required > 0. Geçersiz parametre için isimli
ArgumentOutOfRangeException oluşur. Init/with yolu yoktur. Completed > required
geçerlidir. HTTP'de nullable sayılar eksik alanı gerçek 0'dan ayırır; kullanıcı
hataları kurucu exception'ına bırakılmadan alan bazlı 400 olarak toplanır.

- Graduation: remaining = Max(0, required - completed); remaining <= 20 için
  100, aksi halde 0. 20/100 gerçek yönetmelik değil, örnek politika seçimidir.
- Completed credits: puan tamamlanan kredidir.

Yüksek puan yalnız aynı politika içinde daha yüksek öncelik demektir. 100
mezuniyet puanı ile 225 tamamlanan kredi puanını doğrudan karşılaştırmak anlamlı
bir ortak ölçek kurmaz. Eşit puan sıralaması, kapasite dağıtımı ve yerleştirme ayrı
kararlardır; bugün uygulanmadılar.

Seçim ile hesap ayrıdır. Policy Trim/ToLowerInvariant sonrasında küçük switch
ile graduation veya completed-credits seçilir; bilinmeyende fallback yoktur.
Switch'in tamamen kaybolması Strategy'nin şartı değildir. Formüller switch'te
tekrarlanmaz. Stratejiler durumsuz/bağımlılıksız olduğu için burada doğrudan
oluşturulur; DI kaydı ve resolver/factory zinciri eklenmez.

Alternatifler: İki kısa formül tek switch içinde daha az türle yazılabilirdi;
Func<PriorityInput,int> ile fonksiyon seçimi de mümkündü. Burada farklı davranışları
aynı isimli sözleşme altında ayrı sınamak için Strategy seçildi. Kazanç ayrı
hesaplama testleri, maliyet birkaç tür ve yeni politika eklenirken seçim bakımının
sürmesidir. Genel plugin/reflection/registry framework'ü gerekli değildir.

Önizleme verilen kredilerin gerçek öğrenciye ait olduğunu doğrulamaz. Student'a
alan eklenmedi, EnrollmentOperations ve tekli/batch akışları değiştirilmedi.
Gerçek kullanım güvenilir transkript verisi, yetkili politika seçimi ve ayrı
kabul/yerleştirme kararları gerektirir. Puan bugün kayıt kabulü veya sırası değildir.

Birim kontrolleri `scripts/PriorityChecks.Unit` içinde DB'siz çalışır. Gerçek HTTP
kontrolleri aynı dosyanın Http metodunda uygulamayı geçerli biçimde ama erişilemeyen
SQL hedefiyle başlatır. Rezerve edilmiş, dinlemeyen loopback portu kullanılır;
LocalDB durdurulmaz. /courses kontrol isteği 500 verirken preview 200 verir.
Bu, önizlemenin erişilebilir DB gerektirmediğinin davranış kanıtıdır. Her bağlantı
teşebbüsünü sayan bir interceptor kurulmadı; sıfır SQL teşebbüsü ölçüldüğü iddia
edilmez. Kod incelemesinde preview/strateji yolunda DB bağımlılığı veya sorgu yoktur.
