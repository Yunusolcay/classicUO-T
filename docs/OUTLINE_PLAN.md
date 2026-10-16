# Hedef/hover vurgusu planı

Hedef: kalabalıkta fareyle üstüne gelinen ve son hedeflenen karakter bir bakışta seçilebilsin; seçim, hedefleme
ve protokol aynı kalsın. Ayar: Options → Video → Misc → "Outline hovered and targeted characters"
(`Profile.MobileOutline`, varsayılan açık).

## Uygulama

- Kim: fare altındaki mobil (`SelectedObject.Object`) ve son hedef (`TargetManager.LastTargetInfo`); oyuncunun
  kendisi, gizli ve yok edilmiş mobiller hariç.
- Renk: mobilin notoriety rengi (`Notoriety.GetHue`: mavi masum, yeşil dost, gri, turuncu düşman, kırmızı katil…),
  shader `HUE_TEXT` modu ile tüm opak pikseller tek renk.
- Çizim (`MobileView.Draw`): mobil önce 12 kez tek renkli kopya olarak çizilir (1 px halkada 0.9/0.6, 2 px halkada
  0.3 opaklık), sonra normal hâli üstüne. Kopyalar `UltimaBatcher2D.HueOverride` (renk zorlama) ve
  `SpriteOffset` ile yapılır; bu sırada gölgeler/temas gölgesi atlanır, `GameScene.SuppressLights` ile mobilin
  ışıkları ikinci kez toplanmaz. Alt-piksel kamera kesri korunur.

## Kabul

- Hover edilen ve hedeflenen karakterin etrafında notoriety renginde ince, yumuşak kontur; oyuncunun kendisinde yok.
- Binekli, giyimli karakterlerde tüm katmanlar konturda; gölge/ışık iki kat olmuyor.
- Ayar kapalıyken görüntü eskisi; tıklama/hedefleme davranışı değişmemiş.
