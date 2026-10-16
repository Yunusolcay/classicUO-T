# Proje: ClassicUO tabanlı geliştirilmiş UO istemcisi

## Amaç
ClassicUO fork'u üzerinde, kendi shard'ım Nimloth (Sphere/Source-X ve ileride
kendi emülatörüm SphereNet) için çok daha kaliteli görüntü sunan bir UO istemcisi.
Oynanış ve protokol uyumluluğu korunur; değişiklikler görsel katmanda yoğunlaşır.

## Taban
- Repo: https://github.com/Yunusolcay/classicUO-T (`origin`). Upstream ClassicUO `main` (`ee79d7eb`,
  2025-11-26) + classicUO-T özellikleri (HD world/karakter paketleri, Modern look post-process,
  nature effects, smooth movement, zoom xBR, launcher). .NET 10, submodule'ler `external/` altında.
  Eski repo `Yunusolcay/ClassicUO` (`old-origin`) ve 2020 kopyası (`legacy/2020`) yalnızca referans.
- Not: upstream `.gitignore`'daki `Data` kuralı yüzünden `Data` adlı klasörlere yeni dosya `git add -f` ile eklenir.
- Referans UO verisi: `D:\Games\Electronic Arts\Ultima Online Classic` (7.0.112.75).
  Protokol hedefi: 7.0.20 (bkz. docs/DECISIONS.md D-003).

## Görsel hedefler (öncelik sırasıyla)
1. Dinamik ışık + gece/gündüz döngüsü
2. Yumuşak gölgeler
3. Shader efektleri (su, sis, hava durumu, bitki salınımı)
4. Akıcılık (yüksek FPS, yumuşak kamera, kademesiz zoom)
5. Post-processing (renk düzenleme, bloom)
6. İleride: yüksek çözünürlüklü/özel art paketleri (asset katmanı değiştirilebilir olmalı)

## Kurallar
- Mevcut oynanışı ve ağ protokolünü bozma. Her görsel özellik ayarlardan kapatılabilir olmalı.
- Değişiklikleri mümkün olduğunca yeni dosya/sınıflarda topla, upstream kodu az değiştir
  (fork'u upstream ile senkron tutabilmek için).
- UO data dosyaları (.mul, .uop vb.) asla repoya eklenmez.
- Dokümanlar Türkçe, kod ve kod yorumları İngilizce.
- Kod tabanını rastgele baştan sona okuma; önce docs/ altındaki haritalara bak.
- Her görev sonunda docs/PLAN.md güncellenir.
- Dallar: iş `dev`'de birikir (push'u yalnız Build-Test çalıştırır); `main`'e push oyunculara yayın
  (Deploy) demektir, yalnız kullanıcı "yayınla" deyince yapılır.

## Dokümanlar
- docs/arch/rendering.md   – render pipeline haritası
- docs/arch/network-assets.md – ağ, paket işleme, asset yükleme haritası
- docs/BUILD.md            – derleme ve çalıştırma
- docs/LIGHTING_PLAN.md    – ışık sistemi tasarımı
- docs/SHADOWS_PLAN.md     – gölge sistemi tasarımı (M2)
- docs/CAMERA_PLAN.md      – kamera ve zoom akıcılığı (M4)
- docs/WATER_WEATHER_PLAN.md – su ve hava durumu (M3)
- docs/BLOOM_PLAN.md       – bloom / parlama (M5)
- docs/EFFECTS_PLAN.md     – büyü/efekt görselleri
- docs/OUTLINE_PLAN.md     – hedef/hover konturu
- docs/ATMOSPHERE_PLAN.md  – bölge atmosferi (ton, pus)
- docs/TERRAIN_BLEND_PLAN.md – zemin geçişleri
- docs/PLAN.md             – kilometre taşları ve görev durumu
- docs/DECISIONS.md        – alınan kararlar ve nedenleri

## İş bölümü
- Claude Code: mimari, render/shader işleri, çok dosyalı refactor'lar
- Codex: net tanımlı görevler, build/config, testler, küçük düzeltmeler
- Her iş diğer araç tarafından incelenir (çapraz review).
