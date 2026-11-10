# ADR 0038 — Course PUT için SQL Server rowversion

Durum: Kabul edildi.

1. İhtiyaç: Aynı dersin eski detayını düzenleyen ikinci istemcinin ilk yazarı sessizce
   ezmesini önlemek; açık bir409 ve güncel sürümle bilinçli yeniden yazma sağlamak.
2. Önceki sınır: PUT dersin o anki entity'sini yükleyip istemci sürümü olmadan
   SaveChanges yapıyordu; başarılı yazmaların sırası lost update'i önlemiyordu.
3. Seçim: DB üretimli rowversion, IsRowVersion ve istemci sürümünü OriginalValue
   olarak kullanmak. Detay/PUT DTO'ları sürüm taşır, listeler değişmez. Sadece
   DbUpdateConcurrencyException yeni409 koduna çevrilir. Aynı içerik de koşullu
   UPDATE yaparak sürümü kontrol eder. Yetki ve iş kuralları korunur.
4. Maliyet: SQL Server'a özgü sütun/migration, PUT istemci sözleşmesi ve fixture
   değişiklikleri; kullanıcı çakışmayı çözmek zorundadır. Aynı içerikte bile yazma
   yapılır. Bütün veri erişim yollarına kendiliğinden concurrency koruması yayılmaz.

Alternatifler: Son yazan kazanır veri kaybettirir. Uzun kullanıcı düzenlemesi boyunca
pessimistic kilit tutmak uygun değildir. ETag/If-Match ayrı HTTP sözleşmesi gerektirir;
bu gün DTO içi sürüm yeterlidir. Genel retry/merge framework'ü eklenmedi; kör retry
başka kullanıcının değişikliğini onaysız ezebilir. Rowversion yetki kanıtı değildir.
