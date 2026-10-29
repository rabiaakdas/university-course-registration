# Gün 16 — Durum, kontrollü geçiş ve State alternatifi

Durum bir anlık değerdir; geçiş, mevcut durumdan izin verilen yeni duruma
harekettir. Enum tek başına `Draft → Approved` atamasını engellemez.
`Models/Enrollment.cs` bu yüzden private setter ve TrySubmit/TryApprove/TryReject
kullanır. Yeni nesne Draft/null gerekçe başlar; başarısız geçiş hiçbir alanı
değiştirmez. Reject gerekçesi Trim sonrası1–500 karakter olmalıdır.

Entity metodu yalnız yerel geçiş tablosunu korur. Öğrencinin akademik uygunluğunu
ve işlem yapan kişinin yetkisini kanıtlamaz. Koordinasyon
`Application/EnrollmentOperations.AdvanceAsync` içinde: kayıt → mevcut durum →
güncel ortak akademik değerlendirme → entity metodu → SaveChanges. HTTP
eşlemesi `Endpoints/EnrollmentEndpoints.Transition` içinde kalır. Reject için
gerekçe doğrulama → kayıt varlığı → Submitted kontrolü → geçiş/yazma sırası vardır;
akademik değerlendirme yapılmaz. Gerekçe geçersizse var olmayan kimlikte bile400.

## Yük ve hedefin dışlanması

`EnrollmentOperations.EvaluateAsync` kredi SUM(bigint) ve mevcut saat JOIN'leri
için aynı filtrelenmiş IQueryable'ı kullanır: öğrenci + Submitted/Approved.
Draft/Rejected mevcut yük değildir. Yeni adayların kendi kredi toplamı ve
birbirleriyle saat çakışması yine değerlendirilir; Draft batch bunu atlamaz.

Gönderme/onaylamada yükten yalnız izlenen hedefin Enrollment.Id'si çıkarılır;
hedef açılış aday olarak bir kez eklenir. Özellikle Submitted→Approved sırasında
hedefin kredisi ikiye katlanmaz ve kendi saatiyle çakışmaz. Başka aktif kayıtlar
korunur. Bu dışlama private değerlendirme parametresidir; HTTP'den alınmaz.
Mevcut çift kontrolü geçiş yoluna uygulanmaz, çünkü kaydın kendisi yeni kayıt
değildir. Yeni oluşturma/preview ise tüm durumlardaki mevcut çifti reddeder.

## Enum ve State karşılaştırması

| Yaklaşım | Bu projedeki rol | Maliyet |
|---|---|---|
| Enum + kontrollü metotlar | Üretim Enrollment yaşam döngüsü | Küçük geçiş tablosu entity içinde görünür |
| State nesneleri | `scripts/EnrollmentStateExperiment.cs` öğrenme deneyi | Dört durum sınıfı, ortak durum sözleşmesi ve özel geçiş kapısı |
| Strategy | Öncelik önizlemesinde seçilen hesaplama politikası | Aynı girdi için değiştirilebilir algoritma; yaşam döngüsü değil |

Deneyde context Submit/Approve/Reject davranışını private nested durum nesnesine
devreder. Draft Submit, Submitted Approve/Reject sağlar; terminal durumlar
geçiş sağlamaz. Yeni durumu kabul eden kapı private olduğundan dışarıdan keyfi
state ataması yoktur. Gerekçe temizliği mevcut saf entity yardımcısını kullanır.
Bu kadar küçük üretim akışında State sınıfları ek bakım getirir; gerekli ayrı
durum davranışları henüz yok. EF mapping/DI/repository/framework eklenmedi.
`StateChecks.Unit` aynı beklentileri iki uygulamaya delegate adaptörleriyle
uygular; test matrisi kopyalanmaz. Bu adaptörler üretim soyutlaması değildir.

## EF ve veri geçişi

EF private setter'ları yükleyebilir; normal uygulama kodu bu alanları doğrudan
atayamaz. [Microsoft EF constructor/private setter belgesi](https://learn.microsoft.com/en-us/ef/core/modeling/constructors)
esas alındı, yeni context ile gerçek yükleme ayrıca sınandı.

`AddEnrollmentLifecycle` önce nullable nvarchar(500) gerekçe ve required int durum
(kalıcı default0) ekler, o anda mevcut satırları SQL ile2/Approved yapar, CHECK'leri
ekler. Bu eski doğrudan kayıt anlamının taşınmasıdır; geçmiş onay/audit kaydı
uydurmaz. Yazmalar migration sırasında durmalıdır; eski uygulama ile yeni şema
aynı anda yazmamalıdır. [Migration SQL/veri dönüşümü](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)
ayrı incelenir ve açık komutla uygulanır. Startup migration yoktur.

DB, durum0..3 ve Rejected için non-null/boş olmayan gerekçe; diğer durumlarda
NULL koşullarını korur. SQL UNKNOWN riskine karşı `IS NOT NULL` açık yazılmıştır.
TRIM karakter listesi .NET boşluklarını (tab, satır sonu ve Unicode boşluklar dahil)
kapsar. DB gerekçeyi otomatik temizlemez; entity Trim eder. CHECK geçmiş durumun
ne olduğunu ve yetkiyi bilmez; doğrudan SQL Draft→Approved geçirebilir. Bu sınır
izole deneyde gösterilir. Down iki yeni alanı kaldırır ve süreç bilgisini kaybeder;
bu çalışmada Down uygulanmadı, yedek yerine geçmez.

## Sözleşme ve doğrulama sınırı

DTO'larda Status açıkça string'e çevrilir, global enum serializer ayarı yoktur.
Tekli/batch/detay/öğrenci listesi/geçiş yanıtlarında status ve nullable
rejectionReason alanları eklendi. Akademik400/failures aynıdır. Tekrarlanan
işlem409; idempotent başarı değildir. Rejected çift de unique nedeniyle yeni
kayıt oluşturulmasını engeller; tekrar açma/ders bırakma yoktur.

`scripts/StateChecks.cs`: ortak saf matris, önceki şemadan upgrade, gerçek HTTP,
fresh-context satır koruma, SQL kısıtları, aktif yük ve hedef dışlama, restart.
Eski `SpecificationChecks`/`EligibilityChecks` başarı fixture'larını submit eder;
olağandışı eski yük örnekleri yalnız testte `Active` ile açık Approved hazırlanır.
Kurallar kapatılmaz. Önceki fault/interceptor deneyleri aynı gerçek yazma dalını
korur. Sekiz toplu hazırlık sorgusu N+1'e dönüşmez; reject tek entity okur.

Yetkilendirme henüz yoktur: bu onay/ret endpoint'leri geliştirme akışıdır.
Concurrency token/kilit yoktur; eşzamanlı geçişlerde son yazan kazanabilir,
akademik yük okuma-yazma arasında değişebilir. Kontenjan rezervasyonu, audit,
outbox, State framework ve sonraki günün Decorator'ı eklenmedi.
