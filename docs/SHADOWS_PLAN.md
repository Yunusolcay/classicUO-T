# Gölge planı (M2)

Hedef: UO'nun sert, üst üste binince lekelenen gölgelerini yumuşak ve tutarlı hâle getirmek; oynanış, seçim,
çizim sırası ve performans değişmeden. Her adım ayarlardan kapatılabilir; kapalıyken görüntü birebir eski.

## 1. Mevcut durum (upstream + classicUO-T)

- Gölgeler `UltimaBatcher2D.DrawShadow` ile çizilir: sprite silüeti yarı yüksekliğe basılıp sağa eğilir
  (sabit "güneş" yönü), shader `SHADOW` modunda `rgb = 0`, alfa 0.4. Kenarlar point sampling ile sert.
- Mobil: her katman (gövde, giysi, binek) kendi gölgesini çizer (`MobileView`), üst üste binen yerler 0.4 + 0.4…
  ile daha koyu leke olur. Ağaç/kaya: `ShadowsStatics` ile `View.DrawStaticAnimated`, derinlik `+0.25`.
- Gölgeler dünya kuyruğunda derinlik testli çizilir ve derinlik yazar. Gece de gündüzkü kadar koyudur.
- Ayarlar: `ShadowsEnabled`, `ShadowsStatics`, `TerrainShadowsLevel` (zemin normal ışığı, `Brightlight`).

## 2. S1 – Yumuşak gölge geçişi (uygulandı)

Teknik: gölgeler ertelenir ve dünya çiziminden sonra tek seferde çizilir (`Batcher2D.Shadows.cs`):

1. Hedefin alfa kanalı 0'a çekilir (tam ekran, yalnız alfa yazımı).
2. Her gölge kapsamasını alfa kanalına **max** blend ile yazar; derinlik testi dünya derinliğine karşı
   (`LessEqual`, yazma yok). Öndeki nesneler gölgeyi eskisi gibi örter.
3. Renk `1 − alfa` ile çarpılır: üst üste binen gölgeler bir kez karartır (katman lekesi biter).
4. Alfa 1'e geri yazılır (sonraki xBR / ışık / Modern look / RenderTargets alfa'ya güvenmez ama temiz bırakılır).

Shader (`IsometricWorld.fx`, `SHADOW` modu, `Hue.x > 0` yumuşak gölge işaretidir): kapsama 9 örnekli diskte
ortalanır; yarıçap ayakta 0.6 px, gölgenin ucunda 2.2 px (temas noktası keskin, uç yumuşak). Atlas sprite'ları
boşluksuz paketlendiği için örnekler sprite sınırına kenetlenir: sınırlar vertex verisinde taşınır
(`Normal` = u min, v min, u max; `Hue.z` = v max). Ekran pikseli → doku adımı `ddx/ddy` ile dallanma dışında alınır.

Gece: gölgeler güneşten gelir; güç ortam ışığına (`IsometricLevel`) göre 0.85 → 0.35 arasında smoothstep ile
0.4'ten 0'a iner (`ShadowSettings`). Gündüz koyuluğu eskisiyle aynı (0.4).

Ayar: `SoftShadows` (varsayılan açık), Options → Shadows → "Soft shadows". Kapalıyken eski kuyruk içi çizim.

Sınırlar / riskler:
- Sprite çerçevesi dışına yumuşama taşamaz (quad sprite boyutunda); dar kırpılmış animasyon karelerinde yan
  kenarlar sert kalabilir. Gerekirse S2'de quad birkaç piksel genişletilir.
- Dünya hedefinin alfa kanalı geçici olarak maske olarak kullanılır; dünya çizimi sırasında alfa'ya bağımlı yeni
  bir efekt eklenirse bu sıraya dikkat edilmeli (bkz. rendering.md).
- Ertelenen gölgeler derinlik yazmaz: eskiden gölgenin arkasında kalan (daha uzak) bir nesne gölge pikselinde
  gizlenirdi, şimdi çizilip gölgeyle kararır (fiziksel olarak daha doğru).

## 3. S2–S5 (uygulandı)

- **Derinlik düzeltmesi (S1 hatası):** ertelenen gölge, gölgeyi düşüren nesneyle aynı derinlikte olduğundan
  `LessEqual` ile karakterin kendi bacaklarını da karartabiliyordu (kuyrukta gövde gölgenin üstüne çizildiği için
  eskiden görünmezdi). Gölge geçişi artık `Less`: aynı derinlikteki nesne (gölgenin sahibi, bineği, aynı karedeki
  eşyalar) kararmaz, daha uzaktaki zemin kararır.
- **S2 – Kenar payı:** gölge quad'ı sprite'tan 3 texel geniş (yanlar ve baş ucu; ayak çizgisi yerinde). Sprite
  dışındaki örnekler boş sayılır (`step` maskesi); yumuşama silüetin dışına taşar, yan kenarlar da yumuşar.
- **S3 – Yerel ışık gölgeleri:** güneş gölgesi söndükçe (`1 − güneş/0.4`) en fazla 0.45 güçte ışık gölgeleri devreye
  girer. Yalnız Enhanced aktif karede; bu karenin `LightManager` ışıklarından (taşınan ışıklar hariç: kendi
  meşalenden düşen gölge 2D'de yanlış görünür) en fazla 64'ü. Her gölge düşüren için menzildeki en güçlü 2 ışık;
  yön ışıktan uzağa, uzunluk mesafeyle 0.5–1.5 × yarım sprite yüksekliği, güç mesafeyle azalır. Aşağı bakan
  gölgede sarma yönü ters döner; sol/sağ vertex'ler takas edilerek culling'den korunur.
- **S4 – Temas gölgesi:** her görünen mobilin ayak altına 34×14 px yumuşak elips (0.3), gündüz/gece. Bina dibi
  ortam kapatması (AO) kapsam dışı bırakıldı: 2D derinlik tamponu yüzey normali vermiyor, maliyet/fayda düşük;
  ileride ayrı görev (S6) olarak değerlendirilebilir.
- **S5 – Ölçüm:** `-lightdebug` FPS kutusunda gölge sayısını gösterir ("N soft shadows" / "original").
  Kare süresi ölçümü L14 ile aynı yöntemle (kullanıcı makinesi).

## 3b. S6 – Duvar dibi ortam gölgesi (uygulandı, 2026-10-17)

Duvarlar ve yüksekliği ≥ 3 olan geçilmez objeler (çatı, su, yaprak, kapı, köprü hariç;
`ShadowSettings.IsOccluder`) zemine yumuşak bir elips gölge düşürür. Görünen objeler render listesi doldurulurken
toplanır (`GameSceneAmbientOcclusion.cs`, chunk mesh içindekiler dahil), yumuşak gölge geçişinde temas elipsiyle aynı
yoldan çizilir (`UltimaBatcher2D.DrawAmbientOcclusion`, 64×34 px, güç 0.35). Max blend sayesinde duvar boyunca
sürekli bir koyu şerit oluşur, üst üste katlanmaz.

- Derinlik: objenin bir tile önü + yarım öncelik adımı. Önündeki zemin kararır, orada duran mobil (öncelik +1) kararmaz.
- Yalnız zemine yakın objeler (land Z'nin en fazla 12 üstü): üst kat duvarları alt kat duvarlarını karartmasın.
- Gece de durur (ortam gölgesi). Ayar: `AmbientOcclusion` (varsayılan açık), Options → Shadows → "Ambient
  occlusion"; Soft shadows açık olmalı.

## 4. Kabul (S1–S5)

- Gündüz: karakter/ağaç gölgeleri aynı yerde, aynı koyulukta; kenarlar yumuşak, ayak dibi keskin.
- Zırhlı/pelerinli karakter ve binekli karakterde gölge içinde koyu katman lekesi yok.
- Gölge üstünde duran / önünden geçen karakter gölgede kalmıyor (derinlik testi).
- Akşam karardıkça gölgeler kayboluyor; gece yok. Soft shadows kapalıyken görüntü eskisiyle aynı.
- Zoom (xBR açık/kapalı), Modern look, Enhanced lighting ile bozulma yok.
- Bacaklar/gövde kendi gölgesiyle kararmıyor; gölge kenarları yanlarda da yumuşak.
- Gece meşale/mangal yakınındaki ağaç ve karakterlerin gölgesi ışıktan uzağa düşüyor; elde taşınan ışık gölge
  düşürmüyor. Gündüz ışık gölgesi yok.
- Karakterlerin ayak altında hafif temas gölgesi var; Soft shadows kapalıyken yok.
- (S6) Bina/duvar diplerinde yumuşak koyu şerit; duvar önünde duran karakter kararmıyor, üst katlarda yatay bant yok.
