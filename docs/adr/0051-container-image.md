# ADR 0051 — Multi-stage Linux image

- İhtiyaç: tek ASP.NET Core uygulamasını kaynak ve SDK dağıtmadan paketlemek.
- Mevcut sınır: doğrudan `dotnet publish` çıktısı taşınabilir; ancak hedefte
  uygun ASP.NET runtime'ın ayrıca kurulmasını gerektirir.
- Alternatif: doğrudan publish dağıtımı veya SDK içeren tek aşamalı image.
- Seçim: sabit SDK tag'i/global.json ile restore/publish, ayrı resmi ASP.NET
  runtime aşaması. Secret'ları context dışında tutan .dockerignore ve
  Production statik HTTP smoke betiği. Mevcut uygulama davranışı değişmez.
- Maliyet: iki base image'in indirilmesi/depolanması, cache alanı ve tag
  bakım ihtiyacı. Runtime güncellemeleri yeniden build gerektirir. Linux DB,
  HTTPS/key kalıcılığı ve işletim güvenliği ayrı işlerdir. Docker daemon
  erişilemediği için gerçek image doğrulaması henüz tamamlanmamıştır.
