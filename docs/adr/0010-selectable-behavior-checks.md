# 0010 — Mevcut konsol kontrollerini seçilebilir kılmak

- İhtiyaç: Üç temel kayıt davranışını tek başına ve farklı sırada çalıştırmak;
  saf alan doğrulamasını DB gereksiniminden ayırmak.
- Mevcut sınır: Geniş tek komut birçok entegrasyon davranışını doğruluyor,
  ancak çekirdek senaryolar ortak seed ve önceki adımlar içinde yer alıyordu.
- Seçim: Mevcut geçici konsol projesine unit/core seçimleri; bağımsız fixture,
  yeni context ile korunma assertion'ları ve hata kodu yayılımı. Full mod korunur.
- Alternatif: Standart test framework'ü keşif/IDE raporu sağlayabilir; bu küçük
  kapsam için yeni paket ve ikinci çalıştırma düzeni maliyeti gerekmedi.
- Kazanç: Hızlı DB'siz seçim, gerçek DB çekirdeği ve geniş regresyon ayrı çalışır.
  Maliyet: Küçük assertion/raporlama kodunu korumak; standart runner keşfi/XML
  raporu yok. Önceki deneyler yeni framework'e taşınmaz veya çoğaltılmaz.
