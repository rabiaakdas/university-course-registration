# 0018 — Sahte bildirim API'lerini Adapter ile uyarlama

1. **İhtiyaç:** Aynı çağıranın ayrı parametre/exception kullanan bir API ile kendi
   request/error-code sonucunu kullanan diğer API'yi ortak anlamda kullanması.
2. **Alternatif/sınır:** Çağıran iki sağlayıcıyı doğrudan kullanırsa alan eşlemeleri
   ve bilinen hata ayrımları her kullanımda tekrarlanır. Gerçek ürünün henüz
   bildirim ihtiyacı/altyapısı yok; üretime yeni servis zinciri gerekmiyor.
3. **Seçim/kazanç:** Üretim dışı deneyde küçük kontrollü mesaj/sonuç, INotificationSender
   ve iki somut adapter. Sahte sağlayıcı delegate'leri kontrollü async sonucu
   üretir; aynı çağıran sağlayıcı türlerini bilmez. Yalnız bilinen sonuçlar çevrilir,
   bilinmeyen/çelişkili cevaplar exception, cancellation iptal olarak kalır.
4. **Maliyet:** İki dönüşümün ve sağlayıcıya özgü tutarlılık koşullarının bakımı;
   değişen bir sağlayıcı protokolü adapter ve kontrollerinin güncellenmesini ister.
   Bu eğitim sözleşmeleri gerçek SDK uyumluluğunu kanıtlamaz.

Tüm türler scripts alanında, ana web derlemesi/DI dışında. Onay/kayıt akışına
bağlanmadı: DB ve dış gönderim atomikliği, yeniden deneme, teslimat ve yetki
kararları kapsam dışı. Accepted yalnız kabul; başarısız sonuçta referans yok;
referanssız kabul için kimlik üretilmez. Retry/fallback, queue/Outbox, migration,
framework, paket, gerçek gönderim veya sonraki günün Facade'ı eklenmedi.
