# Su ve hava durumu planı (M3)

Hedef: UO haritasının büyük kısmını oluşturan su ve sunucunun gönderdiği hava durumu (`0x65`) canlı görünsün;
oynanış, seçim, yol bulma ve protokol değişmeden. Her özellik ayardan kapatılabilir; kapalıyken görüntü eskisi.

## 1. Mevcut durum

- **Su:** su karoları `TileData.IsWet` (land ve static). Su land karoları her zaman düz 44×44 art quad'dır
  (`Land.Create` ilk `IsStretched` bayrağını `ApplyStretch` sıfırlar). Upstream "Animated water effect"
  (`Profile.AnimatedWaterEffect`, varsayılan kapalı): karo bir kez çizilip üstüne %10 büyütülmüş, sin/cos ile
  sallanan ikinci bir kopya çizilir (`LandView`, `View.DrawStatic*`); açıkken su karoları chunk mesh'ten çıkarılır.
  Kaba görünür, kıyı yok, karo dikişleri oynar.
- **Hava durumu:** `0x65` → `Weather.Generate(type, count, temp)`. En fazla 70 parçacık; yağmur 1 px çizgi
  (`DrawLine`), kar küçük dikdörtgen, rüzgâr `Wind`. Fırtına türleri yağmur/kar gibi çizilir; şimşek, sis,
  karartma yok. Dünya hedefinde, ışık kompozitinden önce çizilir (gece kararır).

## 2. Görevler

| ID | Görev | Durum |
|---|---|---|
| W1 | Su shader'ı: hareketli ışık deseni, parıltı, kıyı köpüğü | uygulandı (aşağıda) |
| W2 | Gökyüzü/ortam rengi yansıması: suya ortam ışığı tonuna göre hafif renk (gece ay ışığı, gün mavi) | uygulandı (W1 içinde) |
| H1 | Yağmur: çok katmanlı, yumuşak damla izleri, zemine sıçrama halkaları, yağmurda hafif karartma/serinlik | uygulandı |
| H2 | Kar: yumuşak yuvarlak taneler, boyut/derinlik çeşitliliği, salınım, hafif beyaz pus | uygulandı |
| H3 | Fırtına: ara ara şimşek parlaması (ekran aydınlanması), rüzgârla eğik yağmur | uygulandı |
| H4 | Ayar ve ölçüm: "Enhanced weather" + yoğunluk; `-lightdebug`'a parçacık sayısı | uygulandı |

## 3. W1 – Su shader'ı

Mevcut "Animated water effect" ayarı yeniden kullanılır (Options → Video → Misc): eski sallanan kopya kaldırılır,
yerine shader modu `WATER` (13) gelir. Pikseller yerinden oynamaz (atlas'ta sprite'lar boşluksuz paketli,
UV bozması dikiş ve sızma yapar); hareket yalnız renk/parlaklık deseniyle verilir:

- **Dünya konumu:** vertex shader `WorldPos = (pozisyon × WorldMatrix) + WorldOrigin` üretir. Sprite'larda
  pozisyon kamera ofsetine göreli, mesh'te mutlak (`SetWorldOffset`); `WorldOrigin` = sahnenin çizim ofseti, ikisinde
  de mutlak dünya pikseli verir. Desen dünya pikseline yuvarlanır (`floor`), kamera kayınca yüzmez, piksel sanatı keskin.
- **Desen:** üç sinüs dalgasının toplamı parlaklığı ±%7 dalgalandırır; dalgaların tepe çakışmalarında seyrek, küçük
  parıltılar (smoothstep eşiği) oluşur. `Time` (rüzgâr ile aynı saat) ile akar.
- **Kıyı köpüğü:** chunk mesh kurulurken her su land karosu için dört komşudan (NE `(x, y−1)`, NW `(x−1, y)`,
  SE `(x+1, y)`, SW `(x, y+1)`) su olmayanlar 4 bitlik maskeye yazılır (`Hue.X`); vertex'lere karo içi koordinat
  (`Normal.xy`, köşeler 0/1) verilir. Shader elmas kenarına uzaklığı hesaplar, maskeli kenarlarda ~%25 genişliğinde,
  pikselli gürültüyle kırılan, nefes alan beyaz köpük bandı çizer.
- **Ortam tonu (W2):** suya `AmbientTint` (Enhanced lighting'in ortam rengi, yoksa beyaz) ile çok hafif renk karışır.
- Hue'lu su karoları (nadir) dokunulmadan eski modda çizilir. Statik su karoları (`IsWet` statics) da `WATER` modunu
  alır (köpük maskesi 0). Ayar değişince chunk mesh yeniden kurulur (`BuiltWithWaterEffect`).

## 4. H1–H4 – Gelişmiş hava durumu

`Weather` sunucu kurallarını (tür, sayı, süre, rüzgâr, sesler) aynen yürütür; `Profile.EnhancedWeather` açıkken
çizimi `WeatherRenderer` yapar (Options → Video → Misc: "Enhanced weather", "Lightning flashes"). Parçacıklar ekran
pikselinde tutulur ve kameranın görünen dünya dikdörtgenine (`Camera.ScreenToWorld`, zoom ölçeği) yerleştirilir:
zoom'da da ekranı doldurur, boyutları sabit kalır. Yoğunluk sunucu sayısıyla (0–70) orantılı, 3 sn'de açılıp kapanır.

- **H1 Yağmur:** pencere alanına göre en fazla 1800 damla. Damla, kuyruğu saydam başı parlak, hafif soğuk beyaz
  bir iz dokusudur (`CreateStreak`); yarı saydam (%30–80), hız 1000–1900 px/sn (±%15), iz uzunluğu hızla orantılı
  (hareket bulanıklığı, ~10–34 px). Her damlanın kendi eğimi var (sağa/sola ±0.15, kendi salınımıyla); genel eğim
  rüzgâr + yavaş esinti. Ekran ortasında (oyuncu) yakın damlalar seyrelir (`clearRadius`, kısa kenarın %24'ü).
  Damlalar ekrana göre tutulur ama kamera kayınca dünyayla birlikte kaydırılır (`FollowCamera`, mutlak konum =
  görünüm başlangıcı + `GameScene.DrawOffset`; yürürken/koşarken yağmur ekranla sürüklenmez, büyük sıçramada baştan
  dağıtılır). Uzak olmayan damlalar rastgele "zemin" yüksekliğinde biter; düştüğü yer su mu kara mı haritadan okunur
  (`Weather.IsWaterAt`: mutlak dünya pikseli → karo, oyuncu yüksekliğiyle başlayıp karo Z'siyle bir kez düzeltilir;
  land/static `IsWet`). Halkalar ve damlacıklar mutlak dünya pikselinde tutulur, yere sabit kalır. **Su:** sıçrama
  yok; arka arkaya açılan 2–3 geniş halka (~0.5–0.75 sn), yakın damlada ortadan dik zıplayan tek damlacık.
  **Kara:** kısa parlama, 3–4 dışa saçılan damlacık (~0.25 sn) ve küçük, kısa ıslak halka (≤ 1000). Parçacıklar
  doğrusal örneklemeyle çizilir. Gök: koyu mavi-gri katman (%14, fırtınada %24).
  Zaman: hava, iki çizim arasındaki gerçek süreyle ilerler (`Time.Ticks` farkı). `Time.Delta` son güncelleme
  adımıdır; FPS sınırında kare başına birkaç güncelleme çalıştığından parçacıklar ~8 kat yavaş akıyordu.
  `GameSceneAtmosphere` de `Time.Delta`'yı çizimde kullanıyor; aynı sorun orada da olabilir (ayrı iş).
- **H2 Kar:** en fazla 450 yumuşak yuvarlak tane, derinliğe göre boyut 2–5 px, sinüs salınımı + rüzgâr; %8 beyaz pus.
- **H3 Fırtına:** gök gürültüsü sesi çalındığında (`PlayThunder`) şimşek: hızlı çift çakma + 0.6 sn sönüm. Ekrana
  beyaz katman (%30'a kadar) ve Enhanced lighting'de ortam ışığına `Flash × 0.7` eklenir (gece de aydınlatır).
  "Lightning flashes" kapalıyken çakma yok (ses kalır).
- **H4:** `-lightdebug` FPS kutusunda hava parçacık sayısı. Ayarlar kapalıyken eski 70 parçacıklı çizim.

Not: "Animated water effect" yeni profillerde varsayılan açık (eski profiller kendi değerini korur).

## 5. Kabul

- Deniz/göl yüzeyi yavaşça "kıpırdıyor", ara ara küçük parıltılar; karo dikişi yok, kamera kayınca desen yüzmüyor.
- Kumsal/kıyı kenarında köpük bandı; açık denizde köpük yok. Gece su da kararıyor (ışık kompoziti).
- Ayar kapalıyken su eskisi gibi (düz). FPS'te belirgin düşüş yok (desen piksel başına birkaç sinüs).
- Yağmur ekranı dolduruyor, derinlik hissi var, yere sıçrıyor; kar yumuşak salınıyor; fırtınada şimşek çakıyor ve
  gece sahneyi bir an aydınlatıyor. Hava bitince parçacıklar yavaşça kayboluyor; sunucu mesajları ve sesler aynı.
- "Enhanced weather" kapalıyken eski görünüm; "Lightning flashes" kapalıyken çakma yok.
