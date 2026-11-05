# ADR 0032 — öğrenci kaynağına göre erişim

Durum: Kabul edildi.

1. İhtiyaç: Rolü olan kişinin başka StudentId/EnrollmentId ile kişisel kayıt okumasını
   veya değiştirmesini engellemek; danışman kararını güncel atamaya bağlamak.
2. Önceki sınır: Gün31 katalog yazmalarını rol ile koruyordu; kişisel akademik yollar
   açıktı. Role kontrolü tek başına sahiplik/atama belirtmez.
3. Seçim/kazanç: Üç küçük cookie-only policy ve ortak resource requirement/handler.
   Kaynak DB projection’ından alınır; read/manage/decide rolleri ayrı, mevcut işlem
   koordinasyonu korunur. Yanlış ilişki ve olmayan kaynak aynı404’tür. Liste SQL
   filtreli kalır; tek resource sorgusu N+1 yerine sabit ek maliyettir.
4. Maliyet: Ek SELECT, rol/ilişki fixture’ları ve bilinçli HTTP sözleşme değişikliği.
   HTTP dışı çağrılar güvenilir kabul edilir; bütün domain’e ClaimsPrincipal yayılmaz.
   Kaynak kontrolü ile yazma arasında TOCTOU kalır; geniş kilit altyapısı kurulmadı.

Alternatif endpoint içinde tekrarlanan if’lerdi; on farklı kişisel route için tutarlı
karar ve ret davranışı küçük handler/filter maliyetini karşılıyor. Genel authorization
framework/repository veya her route için farklı handler gerekmiyor.

Tek mevcut AdvisorUserId nullable ilişki yeterlidir. Tarihçeli/dönemli atama tablosu
bugünkü ihtiyacı aşar. Nonunique FK NO ACTION hesap silinmesini korur; rol kontrolü
atama komutunda ve principal üzerinden erişimde ayrıdır. Student.UserId ayrı ve unique
kalır. Komutlar yalnız Development, public self-assignment yoktur. Provisioning POST
StudentAffairs+CSRF, danışman read açıkça izinli, create/submit izinli değildir.

EnrollmentOperations, tek SaveChanges batch sınırı, onay sonrası observer ve refresh
transaction’ları değişmedi. Account endpoints kendi şema/CSRF sözleşmelerini korur.
Atama değişimi anlık kaynak sorgusunda görülür; eski rol cookie’si ayrı güncellik
sınırına sahiptir. Instructor–açılış ilişkisi bu kararla sağlanmış sayılmaz.
