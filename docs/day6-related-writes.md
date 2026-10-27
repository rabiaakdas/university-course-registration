# Gün 6 — İlişkili yazma, izleme ve SaveChanges

`new` yalnızca C# nesnesi oluşturur: **Detached**. `Add` onu **Added** yapar;
henüz başka context'in okuyabileceği DB kaydı yoktur. Başarılı `SaveChangesAsync`
INSERT'i yürütür, üretilen Id gelir ve nesne **Unchanged** olur.

Normal entity sorgusu nesneyi Unchanged izler. Özelliği değiştirip açık
`DetectChanges` çağırmak **Modified** durumunu gösterir. SaveChanges öncesinde
DB eski değeri, sonrasında yeni değeri taşır. **Deleted**, Remove ile silinmek
üzere işaretlenen durumdur; henüz SQL DELETE değildir. Deneyde bu durum gösterilir,
silme kaydedilmez; kontrollü test temizliği ayrıca yapılır. API'ye DELETE eklenmedi.

SaveChanges yalnızca son Add edilen nesneyi değil, bağlamın bekleyen değişikliklerini
değerlendirir. Gerçek SQL Server deneyinde tek SaveChanges'e eklenen iki dersten
birinin kredi CHECK'i başarısız olduğunda ikisi de DB'de yoktu. İki ayrı SaveChanges
çağrısında ilk kayıt başarılı kaldı, ikinci başarısız kayıt yoktu. Başarısız context
Added kayıtları hâlâ tutuyordu; DB rollback'i tracker'ı temizlemek değildir.
Bağımsız doğrulamalar yeni context ile yapılır, başarısız context dispose edilir;
kör retry yoktur. Sequence'de tüketilen numaraları geri gelmiş saymayız.

`CourseOfferingEndpoints.CreateAsync`, ders/dönemi alan projection'ıyla bulur,
FK kimliklerini yeni açılışa atar. Mevcut üst kayıtlar Add edilmez. Dönem POST'u
ile açılış POST'u ayrı request ve SaveChanges sınırıdır. Açılışın başarısızlığı
dönem oluşturmayı geri almaz. Ön kontrol yarış garantisi sağlamaz; unique/FK
koruması ve dar hata eşlemesi kayıt anındaki aralığı karşılar.

`CourseOfferingEndpoints.Query` iki SQL JOIN ve yalnızca yanıt alanları üretir.
Navigation/Include veya üç tabloyu belleğe indirme yoktur. Temiz context'te bu
projection ve ders alan projection'ından sonra tracker boştur. Entity içeren
bir projection ise farklı izleme davranışı gösterebilir; burada entity seçilmez.

Kod karşılıkları:

- `Endpoints/AcademicTermEndpoints.cs`: Add + SaveChanges + 201/Location.
- `Endpoints/CourseOfferingEndpoints.cs`: FK kontrolleri, SQL JOIN ve açılış yazma.
- `Data/OfferingWriteErrors.cs`: ilgili unique/FK hatalarının sınıflandırılması.
- `Data/UniversityDbContext.cs`: üç ayrı sequence/default, scoped kullanım.
- `scripts/RelatedWriteChecks.cs`, `Tracking`: on adımlı gerçek izleme deneyi;
  `Run`: HTTP, yarış ve bağımsız DB kontrolleri.

Resmî kaynaklar: [EF change tracking](https://learn.microsoft.com/en-us/ef/core/change-tracking/),
[change detection](https://learn.microsoft.com/en-us/ef/core/change-tracking/change-detection),
[SaveChanges transaction sınırı](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
Transaction/isolation ayrıntıları 9. gün genişletilecek.
