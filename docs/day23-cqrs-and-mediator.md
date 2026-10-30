# Gün 23 — CQRS ve Mediator

CQRS okuma ve yazma niyetlerinin sorumluluklarını ayırır. Bu uygulamada aynı süreç,
aynı SQL Server DB ve aynı scoped DbContext altyapısı kullanılır. Ayrı okuma DB'si,
event sourcing veya bir mesajlaşma kütüphanesi gerekmez. Yalnız iki ilgili akış ele
alındı; bütün endpoint'ler Command/Handler çiftlerine dönüştürülmedi.

## Üretim sınırları

- `Contracts/CreateEnrollmentRequest` HTTP'den gelen, hatalı olabilecek veridir.
  `Application/CreateEnrollmentCommand` yalnız pozitif StudentId/CourseOfferingId
  taşıyan get-only uygulama isteğidir. Constructor ortak Validate koşullarını korur.
  `Endpoints/RegistrationInput.Enrollment` aynı Validate fonksiyonunu kullanır:
  bağımsız alan hataları birlikte HTTP400 olur. Doğrudan yanlış komut oluşturmak
  programlama hatasıdır: ilgili parametre için ArgumentOutOfRangeException.
- `EnrollmentOperations.CreateAsync` komutu kabul eder; aynı öğrenci/açılış/
  mükerrer/akademik kontrolleri ve tek SaveChanges ile aynı uygulama sonucunu döndürür.
  Komutun SELECT yapması ve kimlik/durum/ret sonucu döndürmesi normaldir. Yeni bir
  handler aktarım zinciri veya ikinci koordinasyon kopyası yoktur. Null komut
  ArgumentNullException'dır; HTTP kullanıcı hatası bu exception yoluna bırakılmaz.
- `CourseCatalogQuery` Search.Trim, Credits/Page/PageSize ve güvenli Offset'i taşır.
  HTTP ve constructor aynı Validate fonksiyonundan yararlanır. Defaults1/20,
  page>=1, pageSize1..100, credits>0 ve Int32 offset sınırı aynıdır. HTTP400 alan
  hatası ile doğrudan çağrının ArgumentOutOfRangeException davranışı ayrıdır.
  Sayı binding hatalarını ASP.NET Core400 olarak ele almaya devam eder.
- `CourseCatalogReader.ReadAsync` SQL filtrelerini, sıralı CountAsync ardından
  OrderBy/Skip/Take/DTO Select/ToListAsync'yi yürütür. Endpoint sadece parametre,
  alan hata eşlemesi ve Results.Ok ile ilgilenir. Reader null query'yi sorgudan
  önce reddeder; get-only query normal kullanımda sonradan bozulamaz.

Katalog iş verisi yazmaz; entity döndürmez/takip etmez. Arama collation ve literal
Contains davranışı değişmez. Count ve items iki ayrı sorgudur; eşzamanlı değişimde
tek anlık snapshot garantisi yoktur. Query adı tek başına yazmama kanıtı değildir:
scripts/CatalogChecks gerçek yürütülen SQL'i ve yeni context'te fixture değerlerinin
aynı kaldığını kontrol eder. Beklenen SQL/projection testleri kopyalanmadı; mevcut
test doğrudan yeni reader'ı çağırır, HTTP kontrolleri ayrıca endpoint'i doğrular.

## Mediator ayrı bir karardır

Üretim endpoint'leri işlemi doğrudan çağırır. Reader'ı DI ile almak Mediator değildir.
Mediator isteği işleyiciye yönlendirir; CQRS ise sorumluluk ayrımıdır. Birini kullanmak
diğerini zorunlu kılmaz. Dolaylı yönlendirme ek akış takibi, kayıt ve hata ayıklama
maliyeti getirdiğinden bugünkü üretime eklenmedi.

`scripts/MediatorExperiment.cs`: DemoRegister/Receipt ve DemoRead/Summary; her
isteğe bir somut handler, aracıda iki açık typed SendAsync overload'u. Komutun
örnek davranışı yalnız bellekteki listeyi değiştirir, sorgu onu okur. Davranışlar
CqrsChecks deney kurulumunda delegate ile sağlanır. Doğrudan çağrı karşılığı aynı
handler'dır. Sonuç ve Task aynen iletilir; catch/retry/queue/Task.Run yoktur.
Her istek türünün tek constructor slot'u vardır; duplicate registration mümkün
değildir. Eksik slot, ilgili handler adıyla açık InvalidOperationException üretir.
Reflection tarama veya generic dispatcher yok; üretim assembly/DI bu türleri içermez.

`scripts/CqrsChecks.cs`: iki yönlendirme, yanlış handler'ın çalışmaması, bir çağrı,
aynı token ve sonuç, doğrudan eşdeğerlik; TCS ile henüz bitmemiş iş, aynı exception
nesnesi ve cancelled Task. Keyfi süre bekleme yoktur. Bu deney DB/SQL kanıtı değildir.

Observer'da gerçekleşmiş onay olayı sıfır/çok sayıda bağımsız gözlemciye dağıtılır;
burada istek tek handler'a gidip tek cevap alır. CQRS/Mediator kalıcı teslim veya
exactly-once sağlamaz. Batch hâlâ tek SaveChanges; onay hâlâ bağımsız commit sonrası
yayın; dış transaction guard'ı ve observer hata/iptal politikası korunur. Timing
decorator yalnız hazırlanmış akademik hesaplamayı ölçmeye devam eder. Yeni pipeline,
otomatik transaction/retry, repository, genel Result veya MediatR paketi eklenmedi.
