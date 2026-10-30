# 0022 — Doğrudan EF'yi korumak, dar repository'yi deneyde karşılaştırmak

1. **İhtiyaç:** Kayıt erişiminin tracking ve yazma sahipliğini açık değerlendirmek.
   Tekli/batch akışta veri hazırlama, akademik ret ve commit sonrası olay sınırları var.
2. **Mevcut yeterlilik:** EnrollmentOperations toplu hazırlığı zaten paylaşır;
   katalog ve summary farklı projection/filtre anlamlarına sahiptir. Find/Add katmanı
   bu hazırlığı sadeleştirmez. Geçişlerde tracked entity gerekirken okuma DTO ister.
3. **Seçim/kazanç:** Üretimde A, doğrudan scoped DbContext. B yalnız üretim dışı
   DemoEnrollmentRepository deneyidir. Find tracked/null, Add yalnız Added;
   save/commit/dispose çağıranda. İki yol aynı davranış harness'iyle gerçek DB'de
   karşılaştırılır; iki bileşenin ortak context'i tek save'e katkısı görünür olur.
4. **Maliyet:** Deney ve sözleşme kontrolleri bakım ister; üretim tür sayısı artmaz.
   Repository üretime alınsaydı projection ve toplu sorgular için yeni metotlar ya
   da sağlayıcı bağımlılığını dışarı taşıyan IQueryable gerekecekti. Yalnız EF
   çağrısı aktaran IUnitOfWork somut kazanç sağlamıyor.

Generic CRUD, birleşik anahtar/işe özgü silme/projection ihtiyaçlarını saklamakta
yetersizdir; yeni generic altyapı eklenmedi. Batch tek SaveChanges olarak kalır.
Onay sonrası olay bağımsız save tamamlandıktan sonra yayınlanır; dış transaction
guard'ı, observer hata/iptal politikası ve kalıcı teslim sınırları aynen korunur.
Yeni şema/migration, paket, üretim interface'i veya mimari proje yoktur.
