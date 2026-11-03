# 0024 — Hesabı akademik profilden ayırmak

1. **İhtiyaç:** Öğrenci olmayan hesapları da destekleyecek Identity veri modeli ve
   mevcut akademik öğrencinin isteğe bağlı hesap bağlantısı.
2. **Önceki sınır:** Student iş profilidir; kullanıcı hesabı değildir. Hesap alanlarını
   buraya taşımak öğrencisiz hesapları ve iki verinin sorumluluk ayrımını zorlaştırır.
3. **Seçim/kazanç:** Küçük ApplicationUser, mevcut context için IdentityDbContext
   tabanı ve minimal AddIdentityCore/EF store. Student.UserId nullable, private setter,
   filtreli unique index ve NO ACTION FK. Tek seferlik TryLinkAccount keyfi relink'i
   önler; eski akademik Id'ler, ilişkiler ve endpoint sözleşmeleri korunur.
4. **Maliyet:** Aynı migration/model yaşam döngüsünde Identity ve akademik mapping
   birlikte yönetilir. Yeni tablo/index'ler ve SQL oturum gereksinimleri test ister.
   UserManager'ın otomatik SaveChanges davranışı koordinasyonda bilinmelidir.

Ayrı DbContext/DB hesap modelini ayırabilirdi; bugün ek bağlantı, migration ve
hesap–profil tutarlılığı maliyetine karşı somut fayda sağlamıyor. Tek context ise
ayrı CreateAsync ve profil SaveChanges çağrılarını kendiliğinden atomik yapmaz.
Gerçek provisioning transaction tasarımı ertelendi; deney ayrı kaydetmenin sonucunu gösterir.

NO ACTION bağlı hesabın silinmesinin akademik kaybına dönüşmesini önler.
Birden fazla NULL profil kabulü açık filtreyle, tek hesap/tek profil DB unique ile
korunur. Metot hesap varlığını veya yetkiyi garanti etmez; public bağlama yoktur.
Temel model rol tabloları üretse de rol servisi/ataması eklenmez. Giriş endpoint'i,
cookie/JWT, SignInManager, özel repository veya otomatik hesap seed'i eklenmedi.
