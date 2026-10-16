# Bloom planı (M5)

Hedef: meşale, ateş, lav, büyü efektleri ve (Enhanced lighting ile) ışık alan bölgelerin çevresinde yumuşak parlama;
gündüz kum/kar gibi parlak zeminlerin yanmaması. Ayar: Options → Video → Misc → "Bloom" (varsayılan açık).

## 1. Önceki durum

Modern look'ta küçük bir "glow" vardı: piksel başına 12 örnekle luma > 0.62 olan komşuları ekler (yarıçap 3–7 px).
Ayrı bir bloom zinciri yoktu; parlama dar, çözünürlüğe bağlı ve ışıklı sahnede (L15) kısılmıştı.

## 2. Uygulama

`BloomRenderer` (`Game/Lighting`), `LightingEffect`'in tekniklerini kullanır:

1. **BrightPass** (`Lighting.fx`): kaynak görüntü 1/4 boyuta, 4 lineer örnekle (≈ 4×4 kutu) indirilir; luma eşiğin
   altı yumuşak geçişle (eşik … eşik + 0.25) atılır.
2. **Blur**: 1/4 boyutta iki tur BlurH/BlurV (9 tap); geniş, ucuz yayılım.
3. **Kompozit**: bloom görüntüsü hedefe lineer büyütülerek **toplanır** (One/One), güç 0.6.

Sıra (`GameScene.Draw`): dünya → (Enhanced ışık kompoziti) → bloom hazırlığı → Modern look (bloom açıkken kendi
glow'u `GlowAmount = 0`) ve aynı bağlamada bloom eklenir. Modern look kapalıysa `Composite` kaynağı kopyalayıp bloom'u
ekler. Bloom açıkken dünya her zaman ara hedefe çizilir (bağlamak temizler, bkz. rendering.md).

Eşik: Enhanced lighting aktifken ortam ışığına göre gece 0.6 → gündüz 0.85; Enhanced kapalıyken gece karanlığı bu
görüntüden **sonra** uygulandığı için hep gündüz eşiği (0.85), yoksa karanlığın altında her parlak zemin parlardı.

## 3. Kabul

- Gece: meşale/mangal/büyü çevresinde yumuşak hâle; Enhanced lighting ile ışık alan duvarlar hafifçe parlıyor ama
  doku kaybolmuyor. Gündüz: yalnız çok parlak pikseller (alev, lav, kar parıltısı) hafif parlıyor.
- Modern look açık/kapalı, xBR zoom, pencere boyutu değişimi ile bozulma yok; Bloom kapalıyken görüntü eskisi
  (Modern look glow'u geri gelir).
- Maliyet: 1/4 çözünürlükte 5 tam ekran geçiş; FPS'te belirgin düşüş beklenmez (ölçüm `-lightdebug` ile).
