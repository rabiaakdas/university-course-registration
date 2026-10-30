# Gün 22 — Repository/UoW ve DbContext

## Somut üretim kararı

Doğrudan EF Core korunuyor. EnrollmentOperations içindeki Offerings ve EvaluateAsync
zaten toplu veri hazırlığını bir araya getiriyor. AdvanceAsync izlenen entity'yi
değiştiriyor; kayıt detayları/katalog DTO projection kullanıyor. Benzer görünen
`Where(Id)` ifadelerinin tracking ve yanıt ihtiyaçları aynı değil. Summary ise
durumların tümünü gösterirken uygunluk yalnız aktif yükü ve hedef dışlamasını kullanır.
Bu davranışları tek genel sorgu veya GetAll üzerinden birleştirmedik.

| Boyut | Doğrudan context | Dar Find/Add alternatifi |
|---|---|---|
| Gizlenen ayrıntı | EF açık | Yalnız entity bulma/Added yapma yazımı |
| Tüketici | Projection ve toplu sorgu açık | İki kısa çağrı; hazırlık/SaveChanges yine çağıranda |
| SQL | Where/Select/SUM/Join korunur | Find entity sütunlarını ister; katalog DTO'su yerine geçmez |
| Tracking | Sorguya göre açık seçim | Sözleşmede açıkça tracked Find |
| Yaşam süresi | Scoped context DI/test scope sahibi | Repository context oluşturmaz/dispose etmez |
| Commit | İşlemin tek SaveChanges sınırı | Repository save etmez; ek atomiklik sağlamaz |
| Maliyet | EF bağımlılığı görünür | Ek tür/sözleşme; mevcut tüketicide yeterli kazanç yok |

DbSet entity sorgulama ve ekleme gibi repository ile örtüşen yeteneklere sahiptir;
DbContext tracking, identity map ve bekleyen değişiklikleri SaveChanges ile kaydetme
gibi UoW yetenekleri sunar. Bu, bütün iş ihtiyaçlarının hazır soyutlandığı veya
uygulamanın EF'den bağımsız olduğu anlamına gelmez. Ek IUnitOfWork bugün yalnız
SaveChanges aktarımı olurdu. Repository veya interface adı atomiklik sağlamaz.

## Çalışan dar deney

scripts/EnrollmentRepositoryExperiment.cs yalnız test derlemesine dahil:
DemoEnrollmentRepository.FindAsync normal tracked entity veya null döndürür;
DB hatası/iptali yutmaz. DbSet.FindAsync'nin yerel cache davranışını taklit etmez;
direct karşılaştırma da aynı SingleOrDefaultAsync sorgusunu çalıştırır.
Add yalnız Added yapar. SaveChanges, transaction ve dispose çağırana aittir.
DemoStudentWriter ikinci küçük katılımcıdır; üretim servisi değildir.

scripts/RepositoryChecks.cs aynı delegate harness ile direct/repository yollarını
çalıştırır: bulunma/yokluk, context takibi, Add, save öncesi/sonrası ayrı context
görünürlüğü ve gerçek offering FK ihlalinde iki yazmanın da kalıcılaşmaması.
Başarısız context tekrar kullanılmaz. İki bileşenin aynı context'e bıraktığı
Student/Enrollment tek SaveChanges ile kaydedilir. Sequence boşlukları normaldir.

Mevcut scripts/RelatedWriteChecks.cs Tracking deneyindeki bağımsız SaveChanges
kontrolü dar biçimde genişletildi: ilk kayıt bir context'te, hatalı ikinci kayıt
başka context'te. İlk kayıt kalır. Eski A/B/C/D transaction deneyi kopyalanmadı.
Aynı context'te bekleyen değişiklikler ortak save'e katılır; iki SaveChanges'in aynı
context'te olması onları kendiliğinden tek transaction yapmaz. Açık transaction'da
SaveChanges dış commit değildir. Onay olayının bağımsız save sonrası yayın ve dış
transaction reddi korunuyor; üretim dosyaları değişmedi.

## Generic sözleşmenin maliyetini görünür kılan örnekler

```csharp
// CoursePrerequisite'ın anahtarı (CourseId, PrerequisiteCourseId).
// Find(int id) bu çifti ifade edemez. object[] gibi genelleme tip güvenliğini azaltır.

// Hatalı tasarım örneği, çalıştırılmıyor:
// var all = await repository.GetAllAsync();
// var selected = all.Where(e => e.StudentId == studentId);
// Where artık bellekte; tablo önceden yüklenmiştir.

// Query() => IQueryable<Enrollment> EF çevirisi ve context yaşam süresini dışarı taşır.
// Aynı sorgu esnekliğini sağlamak, sağlayıcıdan bağımsızlık demek değildir.
```

Yaşam süresi riski deneyde gerçekten çalışır: context kapandıktan sonra ertelenmiş
IQueryable yürütülür ve kontrollü ObjectDisposedException beklenir. Repository Find
de kapanmış context'i null diye gizlemez. Geçerli sorguda filtre parametreli WHERE'de,
dar projection gerekli sütunlarla SQL'de kalır; gerçek yürütülen komutlar mevcut
CatalogSqlCapture ile bellekte incelenir. SQL alias/whitespace snapshot'ı yoktur.
Katalogun daha kapsamlı COUNT/sayfalama kanıtı mevcut full regresyondan yeniden kullanılır.

Her entity'ye Remove vermek de iş ihtiyacı değildir: Enrollment durum geçişleri,
NO ACTION ilişkileri ve kayıt geçmişi fiziksel silme ile eşdeğer değildir. Bu
sorunları çözmek için generic repository veya query framework yazılmadı.

Sahte repository bir tüketicinin davranışını izole edebilir; SQL Server FK/unique,
çeviri, tracking veya transaction garantisini kanıtlamaz. Bugünkü deney gerçek
LocalDB kullanır; teknoloji bağımsızlığı veya performans kazancı iddia etmez.
