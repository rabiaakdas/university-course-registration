# ADR 0043 — Açılış koordinasyonunda yer açma ve yerleştirme

## İhtiyaç ve önceki sınır
Açılan yeri ayrı bir işlemle adaylara vermek sayaç/başvuru/kayıt arasında kısmi
sonuç ve sıra atlama doğurabilir. Önceki waitlist katılım kontrolü de atomik değildi.

## Seçim ve kazanç
Mevcut açılış satırı DB kilidi bütün ilgili yazarların ortak koordinasyonudur.
Drop/reject ve private yerleştirme aynı transaction; koşullu durum yazmaları,
sayaç ve başvuru kapanışları beraber commit/rollback. Waiting normal submit'i
engeller. Adaylar sayfalı ve toplu akademik veriyle mevcut evaluator'dan geçer.
Uygun Draft yeniden kullanılır; Rejected/Dropped yeniden açılmaz.

## Alternatif, maliyet ve sınır
Asenkron job kısa transaction sağlayabilir fakat kalıcı teslim/ara durum ve
toparlama tasarımı gerektirir; bugün eklenmez. In-process lock çok süreç için
yeterli değildir. Genel kilit/retry framework yerine mevcut SQL Server açılış
koordinasyonu kullanılır. Uzun uygunsuz aday listesi kilit süresini uzatır;
sayfalama toplam işlem süresini sınırlamaz. Farklı açılış akademik yarışları ve
yetki TOCTOU sürer. Mevcut boş yer/Waiting periyodik toparlama kapsam dışıdır.
