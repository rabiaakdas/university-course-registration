# ADR 0042 — Ayrı, sıralı bekleme başvuruları

## İhtiyaç / önceki sınır
Dolu açılış için öğrenci talebini kaydetmek gerekir. Enrollment durumunu bu amaçla
kullanmak aktif yük/kontenjan ve mevcut durum geçişleriyle farklı anlamları karıştırır.

## Seçim / kazanç
Ayrı WaitlistEntry; SQL sequence ile sabit numara, Waiting için filtreli UNIQUE,
NO ACTION FK ve kapanış CHECK'leri. Mevcut cookie Student sahipliği ve CSRF
korunur. Pozisyon yalnız ilgili açılışın Waiting satırlarından SQL'de hesaplanır.
İptal koşullu UPDATE; tekrar iptal idempotent, yeniden katılım yeni numaradır.

## Alternatif / maliyet
MAX+1 eşzamanlılıkta güvenli değildir; JoinedAtUtc tek başına eşsiz sıra sağlamaz.
Sequence commit/tıklama sırası sağlamaz ve boşluk bırakır. Ek tablo/index ve
pozisyon COUNT maliyeti vardır; kapanmış kayıtlar için saklama/temizlik politikası
ileride gerekir. Enrollment/kontenjan kontrolü ile katılım henüz ortak
koordinasyonda değildir. Geniş lock framework veya yerleştirme işi eklenmez;
43. gün bu sınırı ele alacaktır.
