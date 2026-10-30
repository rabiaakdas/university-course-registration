# 0017 — Hazırlanmış akademik değerlendirmeye timing decorator

1. **İhtiyaç:** Ortak akademik hesaplamanın sonucunu değiştirmeden tamamlanma
   süresini gözlemek. Aynı hesap oluşturma, preview ve geçişlerde kullanılır.
2. **Önceki sınır/alternatifler:** Statik saf Evaluate doğrudan çağrılıyordu.
   İçine Stopwatch koymak kuralları logging'e bağlar; HTTP middleware bütün
   isteği ölçer ve DB/yazma sürelerini bu dar hesaplamadan ayırmaz.
3. **Seçim/kazanç:** Küçük IAcademicEligibilityEvaluator, somut saf evaluator ve
   TimingAcademicEligibilityEvaluator. Program açık scoped factory ile somut
   evaluator'ı sarar; interface'i kendi çözümlemesinde tekrar istemez. İşlem yalnız
   hazırlık bittiğinde interface'i çağırır. Sonuç nesnesi, ret sırası ve hata akışı
   korunur; yerel Stopwatch eşzamanlı çağrılar arasında paylaşılmaz.
4. **Maliyet/sınır:** Bir interface, bir decorator ve DI bağlantısı; testlerde
   constructor bağlantısı güncellenir. Olay üretme maliyeti vardır, performans
   iyileştirmesi iddia edilmez. Log sağlayıcısı hata verebilir; genel fallback yok.

Ölçüm DB sorguları, SaveChanges, HTTP ve toplam istek süresini kapsamaz.
Event1701, ElapsedMs, Outcome ve nullable FailureCount dışında akademik/kişisel
veri loglanmaz. ineligible bir teknik hata değildir. finally içindeki logging
asıl exception'ı dönüştürmez; logger'ın kendi exception riski ayrıca kabul edilir.

Senkron hesaplamayı asenkronlaştırmak veya ikinci üretim decorator'ı eklemek
mevcut ihtiyaca hizmet etmiyor. Await ve sıra davranışları yalnız production
harici script deneylerinde gösterilir. Yeni paket, migration, middleware,
benchmark, metrics sistemi, retry/cache/yetkilendirme ve Adapter eklenmedi.
