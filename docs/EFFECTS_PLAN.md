# Büyü/efekt görselleri planı

Hedef: büyü ve eşya efektleri (fireball, energy bolt, lightning, heal parıltısı, patlamalar) daha canlı görünsün;
efekt zamanlaması, hedef, ses ve protokol aynı kalsın. Ayar: Options → Video → Misc → "Enhanced spell effects"
(`Profile.EnhancedEffects`, varsayılan açık).

## 1. Mevcut durum

`GameEffect` alt sınıfları (`MovingEffect` mermi, `FixedEffect` yerinde/karakter üstünde, `LightningEffect`)
`GameEffectView.Draw` ile art karesi olarak çizilir; sunucunun blend modu (Screen, Multiply…) uygulanır. Işık yalnız
tiledata'da `IsLight` olan efektlerde ve kaynak nesnenin ışığı olarak eklenir; çoğu büyü çevresini aydınlatmaz.

## 2. Uygulama

- **E1 – Efekt ışığı:** her efekt karesinin art'ının ortalama rengi bir kez hesaplanır (parlak pikseller ağırlıklı,
  parlaklık çıkarılmış; koyu art ışık vermez) ve `EffectLights`'a o karenin konumunda renkli yuvarlak ışık eklenir
  (yarıçap art boyutunun 1.6 katı, 60–200 px). Enhanced lighting'de light map'e `LightingEffect.LightRadial` ile
  RGB olarak çizilir (diğer ışıklar gibi yarım güç kodlaması). Multiply/ShadowBlue (karartan) efektler ışık vermez.
- **E2 – Mermi izi:** hareket eden efekt son 4 konumunu (30 ms aralık, dünya pikseli) tutar; arkasında giderek
  sönen (0.4 → 0.1) additive kopyalar çizilir.
- **E3 – Çarpma parlaması:** mermi hedefe ulaşınca hedef karede 0.35 sn'lik renkli ışık patlaması (hızlı yükselme,
  yavaş sönme, büyüyen yarıçap). Konum dünya pikselinde tutulur, kamera kayarsa yerinde kalır.
- Bloom (M5) bu ışıkları ve efektlerin parlak piksellerini ayrıca hâlelendirir.

Sınırlar: efekt ışıkları yalnız Enhanced lighting aktif karede (gece) görünür; gündüz iz ve bloom kalır.
Kare başına en fazla 128 efekt ışığı.

## 3. Kabul

- Gece fireball yolunda turuncu, energy bolt'ta mavi ışık taşıyor; çarptığı yerde kısa parlama var.
- Mermilerin arkasında kısa, sönen iz; yerinde duran efektlerde iz yok.
- Ayar kapalıyken efektler eskisi gibi; efekt süreleri/hedefleri değişmemiş.
