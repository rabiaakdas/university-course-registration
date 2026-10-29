# 0016 — Kontrollü kayıt yaşam döngüsü

1. **İhtiyaç:** Kayıt isteğini göndermek ve karara bağlamak; mevcut kaydın süreç
   durumu ve ret açıklamasını korumak. Önceki Enrollment yalnız ilişki/zaman
   taşıyor, bütün kayıtlar akademik yük sayılıyordu.
2. **Seçim:** Üretimde enum + private setter + üç kontrollü entity metodu.
   State alternatifi dört davranış nesnesiyle yalnız script deneyinde gösterilir.
   Küçük geçiş tablosu için üretime kalıtım/DI/state-machine eklemek gerekmedi.
3. **Kazanç:** Yerel geçiş, akademik koordinasyon ve HTTP ayrımı sürer. Ortak
   uygunluk kodu submit/approve anında tekrar çalışır. Yük sorgusu yalnız
   Submitted/Approved; hedef kayıt private yoldan tek başına dışlanır.
   Gerekçe SQL ve entity sınırında korunur; kullanıcıya açık durum döner.
4. **Maliyet:** İki sütun/migration, yeni sözleşme alanları, üç işlem ve durum
   bazlı fixture hazırlığı. Küçük bool seçimiyle ortak AdvanceAsync submit/approve
   koordinasyonunu paylaşır; ikisi farklı entity geçiş metodunu çağırır.

Yeni kayıt Draft, eski satırlar migration sırasında Approved olur. Eski doğrudan
kayıt anlamı korunur; gerçek geçmiş onay olayı/audit üretilmez. Kalıcı default0,
eski doldurma ayrı UPDATE2'dir. Migration sırasında yazmalar durdurulur; app
hedeflenir, lab korunur. Down süreç bilgisini geri getiren yedek değildir.

Draft/Rejected yük değildir, fakat batch adayları birlikte hesaplanır.
Rejected çift yeniden başvuruyu engellemeye devam eder; yeniden açma seçeneği
bugün yoktur. Önizleme rezervasyon değildir. Kayıt anındaki eski unique/FK
sınıflandırması ve batch tek SaveChanges sınırı değişmez.

Enum tek başına, DB CHECK de tek başına geçmiş geçişi korumaz. Kontrollü metotlar
normal kullanım içindir; EF/SQL private setter dışından veri yükleyebilir.
Yetki akademik kurallardan ayrıdır ve henüz uygulanmaz. Eşzamanlı geçiş/kredi/saat
garantisi, kontenjan, audit ve genel framework eklenmedi; gerçek kullanım için
bu sınırlar ayrıca çözülmelidir.
