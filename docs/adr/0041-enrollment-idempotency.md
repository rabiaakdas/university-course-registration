# ADR 0041 — Tek enrollment create sonucunu kalıcı saklama

## İhtiyaç ve önceki sınır
İstemci ilk başarılı yanıtı kullanamadan tekrar POST gönderdiğinde mevcut
öğrenci/açılış UNIQUE kısıtı yalnız duplicate409 üretir; ilk201 gövdesini geri
vermez. Başarılı mantıksal isteği, hesabı ve anlamlı içeriği birlikte tanımlamak gerekir.

## Seçim
Cookie/rol/sahiplik/CSRF sonrasında AccountId + sabit işlem + UUID için UNIQUE
rezervasyon, mevcut EnrollmentOperations ve tam ilk JSON/Location sonucu tek açık
DB transaction'ında kesinleşir. İstek hash'i doğrulanmış iki integer alanından
üretilir. Kaybeden UNIQUE transaction'ından sonra yeni context ile replay okunur.
Yalnız başarı saklanır;24 saat sabit süre sonunda mevcut key409 verir ve tutulur.

## Alternatif ve kazanç
Bellek içi cache restart ve çok bağlantı için yeterli değildir. Yalnız enrollment
UNIQUE kısıtı ilk cevabı saklamaz. Genel idempotency middleware bütün işlemlerin
farklı yetki/iş sınırlarını gizler. Dar koordinatör mevcut işi kopyalamadan bu
tek işlemde DB yazmasıyla yanıt kaydını atomik yapar; batch ve submit değişmez.

## Maliyet ve sınır
Ek tablo, indeks, SELECT ve SaveChanges, JSON sözleşmesi sürümleme ve saklama
maliyeti vardır. FK'lar silme/cleanup sırasını etkiler. Expired kayıtları silmek
anahtar yeniden kullanım semantiğini değiştirir; cleanup ürünü eklenmedi.
Saklanan sonuç güncel resource detayı değildir. Güncel erişim her replay'de kontrol
edilir; yetki TOCTOU, dış yan etki atomikliği, ağ teslimi ve genel exactly-once
çözülmez. Otomatik retry/paket/genel framework eklenmedi.
