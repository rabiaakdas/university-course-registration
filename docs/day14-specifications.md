# 14. gün — Specification: ön koşul ve dönemlik kredi

## Veri ile koşulun ayrılması

`CoursePrerequisite` doğrudan ders→ders ilişkisi; `StudentCompletedCourse`
öğrenci→başarıyla tamamlanan ders ilişkisidir. Enrollment kayıt seçimini gösterir,
başarıyı göstermez. Aynı batch'te bir ön koşula kaydolmak onu tamamlamak değildir.
İki tabloda birleşik PK, zorunlu FK ve NO ACTION vardır. Self CHECK yalnız
CourseId != PrerequisiteCourseId sağlar; A→B→A döngüsünü engellemez. Test bu
sınırı gözler. Dolaylı zincir/döngü algoritması, not/transkript/muafiyet yoktur.

`Specifications/PrerequisitesSatisfiedSpecification.cs`: PrerequisiteInput
pozitif kimlik kümelerini FrozenSet'e savunmacı kopyalar; read-only arayüzünün
arkasındaki değişebilir listeye güvenmez. Null açık ArgumentNullException,
pozitif olmayan kimlik ilgili parametreyle ArgumentOutOfRangeException verir.
IsSatisfiedBy tüm gerekli kimlikler tamamlanmış kümesindeyse true verir; boş
ön koşul kabul edilir. Dış liste sonradan değişse bile girdi değişmez.

`Specifications/CreditLimitSpecification.cs`: CreditLoad negatif olmayan long
mevcut/istenen kredi ve pozitif long sınırı korur. Mevcut yük sınırı aşmışsa ret;
aksi halde requested <= limit-current. Kontrolsüz toplama ve int daraltması yok.
Specification'lar DB/EF/HTTP sorgulamaz, kayıt üretmez, null girdiyi açık reddeder.
Strategy alternatif bir hesaplama seçer; bu specification'lar hazırlanmış veriye
uygunluk koşulu uygular. Generic expression/rule engine veya And/Or/Not yoktur.

## Koordinasyon ve sorgular

`EnrollmentOperations.CreateAsync/CreateBatchAsync` önce mevcut istek/varlık/
mükerrer sözleşmesini korur. Açılış sorgusu yalnız seçilen Id'leri dersle birleştirip
CourseId/AcademicTermId/Credits taşır. Sonra EvaluateAsync:

1. İstenen derslerin doğrudan ön koşullarını toplu okur.
2. Yalnız bu öğrenci ve ilgili gerekli derslerin başarılarını toplu okur.
3. İstek sırasındaki açılışları saf prerequisite specification ile değerlendirir;
   ilk rette durur. Bu durumda kredi yükü sorgusu yapılmaz.
4. İlgili öğrenci ve dönemlerin mevcut Enrollment yükünü JOIN/GROUP BY/SUM ile
   SQL'de hesaplar. Credits toplamdan **önce** bigint'e çevrilir, sonuç long taşınır.
5. İstek sırasında ilk görünen dönemden başlayarak yeni seçimleri long toplar,
   mevcut + istenen yükü kredi specification'ına verir. İlk aşan dönemde durur.

Başarılı yol toplam altı SELECT'tir (önceki üç + yeni üç); sayı açılış/kural
sayısıyla büyümez. Veri sorgulama EF LINQ, koşul değerlendirme bellekteki küçük
projection'lar üzerindedir. IsSatisfiedBy EF Where içine konmaz. Sıra DB'nin rastgele
sonuç sırasından değil istek Id dizisinden kurulur. İlgisiz katalog/enrollment
satırları belleğe indirilmez. Aynı DbContext'te işlemler sırayla await edilir.

Microsoft [karmaşık sorgu operatörleri](https://learn.microsoft.com/en-us/ef/core/querying/complex-query-operators)
belgesi JOIN ve aggregate içeren GroupBy çevirisini açıklar; burada gerçek
çalışan komut capture'ı ayrıca WHERE/JOIN/GROUP BY/SUM(bigint) davranışını doğrular.

Her Enrollment yük sayılır; aynı dersin iki şubesi iki kez sayılır. Başka dönemin
yükü karışmaz. Başarıyla tamamlanan toplam krediyle dönemlik yük farklıdır.
Course.Credits güncel değeri kullanılır: sonradan kredi değişikliği eski kayıtların
hesaplanan yükünü değiştirir. Tarihsel kredi snapshot'ı bugün eklenmedi.

## Sonuç ve sınırlar

`AcademicRejection` yalnız ilgili açılış/dönem kimliğini taşır; HTTP bağımlılığı
yoktur. `AcademicRejectionHttp` 400 Problem Details ve kararlı code üretir:
prerequisite_not_satisfied / credit_limit_exceeded. Bütün retleri veya bütün eksik
dersleri toplamaz. Duplicate409, öğrenci404/binding-alan400 ve save FK/unique
sözleşmeleri devam eder. Gerçek SQL hatası akademik ret diye gizlenmez.

Bütün kontroller geçmeden Add/AddRange yoktur. Batch hâlâ tek SaveChanges;
bir yeni kayıtta yazma hatası bütün batch yazmalarını geri alır. Ön kontroller
transaction dışındadır; iki istek aynı yükü okuyabilir. Bu değişiklik eşzamanlı
kredi aşımını kesin engellemez. Serializable/kilit/token/retry veya kontenjan
çözümü eklenmedi. Sorgulardan sonra ön koşul/kredi verisi de değişebilir.

`EnrollmentPolicy:MaxCreditsPerTerm=30` sunucu taraflı örnek proje politikasıdır.
Program başlangıçta eksik/boş/biçimsiz/Int64 dışı/sıfır/negatif değeri anlaşılır
hata ile reddeder. Varsayılan fallback yoktur; 30 açık appsettings değeridir.
Immutable EnrollmentPolicy singleton yalnız sayı taşır; DbContext/işlem scoped
kalır. Kullanıcı istek gövdesinden limit seçemez.

## Deneylerin güvenliği

`scripts/SpecificationChecks` saf birim + gerçek HTTP/SQL sınırlarını ayırır.
Yeni ilişkiler cleanup'ta üst kayıtlardan önce kaldırılır. Test verileri GUID
etiketlidir, üretilen Id'ler izlenir. 50 öğelik eski başarı testi 10 ayrı test
dönemine beşer adet 4 kredilik açılış dağıtır; politika gevşetilmez. Yazma fault
kontrolleri altı sorgudan sonra gerçek unique/FK hatasına ulaşmaya devam eder.

`sql/app/academic-example.sql` yalnız day6 adlı, sahiplik işareti bulunan izole
test DB'sinde sentetik ders/öğrenci oluşturur. OUTPUT ile yeni kimlikleri alır,
ilişkileri doğrular ve daima rollback yapar. App/lab'de reddedilir; gerçek öğrenciye
başarı kaydı eklemez. Sequence boşlukları geri alınmaz. Varsayılan seed değişmedi.
