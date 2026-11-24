# ADR 0049 — public katalog için ayrı keyset alternatifi

İhtiyaç: derin offset'in atlanan satır maliyetini azaltmak ve sınırlı parçalarla
ileri okumayı göstermek. Mevcut offset rastgele sayfa ve toplam sözleşmesi
nedeniyle kaldırılmamalı.

Seçim: ortak filtreyi kullanan ayrı Id/afterId route'u. pageSize+1 ile devam
bilgisi, yalnız mevcut liste DTO'su; toplam sorgusu yok. Cursor token altyapısı,
yeni index ve yeni yetkilendirme modeli eklenmez.

Kazanç: mevcut API bozulmadan ileri gezinme; deneyde56 yerine3 logical reads.
Maliyet: iki sayfalama sözleşmesi, değişen filtre/veri altında snapshot olmaması,
rastgele sayfaya doğrudan atlayamama. Kapsamlı cursor imzalama bugün gereksiz.

Rapor üretimde LINQ kalır. EF raw SQL/Dapper yalnız test derlemesinde karşılaştırılır;
SQL/DTO bakım maliyeti ve küçük ölçümün genellenememesi üretime geçişi gerekçelendirmez.
