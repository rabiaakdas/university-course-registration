# Gün 7 — LINQ, SQL, DTO ve izleme

LINQ her zaman SQL değildir. EF DbSet'ten gelen IQueryable ifade ağacını sağlar;
provider çevrilebilen işlemleri SQL'e dönüştürür. IEnumerable üzerinden yapılan
işlemler C# tarafında çalışabilir. AsEnumerable tek başına bütün veriyi yüklemez;
sonraki LINQ işlemlerini IEnumerable tarafına geçirir. Bellekteki listeye
AsQueryable eklemek bir DB provider'ı veya SQL sorgusu oluşturmaz.

`CourseCatalogEndpoint.GetAsync` filtreleri ToListAsync öncesinde ekler. CountAsync
ilk SQL'i, ToListAsync ikinci SQL'i çalıştırır. Bundan sonra DTO listesine uygulanacak
filtre/sıralama bellekte olurdu; bu katalogda böyle yapılmaz. Contains, StringComparison
overload'u veya özel C# yardımcı metoduna dönüştürülmeden provider'a bırakılır.
Girdi değerleri normal LINQ parametreleridir.

Projection **hangi alanların seçildiğini**, DTO **yanıtın şeklini**, AsNoTracking
ise entity sonuçlarının **izlenip izlenmeyeceğini** belirler. Saf CourseListItemDto
projection entity üretmez; ek AsNoTracking zorunlu değildir ve kanıtlanmamış ek
performans kazancı iddia edilmez. Entity içeren anonim projection ise varsayılan
olarak entity'yi izler. Ayrı temiz context'lerde gözlenen sonuçlar:

| Sorgu | Course entry |
|---|---|
| Normal entity | 1 |
| AsNoTracking entity | 0 |
| Saf CourseListItemDto | 0 |
| new { Entity = course, course.Code } | 1 |

AsNoTracking sonucu değiştirildiğinde SaveChanges tek başına DB'yi değiştirmedi;
ayrı context eski adı okudu. Bu kontrol yalnızca testin dersinde yapıldı. PUT'un
izlenen entity sorgusuna veya genel tracking ayarına dokunulmadı.

Id'ye göre benzersiz artan sıralama sabit veride sayfa sınırlarını belirler. Offset
atlanan satırları işleme maliyeti taşıyabilir. Veri değişirken sayfalar kayabilir;
ayrı COUNT ve items sorguları aynı anı garanti etmez. Bu gün snapshot transaction,
keyset, index veya full-text optimizasyonu yoktur.

Kod: `Endpoints/CourseCatalogEndpoint.cs` sorgu/validasyon; `Contracts/CourseCatalogResponse.cs`
DTO'lar; `scripts/CatalogChecks.cs` HTTP, gerçek tamamlanan SQL komutları ve dar
tracking karşılaştırması. Önceki SaveChanges deneyi kopyalanmadı.

Resmî kaynaklar: [tracking/projection](https://learn.microsoft.com/en-us/ef/core/querying/tracking),
[SQL Server çevirileri](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/functions),
[pagination](https://learn.microsoft.com/en-us/ef/core/querying/pagination).
