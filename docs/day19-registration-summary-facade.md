# Gün 19 — Dönem kayıt özetinde Facade

Facade, ilgili birkaç okuma ve hesaplamayı tek anlamlı uygulama işlemiyle sunar.
`Application/TermRegistrationSummaryFacade.GetAsync` öğrenci, dönem, kayıtlar ve
aktif programı hazırlar; HTTP bağımlılığı yoktur. Sonuç ayrık Found/StudentMissing/
TermMissing türlerinden biridir. `Endpoints/TermRegistrationSummaryEndpoint`
pozitif kimlik doğrulaması ve200/400/404 eşlemesini yapar. Beklenmeyen DB hatası
catch edilip kaynak yok diye sunulmaz; mevcut güvenli merkezi mekanizmaya gider.
DI somut scoped sınıfı sağlar; DbContext aynı scope'tadır, sorgular sıralı await
ve CancellationToken kullanır.

17. gün Decorator aynı evaluator sözleşmesinin çevresine süre gözlemi ekledi.
18. gün Adapter farklı sahte API protokollerini ortak sözleşmeye çevirdi. Buradaki
Facade ise bir dönem özeti hazırlamayı tek işlemle erişilebilir kılar. Kapsamlı
servis katmanı, zorunlu interface veya başka endpoint'lere HTTP çağrısı gerektirmez.

## Veri sınırı ve satır çoğalması

1. Öğrenci var mı? Yoksa StudentMissing.
2. Dönem var mı? Yoksa TermMissing.
3. Enrollment→Offering→Course JOIN, yalnız öğrenci/dönem filtreli ve Id sıralı
   scalar projection. Her enrollment tek satır.
4. Aktif kimlik varsa yalnız o açılışların saatlerini filtreleyen tek sorgu.

Saatleri üçüncü sorguya katmak çok saatli bir dersin satırını çoğaltır; COUNT ve
kredi SUM yanlış büyüyebilir. Bu nedenle counts ve checked long kredi toplamı
üçüncü sorgunun listesinden çıkarılır. Her durum sayılır, fakat yalnız Submitted/
Approved yük oluşturur; Draft/Rejected programdan da dışlanır. İki şube iki
kayıtsa güncel kredileri ayrı ayrı sayılır. Geçmiş kredi snapshot'ı oluşturulmadı.
SQL tarafındaki mevcut aktif yük koşuluyla aynı enum anlamı kullanılır;
çevrilemeyen helper SQL Where içine sokulmadı.

Aktif kimlikler tekilleştirilir. Saat sorgusu yoksa gereksiz sorgu gönderilmez.
Pazar=0 değerini Pazartesi başlangıcına çevirmek için `(day + 6) % 7`, sonra
başlangıç/bitiş/açılış/slot Id sırası kullanılır. Gün string'e açık çevrilir;
TimeOnly DTO'da tutulur, saniyeye özel formatla kesilmez. İç slot Id yalnız sıra
belirler, HTTP sözleşmesine eklenmez. Contracts/TermRegistrationSummaryDto.cs
entity taşımayan isimli yanıtları içerir; global enum converter eklenmedi.

## Boş sonuç ve tutarlılık

Kayıtsız öğrenci/dönem geçerli kaynaklardır:200, sıfır ve boş diziler. Kaynağın
yokluğu404'tür. Mevcut kaydı olmayan öğrenci ile olmayan öğrenci aynı değildir.
Sayılar ve kredi aynı listeden türediği için kendi aralarında tutarlıdır; saatler
sonraki sorgudan gelebilir. Eşzamanlı veri değişiminde bütün özet aynı anı temsil
etmeyebilir. Transaction/kilit, cache, uygunluk veya otomatik düzeltme eklenmedi.

## Kontroller

`scripts/SummaryChecks.cs`: gerçek LocalDB/HTTP fixture'ı3/4/5/6 kredili dört
statü, başka öğrenci/dönem, çok saatli aktif ve saatsiz aktif açılış içerir.
Beklenen kredi9; pasif saatler hariçtir. Tekrarlı GET, tick hassasiyeti, boş/pasif
sonuç,404/400 ve öğrenci-önce sırası kontrol edilir. Fresh context ile yalnız
fixture kayıtlarının önce/sonra içerikleri karşılaştırılır. 4 ve24 enrollment
ile dört SELECT; boş/pasif yolda üç SELECT. SQL filtre/projection/sıra kontrolü
alias veya whitespace snapshot'ına dayanmaz. Tracker boş kalır. İki yüksek
kredili açılış toplamın Int32 sınırını aşınca da long kalmasını doğrular.

Scalar projection entity oluşturmadığından tracking yapmaz; gereksiz global
AsNoTracking ayarı eklenmedi. Referanslar:
[EF tracking ve projection](https://learn.microsoft.com/en-us/ef/core/querying/tracking),
[TimeOnly JSON desteği](https://learn.microsoft.com/en-us/dotnet/standard/datetime/how-to-use-dateonly-timeonly).
