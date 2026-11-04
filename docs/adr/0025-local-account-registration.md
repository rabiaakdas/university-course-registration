# 0025 — Identity hesabı ve yerel confirmation kutusu

1. **İhtiyaç:** E-posta/parola ile doğrulanmamış hesap oluşturmak, Identity token'ıyla
   doğrulamak. 24. gün yalnız hesap modeli ve öğrenci bağlantısını sağlıyordu.
2. **Alternatif:** Özel parola hash'i/token sistemi veya Identity hazır endpoint
   ailesi yerine mevcut UserManager/store ve iki küçük endpoint seçildi. İlki
   güvenlik bakımını büyütür, ikincisi bugün istenmeyen login/token akışlarını açardı.
3. **Seçim/kazanç:** E-posta kullanıcı adıdır; yalnız Trim, Identity normalizer ve
   mevcut UserNameIndex korunur. RequireUniqueEmail açık, EmailIndex unique değil.
   Uzunluk odaklı 12–128 parola; harf/sayı/sembol bileşimi zorunlu tutulmaz.
   Default DataProtector sağlayıcısı iki saat, açık token kontrolü ve dar hata eşleme.
   Küçük sender sözleşmesi yalnız açık Development dosya kutusuna yazar; başka
   ortamda yanlış etkinleştirme başlangıçta, kapalı register ise yazmadan503 ile reddedilir.
4. **Maliyet/sınır:** Hesap commit'i ve dosya yazma atomik değildir. 201 cevabı
   queued-locally/failed/cancelled ayrımıyla hesabın oluştuğunu dürüstçe bildirir;
   kalıcı resend/recovery sonra tasarlanmalıdır. Tekrar hesapta400 ve başarıda201
   hesap varlığını ayırt ettirebilir; tam enumeration koruması değildir.

Onay adresi Host'tan alınmaz, güvenilir config'ten gelir. HTTPS beklenir, geliştirmede
yalnız loopback HTTP istisnası vardır. Sabit uygulama adı + yerel kalıcı key ring
restart'ı destekler; Windows DPAPI kullanılır. Dosyalar Git ve wwwroot dışındadır.
Token'ın süreli oluşu tek kullanımlılık değildir; provider davranışı test edilir.
Küçük mevcut SQL sınıflandırıcı genişletildi; genel sonuç/retry/transaction sistemi,
e-posta SDK'sı veya yeni paket eklenmedi. Login, öğrenci bağlama ve yetkilendirme
ayrı sorumluluklardır; akademik akışa veya şemaya değişiklik yapılmadı.
