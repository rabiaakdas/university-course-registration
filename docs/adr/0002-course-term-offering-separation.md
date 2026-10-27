# 0002 — Ders, dönem ve açılışı ayırmak

Durum: Kabul edildi (2. gün).

1. **İhtiyaç:** Aynı ders farklı dönemlerde ve aynı dönemin farklı şubelerinde
   açılabilmeli. Kontenjan her açılışa ait olmalı.
2. **Eski çözümün yetersizliği:** Tek ders listesi dönem, şube ve kontenjan
   ayrımını temsil etmiyor.
3. **Seçim ve kazanç:** `Course`, `AcademicTerm` ve `CourseOffering` ayrı küçük
   modellerdir. Açılış ders ve döneme kimlikleriyle bağlanır. Ders bilgileri
   tekrar edilmez, şubeler ayrı kimlik ve kapasite taşır. API yanıtında gerekli
   adlar kimlik eşleştirmesiyle oluşturulur; model ile yanıt ayrı tutulur.
4. **Alternatif ve maliyet:** Tüm bilgileri tek yapıda tekrar etmek yerine
   kimliklerle ilişkilendirmek seçildi. Bunun maliyeti kayıtları eşleştirmek ve
   ilişki bütünlüğünü yönetmektir. C# `int` alanları bir FK kısıtı değildir;
   bugün sabit örnek veriler HTTP betiğiyle doğrulanır. Kalıcılık veya kapsamlı
   iş kuralı doğrulaması eklenmedi.
