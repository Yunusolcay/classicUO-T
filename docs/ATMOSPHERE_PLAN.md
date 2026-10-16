# Bölge atmosferi planı

Hedef: oyuncunun bulunduğu yerin türüne göre (zindan, kar, çöl, orman/jungle, asit bataklık, volkanik, void)
dünyaya hafif renk tonu ve pus; sunucuya ve haritaya dokunmadan. Ayar: Options → Video → Misc → "Region
atmosphere" (`Profile.RegionAtmosphere`, varsayılan açık).

## Uygulama (`AtmosphereRenderer`)

- **Yer tespiti:** 500 ms'de bir oyuncunun çevresindeki 7×7 örnek karede (yarıçap 6, 2'şer adım) land tile'ın
  tiledata adına bakılır (`decimated_` türevleri dahil): `snow` → kar, `cave` → zindan, `lava`/`obsidian` → volkanik,
  `acid` → bataklık, `jungle` → jungle, `void*` → void, `sand` (`sand stone` hariç) → çöl, diğerleri nötr. Felucca/
  Trammel'de X ≥ 5120 (zindan bölgesi) doğrudan zindan. Görünüm, türlerin örneklerdeki payıyla karışır.
- **Görünüm:** tür başına çarpımsal renk tonu (ör. zindan serin mavi-yeşil, çöl sıcak, volkanik kızıl) + düşük
  opaklıklı pus rengi (%5–12). Değerler 3 sn'de yumuşakça hedefe gider.
- **Çizim:** dünya çiziminin sonunda, havadan önce, ışık kompozitinden önce (gece kararması pusu da karartır):
  görünür dünya dikdörtgenine tam ekran ton (çarpma blend) ve pus (alfa blend). Renkler her kare 1×1 dokuya yazılır
  (renk başına doku önbelleği sürekli değişen renkte sınırsız büyürdü).

## Kabul

- Zindana girince serin, karanlık bir hava; karlı bölgede hafif beyaz pus; çölde sıcak ton; geçişler yumuşak.
- Şehir/çayır gibi nötr yerlerde görüntü değişmiyor. Ayar kapalıyken görüntü eskisi.
