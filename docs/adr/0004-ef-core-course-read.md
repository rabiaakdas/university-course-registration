# 0004 — Scoped EF Core ile ayrı uygulama DB'sinden ders okuma

Durum: Kabul edildi (4. gün).

- **İhtiyaç/sınır:** Bellek listesi kalıcı veri kaynağı değildir. Ders okumasını
  model mapping ve migration ile gerçek SQL Server'a taşımak gerekiyor.
- **Seçim/alternatif:** EF Core SQL Server 10.0.14 seçildi. Doğrudan SQL/ADO.NET
  de mümkün; bugün LINQ sorgusu, context yaşam süresi ve migration öğreniliyor.
- **Yaşam süresi:** AddDbContext scoped. Request başına context, sıralı await;
  context aynı anda paylaşılmaz, singleton veya arka plan alanında tutulmaz.
- **DB sınırı:** `university_registration_lab` korunur. İlk migration öncesi
  bulunmadığı doğrulanan `university_registration_app` migration ile oluşturuldu.
  EnsureCreated/EnsureDeleted veya başlangıçta migration yoktur.
- **Kimlik/benzersizlik:** ValueGeneratedNever ve açık seed kimlikleri. Code ve
  açılış birleşimi unique index; alternate key gerekmiyor. Lab UQ nesnesinden farklı
  olsa da benzersizlik davranışı aynı; ortak deneyde 2601/2627 farkı açık tanımlı.
  FK'ler NoAction; nav property veya model genişletmesi gerekmedi.
- **Seed:** Ayrı SQL, tek transaction, eksik kayıt ekleme; mevcut değer farklıysa
  açık hata. Sessiz update/overwrite veya otomatik başlangıç seed'i yok.
- **Kazanç/maliyet:** Kalıcı ders okuması, açık mapping ve sürümlü şema elde edildi.
  Bağlantı, migration bakımı ve DB gerektiren doğrulama maliyeti var. Diğer iki
  endpoint geçici olarak bellekten çalışır; aynı ders adı kaynaklar arasında
  farklılaşabilir. Bu sınır README'de açık; sonraki altyapı bugün eklenmedi.
