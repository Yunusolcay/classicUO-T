# Kamera ve zoom planı (M4)

Hedef: yürürken ve zoom yaparken görüntünün akıcı olması; oynanış, seçim ve protokol değişmeden. Her özellik
Options → Video → Zoom bölümünden kapatılabilir.

## 1. Mevcut durum

- Kamera oyuncuyu **tam dünya pikseli** adımlarıyla izler: `GetViewPort` oyuncunun yumuşak hareket ofsetini
  `(int)` ile keser (`winGameCenterX -= (int)Player.Offset.X`). Zoom 1'de fark edilmez; yakınlaştırınca (ör. 2×)
  bir dünya pikseli 2 ekran pikseli olur ve dünya yürürken 2'şer piksel, düzensiz aralıklarla atlar (takılma hissi).
- Zoom `ZoomStep` (0.1) adımlarıyla **anında** değişir (`Camera.Zoom`, `CalculateLerpZoom` lerp yapmıyordu).
- classicUO-T: smooth movement (adım ritmi), xBR zoom filtresi (≥ 1.25× büyütmede dünya 1:1 çizilip büyütülür).

## 2. C1 – Yumuşak zoom (uygulandı)

`Camera.ZoomIn/ZoomOut` hedef zoom'u değiştirir; gösterilen zoom hedefe üstel yaklaşır (saniyede 14, yaklaşık
150 ms), FPS'ten bağımsız (`exp(−14·dt)`). Art arda tekerlek adımları hedefte toplanır. `Zoom = x` (varsayılan
zoom'a dönme, Options kaydırıcısı, makro "reset") hâlâ anında atlar. Kapanış kaydı hedef zoom'u yazar.
Ayar: `SmoothZoom` (varsayılan açık), "Smooth zoom".

Not: xBR filtresi ≥ 1.25× büyütmede devreye girdiği için zoom animasyonu bu eşiği geçerken filtre bir karede açılır.

## 3. C2 – Alt-piksel kamera (uygulandı)

`GetViewPort`, kesilen kesri (`_cameraSubPixel`, −1..1 dünya pikseli) saklar. `GameScene.Draw` dünya matrisinin
önüne `Translation(−kesir)` ekler; oyuncu `UltimaBatcher2D.SpriteOffset = +kesir` ile çizilir (`MobileView.Draw`
sarmalayıcısı; sprite, gölge, oturma çizimi ve temas gölgesi bu ofseti alır). Sonuç: oyuncu ekran merkezinde sabit,
dünya ekran pikseli hassasiyetinde kayar. Point sampling tüm dünyayı aynı kesirle kaydırdığı için bulanıklık olmaz.

Koşullar: büyütme > 1.01× (altında kazanç yok), xBR dünya filtresi kapalı (dünya 1:1 çizildiği için kesir
kaybolur; bu durumda eski davranış). Seçim/mouse hesapları tam piksel kalır (< 1 dünya pikseli fark).
Ayar: `SubPixelCamera` (varsayılan açık), "Smooth camera scrolling when zoomed in".

## 4. Sonraki adımlar

| ID | Görev | Not |
|---|---|---|
| C3 | Diğer mobillerin alt-piksel çizimi | şimdilik yalnız oyuncu; başka karakterler dünyaya göre tam piksel adımlar |
| C4 | xBR yolunda alt-piksel | büyütme adımında kesir × ölçek kadar kaydırma |
| C5 | Zoom'u fare noktasına göre | tekerlekte imlecin altındaki nokta sabit kalsın (opsiyonel) |

## 5. Kabul

- 2× civarı zoom'da yürürken/koşarken dünya akıcı kayıyor, oyuncu titremiyor; zoom 1'de görüntü eskisiyle aynı.
- Ctrl+tekerlek zoom'u yumuşak geçiyor; hızlı tekerlekte adımlar birikiyor; ayarlar kapatılınca eski davranış.
- xBR filtresi açıkken bozulma yok; tıklama/seçim doğru nesneyi buluyor.
