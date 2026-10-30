# Gün 20 — Dar kapsamlı test Builder'ı

## Neden ve nerede?

`scripts/EligibilityChecks.cs` içindeki birleşik ret fixture'ı iç içe
OfferingEligibility, OfferingSchedule, PrerequisiteInput ve ayrı dönem-kredi
sözlüğü kuruyordu. Hangi kimliğin yeni, hangisinin mevcut olduğu ve kredi yükünün
nereden geldiği bu yapılar arasında izleniyordu. `scripts/EligibilityScenarioBuilder.cs`
aynı üretim girdilerini anlamlı hazırlık adımlarıyla oluşturur; yeni akademik
model veya expected-failure hesaplaması değildir.

Önce (asıl testin kısaltılmış hazırlığı):

```csharp
var requested = new[] { new OfferingEligibility(
    new OfferingSchedule(20, 2, slots), 7, new PrerequisiteInput([1], [])) };
var existing = new[] { new OfferingSchedule(99, 2, slots) };
var currentCredits = new Dictionary<int, long> { [2] = 24 };
```

Sonra:

```csharp
var scenario = new EligibilityScenarioBuilder(creditLimit: 30)
    .Request(offeringId: 20, termId: 2, credits: 7)
    .WithSlots(20, slots).Requires(20, [1])
    .Existing(offeringId: 99, termId: 2, credits: 24, status: EnrollmentStatus.Approved)
    .WithSlots(99, slots)
    .Build();
```

Artık mevcut yükün kaynağı, dönem, kredi ve durum aynı adımda görünür. Satır sayısı
mutlaka azalmaz; kazanç niyet ve ilişkilerin görünmesidir. Maliyet yeni hazırlık
API'sinin öğrenilmesi ve kopyalama/validasyon testleridir. Kapsam iki test dosyasındaki
seçili saf fixture'larla sınırlı tutuldu. Basit CreditLoad, tek WeeklyTimeSlot ve
DB fixture object initializer'ları korunur; bütün testler builder'a taşınmaz.
Birleşik test zaten yeni–mevcut, yeni–yeni, üç ret türü ve farklı dönem dışlamasını
aynı açık assertion'da kontrol ediyordu; dört kopya test üretilmedi.

## Alternatifler

| Araç | Burada uygun kullanım | Sınır/maliyet |
|---|---|---|
| Constructor | WeeklyTimeSlot ve CreditLoad gibi küçük kontrollü değer | Çok sayıda iç içe koleksiyonda bağlamı takip zorlaşabilir |
| Object initializer | Basit DB fixture entity'leri, mevcut kontrollü durum metotlarıyla | Referans paylaşımı ve yapısal tutarlılık kendiliğinden çözülmez |
| Builder | Birkaç yeni/mevcut açılışın saat, ön koşul ve dönem yükünü hazırlamak | Ek mutable hazırlık nesnesi/API ve sözleşme testleri |

Fluent metotların `this` dönmesi tek başına Builder değildir; burada hazırlık
biriktirilir ve Build bağımsız sonuç üretir. Director eklenmedi: tekrarlanan ayrı
bir üretim reçetesi yönetme ihtiyacı yok. Kimlikler bellek içi açık sayılardır;
DB sequence veya satır kimliği garantisi değildir.

## Kopyalama ve yapısal koşullar

Builder tek öğrencinin zaten daraltılmış bağlamıdır. StudentId saf evaluator'ın
parametresi değildir; eklenmedi. Her açılışın dönemi/kredisi ve mevcutse durumu
açıktır. Boş koleksiyonlar dışında gizli varsayılan yok; limit zorunludur.
Draft/Rejected tanımlanabilir, durumu değiştirilmez fakat aktif yük girdisinde
yer almaz. Farklı dönemler ayrı hazırlanır; veri karar algoritması yerine geçmez.

ToArray yalnız dış koleksiyonu kopyalar; mutable elemanlar olsaydı yeterli olmazdı.
Burada WeeklyTimeSlot immutable, OfferingSchedule nested slot listesini kendisi
kopyalayıp sarmalıyor, PrerequisiteInput FrozenSet oluşturuyor. Build her seferinde
bu production nesnelerini yeniden kurar; senaryo dış listeleri ayrıca kopyalar ve
salt okunur sarmalar. Kredi sözlüğü de kopya + ReadOnlyDictionary kullanır.
OfferingEligibility record'un `with` ifadesi yeni değer üretir; mevcut read-only
listeye geri atanamaz. Genel JSON/reflection deep clone altyapısı yoktur.

Pozitif kimlikler/krediler, geçerli status/gün/saat ve tanımlanmış hedef gerekir.
Çelişkili veya birebir tekrar açılış tanımı hata; birebir saat tekrarı hata.
Farklı örtüşen aralıklar, eksik ön koşullar ve limit üstü kredi korunur.
Bu **yapısal geçerlilik**, akademik uygunluk değildir. Build evaluator çağırmaz;
ret türlerini/kimliklerini test açıkça yazar, gerçek AcademicEligibility değerlendirir.

`scripts/BuilderChecks.cs` iki Build, dış koleksiyonun önce/sonra mutasyonu,
builder'a sonraki eklemeler, nested slot/küme/dictionary bağımsızlığı, ayrı builder,
istek sırası, geçersiz yapı ve web assembly dışlamasını doğrular. Build/no-reset
sözleşmesi rastgele kimlik veya zamana bağlı değildir. Thread-safety, DB yükleme,
SaveChanges/temizlik ve ürün entity geçişlerini değiştirme kapsam dışıdır.
