# 0005 — Doğrulanan ders yazmaları ve DB sequence kimliği

Durum: Kabul edildi (5. gün).

- **İhtiyaç:** Ders oluşturmak/düzenlemek; girdi hatası, kod çakışması ve sistem
  hatasını ayırmak. Açık sabit kimlikler eşzamanlı yeni kayıt için yeterli değildir.
- **Kimlik alternatifleri:** MAX+1/süreç sayacı yarışa açıktır; kullanılmaz.
  IDENTITY geçişi mevcut sütunun yeniden kurulmasını gerektirebilir. SQL Server
  sequence + sütun default'u mevcut Id/FK'leri koruyan küçük değişikliktir.
  Migration gerçek maksimumu okuyarak başlar; seed gerekirse ileri hizalar.
  Kazanç DB tarafında eşzamanlı tahsis; maliyet bakım sırasında yazıcıları durdurmak,
  sequence boşlukları ve int sınırıdır. Diğer model kimlikleri değiştirilmez.
- **Doğrulama:** Ortak küçük CourseInput yardımcı; request türleri entity'den ayrı.
  Alan hataları birlikte döner. AnyAsync iş kuralı kontrolü okunabilir hata sağlar;
  rezervasyon değildir. Unique index yarışta son korumadır.
- **Hata sözleşmesi:** 201/Location, 200 detay/güncelleme, 400 ValidationProblem,
  404 ve 409 Problem Details. Sadece SQL 2601 + Courses/UX_Courses_Code tanınır.
  İsim için mesaj biçimi bağımlılığı belgelenir; bilinmeyen hata merkezi 500'dür.
- **Sorumluluk ayrımı:** Program uzamaması için CourseEndpoints, ortak normalizasyon
  için CourseInput, sağlayıcıya özgü hata için CourseWriteErrors ayrıldı. Maliyet
  birkaç küçük dosya; repository/interface/domain altyapısı eklenmedi.
- **Doğrulama güvenliği:** Ayrı GUID adlı işaretlenmiş DB, gerçek migrations/HTTP ve
  yalnızca test context'inde deterministik save yarışı. Kullanıcı/app/lab kayıtlarına
  dokunulmaz; yalnızca testin kayıtları finally temizlenir, test DB silinmez.
- **Sınır:** PUT tam güncellemedir; lost update/concurrency token çözümü yoktur.
  Dönem/açılış endpoint'leri hâlâ bellekte; bugün genişletilmez.
