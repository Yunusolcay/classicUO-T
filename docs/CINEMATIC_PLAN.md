# Sinematik görünüm planı

Hedef: oyunu tek bakışta "farklı bir oyun" gibi gösteren son işlem geçişi: film tonu, renk ayrımı, yerel kontrast,
tilt-shift alan derinliği, uzak pus ve vinyet. Sunucuya, haritaya ve oynanışa dokunmaz. Ayarlar: Options → Video →
"Cinematic look" (`Profile.CinematicLook`) ve "Cinematic depth" (`Profile.CinematicDepth`), ikisi de varsayılan açık.

## Uygulama

- **Sıra:** dünya → gelişmiş ışık → bloom → Modern look → **sinematik** (`GameScene.Draw`). Sinematik açıkken önceki
  geçişler kendi kaynak hedefine (`CinematicRenderer.GetSourceTarget`) yazar, son geçiş dünya hedefine yazar.
- **Yumuşak kopya:** görüntü yarı boyuta indirilir (`Down`), iki kez yatay+dikey 9 dokulu gauss (`BlurH`/`BlurV`).
- **Kompozit** (`shaders/Cinematic.fx`): yumuşak kopya s0'da doğrusal gerilir, keskin görüntü s3'te 1:1 okunur
  (batcher s3'ü nokta örnekler; s1/s2 hue tabloları, dokunulmaz).
  - Tilt-shift: oyuncunun göğsünden geçen keskin bant (`FocusY`), üst/alt kenara doğru en çok %85 bulanık.
  - Yerel kontrast: keskin bölgede `c + (c − yumuşak) × 0.35`.
  - Uzak pus: ekranın üstü (izometrik görünümün uzak tarafı) gündüz açık mavi, gece koyu pusa yarı yarıya karışır.
  - Film tonu: orta tonlar kaldırılır (`pow 0.78`), hafif S-eğrisi; gölgeler camgöbeği, parlak yerler kehribar;
    doygunluk artışı (soluk renkler daha çok); oyuncu çevresinde vinyet.
- Gelişmiş ışık kapalıyken gece kararması bu görüntüden sonra uygulandığı için pus gündüz renginde kalır.
- Değerler son ekran görüntüsü üzerinde Python simülasyonuyla seçildi; oyun içi kabulden sonra ince ayar yapılır.

## Kabul

- Gündüz şehirde renkler daha canlı ve sıcak, oyuncu çevresi keskin, ekranın üst/alt kenarları yumuşak.
- Arayüz, yazılar ve isimler etkilenmez (yalnız dünya görüntüsü). Ayarlar kapalıyken görüntü eskisi.
- FPS kaybı küçük (yarı boyutta 5 geçiş + 1 tam ekran kompozit).
