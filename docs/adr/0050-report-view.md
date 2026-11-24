# ADR 0050 — migration yönetimli rapor view'i

İhtiyaç: sadece rapor okuyacak DB kimliğine temel enrollment tablolarını
vermeden küçük bir doluluk yüzeyi sağlamak. Mevcut LINQ HTTP raporu bu DB
izin sınırının yerini tutmaz.

Alternatif: tablo SELECT yetkisi, yalnız uygulama raporu, view. View seçildi;
şema yaşam döngüsü EF migration Up/Down ile, okuma keyless ToView ile yönetilir.
HTTP raporu ve sözleşmesi değiştirilmedi; yeni endpoint yok.

Kazanç: dar SELECT izni, boş açılışları koruyan COUNT_BIG ve ayrı sayaç alanı.
Maliyet: SQL tanımı ile enum/lifecycle uyumunu migration ve testlerle sürdürme;
view otomatik performans/materialization garantisi vermez. Procedure/function/
trigger yalnız deneyde; iş kuralları DB nesnelerine taşınmadı.

Migration kimliği runtime kimliğinden ayrıdır. Laboratuvar rolleri tam
production runtime provisioning değildir. Backup doğrulaması yalnız VERIFYONLY
ile değil gerçek restore/uygulama okumasıyla yapılır; dış kimlik ve anahtar
bağımlılıkları ayrıca yönetilmelidir.
