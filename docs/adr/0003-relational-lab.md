# 0003 — Ayrı SQL Server LocalDB laboratuvarı

Durum: Kabul edildi (3. gün).

- **DB seçimi:** Belgelenmiş önceki seçim yoktu. PostgreSQL 18.6 istemcisi mevcut
  ama localhost:5432 yanıt vermedi. SQL Server LocalDB 17.0.4025.3 Windows hesabıyla
  erişilebildiği için seçildi. Alternatif SQL Server Express servisi de bulundu;
  kullanıcıya ait LocalDB yeterliydi. Yeni sunucu/Docker kurulmadı.
- **İhtiyaç/kazanç:** Aynı dersin farklı dönem/şubelerini üç tabloda kimliklerle
  ilişkilendirmek, adları tekrar etmeden güncelleme/ekleme/silme anomalilerini azaltır.
- **Silme/güncelleme:** FK'lerde açık NO ACTION; bağlı açılış varken üst ders/dönem
  silinmez, kimlik değişikliği cascade edilmez.
- **Benzersizlik:** Ders kodu UNIQUE; açılışta (CourseId, AcademicTermId, SectionCode)
  birleşik UNIQUE. Mevcut CI_AS collation korunur; ayrı harf/boşluk normalizasyonu yok.
- **Kimlikler:** Laboratuvar seed'i açık int kimlikler verir. PK ile otomatik numara
  üretimi farklı kararlardır; EF Core aşamasında değer üretimi tekrar değerlendirilecek.
- **Güvenli alan:** Yeni `university_registration_lab` DB'si ve proje işareti.
  Mevcut ada yazılmaz; şema/seed tekrarı sessizce atlanmaz. Deneyler rollback yapar.
- **Maliyetler:** JOIN, ilişki kontrolü, üst kayıtları önce yazma ve şema bakımı.
  API bellekte kalır; provider/DbContext/migration bugün eklenmez.
