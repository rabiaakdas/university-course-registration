# 0021 — Kesinleşmiş onay için uygulama içi observer

**İhtiyaç:** Onay sonrası kritik olmayan tepkileri kayıt koordinasyonundan ayırmak.
Doğrudan log çağrısı bugün daha az tür isterdi; bağımsız tepkilerin hata/iptal
politikasını koordinasyona ekleyerek büyütürdü.

**Seçim:** Tek, ihtiyaca özel EnrollmentApproved olayı ve observer sözleşmesi;
scoped publisher sıralı ve beklenen dağıtım yapar. Olay onay koordinasyonunda,
bağımsız başarılı SaveChanges sonrasında oluşturulur. Entity event listesi, generic
bus, interceptor dispatcher veya yeni paket yoktur. Dış transaction altında bu
operasyon açıkça reddedilir; onun SaveChanges sonucunu commit saymayız.

**Kazanç:** Onay kuralları ve transaction sınırı yerinde kalır. Tepki hatası loglanıp
bağımsız observer'a devam edilir. Çağıran iptali sonraki observer'ları durdurur;
kesinleşmiş onay sahte ret/rollback'e dönüştürülmez. Üretimde yalnız ApprovalLogObserver
vardır. Testler gerçek endpoint mapping ve scoped DI üzerinden alternatif observer'lar
sağlar; normal HTTP sözleşmesi değişmez.

**Maliyet/sınır:** Ek sözleşme, DI ve yayın akışı izlenmelidir. Her observer request
süresine eklenir. Commit/yayın boşluğu, kısmi dağıtım ve logger arızası kalır.
Olay/teslim kalıcı değildir; EventId dedup veya exactly-once sağlamaz. Eşzamanlı iki
onayın çift olay üretimi engellenmez. Kritik kurallar observer'a taşınmaz; gerçek
bildirim/integration event, retry veya Outbox bugünün kapsamı değildir.
