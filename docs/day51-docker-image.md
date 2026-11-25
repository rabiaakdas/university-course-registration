# Gün 51 — Linux image ve build context

## Paketleme

`Dockerfile`: resmi `mcr.microsoft.com/dotnet/sdk:10.0.401` ile restore ve
Release publish; final aşamada `mcr.microsoft.com/dotnet/aspnet:10.0.14` ve
yalnız publish çıktısı. `global.json` içindeki `latestPatch` değişmedi;
10.0.401 SDK feature band'i gereklidir. Tag'lerin çekilmesi bu ortamda henüz
doğrulanmadı; bulunamayan tag için SDK politikasını gevşetmek doğru değildir.

Image katmanlardan oluşan paket, container o paketten başlatılmış süreç ve
yazılabilir katmandır. Registry image dağıtır; bugün registry push yok.
Multi-stage build, SDK ve kaynak ağacını final runtime'dan ayırır.
`RUN` build sırasında, exec-form `ENTRYPOINT` container başlatılırken çalışır.
`EXPOSE 8080` metadata'dır; port yayınlamaz. Dinleme portunu
`ASPNETCORE_HTTP_PORTS=8080`, host erişimini `-p 127.0.0.1:18080:8080` belirler.

Build context repo köküdür. `.dockerignore` Git/IDE, bin/obj, scripts/docs/sql,
yerel hesap kutusu/anahtarlar, sertifikalar, DB/yedek ve geçici çıktıları context
dışında bırakır. `wwwroot`, kaynaklar, csproj, global.json ve paylaşılan
appsettings korunur. Secret'lar bu dosyalara eklenmemelidir; COPY sonrası silme
ve secret ARG/ENV kullanılmaz. Gelecekte NuGet.config, Directory.Build.props,
Directory.Packages.props veya proje referansı eklenirse restore öncesi COPY
girdileri de güncellenmelidir. Bugün csproj tek projedir.

Önce csproj/global.json kopyalandığından yalnız kaynak değişiklikleri restore
katmanını geçersiz kılmak zorunda değildir. Sonraki COPY/publish değişebilir.
Cache bir doğruluk veya bağımlılık güncelliği garantisi değildir.

## Tekrarlanabilir doğrulama

Repo kökünde:

```powershell
dotnet restore UniversityRegistration.csproj
dotnet build UniversityRegistration.csproj -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-container-image.ps1
```

Betik Linux daemon'ını kontrol eder, benzersiz tag ile iki değişikliksiz build
alır; ikinci build'de restore adımının kendi `CACHED` sonucunu arar. Benzersiz
container, boş seçilen loopback portu ve Production ortamını kullanır. Portun
seçimiyle bind arasında başka süreç portu alırsa koşu başarısız olur; başka
süreç kapatılmaz. `/`, `/site.js`, `/site.css` HTTP durum/içerik kontrolleri,
image entrypoint/port, publish dosyaları, `dotnet --list-runtimes` ve boş
`--list-sdks` kontrol edilir. Container içinde shell bulunduğu varsayılmaz.
Başlangıç beklemesi sınırlıdır; hata exit1 üretir. Finally yalnız kendi
container/tag/geçici dizinini kaldırır; paylaşılan build cache'ine dokunmaz.

## Gerçek sonuçlar — 2026-11-25

- Yerel restore başarılı; Release build: **0 uyarı, 0 hata**.
- PowerShell parser kontrolü başarılı.
- Docker CLI 29.7.2, context `desktop-linux`; `docker version` / `docker info`
  Linux engine named pipe bulunamadığı için başarısız.
- `container-image` betiği aynı önkoşulda **exit1** verdi. Image build/pull,
  ikinci build/cache, final image incelemesi, container başlangıcı ve HTTP
  kontrolleri **çalıştırılmadı**. Kodunun hazır olması geçtiği anlamına gelmez.
- Kaynak değiştirilen geçici context cache deneyi yapılmadı.
- Docker kaynakları/geçici test dosyaları oluşturulmadı. Uygulama/lab DB'sine
  erişilmedi. C# değişmediğinden full/HTTP/DB regresyonu tekrarlanmadı;
  migration oluşturulmadı veya uygulanmadı.

## Sınırlar

`Endpoints/WebClientSetup.cs` yalnız wwwroot'u sunuyor; Program başlangıcı
DB'ye bağlanmıyor. Statik HTTP başarısı DB sağlığı değildir. Repo ayarındaki
Windows LocalDB Linux container'da çalışmaz; DB akışı gün 52'ye aittir.
HTTP denemesi giriş testi değildir: Secure cookie/CSRF/authorization ve
Production hata davranışı değiştirilmedi; gerçek hesap akışı HTTPS gerektirir.
Development'a geçilmez, yerel outbox/OIDC test sağlayıcısı açılmaz.

Data Protection'ın mevcut container içi dosya davranışı korunur; kalıcı key
volume'u ve TLS dağıtımı kurulmadı. Container silindiğinde bu katmandaki
anahtarlar da kaybolabilir. Bu image production işletim çözümünün tamamı
değildir. Non-root/shutdown iyileştirmesi gün 53; Compose, SQL container,
health check, registry/CI ve otomatik migration bu çalışmada yok.

Kaynaklar: [Microsoft .NET container'ları](https://learn.microsoft.com/en-us/dotnet/core/docker/),
[8080 port davranışı](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port),
[Docker build context](https://docs.docker.com/build/concepts/context/),
[cache düzeni](https://docs.docker.com/build/cache/optimize/).
