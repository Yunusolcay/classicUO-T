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
6. Performans: orta GPU (GTX 1050 / RX 560 sınıfı), 1080p, 200 ışık → **60+ FPS**; ışık sistemi ≤ 1.5 ms GPU/kare.

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
`RenderTargets.Draw`'da çarpılıyor; bu yüzden glow, karanlıkta parlayan meşaleleri görmüyor ve gece sahnesinde
Modern look'un parlatması karanlığı da kaldırıyor. Işığı dünya hedefine Modern look'tan önce gömmek, iki özelliği
birlikte doğru çalıştırır. Enhanced kapalıyken classicUO-T'nin mevcut sırası aynen kalır.

- **×2 kompozit shader'sız:** blend `ColorSourceBlend = DestinationColor`, `ColorDestinationBlend = SourceColor`
  → `src·dst + dst·src = 2·src·dst`. Light RT'de 0.5 = nötr, 1.0 = 2× parlak (overbright).
  Gündüz: ambient 0.5 → sahne değişmez. Tonemap/renk düzenleme gerekirse M5'te shader'lı kompozite geçilir.
- **Kendi light RT'miz** (`SurfaceFormat.Color`, ölçek 1, 1/2, 1/4). Upstream `LightRenderTarget`
  backbuffer formatında ve tam boyutlu; ona dokunmuyoruz. `RenderTargets` yalnızca "hangi ışık texture'ı
  çizilecek" kancası alır.
- **Gecikmesiz:** ışık listesi aynı karede (`DrawWorld` sonrası) çizilir.
- **Işık sprite'ları:** yeni `Lighting.fx` / `LightSprite` tekniği: atlas'tan light.mul şekli (linear)
  **veya** prosedürel radyal düşüş × RGB × yoğunluk × flicker. Renk RGB olarak taşınır;
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
| `Lighting.fx` / `.fxc` | `ClassicUO.Renderer/shaders/` | `LightSprite`, `BlurH`, `BlurV` |

Upstream kancaları (hepsi `EnhancedLighting` kapalıyken eski yol):
- `GameScene.AddLight` → başta `if (enhanced) { _lightManager.Add(...); return; }`.
- `GameScene.Draw` → enhanced iken `PrepareLightsRendering` atlanır; `DrawWorld`/`UpscaleWorld` sonrası,
  `ApplyModernLook` öncesi `LightMapRenderer.Draw` + kompozit (dünya hedefine ×2 blend).
- `RenderTargets` → enhanced iken ışık RT çizimini atlayan bayrak (ör. `DrawLightsLayer`), tek satırlık koşul.
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

### L1 – Işık limitini ID limitinden ayır **[Codex]** · K
- Dosyalar: `ClassicUO.Client/Game/Constants.cs` (yeni `MAX_LIGHTS_PER_FRAME = 512`), `Game/Scenes/GameScene.cs` (`_lights` boyutu ve `AddLight` başındaki limit).
- Kabul: `LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT` yalnızca ID kontrolünde kalıyor; 100'den fazla ışıklı sahnede hepsi çiziliyor; başka görsel fark yok.

### L2 – `LightingSettings` + Profile ayarları **[Codex]** · K
- Dosyalar: yeni `Game/Lighting/LightingSettings.cs`; `Configuration/Profile.cs` (§4 property'leri; serileştirme `ProfileJsonContext` source-gen ile, mevcut property kalıbına uy).
- Kabul: Eski `profile.json` ile açılış hatasız; varsayılanlar §4; kaydet/yükle döngüsü çalışıyor.

### L3 – `LightManager`'a taşıma (davranış değişmeden) · O · Claude Code
- Dosyalar: yeni `Game/Lighting/LightManager.cs`, `LightSource.cs`; `GameScene.cs` (`AddLight`, `PrepareLightsRendering` delegasyonu).
- Kabul: Enhanced kapalıyken gece ekran görüntüleri değişiklik öncesiyle aynı; `GameScene.cs` diff'i < 30 satır.

### L4 – `Lighting.fx` + `LightingEffect` · O · Claude Code
- Dosyalar: yeni `ClassicUO.Renderer/shaders/Lighting.fx` + `.fxc`, `Effects/LightingEffect.cs`; `Batcher2D.cs` `Resources` getter; `compile_shaders.bat`'e satır.
- Kabul: Teknikler `fxc /T fx_2_0` ile derleniyor, FileEmbed ile yükleniyor; tek ışık sprite testi D3D11 ve OpenGL'de doğru.

### L5 – ~~Shader derleme scripti~~ · kaldırıldı
- Upstream `fxc.exe` + `compile_shaders.bat` içeriyor; Lighting.fx satırı L4'e dahil.

### L6 – `LightMapRenderer` + kompozit (çekirdek) · Z · Claude Code
- Dosyalar: yeni `Game/Lighting/LightMapRenderer.cs`, `ClassicUO.Renderer/Lighting/LightMapTarget.cs`; `GameScene.cs` (`Draw` kancası, E10), `RenderTargets.cs` (ışık katmanını atlama bayrağı).
- Kabul: Enhanced açıkken ışıklar aynı karede, ölçekli RT'de, ×2 blend ile dünya hedefine gömülüyor; Modern look açık/kapalı, xBR zoom filtresi açık/kapalı ve HD world pack ile doğru; gündüz sahnesi enhanced açık/kapalı aynı; zoom ve pencere/DPI değişiminde RT doğru yeniden kuruluyor, sızıntı yok.

### L7 – `AmbientLight`: yumuşak geçiş + gün/gece tonu · O · Claude Code
- Dosyalar: yeni `Game/Lighting/AmbientLight.cs`; `LightMapRenderer` clear rengi; birim testi `tests/ClassicUO.UnitTests/`.
- Kabul: `0x4F` değişiminde ambient `LightTransitionSeconds` içinde smoothstep geçiyor; tint kapalıyken son değer eski `IsometricLevel` ile aynı; `UseCustomLightLevel` + `LightLevelType` çalışıyor; test seviye/zaman → beklenen RGB.

### L8 – Işık tanım genişletmesi (`lights_ex.txt`) **[Codex]** · K–O
- Upstream `lights.txt` (renk) ve `lightshaders.txt` (RGB + eğri) zaten var; bunlara dokunma.
- Dosyalar: yeni `Game/Lighting/LightDefinitions.cs`; `Data/Client/lights_ex.txt` yoksa örnekli varsayılan oluşturulur (`LightColors.LoadLights` kalıbıyla, `TextFileParser`).
- Format: `ITEM_ID RADIUS INTENSITY FLICKER` (ör. `0x0A0F 1.2 1.0 fire`); bilinmeyen/bozuk satır loglanıp atlanır.
- Kabul: meşale/mum/lamba/brazier/şamdan/ateş alanı/enerji alanı için ≥ 25 varsayılan giriş; dosya silinince yeniden oluşuyor.

### L9 – Prosedürel ışık + RGB renk + flicker · O · Claude Code
- Dosyalar: `LightMapRenderer.cs`, `Lighting.fx` (`LightSprite`), `LightManager.cs`.
- Kabul: Ateş ışıkları `Time.Ticks` tabanlı titriyor (FPS'ten bağımsız), seed konumdan; `LightFlicker` kapalıyken sabit; `lightshaders.txt` renkleri korunuyor.

### L10 – Yumuşak light map: blur + kalite seviyeleri · O · Claude Code
- Dosyalar: `LightMapRenderer.cs`, `Lighting.fx` (`BlurH`, `BlurV`), `LightingSettings.cs`.
- Kabul: Low/Medium/High §4 tablosuna uyuyor; High'da ışık kenarında kademelenme yok.

### L11 – Kişisel ışık (0x4E) görselleştirmesi · O (opsiyonel) · Claude Code
- Dosyalar: `LightManager.cs`, `AmbientLight.cs`.
- Kabul: `0x4E` > 0 iken oyuncu çevresinde yumuşak hale; oyuncu karanlıkta eski sistemden daha az görmüyor; `IsometricLevel` formülündeki ölçek şüphesi doğrulanıp not edilmiş.

### L12 – Ayarlar arayüzü **[Codex]** · O
- Dosyalar: `Game/UI/Gumps/OptionsGump.cs` (mevcut ışık bölümünün yanına: `LightLevelType` civarı); metinler `Resources/ResGumps` kalıbıyla.
- Kabul: §4 ayarları UI'dan değişiyor, yeniden başlatmadan uygulanıyor.

### L13 – Debug overlay + ölçüm **[Codex]** · K
- Dosyalar: yeni `Game/Lighting/LightingDebug.cs`; `CommandManager`'a komut (ör. `lightdebug`); `Profiler` bağlamı.
- Kabul: Işık sayısı, light RT boyutu, ışık pass süresi (ms) ekranda; light map'i tek başına gösterme modu.

### L15 – Modern look ile ışık uyumu · K · Claude Code
- Dosyalar: `ClassicUO.Renderer/shaders/ModernLook.fx` (+ `.fxc`), gerekirse `GameScene.ApplyModernLook`.
- Kabul: Enhanced + Modern look açıkken meşale/lamba glow alıyor ama gece karanlığı Modern look'un parlatmasıyla kaybolmuyor (glow eşiği ve gamma ışıklı sahne için ayarlı); Enhanced kapalıyken Modern look çıktısı değişmemiş.

### L14 – Performans doğrulama ve kabul · O · Claude Code (+ Codex ölçüm)
- Kabul: §1/6 hedefi; açık alan gece, şehir gece, zindan × Low/Medium/High FPS tablosu `docs/PLAN.md`'de.

**Sıra:** L1, L2 paralel → L3 → L4 → L6 → (L7, L8, L9, L10, L15 paralel) → L11, L12, L13 → L14.
Toplam **14 aktif görev** (L5 kaldırıldı, L15 eklendi); Codex'e uygun: L1, L2, L8, L12, L13.

## 6. Riskler

- Işık sızıntısı (duvar/kat arkası) – 2D yaklaşımın doğası; M2. Depth texture örneklenemediği için M2'de MRT ile derinlik hedefi gerekebilir.
- MojoShader/GL farkları – D3D11 ve OpenGL'de test şart.
- ×2 kompozit 8-bit hassasiyette ambient'i yarıya sıkıştırır (koyu tonlarda hafif bantlanma); görülürse High kalitede `HalfVector4` RT seçeneği.
- Upstream merge çakışmaları: kancalar `GameScene.cs`, `RenderTargets.cs`, `Profile.cs`, `OptionsGump.cs`'te; diff'leri küçük tut.
- classicUO-T etkileşimi: Modern look (sıra ve parlaklık, L15), xBR zoom filtresi (ışık kompoziti büyütmeden *sonra*, tam çözünürlükte), nature effects ve HD pack (ışığa etkisi yok; aynı dosyalarda — `GameScene.Draw`, `OptionsGump`, `Profile` — çakışma riski).
