# ADR 0052 — Geliştirmede API ve ayrı SQL Server Compose projesi

- İhtiyaç: Linux API'nin LocalDB yerine ağ üzerinden gerçek SQL Server'a
  bağlanması ve container ömründen bağımsız DB verisi.
- Mevcut sınır/alternatif: dışarıdan yönetilen SQL Server kullanılabilir;
  bağlantı/izin/TLS kurulumunu başka ortamda ayrıca yapmak gerekir. Gün51
  image'i tek başına DB veya kalıcı veri sağlamaz.
- Seçim: iki servis, varsayılan DNS/ağ, DB named volume; host DB portu yok.
  Host SDK ile açık migration SQL üretimi, container sqlcmd ile kurulum;
  ayrı SA kurulum ve tablo/sequence izinli runtime hesabı. API startup'ında
  migration veya otomatik seed yok. Normal Down veriyi korur.
- Maliyet: SQL Server'ın bellek/disk/image maliyeti, x64 gereksinimi, ayrı
  parola/lisans ve volume yaşam döngüsü yönetimi. Env bir secret vault değil;
  yerel self-signed DB TLS istisnası production çözümü değildir. Gerçek
  Compose doğrulaması daemon engeli nedeniyle henüz tamamlanmadı.
