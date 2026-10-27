# 0008 — Student ve açık Enrollment ilişkisi

Durum: Kabul edildi (8. gün).

- **İhtiyaç / eski çözümün sınırı:** Katalog ve açılışlar vardı; öğrenciyi ve
  öğrencinin hangi açılışa bağlandığını temsil eden veri yoktu. Student iş profili,
  Enrollment açık ilişki entity'si seçildi. Kazanç çoktan çoğa ilişki ve ilişkiye
  ait kayıt zamanı; maliyet iki tablo, FK/yazma sırası ve JOIN okumalarıdır.
- **Anahtar alternatifi:** İki FK'den birleşik PK yerine ayrı Id + birleşik UNIQUE.
  Kazanç tek kimlikle detay ve sıralama; maliyet ek PK/unique index/sequence.
  İki FK zorunlu, isimler ilişki tablosuna kopyalanmaz. Navigation gereksizdi.
- **Silme:** NO ACTION, bağlı öğrenci/açılışın silinerek kayıt bilgisinin dolaylı
  kaybolmasını önler. Cascade alternatifi reddedildi. Maliyet ileride açık bir
  silme/arşivleme kararı ihtiyacıdır; bugün silme endpoint'i yoktur.
- **Kimlik/zaman:** Projeyle tutarlı iki ayrı sequence/default; yeni tablolar
  1'den başlar. Mevcut tablolar ve önceki migration'lar korunur. Zaman istemciden
  değil uygulama sunucusunda DateTimeOffset.UtcNow ile üretilir. Maliyet sunucu
  saatinin doğruluğuna bağımlılık; sequence boşlukları normaldir.
- **Doğrulama:** Alanlar ve varlık ön kontrolü kullanıcıya anlaşılır hata verir;
  son koruma unique/FK'dir. İlgili gerçek save hataları 409/alan 400'e eşlenir.
  SQL mesajında nesne adı bağımlılığı belgelenir; diğer hatalar merkezi 500'dür.
- **Sınır:** Temel ilişki ve mükerrer çift koruması; kontenjan, akademik kurallar,
  farklı şubeye kayıt engeli ve yetkilendirme yoktur. Student hesap değildir.
  Eşzamanlı tekrar koruması genel idempotency veya kontenjan garantisi değildir.
