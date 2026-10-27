# 0001 — Küçük tek API ile başlangıç

- Durum: Kabul edildi (1. gün).
- İhtiyaç: Örnek ders listesini HTTP üzerinden sunmak.
- Alternatifler: Küçük tek API veya başlangıçtan çok katmanlı yapı.
- Seçim: Tek ASP.NET Core Minimal API; `Program.cs` içinde sabit anonim nesneler
  ve `GET /courses`. Ek servis, interface veya repository yok.
- Kazanç: HTTP isteğinden JSON yanıtına kadar istek akışının kolay izlenmesi.
- Maliyet/sınır: Kalıcılık ve büyüyen iş kuralları için ileride düzenleme gerekecek.
