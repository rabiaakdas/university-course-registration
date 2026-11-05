# Gün 32 — kaynak bazlı yetkilendirme

IDOR, bir kimliği bilmenin/değiştirmenin başka kişinin kaynağına erişim sağlamasıdır.
Role policy yalnız görev sınıfını belirler. Student rolü ile Student.UserId sahipliği;
Advisor rolü ile AdvisorUserId ataması birlikte gerekir. Student.Id, Identity hesabı
değildir. StudentAffairs/Instructor ve çoklu rol örtülü yönetici bypass’ı sağlamaz.

`Accounts/StudentAccess.cs`: cookie-only genel rol policy’leri endpoint öncesindedir.
Küçük projection yalnız StudentId/UserId/AdvisorUserId taşır. IAuthorizationService
`AuthorizeAsync` ile requirement/handler çağrılır. Handler DB sorgusu veya yazma yapmaz;
options dışında scoped bağımlılığı olmadığından singleton’dır. Filtre scoped DbContext’i
HTTP sınırında kullanır; kaynak SELECT’i CancellationToken ile await edilir. Enrollment
route’u ilişkisini DB’den bulur; gövdedeki alternatif StudentId beyanına güvenmez.

Read: owner veya atanmış advisor. Manage: yalnız owner. Decide: yalnız atanmış advisor.
Genel rol eksiği403; bulunamayan/ilişkisiz kaynak404; kimlik yok401. Bu, kaynak varlığını
uygun role sahip ama ilgisiz kullanıcıya açıklamama tercihidir. JSON binding400 ve
CSRF400 ayrıdır. Yetkili kaynak sonrasında domain kuralları değişmez.

Liste önce tek hedef öğrenci için yetkilendirilir, sonra SQL’de StudentId ile filtrelenir.
Her enrollment için ayrı handler/sorgu yoktur. Summary facade da dar projection ve
saat toplu sorgusunu korur. HTTP filtresi mevcut EnrollmentOperations’ı kopyalamaz.
HTTP dışı çağrılar güvenilen iç kullanımdır; bu ayrım bütün uygulama servisinin kendi
başına kullanıcı güvenliği sağladığı anlamına gelmez.

`Models/Student.cs`, `Data/UniversityDbContext.cs`: nullable AdvisorUserId; FK NO ACTION,
nonunique index. FK rolü doğrulamaz. `Accounts/StudentMaintenance.cs` Development’ta
manager ile rol kontrolü yapar, açık atama/link sağlar. Profil DTO’su ilişki alanlarını
almadığı için public body’ye eklemek yetki vermez. POST/students staff+CSRF oldu.

TOCTOU: kaynak SELECT’iyle sonraki SaveChanges arasındaki atama yarışı çözülmedi.
Akademik batch’in tek SaveChanges/transaction atomikliği bu farklı garantiyi sağlamaz.
Atama bir sonraki istekte yeniden okunur; cookie/JWT rol güncelliği ise ayrı konudur.

Kod yolları: StudentEndpoints, EnrollmentEndpoints, TermRegistrationSummaryEndpoint;
scripts/ResourceAccessChecks gerçek HTTPS+SQL erişim matrisi; CatalogTestIdentity eski
pozitif testlere açık test-owned hesap bağlantısı sağlar. Yeni negatif testler bu
kolaylaştırıcıyı kullanmaz, farklı hesapları doğrudan gönderir. Üretimde bypass yoktur.

Kaynak: [Microsoft ASP.NET Core 10 resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-10.0).
Kaynak yüklenmeden sırf attribute/policy ile sahiplik belirlenemez; kaynak ile birlikte
IAuthorizationService kullanılır. Framework sonucu yerine HTTP404 seçimi bu projenin
bilinçli erişim sözleşmesidir.
