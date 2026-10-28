# 13. gün — Factory alternatifleri ve DI

## Somut üretim kararı

`Endpoints/PriorityPreviewEndpoint.cs:Preview` tek oluşturma/seçim noktasıdır.
Normalize ad switch'e girer; null sonuç policy alan hatasına dönüşür. Ayrı bir
validation destek listesi yoktur. Hata mesajında iki adın yazması kullanıcıya
rehberlik eder; bağımsız çalışan ikinci seçim algoritması değildir. Testlerin
beklenen politika adlarını belirtmesi de ikinci üretim seçimi değildir.

A: Switch'i korumak tek kullanım için kolay izlenir; normalizasyon, seçim ve
HTTP eşlemesi yaklaşık birkaç satırda görünür. B: Simple Factory'ye taşımak
endpoint'teki switch'i bir çağrıya indirir, fakat ikinci tüketici/karmaşık oluşturma
olmadığından yeni tür ve gezinme maliyetine karşı henüz somut bakım kazancı yok.
A seçildi. Üretim kodu değişmedi; B çalıştırılabilir deney olarak gösterildi.

## Yaklaşımların karşılaştırması

| Yaklaşım | Karar nerede? | Bugünkü somut karşılık | Bedel / sınır |
|---|---|---|---|
| Simple Factory | Tek fonksiyondaki açık seçim | Deneyde SimplePriorityFactory.TryCreate | Seçim kaybolmaz, factory içine taşınır; yeni politika switch bakımı gerektirir |
| Factory Method | Ortak akışın çağırdığı override noktası | PreviewCreator.Preview → protected abstract CreateStrategy | Somut creator sınıfları gerekir; üretimde bu kadar kısa akış için kalıtım gereksiz |
| Abstract Factory | Birlikte kullanılacak ürün ailesi | IPriorityFamilyFactory: strategy + explanation | Uyumlu aileyi kolay sağlar, ürün sözleşmelerini büyütür |
| DI | Nesne grafiğini kuran kapsayıcı/kurulum kodu | Mevcut scoped EnrollmentOperations ve DbContext | Sağlama ve yaşam süresi yönetir; politika kararını kendiliğinden vermez |

Simple Factory klasik GoF Factory Method ile aynı desen değildir. Bir metodun
adının Create olması onu Factory Method yapmaz: burada ortak Preview akışı
oluşturmayı alt sınıfın override ettiği metoda bırakır. Abstract Factory ise iki
ayrı ürün sözleşmesini bir aile olarak sağlar. Sadece aynı stratejiyi farklı
metotla döndürmek ürün ailesi örneği olmazdı.

## Çalıştırılabilir deneyler

`scripts/FactoryExperiments.cs` içindeki türler yalnız konsol deney projesinde
explicit Compile Include ile derlenir. Web projesinin mevcut
`Compile Remove="scripts/**/*.cs"` kuralı korunur. Üretim DI/endpoint'lerinde
bu türlere referans yoktur; web assembly'sinde deney türünün bulunmadığı da
kontrol edilir. Bu uygulamanın üretimde Abstract Factory kullandığı söylenmez.

- **Simple Factory:** Normalize edilmiş ad alır; normalizasyon yapmaz. Null,
  bilinmeyen veya normalize edilmemiş ad false/null döndürür. NotNullWhen(true)
  başarılı out değerini nullable analize bildirir. Her başarıda yeni, durumsuz,
  kaynak tutmayan strateji sağlanır. Formüller gerçek Priority stratejilerindedir.
  Deney kendi ayrı validation destek listesi taşımaz. Üretim normalizasyonunun tek
  sahibi endpoint olmaya devam eder.
- **Factory Method:** İki somut creator aynı 225/240 girdisinde ortak Preview
  akışından 100 ve 225 döndürür. Override yalnız oluşturmadır; formül kopyalanmaz.
- **Abstract Factory:** Graduation ailesi 100 ve “Mezuniyet için kalan kredi: 15”;
  completed ailesi 225 ve “Tamamlanan kredi: 225” üretir. Açıklayıcılar ilgili
  veriyi anlatır, 20/100 eşiklerini kopyalamaz. Kredi aşımında kalan0 açıklanır.
  API'ye explanation alanı eklenmez; küçük metin beklentisi yalnız deneye aittir.

Abstract Factory'ye **yeni aile** eklemek mevcut iki ürün sözleşmesini uygulayan
ürünler ve yeni factory gerektirir; tüketicinin sözleşmesi aynı kalabilir.
**Yeni ürün türü** eklemek factory sözleşmesini ve bütün somut aileleri değiştirir;
testler ve bu ürünü kullanan tüketiciler de güncellenir. Bu iki genişleme maliyeti
aynı değildir.

## Sahiplik ve gereksiz altyapı

Bugünkü stratejiler bağımlılıksız/durumsuzdur; DI, IServiceProvider dağıtımı,
registry/resolver, keyed DI veya generic factory eklenmedi. Factory eklemek
otomatik DIP veya veri erişim teknolojisinden bağımsızlık sağlamaz. Mevcut
uygulama EF/SQL Server bağımlılığını korur.

Kaynak kullanan nesne sağlayan bir factory'de kimin dispose edeceği, nesnenin
paylaşılıp paylaşılmadığı ve lifetime sözleşmesi açık olmalıdır. Container'ın
oluşturduğu nesnelerin sahipliğiyle manuel oluşturmanın sahipliği karıştırılmamalı.
Bu stratejiler IDisposable değildir; varsayımsal ihtiyaç için altyapı kurulmadı.

```powershell
powershell -NoProfile -File scripts/verify-course-writes.ps1 -Mode factories
```

Factories modu DB/API süreci kurmaz. Unit/core/full de deneyleri içerir. Mevcut
preview testi erişilemeyen SQL hedefiyle HTTP sözleşmesini ve DB bağımsızlığını
sınamayı sürdürür. Kredi verilerinin güvenilirliği, yetki, kontenjan, yerleştirme
ve eşit puan sırası hâlâ kapsam dışıdır.
