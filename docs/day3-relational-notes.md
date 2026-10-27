# Gün 3 — İlişkisel şema öğrenme notu

- **PK:** Satırı tanımlar; benzersiz ve NULL olmayan kimlik sağlar. Otomatik
  numara üretmekle aynı değildir. Bugün Id değerlerini SQL'de açıkça veriyoruz;
  IDENTITY/EF Core değer üretimi 4. gün değerlendirilecek.
- **FK ve bire çok:** Bir Course veya AcademicTerm birden fazla CourseOffering
  tarafından kullanılabilir. FK var olan üst kayda bağlantıyı korur; CourseId'nin
  açılışlarda tekrarını engellemez. Aynı dersin üç açılışı bunun örneğidir.
- **Birleşik UNIQUE:** CourseId, AcademicTermId ve SectionCode birleşimini korur;
  tek başına bu sütunların her birini benzersiz yapmaz. Aynı dönemde A/B ve farklı
  dönemde tekrar A mümkündür.
- **NOT NULL / CHECK:** İlki eksik değeri, ikincisi değer koşulunu kontrol eder.
  CHECK (Capacity > 0) tek başına NULL'u reddetmez (UNKNOWN); ayrıca NOT NULL
  gerekir. DEFAULT eksik sütuna değer sağlar, doğrulama yerine geçmez. Bu nedenle
  kapasite için DEFAULT 40 yoktur; kapasite bilinçli verilmelidir.
- **Boş metin:** `N''` NULL değildir; NOT NULL boş adı engellemez. Bugün ek boşluk
  veya boş metin doğrulaması eklenmedi. Örnek Code/SectionCode büyük harfli ve
  baş/son boşluksuzdur; DB otomatik normalizasyon yapmaz.
- **Veri türleri:** Id/kredi/kapasite int; takvim tarihleri date; Türkçe metinler
  uzunluğu sınırlandırılmış nvarchar ve N önekli sabitlerdir. Karşılaştırma mevcut
  `SQL_Latin1_General_CP1_CI_AS` kurallarına uyar: CSE201/cse201 aynı kabul edilir.
- **Normalizasyon:** Ders/dönem adları açılışta tekrarlanmaz. Tek ders adı güncellemesi
  bütün JOIN satırlarında görünür (güncelleme anomalisi azalır). Henüz açılışı yokken
  ders eklenebilir (ekleme anomalisi azalır). Son açılışın kaldırılması dersin
  tanımını kaybettirmez (silme anomalisi azalır). Üst kayıt silme girişimleri ise
  bağlı açılış varsa NO ACTION ile engellenir; gizli cascade yoktur.
- **Maliyet:** Okumada JOIN, yazmada önce üst kayıtlar, ilişki bütünlüğü kontrolü
  ve şema bakımı gerekir. Bunlar bugün API'ye taşınmadı.

## Deney yöntemi

`sql/03-verify.sql` her deneyi ayrı transaction içinde çalıştırır. TRY/CATCH hata
numarasını ve kısıt/sütun adını yakalar; başka hata veya beklenen hatanın hiç
gelmemesi başarısızlıktır. XACT_STATE kontrolüyle tüm deney değişiklikleri rollback
yapılır; hata durumundaki transaction sonraki deneye taşınmaz. Engellenen silmeler
sonrası kayıtlar rollback'ten önce de kontrol edilir. Son durumda üç tablonun
önce/sonra içerikleri çift yönlü EXCEPT ile karşılaştırılır. Temel Türkçe metinler
ayrıca binary collation ile tam eşleşme açısından kontrol edilir.

Gerçek LocalDB çalıştırmaları: iki kez başarılı, 23 numaralı kontrol ve son içerik
koruma kontrolü geçti. JOIN üç beklenen açılışı gösterdi. API regresyonu da geçti;
SQL laboratuvarı API'nin veri kaynağı değildir.
