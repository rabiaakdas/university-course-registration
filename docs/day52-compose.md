# Gün 52 — Compose, ağ, volume ve yapılandırma

## Seçim ve kod konumları

- `compose.yaml`: api + db, varsayılan proje ağı, `sql-data:/var/opt/mssql`.
  SQL Server 2022 (`2022-latest`) Developer; yalnız Linux Intel/AMD x86-64
  destekli ortam hedeflenir. SQL_IMAGE dışarıdan değiştirilebilir. Latest
  değişebilir; bu koşuda çekilen digest yok, image uyumluluğu gerçek DB'de
  doğrulanmış sayılmaz. Mevcut migration'ların sequence, rowversion, CHECK,
  view ve kilit SQL'i SQL Server ailesindedir; yeni migration yok.
- `scripts/compose.ps1`: Init/Setup/Up/Logs/Stop/Down/Verify. Setup SDK'yı
  hostta kullanır, mevcut migration'lardan idempotent SQL çıkarır. Runtime
  image'ine SDK eklenmez. SQL `/opt/mssql-tools18/bin/sqlcmd` ile uygulanır;
  bu araç SQL Server 2022 CU14+ image'lerinde bulunur. Hazırlık başarısızsa
  betik API'yi başlatmaz. Önceden çalışan API'yi otomatik kapatmaz; şema
  yükseltirken önce Stop kullanmak yöneticinin sorumluluğundadır.
- `sql/app/compose-runtime-permissions.sql`: gün50'nin ayrı provisioning
  yaklaşımı genişletildi. API rolüne mevcut akademik/Identity tablolarında
  DML, rapor view'inde SELECT ve mevcut sequence'lerde UPDATE verilir.
  Şema/sunucu yönetimi, migration geçmişine yazma, db_owner/db_ddladmin yok.
  UPDLOCK/HOLDLOCK ilgili tablo erişim izinlerini kullanır. Bu tablo düzeyi
  rol uygulamanın bütün akışları için minimum izinlerin kanıtı değildir;
  Verify yalnız katalog yazma, sequence ve kilit örneklerini sınar.
- `compose.env.example`, `.gitignore`, `.dockerignore`: örnek sır içermez;
  üretilen yerel env ve migration geçici dosyası image/Git'e taşınmaz.

## Ağ ve yapılandırma

Compose DNS'te `db` servis adı çözülür; container localhost'u başka servis
anlamına gelmez. API bağlantısı `db,1433`, tarayıcı bağlantısı loopback host
portudur. Sabit IP yok. DB host portu yok; yerel dotnet-ef DB'ye bağlanmadan
SQL üretir, kurulum bunu Compose exec ile DB'ye aktarır. App/lab LocalDB'leri
hedeflenmez; ayrı sunucudaki DB adı `university_compose` sabittir.

.env interpolation, Compose YAML değerlerini doldurur. Dosyadaki bütün
anahtarlar container'a kendiliğinden aktarılmaz; `environment` listesi bunu
belirler. API'ye yalnız runtime parolası, DB'ye SA parolası geçer. Docker
yöneticileri environment değerlerini görebilir; bu bir secret vault değildir.
Betik parolayı CLI argümanı veya log yerine stdin/environment üzerinden taşır;
SQL ve başarısız native komutların ham çıktısı bastırılır, exit code korunur.

Named volume Docker tarafından yönetilir; container yeniden oluşturulunca
aynı volume veri dosyalarını tutar. Bind mount hosttaki açık bir yolu bağlar;
burada DB için kullanılmaz. Stop süreçleri durdurur, Down container/ağı siler
ama veriyi korur. Sadece Verify kendi benzersiz projesinde, başlangıçta kaynak
yokluğunu ve temizlikte volume etiketi/adını denetleyerek down --volumes yapar.
Başka image/container/volume veya genel cache temizliği yok.

İki güçlü parola Init'te oluşturulur. Setup tekrarında login parolası
resetlenmez; mevcut parolayla gerçek runtime bağlantısı başarısızsa durur.
Lisans kabulü Y olmalıdır; Developer production için lisans değildir.
TLS doğrulama istisnası yalnız izole yerel SQL bağlantısı içindir; şifreleme
korunur ama DB kimliği sertifikayla kanıtlanmaz. HTTPS/cookie güvenliği, JWT,
CSRF ve Production hata sözleşmesi değiştirilmedi. Kalıcı Data Protection
anahtarları/TLS işletimi ve kimlik doğrulamalı uçtan uca yazma henüz yok.

## Kontrol akışı ve gerçek sonuçlar (2026-11-27)

Verify: benzersiz proje/env/loopback portu → Linux/x64 kontrolü → DB gerçek
SELECT readiness → mevcut migration SQL'i → ayrı runtime kimliği → API build
ve başlangıç → bilinen ders/dönem/açılış verisi → public GET assertion'ı → DB
stop sırasında güvenli500 → aynı volume ile force-recreate → API yeniden okuma
→ ikinci setup → migration geçmişi, kimlikler ve ilişkiler korunmuş mu kontrolü.
Runtime örneği gerçek bağlantıda sequence/DML/UPDLOCK ve DDL reddini sınar;
EXECUTE AS ile sınırlı kimlik bağlantısı varmış gibi davranılmaz.

- Restore ve Release build başarılı: 0 uyarı/0 hata.
- PowerShell parser başarılı. Init gerçek geçici env üretti, Git ignore kontrolü
  geçti. Secret çıktısı vermeyen `docker compose config --quiet` başarılı.
  Aynı dosyaya ikinci Init beklenen exit1 ile reddedildi; içerik değişmedi.
- dotnet-ef10.0.14 restore ve has-pending-model-changes başarılı: model farkı yok.
  Mevcut idempotent migration SQL'i üretildi, geçmiş/view varlığı kontrol edildi;
  geçici SQL silindi. **DB'ye uygulanmadı.**
- Docker Compose v5.5.1 var; desktop-linux engine named pipe bulunamıyor.
  Verify önkoşulda exit1 verdi. Image pull/build, SQL readiness/migration,
  runtime DB izinleri, servis adıyla API→DB, volume kalıcılığı, ikinci DB
  uygulaması ve kesinti/geri dönüş testleri **çalıştırılmadı**.
- Üretim C#/model değişmedi; full/LocalDB regresyonu tekrarlanmadı.
  Test container/ağ/volume oluşmadı. Geçici env ve SQL dosyaları temizlendi;
  app/lab DB'sine dokunulmadı.

Testte HTTP public GET ve DB tarafı kontrollü yazma ayrıdır; HTTPS login,
CSRF'li yazma veya tüm uygulama akışları doğrulandı denmez. Sınırlı readiness
polling genel retry/dayanıklılık tasarımı değildir. Servisler arası kesinti ve
kalıcı veri deneyi başarılı olsa bile genel kurtarma garantisi sağlamaz.

Komutlar README gün52 bölümünde. Sıradaki gün53: health check/non-root/shutdown
ve işletim iyileştirmeleri.

Kaynaklar: [Microsoft SQL container kurulumu/araç/mimari](https://learn.microsoft.com/sql/linux/install-upgrade/quickstart-install-docker),
[Compose interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/).
