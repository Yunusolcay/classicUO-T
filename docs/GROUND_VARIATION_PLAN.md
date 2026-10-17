# Zemin varyasyonu planı

Hedef: her land/texmap dokusu tek bir karo ve her tile'da aynen tekrar ettiği için zemin ızgara gibi görünür;
bunu resim yeniden çizilerek tamamen çözmek mümkün değil. Dünya konumuna göre yavaş değişen parlaklık, sıcaklık
ve doygunluk lekeleri ızgarayı kırar. HD paketten bağımsız çalışır (orijinal art'ta da).

## Uygulama (2026-10-17)

- **Mod:** düz (gerilmemiş) land artık `SHADER_LAND_FLAT` (14) ile çizilir; shader için `NONE` ile aynı, yalnız
  varyasyon alır. Gerilmiş land `LAND`/`LAND_COLOR`, çimenli land ve bitkiler `NATURE_GRASS(_LIT)` modlarında
  da uygulanır. Statikler (duvar, obje), su, yazı ve UI etkilenmez. Atandığı yerler: `LandView`, `ChunkMesh`,
  `GameSceneDrawingSorting.ApplyMeshHue`.
- **Shader** (`IsometricWorld.fx` `ground_variation`, dünya pikseli `WorldPos` ile, harita üstünde sabit):
  parlaklık ±%20 (260 ve 75 px ölçekli iki gürültü), sıcak/soğuk kayma (420 px), doygunluk ±%18 (180 px).
  Değerler V2 toprak dokusu üzerinde simülasyonla seçildi.
- **Ayar:** `GroundVariation` (varsayılan açık), Options → Video → "Ground variation". Dünya çiziminde
  `UltimaBatcher2D.SetGroundVariation`, sonunda 0.
- **Yan düzeltme:** `ApplyMeshHue` her karede chunk mesh'teki düz su karolarının `WATER` modunu ve kıyı maskesini
  `NONE` ile eziyordu (su ışık deseni ve köpük görünmüyordu); artık `WATER` modundaki land korunur.

## İlk oyun içi görüntü düzeltmeleri (2026-10-17)

- **Düz dikiş:** gürültü `water_hash` (büyük `sin`) kullanıyordu; dünya ölçekli hücrelerde derleyicinin `i + 1`'i
  katlaması komşu pikselde aynı hücreye farklı değer verdi, kaldırımda yatay sert bir mavi/kahverengi sınır
  oluştu. Artık `ground_hash`: hücre 256'ya sarılır, sinüssüz (frac tabanlı) hash.
- **Çimen tonu:** klasik çimen HD taş/toprak yanında çok bağırgan sarı-yeşil. Yeşili baskın texeller (g − max(r, b)
  0.03..0.15) %65'e kadar daha sakin bir zeytin yeşiline çekilir (doygunluk %60, hafif sıcak). Aynı ayara bağlı.

## Sonraki adımlar

- [ ] Oyun içi kabul: lekelerin boyutu/gücü; kum, çimen, kar ve zindan zeminlerinde.
- [ ] Gerekirse doku türüne göre güç (ör. karda daha az sıcaklık kayması).
