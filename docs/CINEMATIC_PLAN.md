# Sinematik görünüm planı

Hedef: oyunu tek bakışta "farklı bir oyun" gibi gösteren son işlem geçişi: film tonu, renk ayrımı, yerel kontrast
ve vinyet. Sunucuya, haritaya ve oynanışa dokunmaz. Ayar: Options → Video → "Cinematic look"
(`Profile.CinematicLook`), varsayılan açık.

Not (2026-10-17): "Cinematic depth" (tilt-shift bulanıklığı ve uzak pus) kullanıcı beğenmediği için kaldırıldı;
ayar, shader parametreleri (`DepthAmount`, `Daylight`) ve kod silindi. Eski profillerdeki `cinematic_depth`
anahtarı yok sayılır.

## Uygulama

- **Sıra:** dünya → gelişmiş ışık → bloom → Modern look → **sinematik** (`GameScene.Draw`). Sinematik açıkken önceki
  geçişler kendi kaynak hedefine (`CinematicRenderer.GetSourceTarget`) yazar, son geçiş dünya hedefine yazar.
- **Yumuşak kopya:** görüntü yarı boyuta indirilir (`Down`), iki kez yatay+dikey 9 dokulu gauss (`BlurH`/`BlurV`).
- **Kompozit** (`shaders/Cinematic.fx`): yumuşak kopya s0'da doğrusal gerilir, keskin görüntü s3'te 1:1 okunur
  (batcher s3'ü nokta örnekler; s1/s2 hue tabloları, dokunulmaz).
  - Yerel kontrast: `c + (c − yumuşak) × 0.35`.
  - Film tonu: orta tonlar kaldırılır (`pow 0.78`), hafif S-eğrisi; gölgeler camgöbeği, parlak yerler kehribar;
    doygunluk artışı (soluk renkler daha çok); oyuncu göğsü (`FocusY`) çevresinde vinyet.
- Değerler son ekran görüntüsü üzerinde Python simülasyonuyla seçildi; oyun içi kabulden sonra ince ayar yapılır.

## Kabul

- Gündüz şehirde renkler daha canlı ve sıcak, görüntü baştan sona keskin.
- Arayüz, yazılar ve isimler etkilenmez (yalnız dünya görüntüsü). Ayarlar kapalıyken görüntü eskisi.
- FPS kaybı küçük (yarı boyutta 5 geçiş + 1 tam ekran kompozit).
