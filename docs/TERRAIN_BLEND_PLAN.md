# Zemin geçişleri planı

Hedef: farklı zemin dokularının (çim/toprak/kum/kaya…) karo sınırlarındaki sert elmas kenarlarını yumuşatmak;
harita, yürünebilirlik, seçim ve çizim sırası değişmeden. Ayar: Options → Video → Misc → "Soft terrain
transitions" (`Profile.TerrainBlending`, varsayılan açık).

## Fikir

Eğimli (stretched) land karoları texmap dokusunu bütün elmasa gerer (köşeler UV 0/1) ve texmap'ler döşenebilir.
Bu yüzden komşu karonun dokusu, bu karonun doku koordinatlarında **kesintisiz devam eder**: bir karonun üstüne,
komşusunun dokusuyla aynı köşe UV'leriyle çizilen bir kopya, ortak kenarda komşuya dikişsiz bağlanır. UV
bozması ya da atlas dışı örnekleme gerekmez (atlas'ta sprite'lar boşluksuz paketli).

## Uygulama

- **Mesh:** `ChunkMesh.LandBlend` katmanı. Her stretched, hue'suz, su olmayan land karosu için dört kenar komşusuna
  (NE `(x, y−1)`, NW `(x−1, y)`, SE `(x+1, y)`, SW `(x, y+1)`) bakılır; komşu da stretched ve farklı texmap
  (doku/UV) gösteriyorsa, karonun elması komşunun dokusuyla yazılır. Kenar numarası `Hue.X`'te, karo içi konum
  `Normal.xy`'de (üst 0,0 · sağ 1,0 · sol 0,1 · alt 1,1), karonun köşe ışığı (`get_light` ile aynı, Brightlight
  öncesi) `Hue.Z`'de. Girişler dokuya göre sıralanır (doku başına bir çizim).
- **Shader** (`TERRAIN_BLEND` = 14): kenara uzaklık (`v`, `u`, `1−u`, `1−v`), 3 piksellik bloklarla gürültü;
  saydamlık kenarda 0.6, karonun %38 içine doğru sıfıra iner. Renk karonun eğim ışığıyla çarpılır (Brightlight
  kontrastı shader'da), yani gölgeli yamaçta da uyumlu.
- **Çizim:** `RenderLists`'te land mesh'inden hemen sonra, derinlik yalnız okunur (kendi karosunun zemin
  derinliği): yollar, statikler, eşyalar, karakterler üstünü eskisi gibi örter. Her iki taraf birbirine karıştığı
  için geçiş simetrik.
- Ölü siyah-beyaz görünümde katman çizilmez (renkli kalırdı). Ayar değişince chunk mesh yeniden kurulur.

## Sınırlar

- Yalnız texmap'li (eğimli/doku) zemin; düz art karolar (kasaba döşemeleri vb.) ve su dahil değil.
- Komşu chunk henüz yüklenmeden kurulan mesh'te chunk kenarındaki geçiş eksik kalabilir (chunk yeniden kurulunca düzelir).
- Menzil dışı gri gösterimde geçiş şeridi renkli kalır (yalnız kenarlarda, küçük fark).

## Kabul

- Çim–toprak, kum–çim, kaya–çim gibi sınırlarda sert elmas kenarı yerine 10–15 px'lik doğal, pikselli geçiş.
- Dikiş/çizgi yok; yamaçlarda geçiş zeminle aynı ışıkta. Yollar/evler/eşyalar geçişin üstünde.
- Ayar kapalıyken görüntü eskisi; FPS'te belirgin düşüş yok (karo başına en fazla 4 ek quad, doku başına bir çizim).
