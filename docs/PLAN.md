# Geliştirme planı

Son güncelleme: 16 Ekim 2026. D-004 ile ana repo `Yunusolcay/classicUO-T` (`origin`), taban upstream `ee79d7eb` + classicUO-T özellikleri. MIG-9 incelemesi `fe0244de4` üzerinden yapıldı; MIG-7 Codex incelemesi için `main` (`c7cb63d92`) `codex/dev` dalına çakışmasız fast-forward ile birleştirildi. Eski repo `old-origin`, 2020 tabanı `legacy/2020` yalnız referanstır. Göç ayrıntısı: [MIGRATION.md](MIGRATION.md).

L2 için `main` (`a8a65b83a`) zaten `codex/dev` içindeydi; `claude/dev` önce `6af2e43a5`, ardından çalışma sırasında gelen L4 (`8effbef48`) ile çakışmasız fast-forward birleştirildi. Aşağıdaki L2 doğrulaması bu birleşik tabana aittir.

L1 için `claude/dev` (`8a9dc3e8b`, L6) `codex/dev` dalına çakışmasız fast-forward birleştirildi. L1 kare limiti bu tabanın `GameSceneLighting.UseEnhancedLighting` sonucu üzerinden seçilir; yalnız profil bayrağına bakılmaz.

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
| MIG-7 — çapraz inceleme | Claude Code + Codex | İki araç incelemesini tamamladı: Claude BUILD/network-assets (D-005), Codex rendering/LIGHTING_PLAN ve §8 kaynak doğrulaması. R1–R5 doküman düzeltmeleri Claude Code tarafından `395f2e7d` ile yapıldı; `6af2e43a5` ile kapatıldı |
| MIG-8 — classicUO-T render/ışık uyarlaması | Claude Code | Tamamlandı: main'den nature, HD, zoom xBR, Modern look ve E10 ışık kompoziti kancası belgeleri alındı; Codex inceleme bulguları aşağıda |
| MIG-9 — mevcut özellikler, launcher ve CI | Codex | Tamamlandı: M3/M4/M5/M6 mevcut özellikleri kaynaklarıyla işaretlendi; BUILD.md launcher/CI ve yerel betik ayrımını içeriyor. `build.ps1` temiz klonda PowerShell 5.1 ile geçti; Windows CI restore/build/test (279 test) ve launcher publish başarılı. Betik/workflow değişikliği gerekmedi; sınırlar BUILD.md'de |

- [x] Eski `.gitignore` eklemeleri `e5595b222` üzerinden aynen taşındı; yeni tabanın mevcut kuralları korundu.
- [x] Oyun/protokol/asset/shader kaynakları değiştirilmeden build ve açılış doğrulandı.
- [ ] Kalan MIG-5 kabulü tamamlanınca M0 kapanışı; çalışma dalları ana dalda uzlaştırılsın. MIG-7 R1–R5 belge bulguları kapalıdır; oyun içi kabulün yerine geçmez.

### MIG-7 — Codex kaynak doğrulaması ve bulgu kaydı

İnceleme tabanı `c7cb63d92`; `docs/arch/rendering.md` ve `docs/LIGHTING_PLAN.md` kaynakla karşılaştırıldı. Render haritasındaki 44 mevcut dosya/yol veya kısa dosya adı, ışık planındaki 12 mevcut dosya referansı Git'in izlediği kaynaklarla eşleşti; açıkça yeni olarak tanımlanan Lighting dosyaları eksik dosya hatası sayılmadı. Ana döngü, batcher/shader, ışık toplama ve §8 nature/Decor, xBR, HD world/karakter, Modern look ve movement kancaları kontrol edildi. Yöntemler genel olarak mevcut; R4 sınıf adı istisnasıdır.

Bu görev kaynak incelemesidir; oyun kodu değişmediği için build/oyun testleri tekrarlanmadı. `rendering.md`, `LIGHTING_PLAN.md`, `DECISIONS.md` main'den alındı, Codex tarafından düzeltilmedi. Aşağıdaki satır numaraları inceleme tabanına aittir; öneriler ilgili belgelerin sahibine raporlandı.

| Bulgu | Kanıt ve etkisi | Takip / sahip |
| --- | --- | --- |
| R1 — P2: L1 ile kapalı durum sözleşmesi çelişiyor | LIGHTING_PLAN §1.5 / L3, Enhanced kapalıyken aynı görüntüyü istiyor; L1 (105–107) `_lights` dizisini ve `AddLight` limitini koşulsuz 512 yapıp 100 üzerindeki tüm ışıkları çizdirmeyi istiyor. Mevcut `src/ClassicUO.Client/Game/Scenes/GameScene.cs:55` ve `:407` 100 sınırını kullanır. 101–512 ışıklı sahnede koşulsuz artış kapalı görüntüyü değiştirir | [x] Claude Code (`395f2e7d`): L1 artık Enhanced kapalıyken 100 sınırını koruyor, L2+L3 sonrası. Önceki metin: legacy 100 sınırını koruyan koşulu veya açık sözleşme değişikliğini planda netleştir; L1 uygulanmadan çözülmeli |
| R2 — P2: RenderTargets kancası iki farklı tarifte | LIGHTING_PLAN §2 (37–39) “hangi ışık texture'ı çizilecek” kancası diyor; §3 (74–76) ve L6 (125–126) kompoziti GameScene/E10'da yapıp yalnız ışık katmanını atlama bayrağı istiyor. Mevcut `src/ClassicUO.Renderer/RenderTargets.cs:147` Light RT'yi doğrudan çizer; iki tarif farklı API/değişiklik gerektirir | [x] Claude Code (`395f2e7d`): §2 E10 + atlama bayrağıyla uzlaştırıldı. Önceki metin: §2'de kalan eski tarifi §3/L6 ve D-001'deki E10 + atlama bayrağı sözleşmesiyle uzlaştır |
| R3 — P2: GPU kabulü için ölçüm yöntemi eksik | LIGHTING_PLAN §1.6 GPU/kare ≤1.5 ms ister; L13 Profiler bağlamı, L14 FPS tablosu tarif eder. `src/ClassicUO.Utility/Profiler.cs:17`, `:26`, `:121` Stopwatch ile CPU geçen süreyi ölçer; bu değer GPU pass süresi değildir | [x] Claude Code (`395f2e7d`): L14 PIX/RenderDoc birincil yöntem, kare süresi farkı destek; L13 CPU etiketli. Ölçüm uygulaması L14'te. Önceki metin: GPU ölçüm aracını/yöntemini ayrıca tanımla; CPU overlay/FPS ile GPU bütçesi doğrulandı sayılmasın |
| R4 — P3: View sınıfı adı yanlış | rendering §5 (152) `View.CalculateDepthZ()` diyor; `src/ClassicUO.Client/Game/GameObjects/Views/View.cs:23` sınıfı `GameObject` abstract partial, metot `:36`'da. Dosya adı doğru ama `View` adlı sınıf yok | [x] Claude Code (`395f2e7d`): düzeltildi. Önceki metin: çağrı sahibini `GameObject.CalculateDepthZ()` olarak belgelemeli |
| R5 — P3: “meşale glow almaz” sonucu fazla kesin | rendering §8 (200) ve LIGHTING_PLAN §2 (29–31) ışık geçişinin Modern look'tan sonra olmasından meşalenin glow alamayacağı sonucunu çıkarıyor. `src/ClassicUO.Renderer/shaders/ModernLook.fx:36`–`:41` dünya görüntüsündeki parlak pikselleri işler; ışık kaynağının art'ı bu eşiği aşarsa glow üretebilir. Dışarıda kalan, sonradan eklenen Light RT katkısıdır | [x] Claude Code (`395f2e7d`): rendering §8 ve LIGHTING_PLAN §2 sınırlandı. Önceki metin: kaynak art glow'u ile Light RT katkısını ayırarak ifadeyi sınırla; görsel sonucu test olmadan genelleme |

M1 görevleri aşağıda orijinal ID/sahipleriyle aktarıldı. R1/R2/R3 sözleşmeleri Claude Code'un belge düzeltmelerinde netleştirildi; uygulama ve görsel/GPU kabul adımları ilgili L görevlerinde izlenir.

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

Kaynak: [LIGHTING_PLAN.md §5](LIGHTING_PLAN.md#5-görevler), güncel taban `8a9dc3e8b`. **14 aktif görev**; L11 opsiyoneldir. **L5 kaldırıldı:** mevcut `fxc.exe`/`compile_shaders.bat` kullanımı L4'e dahildir; ID yeniden kullanılmaz. L3/L4 ve L6 kodu Claude Code'dan birleştirildi; L1/L2 aşağıdaki doğrulamalarla uygulandı. L6 oyun içi kabulü bekliyor. K (<½ gün), O (~1 gün), Z (2+ gün) planın zorluk tahminleridir.

| Durum / görev | Sahip | Zorluk | Kapsam ve kabul özeti |
| --- | --- | --- | --- |
| [x] L1 — Işık limitini ID limitinden ayır | **Codex** | K | `LightManager`: 512 dizi kapasitesi, varsayılan/legacy limit 100, `MaxLights` üzerinden toplama. `GameScene.Draw` her kare gerçek Enhanced sonucuyla 128/256/512 veya 100 seçer. ID kontrolü `LightsLoader` sınırında kalır. 11 yeni birim testi geçti; 101+ ışıklı kapalı sahnenin görsel karşılaştırması bekliyor |
| [x] L2 — LightingSettings + Profile ayarları | **Codex** | K | Yeni `Game/Lighting/LightingSettings.cs`, altı düz `Profile` özelliği, §4 varsayılanları ve kalite önayarları. `ProfileJsonContext` ile eski profil yükleme/kaydetme ve tüm kalite seviyelerinde round-trip testleri geçti. Ayarlar L6/L1 tarafından kullanılmaya başladı; L2 commit'inde davranış bağlantısı yoktu. Doğrulama aşağıda |
| [x] L3 — LightManager'a taşıma | **Claude Code** | O | ✅ `6c96b7c0` (build + 279 test; oyun içi görsel kontrol bekliyor). GameScene +6/−186. Yeni `LightManager.cs`, `LightSource.cs`; `GameScene.AddLight` / `PrepareLightsRendering` delegasyonu. Enhanced kapalı gece görüntüsü aynı; GameScene diff'i <30 satır hedefi |
| [x] L4 — Lighting.fx + LightingEffect | **Claude Code** | O | Tamamlandı (`8effbef48`): shader derleme/gömme ve effect sembollerini doğrulayan 8 birim testi geçti. L6'nın `EnsureResources` yükleme/fallback kancası da `8a9dc3e8b` ile alındı. D3D11 + OpenGL GPU yükleme/tek ışık görsel kabulü L6'da bekliyor |
| [x] L6 — LightMapRenderer + kompozit | **Claude Code** | Z | ✅ `8a9dc3e8b` + L6.1 `8f020df7b`/`7d72620e2`: oyun içi kabul alındı (2026-10-16). Kompozit shader'lı, yumuşak overbright tavanı (0.35 × `light_intensity`), ışık renkleri %75 doygunluk. Düzeltilen hata: kompozit s1'i (hue tablosu) boşaltıp karakter/gump'ları siyah çizdiriyordu; dünya artık s3'te |
| [ ] L7 — AmbientLight: geçiş + gün/gece tonu | **Claude Code** | O | 🔶 `0a6a80f72`, main'de (`6cc105d1b`); oyun içi kabul bekliyor. Smoothstep geçiş (`light_transition_seconds`), ay ışığı tonu (`day_night_tint`), 6 birim testi |
| [ ] L8 — lights_ex.txt tanımları | **Claude Code** (Codex yerine) | K–O | 🔶 `fc1160daa`; oyun içi kabul bekliyor. `ITEM_ID[-LAST] RADIUS INTENSITY FLICKER [SHAPE]`, tiledata LightSource eşyalarından varsayılanlar, invariant sayı okuma, 13 birim testi |
| [ ] L9 — Prosedürel ışık + RGB + flicker | **Claude Code** | O | 🔶 `fc1160daa`; oyun içi kabul bekliyor. Yalnız Enhanced + tanımlı ışıklar: hue alfasıyla yoğunluk/titreme, ölçekle boyut, `round` için gri radyal doku (renk tabloları korunur). `LightFlicker` 6 birim testi |
| [ ] L10 — Blur + kalite seviyeleri | **Claude Code** | O | 🔶 `71972adb2`, main'de (`6cc105d1b`); oyun içi kabul bekliyor. High: light map ayrık BlurH/BlurV (aynı boyutta ping-pong RT) |
| [ ] L11 — Kişisel ışık görselleştirmesi (opsiyonel) | **Claude Code** | O | 🔶 kod hazır; oyun içi kabul bekliyor. Ambient tabanı aynı, Enhanced yolunda oyuncu çevresine yumuşak hale (0x4E). `IsometricLevel` ölçeği tutarlı bulundu (LIGHTING_PLAN L11 notu) |
| [ ] L12 — Ayarlar arayüzü | **Claude Code** (Codex yerine) | O | 🔶 `76fd039ba`; oyun içi kabul bekliyor. Video → Lights: Enhanced, kalite, titreme, gece tonu, geçiş (0–5 sn), yoğunluk (%50–200); Default düğmesi §4'e döner |
| [ ] L13 — Debug overlay + ölçüm | **Claude Code** (Codex yerine) | K | 🔶 kod hazır; oyun içi kabul bekliyor. `-lightdebug` (FPS kutusunda ışık sayısı, light map boyutu, CPU pass süresi "not GPU time"), `-lightdebug map` (yalnız light map). 2 birim testi |
| [ ] L14 — Performans ve kabul | **Claude Code**, Codex ölçüm desteği | O | Açık alan gece/şehir gece/zindan × Low/Medium/High FPS tablosu PLAN.md'ye. Hedef: GTX 1050/RX 560 sınıfı, 1080p, 200 ışıkta 60+ FPS ve GPU ışık süresi ≤1.5 ms; PIX/RenderDoc birincil ölçüm, kare süresi farkı destek; CPU Profiler GPU ölçümü sayılmaz |
| [ ] L15 — Modern look ile ışık uyumu | **Claude Code** | K | 🔶 `f24da74b2`, main'de (`9be705db8`); oyun içi kabul bekliyor. `ModernLook.fx` `LitScene`: Enhanced karede glow eşiği/gücü, parlak bölgede parlatma/vibrance/S-eğrisi geri çekilir, sıcak vurgu kayması kapanır; `LitScene` 0 iken eski matematik |

Tabloda client dosyaları `src/ClassicUO.Client/`, renderer dosyaları `src/ClassicUO.Renderer/` altındadır. `LightManager`, `LightSource`, `LightingSettings` client `Game/Lighting/` altında mevcut; `AmbientLight`, `LightDefinitions` aynı dizinde planlanır. §4 varsayılanları: Enhanced açık, kalite Medium, flicker/tint açık, geçiş 2 s, yoğunluk 1.0 (0.5–2.0).

**Bağımlılık sırası:** L3 ✅ → L2 ✅ → L1 ✅; L4 ✅ → L6 ✅ → L7/L8/L9/L10/L15 (🔶 oyun içi kabul) → L11/L12 (🔶)/L13 → L14. 2026-10-16: Codex başka işlerde; kalan ışık görevlerinin tamamı Claude Code'da.

`UseColoredLights`, `UseCustomLightLevel`/`LightLevel`/`LightLevelType`, `UseDarkNights`, `UseAlternativeLights` ve sunucu `World.Light.Real*` değerleri korunur. `SetTime(0x5B)` yalnız güneş gölgesi saatini (`SunClock`) besler; gece/gündüz karanlığı hâlâ `0x4F` seviyesine dayanır. D-003'ün kalan oyun testleri açık izlenir.

### L1 — Uygulama ve doğrulama (Codex)

- `LightManager.MAX_LIGHTS_PER_FRAME=512` yalnız toplama dizisinin kapasitesidir; varsayılan `MaxLights` ve `LEGACY_MAX_LIGHTS` 100'dür. Özellik 0–512 aralığına sınırlandırılır; `Add` dolu koleksiyonda dünya/asset çözümlemesine girmeden döner. Çözülmüş ışığı ekleyen `TryAdd` aynı limiti uygular ve geçersiz ID için slot tüketmez. `LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT` artık yalnız ışık ID doğrulamasında kullanılır.
- `GameScene.Draw`, `UseEnhancedLighting` sonucunun hemen ardından, legacy çizim ve yeni kare toplama başlamadan `MaxLights` seçer: gerçek Enhanced karede `LightingSettings.GetQualityPreset(...).MaxLights` (128/256/512), diğer karelerde 100. Profil açık olsa bile gündüz, alternatif ışık, ölü siyah-beyaz görünüm, eksik dünya RT'si veya shader yükleme fallback'i 100 sınırını korur. ID/renk/örtme çözümleme kuralları ve legacy çizim sırası değişmedi.
- GPU, gerçek UO verisi ve global profil gerektirmeyen 11 `LightManagerTests` vakası: varsayılan 100 ve ek ışık reddi; Low/Medium/High 128/256/512; 100 farklı ID'den fazla kaynak; ID 99 kabul/100 ret; negatif/sıfır/kapasite üstü limit; limit artışından sonra toplamaya devam; dolu `Add` yolunun dünya/asset verisine erişmeden dönmesi.
- `dotnet build ClassicUO.sln -c Release /p:PublishAot=false`: **0 hata**. `dotnet test tests/ClassicUO.UnitTests/ClassicUO.UnitTests.csproj -c Release --no-build`: **309/309 geçti**, 0 atlanan (298 önceki + 11 L1). Mevcut NuGet/derleyici/analiz uyarıları sürüyor. Yerel kanıtlar: `artifacts/build/l1-build.log`, `artifacts/build/l1-test.log`, `artifacts/test-results/l1/l1-tests.trx` (ignore kapsamında).
- [ ] Claude Code: L1 çapraz review; 101+ ışıklı gece sahnesinde Enhanced kapalı ekran karşılaştırması. Bu görevde oyun içi/GPU testi yapılmadı; L6 görsel kabulü ve L14 ölçümü açık.

### L2 — Uygulama ve doğrulama (Codex)

- Altı ayar `Profile` içinde mevcut düz property kalıbıyla saklanır; mevcut `ProfileJsonContext` source-gen kullanılır. JSON anahtarları `enhanced_lighting`, `lighting_quality`, `light_flicker`, `day_night_tint`, `light_transition_seconds`, `light_intensity`; kalite sayısaldır: Low=0, Medium=1, High=2.
- `LightingSettings` §4 varsayılanlarını ve Low/Medium/High kalite verisini içerir: ¼/½/½ RT, 128/256/512 ışık, yalnız High'da iki geçişli 9-tap blur, Low'da prosedürel düşüş kapalı. Bilinmeyen kalite değeri önayar sorgusunda Medium'a döner. L2 (`1d1ff4be7`) aşamasında çizim bağlantısı yoktu; sonraki L6/L1 entegrasyonu RT ölçeğini ve kare limitini kullanır. L7/L9/L10/L12 uygulanmadı.
- Eski ayarları içeren, yeni altı anahtarı bulunmayan profil dosyası `ConfigurationResolver.Load` ile açıldı; eski ışık ve ses değerleri korundu, §4 varsayılanları geldi. Aynı dosya source-gen üzerinden kaydedilip yeniden yüklendi. Tüm kalite değerleri, açıkça kapalı bayraklar, 0 saniye geçiş, kesirli süre/yoğunluk ve 0.5/2.0 yoğunluk uçları dosya round-trip testlerinden geçti. Bu kayıt profil yükleme testidir; yeni bir oyun içi giriş testi yapılmadı.
- İlk test Türkçe kültürde `LightIntensity` için mevcut `ToLower()` politikasının noktasız `ı` ürettiğini ve kaydet/yükle değerinin kaybolduğunu yakaladı. Yalnız yeni alana `[JsonPropertyName("light_intensity")]` eklendi; en-US/tr-TR bağımsız context testleri geçti. Mevcut profil anahtarlarının adlandırma politikası değiştirilmedi.
- .NET SDK 10.0.113: `dotnet build ClassicUO.sln -c Release /p:PublishAot=false` başarılı, **0 hata**; tabanın mevcut NuGet/derleyici/analiz uyarıları sürüyor. `dotnet test tests/ClassicUO.UnitTests/ClassicUO.UnitTests.csproj -c Release --no-build`: **298/298 geçti**, 0 atlanan (279 önceki + 8 L4 + 11 L2). Yerel kanıtlar: `artifacts/build/l2-build.log`, `artifacts/build/l2-test.log`, `artifacts/test-results/l2/l2-tests.trx` (ignore kapsamında).
- [ ] Claude Code: L2 çapraz review. Oyun içi ışık/GPU kabulü sonraki M1 görevlerinde kalır.

## M2 — Gölgeler

Kaynak: [SHADOWS_PLAN.md](SHADOWS_PLAN.md).

- [x] Claude Code: teknik ve kabul ölçütleri (`SHADOWS_PLAN.md`).
- [x] S1 — Yumuşak gölge: ertelenmiş gölge geçişi (alfa maske, max blend, tek karartma), kenar yumuşatma, gece solması; `SoftShadows` ayarı. Yayında (`3986f0b90`); derinlik düzeltmesi S2 ile.
- [ ] S2 — Kenar payı · S3 — Yerel ışık gölgeleri · S4 — Mobil temas gölgesi · S5 — `-lightdebug` gölge sayısı. 🔶 kod hazır (347 test), oyun içi kabul bekliyor. Bina dibi AO kapsam dışı (S6 adayı).

## M3 — Shader efektleri

- [x] classicUO-T: nature effects; canlı çim tonları, çim kümeleri, bitki/ağaç rüzgâr salınımı. `Profile.NatureEffects`, Options bağlantısı; `src/ClassicUO.Client/Game/Data/NatureTiles.cs`, `Game/Map/ChunkMesh.cs`, `Game/Scenes/GameSceneDrawingSorting.cs` ve `src/ClassicUO.Renderer/shaders/IsometricWorld.fx` içinde mevcut.
- [ ] W1/W2 su shader'ı (ışık deseni, parıltı, kıyı köpüğü) · H1–H4 gelişmiş yağmur/kar/fırtına/şimşek (bkz. [WATER_WEATHER_PLAN.md](WATER_WEATHER_PLAN.md)). 🔶 kod hazır (358 test), oyun içi kabul bekliyor.
- [ ] Claude Code: su/sis/hava shader hedeflerini mevcut efektlere göre daralt; `World.Weather` / `0x65` uyumu, aç/kapa, performans ve görsel kabul testlerini tanımla. Nature effects'in mevcut olması M3'ün tamamını kapatmaz.

## M4 — Akıcılık

- [x] classicUO-T: smooth movement; koşarken dönüş düzeltmesi, sabit adım ritmi ve kesintisiz adım seçeneği. `Profile.SmoothMovements`, `MovementTurnWhileRunningFix`, `MovementStepCadence`, `MovementContinuousSteps`; `src/ClassicUO.Client/Game/Managers/WalkerManager.cs`, `Game/GameObjects/Mobile.cs`, `Game/UI/Gumps/OptionsGump.cs` içinde uygulanmış.
- [x] classicUO-T: zoom sırasında xBR dünya filtresi; `Profile.ZoomPixelArtFilter`, `GameScene.UpscaleWorld()` ve düşük çözünürlüklü dünya RT yolu mevcut.
- [x] C1 — Yumuşak zoom · C2 — Alt-piksel kamera (bkz. [CAMERA_PLAN.md](CAMERA_PLAN.md)). Yayında (`a26041319`); kullanıcı kabulü 2026-10-16 ("daha akıcı"). C3 (diğer mobiller) 🔶 kod hazır; C4/C5 gerekçeli olarak yapılmadı (CAMERA_PLAN §4).
- [ ] Kamera/zoom/interpolasyon için kalan ihtiyacı ölçerek belirle; farklı FPS/gecikme ve Sphere/Source-X üzerinde hareket kabulü ayrı doğrulansın. Önceki kullanıcı yürüme/koşma testi yeni seçeneklerin tamamının ölçümü sayılmaz.

## M5 — Post-processing

- [x] classicUO-T: Modern look; renk düzenleme, parlak bölgelerde glow, keskinlik ve vignette. `Profile.ModernLook` ve Options aç/kapa; `src/ClassicUO.Renderer/Effects/ModernLookEffect.cs`, `shaders/ModernLook.fx`, `src/ClassicUO.Client/Game/Scenes/GameScene.cs` içindeki `ApplyModernLook()` ile uygulanmış.
- [x] Claude Code / MIG-8: ışık kompoziti dünya çizimi/xBR büyütme sonrasında, Modern look öncesinde E10'a konumlandırıldı. R2/R5 belge bulguları kapatıldı; bu tasarım uygulaması ve görsel kabul L6/L15'te.
- [ ] Bloom zinciri (1/4 çözünürlük bright pass + 2× blur + additive; Modern look glow'u devralır), bkz. [BLOOM_PLAN.md](BLOOM_PLAN.md). 🔶 kod hazır (361 test), oyun içi kabul bekliyor.

## M7 — Ek görseller (2026-10-17)

- [ ] Büyü/efekt görselleri: renkli efekt ışıkları, mermi izi, çarpma parlaması ([EFFECTS_PLAN.md](EFFECTS_PLAN.md)). 🔶 yayında, oyun içi kabul bekliyor.
- [ ] Hedef/hover konturu, notoriety renginde ([OUTLINE_PLAN.md](OUTLINE_PLAN.md)). 🔶 yayında, oyun içi kabul bekliyor.
- [ ] Bölge atmosferi: zindan/kar/çöl/jungle/bataklık/volkanik/void tonu ve pusu ([ATMOSPHERE_PLAN.md](ATMOSPHERE_PLAN.md)). 🔶 yayında, oyun içi kabul bekliyor.
- [x] ~~Zemin geçişleri~~ — iptal (2026-10-17). İki sürüm denendi (komşu texmap'in kenardan sönen saydam şeridi; ardından opak, gürültülü, tek taraflı kıyı). Oyun içi testte ikisi de doğal görünmedi (hayalet/çamur görüntü, karo ızgarasını vurgulayan desen); kod geri alındı (`2328cbce8`, `f8f75b8a1`). UO'nun kendi geçiş karoları yeterli kabul edildi; tekrar ele alınırsa çözüm büyük olasılıkla sanatçı yapımı geçiş dokuları (M6 içerik katmanı) olur, shader değil.

## M8 — Sinematik görünüm (2026-10-17)

Kullanıcı geri bildirimi: mevcut iyileştirmeler fazla ince, oyun "farklı bir oyun" gibi görünmeli.

- [x] Son geçiş `Cinematic.fx` + `CinematicRenderer`: film tonu, camgöbeği/kehribar ayrımı, yerel kontrast,
  doygunluk, vinyet ([CINEMATIC_PLAN.md](CINEMATIC_PLAN.md)). "Cinematic depth" (tilt-shift, uzak pus) kullanıcı
  beğenmediği için kaldırıldı (2026-10-17).
  Varsayılan açık, iki ayar ayrı kapatılabilir. Shader testleri (398 test).
- [ ] Oyun içi kabul ve ince ayar (gündüz/gece, zindan, Modern look ile birlikte vinyetin toplamı).
- [x] Duvar/obje diplerinde ortam gölgesi (AO, [SHADOWS_PLAN.md](SHADOWS_PLAN.md) S6): yumuşak gölge geçişinde
  elips; varsayılan açık, "Ambient occlusion" ile kapatılır. 400 test.
- [ ] Oyun içi kabul: AO şeridinin gücü/genişliği, ev içi ve üst katlar.
- [x] Saate göre dönen güneş gölgesi (S7, [SHADOWS_PLAN.md](SHADOWS_PLAN.md)): `SunClock` (sunucu `0x5B`, yoksa
  UTC/OSI hızı), `SunPath` (yön + uzunluk + ufukta sönme); Options → Shadows → "Sun shadow direction". 414 test.
- [ ] S7 oyun içi kabul: Nimloth `0x5B` gönderiyor mu (`-lightdebug` "Sun clock"), uzun gölgelerin görünümü.

## M9 — Yüzey ışığı (2026-10-17)

Kullanıcı hedefi: dünya "yeni bir oyun" gibi görünsün. Yeni 2.5D çizimler kullanıcı tarafında yapay zekâ ile
üretilecek (önerilen araç ve prompt sohbette verildi); motor tarafı yüzey ışığı.

- [x] HD dokulardan yüklenirken normal haritası, s3'te bağlama, `IsometricWorld.fx` kabartma + girinti
  ([SURFACE_LIGHTING_PLAN.md](SURFACE_LIGHTING_PLAN.md)). Varsayılan açık, "Surface lighting" ile kapatılır. 404 test.
- [ ] Oyun içi kabul ve güç ayarı.
- [ ] Gece ışıklarının normal haritasını kullanması; saate göre dönen güneş.
- [x] Zemin varyasyonu: land/çimen modlarında dünya konumlu parlaklık, sıcaklık, doygunluk lekeleri; düz land için
  mod 14 ([GROUND_VARIATION_PLAN.md](GROUND_VARIATION_PLAN.md)). Varsayılan açık. Yan düzeltme: chunk mesh su
  karolarının su modu her karede eziliyordu. 406 test.
- [x] Zemin varyasyonu ilk oyun içi düzeltmeler: gürültü hash'inden kaynaklı düz dikiş giderildi; klasik çimen
  zeytin yeşiline yumuşatıldı (GROUND_VARIATION_PLAN.md).
- [ ] Zemin varyasyonu oyun içi kabul.

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
- [x] HD dünya paketinin dağıtımı (2026-10-17): paket özel `Yunusolcay/classicUO-T-HD-pack` reposunun `hd-world`
  release'inde (`tools/hdpack/publish_pack.py`: import → `manifest.xml` + `hd-world.zip` → `gh release upload`).
  Launcher (`HdPackUpdater`) Ayarlar'daki salt-okunur erişim anahtarıyla (DPAPI ile saklanır) GitHub API'den
  manifesti alır, `Data/HD/world` dışına yazmayı reddeder, yalnız değişen PNG'leri indirir; hata oyunu başlatmayı
  engellemez. Depo adı Deploy'da `vars.HD_PACK_REPOSITORY` ile `launcher.defaults.json`'a girer. İstemci kurulu paketi
  profil başına bir kez otomatik açar (`HdWorldAutoEnabled`). İlk yayın: V2 redraw'ın 1096 görseli, 4291 dosya, 56 MB.
- [x] classicUO-T HD karakter görünümü: `Profile.HdCharacters`, `HdCharacterSprites`, renderer `HdSpriteCache` ve `MobileView`. ≥1.25× raster büyütmede özgün animasyon atlasından 2× xBR kare üretilir; seçim/orijin içerikten kalır.
- [ ] Claude Code: çalışan `HdWorldPack` / `HdCharacterSprites` yollarını ortak sunum arayüzünde (D-005 örneği `IHdTextureSource`: tür/ID → Texture/UV) birleştir; animasyon kare anahtarları ve fiziksel UV/ölçek de tanımlansın. Mantıksal geometri, seçim ve sıra içerik katmanından alınmaya devam etsin.
- [ ] Harici HD animasyon paketini ayrıca değerlendir: mevcut karakter yolu çalışma anında xBR üretir; `tools/hdpack/extract_anims.py` çıktısının paket olarak tüketildiği doğrulanmadı. İleride normal/emissive haritaları sunum metadata'sı olarak eklenebilir; M1'in ön koşulu değildir.
- [ ] Kabul: paket yok/ID eksik/ayar kapalıyken içerik texture'ına dönüş; zoom, atlas bütçesi ve yenileme belleği gerçek yerel paketle doğrulansın. Sunum yalnız texture/UV'yi değiştirir; içerik, seçim ve çizim sırası değişmez. Yeni işler ölçüm sonrası sahiplerine ayrılsın.

Detay: [ağ/asset haritası](arch/network-assets.md), Claude Code'un MIG-7 HD ekleri ve D-005. Işık sistemi iki katmandan bağımsızdır; gelecekte emissive katkısı sunumdan light map'e eklenebilir.

## Çalışma düzeni

L1 Codex görevinde `claude/dev` birleştirildi; doğrudan değişiklikler `LightManager.cs`, `GameScene.Draw` kare limiti, L1 testleri ve bu planla sınırlıdır. `rendering.md`, `LIGHTING_PLAN.md`, `DECISIONS.md` için elle düzeltme yapılmadı; Claude Code'un L6 kaynakları ve dokümanları merge ile alındı. Her iş diğer araç tarafından incelenir; L1/L2 çapraz review, L6 oyun içi kabulü ve kalan MIG-5 doğrulaması ayrıca izlenir.
