# ADR 0048 — bekleme adayı index'ini şimdilik koruma

İhtiyaç: FIFO aday sayfasında gerçek IO ve satır tamamlama maliyetini görmek.
Mevcut (CourseOfferingId,Status,QueueNumber) index'i filtre ve sıralamayı
karşılıyor; bütün entity alanlarını kapsamaması PK erişimi gerektiriyor.

Alternatifler: mevcut index; aynı anahtara INCLUDE eklemek; yalnız Waiting
filtered index; dar DTO için üretim sorgusunu değiştirmek. Son ikisi bu deneyde
ölçülmedi. İkinci aynı-prefix index eklemek gereksiz tekrar olurdu.

Seçim: üretimde mevcut index kalsın. İzole kapsayıcı varyant logical reads'i
10/66/48/6'dan2'ye indirdi; fakat32 satırlık sınır ve0–3ms gözlemi altında
üretim iş yükü/yazma maliyeti kanıtı olmadan genişletme yapmıyoruz.
Bu seçim IO iyileşmesini reddetmek değil, kalıcı değişikliği ertelemektir.

Maliyet: mevcut lookup IO'su sürer. INCLUDE seçilirse kapanış alanları dahil
leaf genişler, depolama ve yazma bakımı artabilir; bunlar sayısal ölçülmedi.
Gerçek yüksek çağrı sıklığında, okuma/yazma ve alan maliyeti birlikte ölçülüp
karar yeniden ele alınmalıdır. Genel index temizliği veya query altyapısı yok.
