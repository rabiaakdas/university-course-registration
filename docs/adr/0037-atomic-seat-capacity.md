# ADR 0037 — Açılış kilidi ve koşullu kayıt geçişleri

Durum: Kabul edildi.

1. İhtiyaç: Son boş yere yarışan submit'lerin kapasiteyi aşmaması; aynı kayda tekrar
   submit/reject veya approve–reject yarışının sayaç ve olayları çoğaltmaması.
2. Önceki sınır: Ayrı kontrol/SaveChanges ve tracked durum değiştirme, okunan bilginin
   yazmaya kadar güncel kalmasını sağlamıyordu. Transaction tek başına yeterli değildir.
3. Seçim: Kontrollü OccupiedSeats + DB aralık CHECK'i; açılış→kayıt kilit sırası,
   UPDLOCK/HOLDLOCK ve koşullu UPDATE'ler tek açık transaction içinde. Satır sayıları
   doğrulanır, başarısız ara adım geri alınır. Onay yayını commit sonrasındadır.
   İlk sayaç guarded migration backfill'inden gelir; aşan eski veri açıkça reddedilir.
4. Maliyet: Tek açılışa yazmalar seri hale gelir, ek kilit/durum sorgusu ve transaction
   yönetimi gerekir. Denormalize sayaç bütün yazma yollarında korunmalıdır. Fixture
   ve SQL şema testleri de güncellenmelidir; genel retry/locking framework eklenmedi.

Alternatifler: Önce COUNT sonra bağımsız INSERT/UPDATE yarışı çözmez. Yalnız rowversion,
kapasite koşulunu ve iki tablonun atomikliğini tek başına ifade etmez; retry tasarımı
ister. Tüm uygulamaya Serializable veya in-process lock eklemek yerine dar SQL Server
koordinasyonu seçildi. Aynı açılış kilidi bütün üç geçişte aynı sıradadır.

Dış transaction kabul edilmez; publisher'ın çağrısı gerçek commit sonrasına karşılık
gelir. Farklı açılış kredi/saat ve ownership/atama yarışları kapsam dışıdır. Olayın
kalıcı teslimi, crash recovery ve genel concurrency çözümü iddia edilmez.
