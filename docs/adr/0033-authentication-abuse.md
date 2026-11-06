# ADR 0033 — Login ve kurtarma taleplerini sınırlama

## İhtiyaç
Herkese açık login ve forgot-password pahalı hash/DB/token/bildirim işi başlatıyor.
Ortak HTTP retleri tek başına istek yoğunluğunu ve olmayan hesapta atlanan hash
maliyetini karşılamıyor.

## Mevcut çözümün sınırı
Identity lockout5/5m hesap odaklıdır; farklı e-posta ve bilinmeyen hesap taleplerini
IP kotasıyla sınırlamaz. SignInManager olmayan kullanıcıyı hash doğrulamadan reddeder.
Mevcut CSRF, cookie ve kaynak yetkileri bu ayrı maliyet sorununu çözmez.

## Karar ve kazanç
Mevcut lockout eşiğini koruyoruz. Login10/60s, forgot3/900s ayrı IP fixed window,
kuyruksuz 429 ve metadata varsa Retry-After kullanıyoruz. IP yalnız Connection'dan;
forwarded header güveni genişletilmiyor. Gerçek kullanıcı Identity'de kalır, olmayan
hesap yapılandırılmış hasher ve bir kez üretilmiş dummy hash yolundan geçer.
Sonuç her zaman ortak 401'dir; DB arızası 500 kalır. Genel auth wrapper yoktur.

## Maliyet ve alternatifler
Dummy doğrulama CPU tüketir ve tam timing eşitliği sağlamaz. Ortak IP kullanıcıları
birbirini sınırlayabilir; fixed-window sınırında yoğunluk olabilir. Dağıtık limiter,
hesap başına ikinci kota, sliding/token bucket, CAPTCHA ve gateway bugün eklenmedi.
Süreç belleği yeniden başlatma ve çok instance ortak kotası sağlamaz. Queue=0 pahalı
işi sonra çalıştırmak yerine açık ret verir. Lockout saldırganın başka hesabı
kilitlemesine de yol açabilir. Debug olayları sır taşımaz; kapsamlı observability
altyapısı kurulmadı. Şema/migration veya paket değişikliği yoktur.

Gerçek düşük kota testi ayrı hostta, eski regresyonlar açık yüksek test kotasında
çalışır; authorization/CSRF kapatılmaz. Hasher/provider sayaçları yalnız testte vardır.
