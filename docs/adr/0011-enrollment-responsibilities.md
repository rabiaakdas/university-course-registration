# 0011 — HTTP ile kayıt koordinasyonunun ayrılması

1. **İhtiyaç:** Tekli ve batch kayıtlarında HTTP yanıt kararını DB ön kontrolleri,
   UTC/Enrollment üretimi ve save sınırından ayrı okuyabilmek ve sınayabilmek.
2. **Eski çözüm:** Küçük endpoint'ler çalışıyordu; ancak CreateAsync metotları
   alan biçimi, Any/Contains sorguları, tracking, save hatası ve Problem Details'i
   birlikte taşıyordu. Aynı save sınıflandırması iki yerde tekrarlanıyordu.
3. **Seçim/kazanç:** Scoped somut EnrollmentOperations, iki ayrı akış ve ayrı
   başarı/ret türleri. HTTP biçimi ve teknik 50 öğe sınırı endpoint'te; varlık/
   tekrar ve kaydetme işlemin içinde. Tekli eksik iki başvuru hatası bir arada
   korunur. Batch ön kontroldeki öğrenci yokluğu ile save FK kaybı farklı sonuçtur.
   Ortak SaveAsync dar SQL sınıflandırmasını kullanır. Mevcut DTO alanları korunur.
4. **Maliyet:** İki yeni dosya, sonuç eşleme kodu ve scoped DI kaydı; işlem
   metotlarına doğrudan gelen girdilerin önceden biçim doğrulamasından geçmesi
   gerekir. EF/SQL Server bağımlılığı sürer; teknoloji bağımsızlığı iddiası yok.

**Alternatif:** Endpoint içinde bırakmak daha az dosya gerektirir ve hâlâ mümkün;
ancak gerçek save fault deneylerinde HTTP üretimi ve işlem sonucunu birlikte
incelemeyi zorunlu kılar. Ayrım bu iki değişim nedenini görünür kılar. Sırf dosya
sayısını artıran read servisi/repository veya her metoda interface eklenmedi.
Somut ikinci uygulama/teknoloji gereksinimi yok; generic Result/UoW/Mediator yok.

**Korunan sınırlar:** Tekli Add ve bir SaveChanges; batch AddRange ve bir
SaveChanges. Batch tekli metodu tekrarlamaz. Ek transaction yok, hata sonrası retry
yok. Ön kontroller yarış garantisi değildir. Sorgular sırayla await edilir;
iptal/ilgisiz SQL hataları ret olarak yutulmaz. Şema/migration ve HTTP sözleşmeleri
değişmez. Kayıt önceliği/Strategy sonraki günün konusudur.
