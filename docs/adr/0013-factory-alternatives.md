# 0013 — Küçük üretim switch'i, ayrı factory deneyleri

1. **İhtiyaç:** İki stratejinin oluşturulması/seçilmesiyle DI, Factory Method ve
   ürün ailesi oluşturmanın farklı rollerini somutlaştırmak.
2. **Mevcut durum:** PriorityPreviewEndpoint.Preview tek tüketici ve tek switch
   içeriyor. Validation ayrı destek listesi tutmuyor, seçilemeyen stratejiyi alan
   hatasına dönüştürüyor. Karmaşık constructor bağımlılığı veya tekrar yok.
3. **Seçim/kazanç:** Üretim switch'i korunur. Simple Factory, Factory Method ve
   Abstract Factory yalnız scripts/FactoryExperiments.cs içinde gerçek stratejileri
   kullanarak çalıştırılır. Factory seçimi merkezi tutabilir ama bugün tek merkezi
   noktayı başka dosyaya taşımak anlamlı sadeleşme sağlamıyor.
4. **Maliyet:** Deney türleri ve davranış kontrollerinin bakımı var; üretim akışına
   tür/DI maliyeti eklenmiyor. İleride ikinci tüketici ya da karmaşık oluşturma
   doğarsa Simple Factory kararı yeniden değerlendirilebilir.

Üretime Simple Factory alma alternatifi endpoint'te daha kısa çağrı sağlardı;
fakat mevcut switch zaten küçük. Factory Method üretim endpoint'ini gereksiz
kalıtım ailesine dönüştürürdü. Abstract Factory'nin ikinci açıklama ürünü gerçek
API ihtiyacı değildir; yalnız deneyde kalır. Web compile dışlama kuralı korunur.

Durumsuz/kaynaksız stratejiler için DI kaydı, factory sahiplik/dispose altyapısı,
IServiceProvider, resolver/registry ve yeni paket yok. HTTP sözleşmesi, hesaplama
formülleri, kayıt transaction sınırları ve DB şeması değişmez.
