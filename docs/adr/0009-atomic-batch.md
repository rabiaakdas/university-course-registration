# 0009 — Tek SaveChanges ile toplu kayıt

Durum: Kabul edildi (9. gün).

- **İhtiyaç:** Öğrencinin birden çok seçimini tam başarı/tam başarısızlık sınırında
  kaydetmek. Tekli endpoint'i tekrarlamak önceki başarılı seçimleri geride bırakır.
- **Seçim:** Ayrı batch endpoint, küçük istek/items zarfı, toplu varlık/tekrar
  sorguları, AddRange ve tek SaveChanges. Mevcut yanıt ve dar DB hata eşlemesi
  kullanılır. Kazanç tek atomik yazma sınırı; maliyet bütün seçimlerin doğrulanması
  ve bir çakışmada bütün batch'in reddidir. 50 öğe teknik sınırdır.
- **Alternatif:** Kısmi başarı her öğeye ayrı sonuç gerektirir ve istenen sözleşmeyi
  karşılamaz. Açık transaction birden fazla save gerekiyorsa uygundur; burada
  gereksiz olduğundan yalnız öğrenme deneyinde kullanılır.
- **Sınır:** Ön kontrol yarış garantisi değildir. Save unique/FK hatasında yalnız
  bu çağrının yeni kayıtları geri alınır; rakip commit korunur. Öğrenci/açılış
  entity'leri yeniden eklenmez. Kontenjan, akademik kurallar ve yetkilendirme yok.
- **Maliyet:** Transaction/sequence boşlukları, hata sonrası context'in hâlâ
  değişiklik tutması, commit-sonrası yanıt kaybı ve ayrı retry/idempotency ihtiyacı.
  Bugün bunlar için genel framework veya yeni altyapı kurulmadı.
- **Kanıt:** Mevcut tek/bağımsız SaveChanges deneylerine açık rollback/commit
  eklenir. Gerçek batch handler'ında test-only interceptor ve komut bölme ile
  başarılı ilk INSERT sonrası unique hatası gözlenir. Production'a test anahtarı
  veya BeginTransaction eklenmez. Şema değişmediği için migration yoktur.
