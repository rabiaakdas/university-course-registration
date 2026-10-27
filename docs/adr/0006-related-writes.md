# 0006 — Dönem ve açılış yazmaları

Durum: Kabul edildi (6. gün).

- **İhtiyaç / eski sınır:** Dönem ve mevcut ders/döneme bağlı açılış oluşturmak.
  Bellek listeleri yazılan kayıtları ve güncel adları gösteremiyordu. Her iki liste
  ve detay SQL projection'a taşındı; açılış iki JOIN kullanır. Kazanç tek kalıcı veri
  kaynağı; maliyet DB erişimi ve JOIN sorgusunu sürdürmektir.
- **Kimlik:** Açık sabit kimlikler paralel POST için yeterli değildi. Mevcut Course
  yaklaşımıyla ayrı sequence/default seçildi. MAX+1 istek sayacı yarışa açıktır;
  IDENTITY için sütun yeniden kurmak gerekebilir. Sequence mevcut Id/FK'leri korur;
  maliyeti bakımda yazıcıları durdurma, int sınırı ve normal kimlik boşluklarıdır.
  Migration gerçek maksimumları okur, önceki migration'lar yeniden yazılmaz.
- **İlişki:** Entity grafiği olarak mevcut ders/dönemi Add etmek yerine FK değerleri
  atanır. Kazanç yanlış üst kayıt eklenmemesi ve küçük model; maliyet varlık kontrolü
  ve kayıt anında FK yarışlarını ele almaktır. Navigation/interface gerekmedi.
- **Sınır:** Dönem ve açılış ayrı işlemlerdir. Zorla ortak transaction kurulmadı;
  dönem bağımsız var olabilir. Kazanç basit request/SaveChanges akışı; maliyet açılış
  başarısız olduğunda dönemin kalmasıdır. Tek ve ayrı SaveChanges sınırları izole
  DB'de gösterildi; ileri transaction/isolation altyapısı eklenmedi.
- **Hata:** Alanlar 400, olmayan FK başvuruları alan 400, birleşik unique 409,
  bulunamayan detay 404. Son koruma FK/CHECK/unique'dir. Sadece ilgili 2601/547
  ve nesne adları eşlenir; mesaj biçimi bağımlılığı maliyeti açık belgelenir.
  Diğer hatalar mevcut güvenli merkezi 500'e gider; ders kodu eşlemesi korunur.
- **Deney:** Mevcut konsol/HTTP runner genişletildi. Test context interceptor'ları
  deterministik gerçek save hataları üretir. Üretimde test kancası yoktur. Maliyet
  her çalıştırmada ayrı DB; güvenli temizlik yalnızca testin kayıtlarını kaldırır.
