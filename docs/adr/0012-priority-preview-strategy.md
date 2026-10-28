# 0012 — Öncelik önizlemesinde Strategy

1. **İhtiyaç:** Aynı kredi girdisine iki alternatif örnek öncelik politikası
   uygulamak; sonuçları kayıt kabul/ret kararına bağlamadan göstermek.
2. **Koşullu çözümün sınırı:** İki formül tek switch'te mümkündür. Politika arttıkça
   HTTP doğrulama, seçim ve formülün aynı yerde değişmesi hesapları ayrı sınamayı
   zorlaştırır. Bugün büyük altyapı ihtiyacı doğduğu iddia edilmiyor.
3. **Seçim:** Kontrollü PriorityInput, IRegistrationPriorityStrategy ve iki saf
   uygulama. Endpoint'te normalize politika için açık switch; formüller stratejide.
4. **Kazanç/maliyet:** Ortak davranış sözleşmesi, deterministik ve DB'siz testler;
   ek türler ve seçim listesini bakım maliyeti. Basit switch veya Func alternatifi
   daha az tür gerektirirdi; öğrenme kapsamı için açık Strategy tercih edildi.

Yalnız önizleme: mevcut Student modelinde güvenilir kredi/transkript bilgisi yok.
İstemci kredisi kayıt kabulünün temeli yapılamaz. Yetkili politika seçimi, güvenilir
veri, kapasite ve eşit puan/yerleştirme kararları eksiktir. Student/Enrollment
şeması ve kayıt transaction sınırları korunur; migration gerekmez.

Bağımlılıksız/durumsuz stratejiler seçim noktasında doğrudan oluşturulur. DI,
factory/resolver/registry, calculator zinciri ve DB sütunu eklemek bugün yalnız
ek bakım getirirdi. Politikalar arası puanlar ortak ölçek gibi karşılaştırılmaz.
