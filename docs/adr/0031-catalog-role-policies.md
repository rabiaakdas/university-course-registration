# ADR 0031 — Cookie ile katalog yönetimi yetkisi

Durum: Kabul edildi (Gün31).

## 1. İhtiyaç

Ders/dönem/açılış yazmaları yalnız öğrenci işleri görevine açılmalı; Student,
Instructor, Advisor ayrı üyelikler olarak temsil edilmeli. Public kayıt ayrıcalık
atamamalı ve cookie/JWT rollerinin kaynağı sunucu olmalı.

## 2. Önceki çözümün sınırı

Identity hesapları ve rol tabloları vardı fakat rol servisleri/policy yoktu; katalog
POST/PUT public idi. Kimlik doğrulaması tek başına görev yetkisini sağlamıyordu.
Cookie'deki rolleri JWT'ye kopyalamak üyelik değişikliklerini yeni token'a taşımazdı.

## 3. Seçim ve kazanç

AddRoles ile yerleşik principal üretimi, dört sabit rol, açık Development bakım
komutu. ManageAcademicCatalog cookie-only ve StudentAffairs; dört yazmada yerleşik
CSRF filter. JWT üretimi güncel manager üyeliğini role claim'iyle taşır, bearer
RoleClaimType eşlenir. Mevcut tek üretim yolu refresh için de kullanılır; ek SaveChanges,
retry veya transaction katmanı yoktur. Okumalar public; kişisel akademik akışların
ownership modeli bu güne taşınmaz. Diğer rol politikaları yalnız test hostundadır.

## 4. Maliyet ve sınırlar

Katalog yazma HTTP sözleşmesi bilinçli değişir. Eski testler yetkili doğrulanmış hesap,
HTTPS cookie jar ve gerçek CSRF almak zorundadır. Test process fixture'ı sertifikayı
sabitler; authorization bypass eklenmez. JWT üretiminde bir rol sorgusu eklenir.
Rol tabloları zaten olduğundan migration yoktur. Rol değişikliği anlık cookie/JWT
revocation sağlamaz; yeni login/token günceldir. Sabit cookie20dk/stamp30dk politikası
korunur. Yerel maintenance Production provisioning değildir; eşzamanlı rol hazırlama
yarışı için otomatik retry yoktur. Başarısız IdentityResult komutu başarısız bitirir.

Alternatif genel permission motoru/dynamic provider bu dört görev için bakım maliyeti
getirirdi; seçilmedi. Bearer'ı aynı yönetim policy'sine eklemek mevcut cookie-only
sınırını genişletirdi; seçilmedi. Roller öğrenci sahipliği/danışman ataması yerine
geçmez. 32. gün resource-based authorization ve IDOR kontrolleri gerekir.
