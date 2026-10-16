# Geliştirme planı

Son güncelleme: 16 Ekim 2026. D-004 ile ana repo `Yunusolcay/classicUO-T` (`origin`), taban upstream `ee79d7eb` + classicUO-T özellikleri. MIG-9 incelemesi `fe0244de4` üzerinden `codex/dev` dalında yapıldı. Eski repo `old-origin`, 2020 tabanı `legacy/2020` yalnız referanstır. Göç ayrıntısı: [MIGRATION.md](MIGRATION.md).

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
| MIG-7 — çapraz inceleme | Claude Code + Codex | Bekliyor: Claude BUILD/network-assets; Codex güncel rendering/LIGHTING_PLAN |
| MIG-8 — classicUO-T render/ışık uyarlaması | Claude Code | Bekliyor: nature, HD, zoom xBR, Modern look ve ışık kompoziti sırası; bu görev ilgili belgeleri değiştirmez |
| MIG-9 — mevcut özellikler, launcher ve CI | Codex | Tamamlandı: M3/M4/M5/M6 mevcut özellikleri kaynaklarıyla işaretlendi; BUILD.md launcher/CI ve yerel betik ayrımını içeriyor. `build.ps1` temiz klonda PowerShell 5.1 ile geçti; Windows CI restore/build/test (279 test) ve launcher publish başarılı. Betik/workflow değişikliği gerekmedi; sınırlar BUILD.md'de |

- [x] Eski `.gitignore` eklemeleri `e5595b222` üzerinden aynen taşındı; yeni tabanın mevcut kuralları korundu.
- [x] Oyun/protokol/asset/shader kaynakları değiştirilmeden build ve açılış doğrulandı.
- [ ] Kalan MIG-5, MIG-7 ve MIG-8 tamamlanınca M0 kapanışı; çalışma dalları ana dalda uzlaştırılsın. MIG-9 bunların kabulünü kapatmaz.

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

Görevler, sahipleri ve kabul kriterleri MIG-8/MIG-7 sonrasında güncel `docs/LIGHTING_PLAN.md` üzerinden aktarılacak; eski L görevleri olduğu gibi uygulanmaz. Hedef: renkli ışık, yumuşak geçiş, gece/gündüz, aç/kapa ve kalite seviyeleri; sunucu ışığının `World.Light.Real*` değerleri korunmalı.

Yeni taban girdileri: `LightLevelType` profil davranışı, mevcut RGB ışık renk texture'ı ve atlaslar dikkate alınmalı; `SetTime(0x5B)` hâlâ boş. D-003 için eksik oyun testleri görünür şekilde izlenir; doğrulanmamış protokol/asset bağımsızlığı varsayımı üzerine tasarım kurulmaz.

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
- [ ] Claude Code / MIG-8: mevcut dünya post-process'i ile yeni ışık kompozitinin RT/sırasını uzlaştır. Mevcut uygulama dünya görüntüsünü ışık/UI birleştirmesinden önce işler; D-004 hedefindeki ışık → post-process sırası ayrıca tasarlanmalı.
- [ ] Glow ile tam bloom zincirini aynı kabul etmeden kalan kalite/performans ihtiyacını ve kapalı durum görsel eşliğini değerlendir.

## M6 — Değiştirilebilir art paketleri

- [x] classicUO-T: HD dünya paketi; `Profile.HdWorld`, `src/ClassicUO.Client/Game/Managers/HdWorldPack.cs`, `tools/hdpack/` araçları ve `Data/HD/world/index.json` + PNG yükleme yolu mevcut. Land, texmap ve static için özgün atlaslardan ayrı bir HD atlası; asenkron decode ve sınırlı GPU upload; özgün geometri/seçim korunuyor. Güncel kod büyütmede 2×, diğer ölçeklerde 1× indirgenmiş texture kullanıyor.
- [x] classicUO-T: HD karakter görünümü; `Profile.HdCharacters`, `src/ClassicUO.Client/Game/Managers/HdCharacterSprites.cs`, `src/ClassicUO.Renderer/Animations/HdSpriteCache.cs` ve `MobileView` bağlantısı mevcut. 1.25× ve üstü raster büyütmede özgün animasyon atlasından 2× xBR kare üretip önbellekler; seçim/orijin özgün kareden kalır.
- [x] Tabanın özel `.art` → MUL/UOP fallback ve `UOFilesOverrideMap` yolları mevcut; bunlar yeniden uygulanacak iş olarak planlanmaz.
- [ ] Harici HD animasyon paketi desteğini ayrıca değerlendir: mevcut karakter yolu çalışma anında xBR üretir; `tools/hdpack/extract_anims.py` çıktısının istemcide paket olarak tüketildiği doğrulanmadı. Yerel HD dünya görselleri oluşturulup repoya/release'e eklenmez.
- [ ] Claude Code: mevcut HD yolları üzerinden genel sağlayıcı sözleşmesi ve kalan metadata ihtiyacını daralt; mantıksal orijin/ölçek, materyal/hue, gerekirse emissive/normal ve CPU cursor tüketicileri kapsansın. Eski `UOFileLoader<T>.GetTexture()` önerisi geçersizdir.
- [ ] Paket yok/ID eksik/ayar kapalı fallback'i, farklı zoom'lar, atlas bütçesi ve paket yenilemesindeki bellek davranışını gerçek yerel paketle doğrula; yeni işler ölçümden sonra sahiplerine ayrılsın.

Eski genel önerinin CPU asset → GPU atlas ayrımı korunur; classicUO-T'nin mevcut HD dünya/karakter yolları önce değerlendirilir. [Ağ/asset haritası](arch/network-assets.md) HD ekleri için ayrıca çapraz incelenmeli; bu görev haritayı veya oyun kodunu değiştirmedi.

## Çalışma düzeni

Codex MIG-9 kapsamı `docs/BUILD.md`, `docs/PLAN.md` ve gerekirse `build.ps1` ile sınırlıdır. Betik mevcut kodla çalıştırılır; değişiklik yalnız doğrulamada gerekirse yapılır. `rendering.md`, `LIGHTING_PLAN.md`, `DECISIONS.md` ve oyun kodu bu görevde değişmez. Her iş diğer araç tarafından incelenir; tamamlanmamış MIG-5/MIG-7/MIG-8 kabulü verilmez.
