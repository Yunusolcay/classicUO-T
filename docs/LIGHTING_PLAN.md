# Dinamik Işık Sistemi – Tasarım Planı (M1)

Taban: classicUO-T `main` (upstream `ee79d7eb` + fork özellikleri). Önkoşul okuma: `docs/arch/rendering.md` §3 (mevcut ışık sistemi),
§6 (genişletme noktaları). Teknik kararı: `docs/DECISIONS.md` D-001.

## 1. Hedefler

1. Renkli dinamik ışık kaynakları (meşale, mum, lamba, büyü, alan efektleri), yumuşak radyal düşüş.
2. Yumuşak geçişler: seviye değişimleri zamanla; ışık kenarları yumuşak; ateşte flicker.
3. Gece/gündüz: sunucunun `0x4F` seviyesine bağlı ambient **renk** (şafak turuncu, gece mavi).
4. Protokol uyumu: `0x4F`, `0x4E`, item `LightID` aynen kullanılır; sunucu otoritedir. Mevcut
   `Data/Client/lights.txt` ve `lightshaders.txt` dosyaları geçerli kalır.
5. Her şey ayarlardan kapatılabilir; kapalıyken upstream yolu birebir çalışır.
6. Performans: orta GPU (GTX 1050 / RX 560 sınıfı), 1080p, 200 ışık → **60+ FPS**; ışık sistemi ≤ 1.5 ms GPU/kare (ölçüm yöntemi: L14).

Kapsam dışı (M2): ışığın duvar/kat arkasına geçmemesi (gerçek occlusion).

## 2. Teknik: ayrı light map RT + çarpımsal kompozit (×2)

```
GameScene.DrawWorld / UpscaleWorld (değişmez) ──► dünya hedefi (WorldRenderTarget veya PostProcessWorld RT)
LightMapRenderer (yeni, dünyadan SONRA)       ──► kendi light RT'miz (ölçekli; ambient/2 ile temizle,
                                                  ışıkları additive çiz /2, opsiyonel blur)
Kompozit (yeni, GameScene içinde, E10)        ──► dünya hedefi ×= Light × 2  (blend: Src=DestColor, Dst=SourceColor)
ApplyModernLook (classicUO-T, değişmez)       ──► glow artık ışıkları da yakalar (meşale bloom'u)
RenderTargets.Draw                             ──► enhanced iken ışık çizimini atlar
```

**Neden Modern look'tan önce?** classicUO-T'de Modern look ışıklardan önce uygulanıyor ve ışık sonra
`RenderTargets.Draw`'da çarpılıyor. Bu yüzden glow yalnızca dünya görüntüsünde zaten parlak olan pikselleri
işler: ışık kaynağının kendi art'ı (alev, lav) eşiği (luma > 0.62) aşarsa glow alabilir, ama **Light RT'nin
çevreye kattığı aydınlık glow'a hiç girmez**; ayrıca gece sahnesinde Modern look'un parlatması karanlığın
çarpımından önce uygulanır. Işığı dünya hedefine Modern look'tan önce gömmek, Light RT katkısını da glow ve
renk düzenlemeye sokar. Görsel sonuç L15'te test edilecek; Enhanced kapalıyken classicUO-T'nin mevcut sırası aynen kalır.

- **×2 kompozit shader'sız:** blend `ColorSourceBlend = DestinationColor`, `ColorDestinationBlend = SourceColor`
  → `src·dst + dst·src = 2·src·dst`. Light RT'de 0.5 = nötr, 1.0 = 2× parlak (overbright).
  Gündüz: ambient 0.5 → sahne değişmez. Tonemap/renk düzenleme gerekirse M5'te shader'lı kompozite geçilir.
  **Güncelleme (L6.1):** İlk oyun içi testte üst üste binen lambalar art'ı düz sarı-turuncuya yaktı (2× sert tavan +
  Modern look glow). Kompozit artık shader'lı (`LightingEffect.Composite`): ışık 1'e kadar aynen, üstü yumuşak eğriyle
  `OverbrightLimit`'e (0.35 × `light_intensity`) yaklaşır; ışık renkleri %75 doygunlukta tutulur. Light map s0'da
  (linear, hedefe gerilir), dünya s3'te (`WorldTexture`, 1:1 olduğu için batcher'ın point örneklemesi doğru). s1/s2'yi kullanma: bkz. rendering.md
  "Hue tabloları" notu (ilk sürüm s1'i boşaltıp karakter ve gump'ları siyah çizdirdi).
- **Kendi light RT'miz** (`SurfaceFormat.Color`, ölçek 1, 1/2, 1/4). Upstream `LightRenderTarget`
  backbuffer formatında ve tam boyutlu; ona dokunmuyoruz. Kompozit `GameScene` içinde (E10) yapılır;
  `RenderTargets` değişmez: enhanced iken upstream Light RT'si, upstream'in kendi "ışık yok" yolundaki gibi
  şeffaf temizlenip boş blend ile verilir (L6 uygulaması). FNA bir `DiscardContents` hedefini bağlarken
  temizlediği için dünya önce ikinci bir hedefe (`GetPostProcessWorldRenderTarget`) çizilir; kompozit
  hedefe tek bağlamada yazılır (dünya opak + ışık ×2).
- **Gecikmesiz:** ışık listesi aynı karede (`DrawWorld` sonrası) çizilir.
- **Işık sprite'ları:** yeni `Lighting.fx`: `LightTexture` (atlas'tan light.mul şekli, linear)
  **veya** `LightRadial` (prosedürel radyal düşüş) × RGB × yoğunluk × flicker. Renk RGB olarak taşınır;
  mevcut `lightshaders.txt` (s2 tablosu) ve hue renkleri RGB'ye çevrilerek kullanılır.
- **Ambient:** `AmbientLight` sunucu seviyesinden hedef yoğunluk + gün/gece tonu üretir, zamanla yaklaşır.
- **Işık toplama:** `GameScene.AddLight` → `LightManager.Add` (aynı örtme testi, aynı ID/renk kuralları
  + tanım tablosundan yarıçap/yoğunluk/flicker).

### Alternatifler
| Seçenek | Artı | Eksi | Karar |
|---|---|---|---|
| **A. Light map RT + ×2 kompozit** | Mevcut mimarinin uzantısı; sprite shader'ına ve mesh'lere dokunmaz; maliyet ışık sayısından büyük ölçüde bağımsız; ölçek/blur ile kalite ayarı; M5'e doğal giriş | 2D ekran uzayı → duvar arkasına sızma (M2) | **Seçildi** |
| B. Forward (sprite shader'ında ışık döngüsü) | Piksel başına doğru | ps_3_0 sabit limiti, chunk mesh'lere per-frame ışık verisi, tek global shader'a büyük müdahale | Reddedildi |
| C. Deferred + normal map | Gerçekçi ışık (HD art ile) | UO art'ında normal yok; MRT + vertex format değişikliği | M6 sonrası yeniden değerlendirilecek |
| D. 2D ışık izleme / SDF GI | Gerçek yumuşak gölge | Orta GPU'da 60 FPS riskli | Reddedildi (M2'de sınırlı varyant) |

## 3. Bileşenler

Yeni dosyalar:

| Sınıf | Yer | Sorumluluk |
|---|---|---|
| `LightingSettings` | `ClassicUO.Client/Game/Lighting/` | Profil ayarları + kalite önayarları |
| `LightSource` (struct) | `ClassicUO.Client/Game/Lighting/` | Ekran pozisyonu, RGB, yarıçap, yoğunluk, flicker seed, şekil (lightID / prosedürel) |
| `LightManager` | `ClassicUO.Client/Game/Lighting/` | Kare başına toplama, örtme testi, tanım çözümleme; limit ≥ 512 |
| `LightDefinitions` | `ClassicUO.Client/Game/Lighting/` | `Data/Client/lights_ex.txt`: yarıçap/yoğunluk/flicker (renk mevcut dosyalardan) |
| `AmbientLight` | `ClassicUO.Client/Game/Lighting/` | `World.Light` → hedef ambient RGB, tint eğrisi, yumuşatma |
| `LightMapRenderer` | `ClassicUO.Client/Game/Lighting/` | Light RT'yi doldurma (ışıklar, blur), kalite seviyeleri |
| `LightMapTarget` | `ClassicUO.Renderer/Lighting/` | Ölçekli RT'lerin yaşam döngüsü (oluştur/boyutla/dispose) |
| `LightingEffect` | `ClassicUO.Renderer/Effects/` | `Lighting.fx` sarmalayıcısı |
| `Lighting.fx` / `.fxc` | `ClassicUO.Renderer/shaders/` | `LightTexture`, `LightRadial`, `BlurH`, `BlurV` |

Upstream kancaları (hepsi `EnhancedLighting` kapalıyken eski yol):
- `GameScene.AddLight` → başta `if (enhanced) { _lightManager.Add(...); return; }`.
- `GameScene.Draw` → enhanced iken `PrepareLightsRendering` atlanır; `DrawWorld`/`UpscaleWorld` sonrası,
  `ApplyModernLook` öncesi `LightMapRenderer.Draw` + kompozit (dünya hedefine ×2 blend).
- `RenderTargets` değişmez; `GameSceneLighting.cs` (partial) upstream Light RT katmanını boşaltır.
- `Batcher2D.cs` `Resources` → `[FileEmbed("shaders/Lighting.fxc")] GetLightingShader()`.
- `Profile.cs` → yeni property'ler; `OptionsGump.cs` → ayar bölümü.

## 4. Ayarlar ve kalite

| Ayar | Varsayılan | Not |
|---|---|---|
| `EnhancedLighting` | açık | Kapalı = birebir upstream |
| `LightingQuality` | Medium | aşağıdaki tablo |
| `LightFlicker` | açık | ateş tabanlı ışıklar |
| `DayNightTint` | açık | kapalıysa gri ambient |
| `LightTransitionSeconds` | 2.0 | 0 = anlık |
| `LightIntensity` | 1.0 | 0.5–2.0 genel çarpan |

Mevcut `UseColoredLights`, `UseCustomLightLevel`/`LightLevel`/`LightLevelType`, `UseDarkNights`,
`UseAlternativeLights` korunur ve yeni sistemde de uygulanır.

| Kalite | Light RT ölçeği | Blur | Maks ışık | Prosedürel düşüş |
|---|---|---|---|---|
| Low | 1/4 | yok | 128 | hayır (light.mul) |
| Medium | 1/2 | yok | 256 | evet |
| High | 1/2 | 2-pass 9-tap | 512 | evet |

## 5. Görevler

Zorluk: K (<½ gün) · O (~1 gün) · Z (2+ gün). **[Codex]** = Codex'e verilebilecek kadar net.
ID'ler sabittir (PLAN.md ile eşleşir).

### L1 – Işık limitini ID limitinden ayır **[Codex]** · K · L2 ve L3'e bağlı
- Dosyalar: `ClassicUO.Client/Game/Lighting/LightManager.cs` (dizi kapasitesi `MAX_LIGHTS_PER_FRAME = 512`; yeni `LEGACY_MAX_LIGHTS = 100`; `Add` limiti yeni `MaxLights` özelliğinden), `Game/Scenes/GameScene.cs` (her kare `MaxLights = EnhancedLighting ? <kalite tablosundaki maks> : LEGACY_MAX_LIGHTS`).
- Kabul: Enhanced **kapalıyken** limit 100 → görüntü birebir eski (101+ ışıklı sahnede de); Enhanced açıkken §4 kalite tablosundaki limit; `LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT` yalnızca ışık ID kontrolünde kalıyor.

### L2 – `LightingSettings` + Profile ayarları **[Codex]** · K
- Dosyalar: yeni `Game/Lighting/LightingSettings.cs`; `Configuration/Profile.cs` (§4 property'leri; serileştirme `ProfileJsonContext` source-gen ile, mevcut property kalıbına uy).
- Kabul: Eski `profile.json` ile açılış hatasız; varsayılanlar §4; kaydet/yükle döngüsü çalışıyor.

### L3 – `LightManager`'a taşıma (davranış değişmeden) · O · Claude Code · ✅ 2026-10-16 (`6c96b7c0`, oyun içi görsel kontrol bekliyor)
- Dosyalar: yeni `Game/Lighting/LightManager.cs`, `LightSource.cs`; `GameScene.cs` (`AddLight`, `PrepareLightsRendering` delegasyonu).
- Kabul: Enhanced kapalıyken gece ekran görüntüleri değişiklik öncesiyle aynı; `GameScene.cs` diff'i < 30 satır.

### L4 – `Lighting.fx` + `LightingEffect` · O · Claude Code · ✅ 2026-10-16 (derleme + gömme + birim testi; GPU'da yükleme L6 kabulüne taşındı)
- Dosyalar: yeni `ClassicUO.Renderer/shaders/Lighting.fx` + `.fxc`, `Effects/LightingEffect.cs`; `Batcher2D.cs` `Resources` getter; `compile_shaders.bat`'e satır.
- Kabul: Teknikler `fxc /T fx_2_0` ile derleniyor, FileEmbed ile yükleniyor; tek ışık sprite testi D3D11 ve OpenGL'de doğru.

### L5 – ~~Shader derleme scripti~~ · kaldırıldı
- Upstream `fxc.exe` + `compile_shaders.bat` içeriyor; Lighting.fx satırı L4'e dahil.

### L6 – `LightMapRenderer` + kompozit (çekirdek) · Z · Claude Code · 🔶 kod hazır (build + 298 test), oyun içi kabul bekliyor
- Not: Bu aşamada ışık sprite'ları upstream renkleriyle (s2 tablosu / hue) varsayılan effect ile, `BlendFactor` 0.5'li additive
  ile çizilir; RGB/prosedürel ışık L9'da, blur L10'da `LightingEffect`'e geçer. Sınırlama: Enhanced yalnız normal gece
  karanlığında; gündüz, `UseAltLights` ve ölü siyah-beyaz görünümde eski yol.
- L6.1 (oyun içi ilk test sonrası): ×2 blend yerine yumuşak tavanlı shader kompoziti (§2 güncellemesi); `light_intensity`
  aşırı parlamanın sınırını ölçekler (0 = art'tan parlak olmaz).
- Dosyalar: yeni `Game/Lighting/LightMapRenderer.cs`, `ClassicUO.Renderer/Lighting/LightMapTarget.cs`; `Game/Scenes/GameSceneLighting.cs` (partial: `UseEnhancedLighting`, `DisableLegacyLightLayer`, `DrawEnhancedLights`); `GameScene.cs` (`Draw` kancası E10, `Unload` dispose).
- Kabul: `LightingEffect` D3D11 ve OpenGL arka uçlarında yükleniyor (yüklenemezse Modern look gibi loglanıp Enhanced kapanıyor); Enhanced açıkken ışıklar aynı karede, ölçekli RT'de, ×2 blend ile dünya hedefine gömülüyor; Modern look açık/kapalı, xBR zoom filtresi açık/kapalı ve HD world pack ile doğru; gündüz sahnesi enhanced açık/kapalı aynı; zoom ve pencere/DPI değişiminde RT doğru yeniden kuruluyor, sızıntı yok.

### L7 – `AmbientLight`: yumuşak geçiş + gün/gece tonu · O · Claude Code · 🔶 kod hazır (319 test), oyun içi kabul bekliyor
- Not: Hedef seviye `IsometricLevel` (custom light level ve dark nights dahil). Geçiş yalnız Enhanced yolu çizerken
  çalışır; yol kapanınca sıfırlanır, tekrar açılınca hedefe atlar. Gece tonu (`day_night_tint`): karanlık arttıkça en fazla
  %60 ay ışığı tonu (0.86, 1, 1.24), parlaklık korunur; gündüz nötr.
- Dosyalar: yeni `Game/Lighting/AmbientLight.cs`; `LightMapRenderer` clear rengi; birim testi `tests/ClassicUO.UnitTests/`.
- Kabul: `0x4F` değişiminde ambient `LightTransitionSeconds` içinde smoothstep geçiyor; tint kapalıyken son değer eski `IsometricLevel` ile aynı; `UseCustomLightLevel` + `LightLevelType` çalışıyor; test seviye/zaman → beklenen RGB.

### L8 – Işık tanım genişletmesi (`lights_ex.txt`) · K–O · Claude Code (Codex yerine) · 🔶 kod hazır, oyun içi kabul bekliyor
- Uygulama notu: Format `ITEM_ID[-LAST_ID] RADIUS INTENSITY FLICKER [SHAPE]`; aralık sözdizimi ve isteğe bağlı
  `SHAPE` (`texture` = light.mul şekli, varsayılan; `round` = prosedürel yuvarlak) eklendi. Duvara monteli ışıklar
  (duvar meşalesi, aplik, şömine) light.mul şeklini korur: o şekiller yalnız duvarın önünü aydınlatır. Varsayılan
  giriş listesi referans 7.0.112 tiledata'daki LightSource bayraklı eşyalardan çıkarıldı. Sayılar invariant culture ile
  okunur (Türkçe Windows'ta `1.2` doğru). Yükleme `Client.cs`'te `LightColors.LoadLights()` sonrası (1 satır).
- Upstream `lights.txt` (renk) ve `lightshaders.txt` (RGB + eğri) zaten var; bunlara dokunma.
- Dosyalar: yeni `Game/Lighting/LightDefinitions.cs`; `Data/Client/lights_ex.txt` yoksa örnekli varsayılan oluşturulur (`LightColors.LoadLights` kalıbıyla, `TextFileParser`).
- Format: `ITEM_ID RADIUS INTENSITY FLICKER` (ör. `0x0A0F 1.2 1.0 fire`); bilinmeyen/bozuk satır loglanıp atlanır.
- Kabul: meşale/mum/lamba/brazier/şamdan/ateş alanı/enerji alanı için ≥ 25 varsayılan giriş; dosya silinince yeniden oluşuyor.

### L9 – Prosedürel ışık + RGB renk + flicker · O · Claude Code · 🔶 kod hazır, oyun içi kabul bekliyor
- Uygulama notu: Yalnız Enhanced yolunda ve yalnız `lights_ex.txt`'te tanımlı ışıklara uygulanır; tanımsız ışıklar ve
  Enhanced kapalı yol birebir eski çizim. Yoğunluk + titreme hue alfasıyla (renk × alfa) verilir, boyut ölçekle.
  Titreme `Time.Ticks` tabanlı (double, uzun oturumda hassas), faz ışığın serial'ından ya da konumundan. `round` şekil:
  CPU'da üretilen 128×128 gri radyal doku, light.mul gibi renk tablosundan geçer (lights.txt/lightshaders.txt renkleri
  korunur); yalnız prosedürel düşüşü olan kalitelerde (Medium/High), Low'da light.mul şekli.
- Dosyalar: `LightMapRenderer.cs`, `Lighting.fx` (`LightTexture`, `LightRadial`), `LightManager.cs`.
- Kabul: Ateş ışıkları `Time.Ticks` tabanlı titriyor (FPS'ten bağımsız), seed konumdan; `LightFlicker` kapalıyken sabit; `lightshaders.txt` renkleri korunuyor.

### L10 – Yumuşak light map: blur + kalite seviyeleri · O · Claude Code · 🔶 kod hazır, oyun içi kabul bekliyor
- Not: `BlurPasses` ayrık geçiş sayısıdır; her çift = yatay (light map → blur RT) + dikey (blur RT → light map).
  Blur RT light map ile aynı boyutta (`LightMapTarget.GetBlurTarget`). Low/Medium'da blur yok.
- Dosyalar: `LightMapRenderer.cs`, `Lighting.fx` (`BlurH`, `BlurV`), `LightingSettings.cs`.
- Kabul: Low/Medium/High §4 tablosuna uyuyor; High'da ışık kenarında kademelenme yok.

### L11 – Kişisel ışık (0x4E) görselleştirmesi · O (opsiyonel) · Claude Code · 🔶 kod hazır, oyun içi kabul bekliyor
- Ölçek notu: `IsometricLevel = max(Personal, 32 − Overall) / 32`. `Overall` karanlık (0 aydınlık … 30 karanlık),
  `32 − Overall` parlaklığa çevrilir; `Personal` (0x4E, 0…30) zaten parlaklık ölçeğinde. İkisi aynı birimde, şüphe
  doğrulanmadı: ölçek tutarlı (kişisel ışık en fazla 30/32 parlaklık verir).
- Uygulama: Ambient eskisi gibi kişisel seviyeyi taban alır (kimse daha az görmez); Enhanced yolunda ek olarak oyuncunun
  çevresine beyaz yuvarlak hale çizilir (çap 360 px, güç 0.45 × Personal/30). Custom light level açıkken Personal = 0,
  hale yok. Not: SphereNet `0x4E`'yi farklı anlamda gönderiyorsa (oturum notu: In Lor sonrası davranış) hale sürekli
  görünebilir; oyun içinde kontrol edilecek.
- Dosyalar: `LightManager.cs`, `AmbientLight.cs`.
- Kabul: `0x4E` > 0 iken oyuncu çevresinde yumuşak hale; oyuncu karanlıkta eski sistemden daha az görmüyor; `IsometricLevel` formülündeki ölçek şüphesi doğrulanıp not edilmiş.

### L12 – Ayarlar arayüzü · O · Claude Code (Codex yerine) · 🔶 kod hazır, oyun içi kabul bekliyor
- Uygulama notu: Video → Lights bölümüne Enhanced lighting, kalite, titreme, gece tonu, geçiş süresi (0–5 sn, tam
  saniye) ve ışık yoğunluğu (%50–200) eklendi. Metinler classicUO-T'nin diğer eklemeleri (Modern look vb.) gibi
  doğrudan İngilizce; ResGumps'a taşıma çeviri işi gelince. Default düğmesi §4 varsayılanlarına döner.
- Dosyalar: `Game/UI/Gumps/OptionsGump.cs` (mevcut ışık bölümünün yanına: `LightLevelType` civarı); metinler `Resources/ResGumps` kalıbıyla.
- Kabul: §4 ayarları UI'dan değişiyor, yeniden başlatmadan uygulanıyor.

### L13 – Debug overlay + ölçüm · K · Claude Code (Codex yerine) · 🔶 kod hazır, oyun içi kabul bekliyor
- Uygulama notu: `-lightdebug` FPS kutusuna (Options > General > Show FPS) ışık sayısı/limit, light map boyutu ve ışık
  pass'inin CPU süresini ("not GPU time" etiketli, Stopwatch, yumuşatılmış) ekler; `-lightdebug map` dünyayı beyaz çizip
  yalnız light map'i gösterir. `Profiler` enum'una yeni bağlam eklemek yerine yerel Stopwatch (Utility'ye dokunmamak için).
- Dosyalar: yeni `Game/Lighting/LightingDebug.cs`; `CommandManager`'a komut (ör. `lightdebug`); `Profiler` bağlamı.
- Kabul: Işık sayısı, light RT boyutu, ışık pass'inin **CPU** süresi (ms, açıkça "CPU" etiketli; `Profiler` Stopwatch tabanlıdır, GPU süresi değildir) ekranda; light map'i tek başına gösterme modu.

### L15 – Modern look ile ışık uyumu · K · Claude Code · 🔶 ilk ayar yapıldı, oyun içi kabul bekliyor
- Not (ilk ayar): Modern look açıkken lamba çevresi yine düz sarıya yanıyordu (glow + vibrance + sıcak vurgu kayması).
  `ModernLook.fx`'e `LitScene` eklendi: Enhanced ışık uygulanmış karede glow eşiği 0.62→0.82, gücü 0.5→0.2; parlak
  bölgelerde (luma 0.35–0.8 arası yumuşak geçiş) parlatma, vibrance ve S-eğrisi geri çekilir; sıcak vurgu kayması
  kapanır, gölgelerdeki serin kayma kalır. `LitScene` = 0 iken (gündüz, Enhanced kapalı) matematik birebir eskisi.
- Dosyalar: `ClassicUO.Renderer/shaders/ModernLook.fx` (+ `.fxc`), gerekirse `GameScene.ApplyModernLook`.
- Kabul: Enhanced + Modern look açıkken meşale/lamba glow alıyor ama gece karanlığı Modern look'un parlatmasıyla kaybolmuyor (glow eşiği ve gamma ışıklı sahne için ayarlı); Enhanced kapalıyken Modern look çıktısı değişmemiş.

### L14 – Performans doğrulama ve kabul · O · Claude Code (+ Codex ölçüm)
- GPU ölçüm yöntemi (FNA3D timer query sunmadığı için harici):
  1. **Birincil:** PIX veya RenderDoc kare yakalaması; ölçüm arka ucu sabitlenir (`FNA3D_FORCE_DRIVER`, ör. D3D11) ve raporda yazılır; ışık pass'i (light RT çizimi + blur + kompozit) draw call'larının GPU süresi toplamı.
  2. **Destek:** VSync kapalı, FPS limiti 250, GPU-bound sahnede Enhanced açık/kapalı ortalama kare süresi farkı (`Profiler` RENDER_FRAME değil, 10 sn ortalama FPS'ten).
- Kabul: §1.6 hedefi yöntem 1 ile doğrulanmış; açık alan gece, şehir gece, zindan × Low/Medium/High için GPU ms + FPS tablosu `docs/PLAN.md`'de. Yalnız CPU overlay'i veya FPS ile GPU bütçesi "doğrulandı" sayılmaz.

**Sıra:** L3 ✅ → L2 → L1; L4 → L6 → (L7, L8, L9, L10, L15 paralel) → L11, L12, L13 → L14.
Toplam **14 aktif görev** (L5 kaldırıldı, L15 eklendi); Codex'e uygun: L1, L2, L8, L12, L13.

## 6. Riskler

- Işık sızıntısı (duvar/kat arkası) – 2D yaklaşımın doğası; M2. Depth texture örneklenemediği için M2'de MRT ile derinlik hedefi gerekebilir.
- MojoShader/GL farkları – D3D11 ve OpenGL'de test şart.
- ×2 kompozit 8-bit hassasiyette ambient'i yarıya sıkıştırır (koyu tonlarda hafif bantlanma); görülürse High kalitede `HalfVector4` RT seçeneği.
- Upstream merge çakışmaları: kancalar `GameScene.cs`, `RenderTargets.cs`, `Profile.cs`, `OptionsGump.cs`'te; diff'leri küçük tut.
- classicUO-T etkileşimi: Modern look (sıra ve parlaklık, L15), xBR zoom filtresi (ışık kompoziti büyütmeden *sonra*, tam çözünürlükte), nature effects ve HD pack (ışığa etkisi yok; aynı dosyalarda — `GameScene.Draw`, `OptionsGump`, `Profile` — çakışma riski).
