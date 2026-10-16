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

## 3. Sonraki adımlar

| ID | Görev | Not |
|---|---|---|
| S2 | Gölge quad'ını kenar payıyla genişletme | yan kenarlarda da yumuşama; UV sınır kenetlemesi zaten var |
| S3 | Yerel ışıklardan gölge (gece) | Enhanced ışıklar için en yakın 1–2 güçlü ışığa göre yön/uzunluk; maliyet ölçülmeli |
| S4 | Temas gölgesi / ortam kapatma | bina dibi ve nesne tabanında yumuşak kararma; derinlik tabanlı, düşük çözünürlük |
| S5 | Ölçüm | L14 ile aynı yöntem; gölge sayısı yüksek sahnede (orman, kalabalık) kare süresi farkı |

## 4. Kabul (S1)

- Gündüz: karakter/ağaç gölgeleri aynı yerde, aynı koyulukta; kenarlar yumuşak, ayak dibi keskin.
- Zırhlı/pelerinli karakter ve binekli karakterde gölge içinde koyu katman lekesi yok.
- Gölge üstünde duran / önünden geçen karakter gölgede kalmıyor (derinlik testi).
- Akşam karardıkça gölgeler kayboluyor; gece yok. Soft shadows kapalıyken görüntü eskisiyle aynı.
- Zoom (xBR açık/kapalı), Modern look, Enhanced lighting ile bozulma yok.
