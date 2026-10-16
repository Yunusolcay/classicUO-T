# Geliştirme planı

Son güncelleme: 16 Ekim 2026. D-004 ile ana repo `Yunusolcay/classicUO-T` (`origin`), taban upstream `ee79d7eb` + classicUO-T özellikleri. MIG-9 incelemesi `fe0244de4` üzerinden yapıldı; MIG-7 Codex incelemesi için `main` (`c7cb63d92`) `codex/dev` dalına çakışmasız fast-forward ile birleştirildi. Eski repo `old-origin`, 2020 tabanı `legacy/2020` yalnız referanstır. Göç ayrıntısı: [MIGRATION.md](MIGRATION.md).

M3–M6'daki işaretli özellikler classicUO-T'de zaten bulunan uygulamaları gösterir; MIG-9 kaynak/ayar bağlantılarını ve derlemeyi doğrular. Bu işaretler bütün görsel hedeflerin, performans ölçümlerinin veya oyun içi kabul testlerinin tamamlandığı anlamına gelmez.

## M0 — Analiz ve dokümantasyon / taban geçişi

| Görev | Sahip | Durum ve kanıt |
| --- | --- | --- |
| MIG-1 — dallar/varsayılan dal/submodule/worktree düzeni | Kullanıcı / Claude Code | Tamamlandı: MIGRATION.md kaydına göre 16 Ekim 2026; D-004 sonrası `origin=classicUO-T`, Codex çalışma dalı `codex/dev` |
| MIG-2 — yeni build ortamı | Codex | Tamamlandı: .NET 10 Release win-x64, recursive submodule'ler, `build.ps1`, BUILD.md; self-contained managed exe çalıştı. PowerShell 7 ve temiz yerel klonda PowerShell 5.1 ile build/publish geçti. NativeAOT için C++ bağlayıcısı eksik, ayrı seçenek olarak belgelendi |
| MIG-3 — render haritası | Claude Code | Tamamlandı: güncel upstream tabanı için belge bu dalda mevcut; classicUO-T ekleri MIG-8'de |
| MIG-4 — ışık planını uyarlama | Claude Code | Tamamlandı: upstream uyarlaması bu dalda mevcut; D-004 sonrası kancalar/Modern look sırası MIG-8'de |
| MIG-5 — veri/protokol testi | Codex + kullanıcı | **Kısmi:** 7.0.112.75 + 7.0.20.0 açılış, Nimloth'a giriş, yürüme, koşma, ışınlanma geçti. Kalan oyun testleri ve shard özel asset eşlemeleri aşağıda |
| MIG-6 — yeni ağ/asset haritası | Codex | Tamamlandı: `docs/arch/network-assets.md`; yeni paket/taşıma ve Assets/IO/Renderer ayrımı |
| MIG-7 — çapraz inceleme | Claude Code + Codex | İki araç incelemesini tamamladı: Claude BUILD/network-assets (D-005), Codex rendering/LIGHTING_PLAN ve §8 kaynak doğrulaması. Aşağıdaki R1–R5 bulguları açık; doküman düzeltmesi/onayı Claude Code'da |
| MIG-8 — classicUO-T render/ışık uyarlaması | Claude Code | Tamamlandı: main'den nature, HD, zoom xBR, Modern look ve E10 ışık kompoziti kancası belgeleri alındı; Codex inceleme bulguları aşağıda |
| MIG-9 — mevcut özellikler, launcher ve CI | Codex | Tamamlandı: M3/M4/M5/M6 mevcut özellikleri kaynaklarıyla işaretlendi; BUILD.md launcher/CI ve yerel betik ayrımını içeriyor. `build.ps1` temiz klonda PowerShell 5.1 ile geçti; Windows CI restore/build/test (279 test) ve launcher publish başarılı. Betik/workflow değişikliği gerekmedi; sınırlar BUILD.md'de |

- [x] Eski `.gitignore` eklemeleri `e5595b222` üzerinden aynen taşındı; yeni tabanın mevcut kuralları korundu.
- [x] Oyun/protokol/asset/shader kaynakları değiştirilmeden build ve açılış doğrulandı.
- [ ] Kalan MIG-5 ve MIG-7 bulguları kapatılınca M0 kapanışı; çalışma dalları ana dalda uzlaştırılsın. İncelemenin tamamlanması açık bulguların kabulü anlamına gelmez.

### MIG-7 — Codex kaynak doğrulaması ve açık bulgular

İnceleme tabanı `c7cb63d92`; `docs/arch/rendering.md` ve `docs/LIGHTING_PLAN.md` kaynakla karşılaştırıldı. Render haritasındaki 44 mevcut dosya/yol veya kısa dosya adı, ışık planındaki 12 mevcut dosya referansı Git'in izlediği kaynaklarla eşleşti; açıkça yeni olarak tanımlanan Lighting dosyaları eksik dosya hatası sayılmadı. Ana döngü, batcher/shader, ışık toplama ve §8 nature/Decor, xBR, HD world/karakter, Modern look ve movement kancaları kontrol edildi. Yöntemler genel olarak mevcut; R4 sınıf adı istisnasıdır.

Bu görev kaynak incelemesidir; oyun kodu değişmediği için build/oyun testleri tekrarlanmadı. `rendering.md`, `LIGHTING_PLAN.md`, `DECISIONS.md` main'den alındı, Codex tarafından düzeltilmedi. Aşağıdaki satır numaraları inceleme tabanına aittir; öneriler ilgili belgelerin sahibine raporlandı.

| Bulgu | Kanıt ve etkisi | Takip / sahip |
| --- | --- | --- |
| R1 — P2: L1 ile kapalı durum sözleşmesi çelişiyor | LIGHTING_PLAN §1.5 / L3, Enhanced kapalıyken aynı görüntüyü istiyor; L1 (105–107) `_lights` dizisini ve `AddLight` limitini koşulsuz 512 yapıp 100 üzerindeki tüm ışıkları çizdirmeyi istiyor. Mevcut `src/ClassicUO.Client/Game/Scenes/GameScene.cs:55` ve `:407` 100 sınırını kullanır. 101–512 ışıklı sahnede koşulsuz artış kapalı görüntüyü değiştirir | [x] Claude Code (`395f2e7d`): L1 artık Enhanced kapalıyken 100 sınırını koruyor, L2+L3 sonrası. Önceki metin: legacy 100 sınırını koruyan koşulu veya açık sözleşme değişikliğini planda netleştir; L1 uygulanmadan çözülmeli |
| R2 — P2: RenderTargets kancası iki farklı tarifte | LIGHTING_PLAN §2 (37–39) “hangi ışık texture'ı çizilecek” kancası diyor; §3 (74–76) ve L6 (125–126) kompoziti GameScene/E10'da yapıp yalnız ışık katmanını atlama bayrağı istiyor. Mevcut `src/ClassicUO.Renderer/RenderTargets.cs:147` Light RT'yi doğrudan çizer; iki tarif farklı API/değişiklik gerektirir | [x] Claude Code (`395f2e7d`): §2 E10 + atlama bayrağıyla uzlaştırıldı. Önceki metin: §2'de kalan eski tarifi §3/L6 ve D-001'deki E10 + atlama bayrağı sözleşmesiyle uzlaştır |
| R3 — P2: GPU kabulü için ölçüm yöntemi eksik | LIGHTING_PLAN §1.6 GPU/kare ≤1.5 ms ister; L13 Profiler bağlamı, L14 FPS tablosu tarif eder. `src/ClassicUO.Utility/Profiler.cs:17`, `:26`, `:121` Stopwatch ile CPU geçen süreyi ölçer; bu değer GPU pass süresi değildir | [x] Claude Code (`395f2e7d`): L14 PIX/RenderDoc birincil yöntem, kare süresi farkı destek; L13 CPU etiketli. Ölçüm uygulaması L14'te. Önceki metin: GPU ölçüm aracını/yöntemini ayrıca tanımla; CPU overlay/FPS ile GPU bütçesi doğrulandı sayılmasın |
| R4 — P3: View sınıfı adı yanlış | rendering §5 (152) `View.CalculateDepthZ()` diyor; `src/ClassicUO.Client/Game/GameObjects/Views/View.cs:23` sınıfı `GameObject` abstract partial, metot `:36`'da. Dosya adı doğru ama `View` adlı sınıf yok | [x] Claude Code (`395f2e7d`): düzeltildi. Önceki metin: çağrı sahibini `GameObject.CalculateDepthZ()` olarak belgelemeli |
| R5 — P3: “meşale glow almaz” sonucu fazla kesin | rendering §8 (200) ve LIGHTING_PLAN §2 (29–31) ışık geçişinin Modern look'tan sonra olmasından meşalenin glow alamayacağı sonucunu çıkarıyor. `src/ClassicUO.Renderer/shaders/ModernLook.fx:36`–`:41` dünya görüntüsündeki parlak pikselleri işler; ışık kaynağının art'ı bu eşiği aşarsa glow üretebilir. Dışarıda kalan, sonradan eklenen Light RT katkısıdır | [x] Claude Code (`395f2e7d`): rendering §8 ve LIGHTING_PLAN §2 sınırlandı. Önceki metin: kaynak art glow'u ile Light RT katkısını ayırarak ifadeyi sınırla; görsel sonucu test olmadan genelleme |

M1 görevleri aşağıda orijinal ID/sahipleriyle aktarıldı. R1/R2/R3, ilgili uygulama ve kabul adımları öncesinde netleştirilmeli; bu kayıt ışık planının metnini değiştirmez.

### Eski fork takip işlerinin kapanışı

- [x] Eski `ClilocLoader.Load()` negatif uzunluk düzeltmesi — **geçersiz (D-002)**. 2020 tabanına ait iş kapatıldı; yeni tabanda BWT yolu var, referans veriyle açılışta eski hata görülmedi.
- [x] Eski `GumpsLoader.GetGumpPixels()` overflow düzeltmesi — **geçersiz (D-002)**. 2020 kod yoluna ait iş kapatıldı; yeni `GetGump()` zlib/BWT yollarıyla açılış geçti.
- [x] Newtonsoft.Json 12.0.2 / NU1903 bakım işi — **geçersiz (D-002)**. Yeni istemci ayar/profilleri System.Text.Json kullanıyor; eski paketi yükseltme işi taşınmadı.

### Yeni tabanda kalan doğrulamalar

- [x] Referans veri: `D:\Games\Electronic Arts\Ultima Online Classic`, 7.0.112.75; `clientversion=7.0.20.0`.
- [x] Sunucu `166.1.94.99:2593`, `encryption=0`; TCP erişimi ve kullanıcı oyuna girişi doğrulandı.
- [x] Kullanıcı: yürüme, koşma, ışınlanma; gözle görülür hata bildirilmedi.
- [ ] MIG-5: eşya/konteyner, shard gump'ları, ayrı harita/facet geçişi, büyü efektleri, `0x4E/0x4F` ışık ve `0x65` hava. Ayrıntılı liste: [BUILD.md](BUILD.md).
- [ ] MIG-5: shard'a özel asset uyumu; kullanıcı bazı MUL'ların `C:\UOSoft\Spherenet` paketine göre olduğunu bildirdi. Orijinal sette bazı art ID'leri eksik; logdaki index'ler BUILD.md'de. UO verilerini değiştirmek/karıştırmak bu görevde yapılmadı.
- [ ] Codex bakım değerlendirmesi: yeni tabanın ImageSharp 3.1.11 `NU1902/NU1903` uyarıları. Eski Newtonsoft işi yeniden açılmaz; kapsam/uyumluluk ayrı değerlendirilir.
- [ ] Launcher: trimmed publish'teki `ClientStarter.MergeSettings()` / `JsonArray.Add<T>()` IL2026 uyarısını değerlendirme ve yayınlanan launcher ile ayar/plugin yazma davranışını doğrulama. MIG-9 publish ve kaynak testleri geçti; launcher UI/updater kabulü verilmedi.
- [ ] İstenirse NativeAOT doğrulaması: Visual C++ bağlayıcısı ve Windows SDK sağlandıktan sonra `build.ps1 -NativeAot`. Managed build/yayın başarılıdır.

## M1 — Dinamik ışık sistemi

Kaynak: [LIGHTING_PLAN.md §5](LIGHTING_PLAN.md#5-görevler), inceleme tabanı `c7cb63d92`. **14 aktif görev**; L11 opsiyoneldir. **L5 kaldırıldı:** mevcut `fxc.exe`/`compile_shaders.bat` kullanımı L4'e dahildir; ID yeniden kullanılmaz. Yeni dosyalar bu aşamada oluşturulmadı. K (<½ gün), O (~1 gün), Z (2+ gün) planın zorluk tahminleridir.

| Durum / görev | Sahip | Zorluk | Kapsam ve kabul özeti |
| --- | --- | --- | --- |
| [ ] L1 — Işık limitini ID limitinden ayır | **Codex** | K | `Game/Constants.cs`: `MAX_LIGHTS_PER_FRAME=512`; `GameScene` dizi/limit kullanımı. ID kontrolü `LightsLoader`'da kalır; 100 üzeri ışık sahnesi doğrulanır. Kapalı durum sözleşmesi için önce R1 netleştirilsin |
| [ ] L2 — LightingSettings + Profile ayarları | **Codex** | K | Yeni `Game/Lighting/LightingSettings.cs`, `Configuration/Profile.cs`; §4 varsayılanları ve kalite önayarları. Eski profil açılışı ve source-gen `ProfileJsonContext` kaydet/yükle döngüsü |
| [x] L3 — LightManager'a taşıma | **Claude Code** | O | ✅ `6c96b7c0` (build + 279 test; oyun içi görsel kontrol bekliyor). GameScene +6/−186. Yeni `LightManager.cs`, `LightSource.cs`; `GameScene.AddLight` / `PrepareLightsRendering` delegasyonu. Enhanced kapalı gece görüntüsü aynı; GameScene diff'i <30 satır hedefi |
| [ ] L4 — Lighting.fx + LightingEffect | **Claude Code** | O | Yeni renderer `shaders/Lighting.fx` + `.fxc`, `Effects/LightingEffect.cs`; `Batcher2D.Resources` getter ve `compile_shaders.bat` satırı. `fx_2_0`/FileEmbed, D3D11 + OpenGL tek ışık testi |
| [ ] L6 — LightMapRenderer + kompozit | **Claude Code** | Z | Yeni client `Game/Lighting/LightMapRenderer.cs`, renderer `Lighting/LightMapTarget.cs`; GameScene E10 ve RenderTargets ışık katmanı bayrağı. Aynı kare/ölçekli RT/×2 kompozit; Modern look, xBR ve HD world kombinasyonları; gündüz eşliği ve zoom/DPI/resize/dispose. Önce R2 netleştirilsin |
| [ ] L7 — AmbientLight: geçiş + gün/gece tonu | **Claude Code** | O | Yeni `Game/Lighting/AmbientLight.cs`, light RT clear ve birim testleri. `0x4F` değişiminde smoothstep; tint kapalı son değer eski IsometricLevel ile eş; özel ışık + LightLevelType uyumu |
| [ ] L8 — lights_ex.txt tanımları | **Codex** | K–O | Yeni `Game/Lighting/LightDefinitions.cs`; mevcut `lights.txt`/`lightshaders.txt` korunur. `ITEM_ID RADIUS INTENSITY FLICKER`, bozuk satırları logla/atla; ≥25 varsayılan giriş, dosya yoksa örnekli yeniden üretim |
| [ ] L9 — Prosedürel ışık + RGB + flicker | **Claude Code** | O | LightMapRenderer, Lighting.fx/LightSprite, LightManager. Konum seed'i ve Time.Ticks ile FPS'ten bağımsız flicker; ayar kapalıyken sabit; mevcut ışık shader renkleri korunur |
| [ ] L10 — Blur + kalite seviyeleri | **Claude Code** | O | LightMapRenderer, Lighting.fx/BlurH/BlurV, LightingSettings. Low: ¼ RT/128 ışık/blur yok/light.mul; Medium: ½ RT/256/prosedürel; High: ½ RT/512/prosedürel/iki geçişli 9-tap blur; kenar kademelenmesi kontrolü |
| [ ] L11 — Kişisel ışık görselleştirmesi (opsiyonel) | **Claude Code** | O | LightManager/AmbientLight; `0x4E > 0` oyuncu çevresinde yumuşak hale, eski sistemden az görüş yok; IsometricLevel ölçek şüphesi doğrulanıp kaydedilir |
| [ ] L12 — Ayarlar arayüzü | **Codex** | O | `Game/UI/Gumps/OptionsGump.cs`, `Resources/ResGumps` kalıbı; §4 ayarları yeniden başlatmadan uygulanır |
| [ ] L13 — Debug overlay + ölçüm | **Codex** | K | Yeni `Game/Lighting/LightingDebug.cs`, CommandManager komutu ve Profiler bağlamı. Işık sayısı, RT boyutu, pass süresi, yalnız light map görüntüleme; R3 uyarınca CPU/GPU ölçümü açık etiketlenmeli |
| [ ] L14 — Performans ve kabul | **Claude Code**, Codex ölçüm desteği | O | Açık alan gece/şehir gece/zindan × Low/Medium/High FPS tablosu PLAN.md'ye. Hedef: GTX 1050/RX 560 sınıfı, 1080p, 200 ışıkta 60+ FPS ve GPU ışık süresi ≤1.5 ms; GPU yöntemi R3 ile tanımlansın |
| [ ] L15 — Modern look ile ışık uyumu | **Claude Code** | K | ModernLook.fx + `.fxc`, gerekirse GameScene.ApplyModernLook. Enhanced+Modern açıkken ışık/glow ve gece karanlığı; Enhanced kapalı Modern look görüntüsü aynı |

Tabloda client dosyaları `src/ClassicUO.Client/`, renderer dosyaları `src/ClassicUO.Renderer/` altındadır. Yeni `LightManager`, `LightSource`, `AmbientLight`, `LightDefinitions` client `Game/Lighting/` altında planlanır. §4 varsayılanları: Enhanced açık, kalite Medium, flicker/tint açık, geçiş 2 s, yoğunluk 1.0 (0.5–2.0).

**Bağımlılık sırası (planın aktarımı):** L3 ✅ → L2 → L1; L4 → L6 → L7/L8/L9/L10/L15 → L11/L12/L13 → L14. Bu sıralama M1 uygulaması başlatma talimatı değildir; aynı dosyalara paralel yazılacak işler sahipleri arasında koordine edilmeli.

`UseColoredLights`, `UseCustomLightLevel`/`LightLevel`/`LightLevelType`, `UseDarkNights`, `UseAlternativeLights` ve sunucu `World.Light.Real*` değerleri korunur. `SetTime(0x5B)` hâlâ boş; gece/gündüz hedefi `0x4F` seviyesine dayanır. D-003'ün kalan oyun testleri açık izlenir; bu görev hiçbir L işini uygulanmış olarak işaretlemez.

## M2 — Gölgeler

- [ ] Claude Code: yumuşak gölge/occlusion tekniği; oynanış, seçim, sıralama ve performans ölçütleri; net alt işler Codex'e.

## M3 — Shader efektleri

- [x] classicUO-T: nature effects; canlı çim tonları, çim kümeleri, bitki/ağaç rüzgâr salınımı. `Profile.NatureEffects`, Options bağlantısı; `src/ClassicUO.Client/Game/Data/NatureTiles.cs`, `Game/Map/ChunkMesh.cs`, `Game/Scenes/GameSceneDrawingSorting.cs` ve `src/ClassicUO.Renderer/shaders/IsometricWorld.fx` içinde mevcut.
- [ ] Claude Code: su/sis/hava shader hedeflerini mevcut efektlere göre daralt; `World.Weather` / `0x65` uyumu, aç/kapa, performans ve görsel kabul testlerini tanımla. Nature effects'in mevcut olması M3'ün tamamını kapatmaz.

## M4 — Akıcılık

- [x] classicUO-T: smooth movement; koşarken dönüş düzeltmesi, sabit adım ritmi ve kesintisiz adım seçeneği. `Profile.SmoothMovements`, `MovementTurnWhileRunningFix`, `MovementStepCadence`, `MovementContinuousSteps`; `src/ClassicUO.Client/Game/Managers/WalkerManager.cs`, `Game/GameObjects/Mobile.cs`, `Game/UI/Gumps/OptionsGump.cs` içinde uygulanmış.
- [x] classicUO-T: zoom sırasında xBR dünya filtresi; `Profile.ZoomPixelArtFilter`, `GameScene.UpscaleWorld()` ve düşük çözünürlüklü dünya RT yolu mevcut.
- [ ] Kamera/zoom/interpolasyon için kalan ihtiyacı ölçerek belirle; farklı FPS/gecikme ve Sphere/Source-X üzerinde hareket kabulü ayrı doğrulansın. Önceki kullanıcı yürüme/koşma testi yeni seçeneklerin tamamının ölçümü sayılmaz.

## M5 — Post-processing

- [x] classicUO-T: Modern look; renk düzenleme, parlak bölgelerde glow, keskinlik ve vignette. `Profile.ModernLook` ve Options aç/kapa; `src/ClassicUO.Renderer/Effects/ModernLookEffect.cs`, `shaders/ModernLook.fx`, `src/ClassicUO.Client/Game/Scenes/GameScene.cs` içindeki `ApplyModernLook()` ile uygulanmış.
- [x] Claude Code / MIG-8: ışık kompoziti dünya çizimi/xBR büyütme sonrasında, Modern look öncesinde E10'a konumlandırıldı. Bu tasarım uygulaması L6/L15'te; R2/R5 belge bulguları açık.
- [ ] Glow ile tam bloom zincirini aynı kabul etmeden kalan kalite/performans ihtiyacını ve kapalı durum görsel eşliğini değerlendir.

## M6 — Değiştirilebilir art paketleri

M6, [DECISIONS.md D-005](DECISIONS.md#d-005--asset-soyutlaması-iki-katman-içerik--sunum) gereği iki katmandır. İçerik neyin var olduğunu ve mantıksal piksel anlamını belirler; sunum bunun nasıl çizildiğini değiştirir. HD kapalı veya paket eksik durumunda oyun içeriği kaybolmamalı. Bu görev katmanların hiçbirini uygulamaz.

### M6.1 — İçerik (ClassicUO.Assets)

- [x] Tabanın özel `.art` → MUL/UOP fallback ve `UOFilesOverrideMap` yolları mevcut; bunlar yeniden uygulanacak iş olarak planlanmaz.
- [x] Mevcut başlangıç noktaları `ArtLoader.LoadOurs()`, `GumpsLoader.LoadOurs()`, `files_override`; içerik değişimini renderer HD katmanına taşımadan kullanma kararı D-005'te kabul edildi.
- [ ] Nimloth'un eksik art/gump/anim eşlemeleri: orijinal çözünürlükte doğru kaynak ve mantıksal ID sağlansın. MIG-5'te eksik görülen art index 127 ve 1006–1012 bu katmanda ele alınır; HD paketi çözüm yerine kullanılmaz. Bu dar iş M6 genel arayüzünden önce yapılabilir.
- [ ] Claude Code: genel içerik sağlayıcı sözleşmesini mevcut Assets loader/fallback yollarından büyüt; boyut, orijin, seçim maskesi ve UO piksel/hue anlamının tek kaynağı içerik olsun. Cursor gibi doğrudan CPU tüketicileri, cache yenileme ve eksik ID dönüşü kapsansın; net uygulama/doğrulama işleri Codex'e ayrılsın.
- [ ] Kabul: içerik, HD açık/kapalı aynı kimlik ve mantıksal boyut/seçim alanını sağlar; çarpışma, tiledata, ağ Graphic ID'leri ve animasyon grup anlamları korunur. Kullanıcı verileri repoya/release'e eklenmez.

### M6.2 — Sunum (ClassicUO.Renderer / ClassicUO.Client)

- [x] classicUO-T HD dünya paketi: `Profile.HdWorld`, `src/ClassicUO.Client/Game/Managers/HdWorldPack.cs`, `tools/hdpack/` ve `Data/HD/world/index.json` + PNG. İçerik atlasından ayrı HD atlası, asenkron decode ve sınırlı GPU upload; büyütmede 2×, diğer ölçeklerde 1× indirgenmiş texture.
- [x] classicUO-T HD karakter görünümü: `Profile.HdCharacters`, `HdCharacterSprites`, renderer `HdSpriteCache` ve `MobileView`. ≥1.25× raster büyütmede özgün animasyon atlasından 2× xBR kare üretilir; seçim/orijin içerikten kalır.
- [ ] Claude Code: çalışan `HdWorldPack` / `HdCharacterSprites` yollarını ortak sunum arayüzünde (D-005 örneği `IHdTextureSource`: tür/ID → Texture/UV) birleştir; animasyon kare anahtarları ve fiziksel UV/ölçek de tanımlansın. Mantıksal geometri, seçim ve sıra içerik katmanından alınmaya devam etsin.
- [ ] Harici HD animasyon paketini ayrıca değerlendir: mevcut karakter yolu çalışma anında xBR üretir; `tools/hdpack/extract_anims.py` çıktısının paket olarak tüketildiği doğrulanmadı. İleride normal/emissive haritaları sunum metadata'sı olarak eklenebilir; M1'in ön koşulu değildir.
- [ ] Kabul: paket yok/ID eksik/ayar kapalıyken içerik texture'ına dönüş; zoom, atlas bütçesi ve yenileme belleği gerçek yerel paketle doğrulansın. Sunum yalnız texture/UV'yi değiştirir; içerik, seçim ve çizim sırası değişmez. Yeni işler ölçüm sonrası sahiplerine ayrılsın.

Detay: [ağ/asset haritası](arch/network-assets.md), Claude Code'un MIG-7 HD ekleri ve D-005. Işık sistemi iki katmandan bağımsızdır; gelecekte emissive katkısı sunumdan light map'e eklenebilir.

## Çalışma düzeni

Bu MIG-7 Codex görevinde `main` birleştirildi; yalnız `docs/PLAN.md` düzenlendi. `rendering.md`, `LIGHTING_PLAN.md`, `DECISIONS.md` ve oyun koduna elle müdahale edilmedi. Her iş diğer araç tarafından incelenir; açık R1–R5 ve kalan MIG-5 kabulü ayrıca izlenir. M1 tablosu plan aktarımıdır; ışık uygulaması bu görevde başlamadı.
