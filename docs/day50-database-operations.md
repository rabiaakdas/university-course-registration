# Gün 50 — DB nesneleri, izinler ve backup/restore

## Üretim ve deney ayrımı

- `Models/OfferingCapacityView.cs`, `Data/UniversityDbContext.cs`: keyless
  ToView eşlemesi; sayım CLR long / SQL bigint. ToView nesne oluşturmaz.
- `Data/Migrations/20261124111639_AddOfferingCapacityReportView.cs` ve EF'nin
  ürettiği designer/snapshot: Up ayrı batch'te CREATE VIEW, Down DROP VIEW.
- `scripts/DatabaseOperationsChecks.cs`: izole procedure/inline TVF/trigger,
  migration, izin, gerçek backup/restore ve temizlik. Tek database-operations modu.
- `sql/app/database-operations-permissions-lab.sql`: migration'dan ayrı,
  test DB'sine özgü rol/user provisioning; startup/HTTP üzerinden çalışmaz.

View açılış başına bir satırdır. Enrollment önce CourseOfferingId ile
COUNT_BIG gruplandırılır; Status1=Submitted,2=Approved. Draft0/Rejected3/
Dropped4 dışarıda. LEFT JOIN ve bigint0 COALESCE boş açılışı korur. Saatlere
JOIN yok. Hesaplanan aktif sayı ve saklanan sayaç farklı adlarla döner;
testte sayaç kasıtlı1 yapılırken hesaplanan2 kaldı, sonra fixture eski2'ye
getirildi. Bu farkı view gizlemez veya onarmaz.

Mevcut HTTP doluluk raporu/DTO/yetkilendirme korunur. View dar DB rapor
okuma yüzeyi sağlar; aynı rapor için yeni endpoint eklenmez. Yeni package,
index, retry, trigger tabanlı kontenjan veya kayıt yaşam döngüsü yok.

## Nesne farkları

View parametresiz ilişkisel sorgu yüzeyidir; burada saklanmış özet veri değil.
Procedure `@term` ile çağrılan sonuç üretir; query JOIN'ine tablo gibi konmaz.
Inline TVF `@term` alan tablo ifadesidir; dış SELECT sıralamasına katılır.
İki deney nesnesi aynı view'den iki satırı (aktif2/0) döndürür; parametreler
SqlParameter ile bağlanır. Üretime bu procedure/function eklenmedi.

AFTER INSERT trigger yalnız Day50Input tablosundadır. inserted kümesinden
COUNT_BIG/SUM(bigint) ile Day50Audit'e tek özet yazar. Üç satırlı tek INSERT
bir audit satırı, InsertedRows3/Total9 üretir. Açık transaction'daki önceki
başarılı INSERT'ten sonra negatif değerli ikinci INSERT gerçek51050 THROW
üretir. CATCH rollback sonrasında ilk yazma ve audit dahil tüm transaction
geri alınır; başlangıçtaki3 input/1 audit kalır. Timeout/cancellation bu hata
kanıtı sayılmadı. Trigger row başına çalışıyor varsayımı yapılmadı.

## Şema güvenliği ve gerçek sonuç

Restore/build başarılı,0 uyarı/0 hata. Yeni migration EF aracıyla üretildi;
view SQL'i Up/Down'a eklendi, designer/snapshot elle yazılmadı.
İzole DB'de temiz latest kurulum ve boş view, önceki migration'a Down ile
view'in kaldırılması, eski şemada örnek veri, latest'e yeniden yükseltme
geçti. Beş enrollment durumu, öğrenci ilişkileri, iki açılış ve iki saat
korundu. İkinci update migration geçmişini değiştirmedi; model/snapshot uyumlu.
Down yalnız view'i kaldırır; veri kurtarma garantisi değildir.

App hedefi mevcut appsettings'ten `(localdb)\MSSQLLocalDB` /
`university_registration_app` olarak doğrulandı; uygulama executable'ı
çalışmıyordu. Yalnız hedef migration EF ile uygulandı; ikinci update no-op,
has-pending-model-changes temiz, view okunabilir (3 satır).

Açık eksik: app üzerinde ek önce/sonra tüm tablo checksum kontrolü eski
System.Data.SqlClient sürücüsünün encryption bağlantı hatasında çalışmadı.
PowerShell akışı devam edip yanıltıcı PASS yazdı; bu satır kanıt değildir.
App için sayısal before/after veri eşitliği iddia edilmez. İncelenen migration
SQL'i yalnız CREATE VIEW içerir; izole yükseltmede veri koruması ayrıca geçti.
App'i geri alıp bu ölçümü yeniden üretmedik; lab DB'sine dokunulmadı.

## İzin matrisi

| Kimlik | İzin verilen | Gerçek reddedilen |
| --- | --- | --- |
| Day50ReportUser WITHOUT LOGIN | view SELECT (2 satır) | Enrollments SELECT, CREATE TABLE |
| Day50RuntimeUser WITHOUT LOGIN | Courses SELECT/INSERT, yalnız Code/Name/Credits UPDATE | AspNetUsers SELECT, CREATE TABLE, sequence NEXT VALUE |
| Geçici SQL login'e bağlı Day50Connected | aynı catalog runtime rolü; ayrı bağlantıda gerçek EF read/insert/update | Enrollments SELECT |

Roles db_owner/db_ddladmin veya server yönetim yetkisi içermez. Runtime smoke
CourseCatalogReader ile mevcut sorguyu, Course SaveChanges ile rowversion
INSERT/UPDATE yolunu kullanır; test transaction'ı rollback edilir. Web login,
Identity, enrollment/receipts, waitlist veya tüm uygulama runtime kapsamı
değildir. Bunların sequence/UPDLOCK/ilgili tablo yetkileri bu role verilmedi;
seçilen katalog akışları bunları kullanmaz. Sequence reddi ayrıca doğrulandı.

EXECUTE AS USER aynı owner bağlantısındaki DB impersonation'dır; harici
kimlik doğrulama değildir. Her impersonation finally REVERT ile kapanır.
Ayrıca gerçek ortam SERVERPROPERTY IsLocalDB=1, IsIntegratedSecurityOnly=0
bildirdi. Mevcut modu değiştirmeden benzersiz geçici SQL login, yalnız bu
DB'de rol üyeliği, bellek içi rastgele parola ve ayrı SqlConnection ile
başarılı kimlik doğrulama/EF işlemleri test edildi. Parola/connection string
çıktıya verilmedi. Login/user backup'tan önce kaldırıldı. Genel makine auth,
Windows hesabı veya kalıcı kullanıcı ayarı değiştirilmedi.

## Backup zinciri

Son başarılı hedefli koşu exit0:

1. Yeni kaynak DB'de migration+bilinen Course.Name='Before backup', iki view
   satırı ve akademik fixture doğrulandı. Test nesneleri/rolleri kaldırıldı.
2. Repo dışında benzersiz temp dizinine COPY_ONLY,CHECKSUM tam yedek alındı.
3. VERIFYONLY CHECKSUM başarılı; bununla yetinilmedi.
4. FILELISTONLY iki fiziksel dosya döndürdü; her logical name için farklı,
   önceden bulunmayan path parametreli MOVE ile kullanıldı.
5. Kaynak Course.Name='After backup' yapıldı. Farklı benzersiz/önceden yok
   hedef DB'ye gerçek RESTORE CHECKSUM yapıldı; WITH REPLACE kullanılmadı.
6. Restore hedefinde DBCC CHECKDB TABLERESULTS/NO_INFOMSGS hata satırı yoktu.
   Migration geçmişi,5 enrollment/5 öğrenci/2 saat, FK kimlikleri, view2/0,
   keyless tracking yokluğu ve mevcut katalog DTO okuması doğrulandı.
7. Hedef eski adı, kaynak yeni adı taşıdı; restore kaynak verisini geri almadı.
8. Son koşuda hem DB'ler hem yalnız üretilen dosyalar finally'de silindi;
   tarih/ad/unused target kontrolleri, connection dispose ve pooling=false.
   Mevcut bağlantı zorla kapatılmadı, app/lab restore hedefi olmadı.

İlk derleme SqlParameter hedef türü hatasıyla, ilk çalışma trigger sütun
adının RowCount keyword olmasıyla başarısız oldu; düzeltildi. Bir sonraki
koşuda bütün davranışlar geçti ama pooled connections nedeniyle temizlik
exit3 verdi. Deney bağlantıları pooling=false yapıldıktan sonra son koşu
bütünüyle exit0. Önceki koşuların üç DB'si ve iki temp dizini oluşturulma
zamanı/ad/dosya eşleşmesiyle doğrulanıp ayrıca temizlendi. Geçici login kalmadı.
Bu başarısız adımlar başarı diye sayılmadı.

DB yedeği server login'lerini, SQL Server servis/dosya yetkilerini, uygulama
ayarlarını, JWT özel anahtarını veya Data Protection dosyalarını bütünüyle
kurtarmaz. Kullanıcı/login SID eşlemesi ve dış bağımlılıklar ayrı recovery
konusudur. PITR/log backup, otomasyon, production RPO/RTO ve şifreli yedek
anahtarı kurtarma bu dersin kapsamı değil. Salt file erişimi LocalDB'nin
çalıştığı mevcut Windows kullanıcısına ait temp dizininde denendi.

Kaynaklar: [LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb),
[FILELISTONLY](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-filelistonly-transact-sql),
[çok satırlı trigger](https://learn.microsoft.com/en-us/sql/relational-databases/triggers/create-dml-triggers-to-handle-multiple-rows-of-data).
Full regresyon sonucu ilerleme kaydında; sıradaki gün51.
