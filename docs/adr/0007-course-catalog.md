# 0007 — Ayrı filtreli ve sayfalı katalog

Durum: Kabul edildi (7. gün).

- **İhtiyaç / önceki sınır:** Mevcut liste bütün dersleri döndürür; katalogda arama,
  kredi filtresi ve sınırlı sayfa gerekir. `/courses` davranışını değiştirmek mevcut
  istemcileri bozabilir. Ayrı `/courses/catalog` seçildi; maliyeti ikinci okuma
  sözleşmesidir. Detay rotasının int kısıtı literal catalog ile çakışmaz.
- **DTO:** Küçük CourseListItemDto eski liste ve katalogda ortak; CourseCatalogResponse
  sayfa bilgilerini taşır. Kazanç açık alan sözleşmesi/credits'in yanlış eklenmemesi;
  maliyet iki küçük tür. Generic pagination altyapısı gerekmedi.
- **Sorgu:** IQueryable üzerinde koşullu Contains ve kredi eşitliği; SQL'de Id sırası,
  Skip/Take ve projection. Bellekte filtrelemek/sayfalamak gereksiz veri yüklerdi.
  Offset doğrudan sayfa numarası sağlar; büyük offset maliyeti ve değişen veride
  kayma kabul edilir. Keyset/full-text/index çalışması bugünün kapsamı dışıdır.
- **Toplam:** Ayrı CountAsync bütün eşleşmeleri sayar. Kazanç toplam bilgisidir;
  maliyet ikinci sorgu ve count/items arasında aynı an garantisinin olmamasıdır.
  Aynı context'te sırayla await; transaction eklenmedi.
- **İzleme:** Saf DTO projection entity içermez. Kataloğa gereksiz AsNoTracking veya
  genel no-tracking ayarı eklenmedi; düzenleme akışı korunur. Entity/DTO farkları
  mevcut izole test runner'ındaki küçük karşılaştırmayla gösterilir.
- **Kanıt:** HTTP kontrolleri ve production handler'ı çağıran test context'inde gerçek
  SQL interceptor'ı. Sadece ToQueryString yeterli sayılmaz; hassas SQL logu tutulmaz.
  Şema ve paket değişikliği olmadığından migration üretilmez.
