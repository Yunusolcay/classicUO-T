# Geliştirme planı

Son güncelleme: 16 Ekim 2026. D-002 ile taban güncel upstream `main` (`ee79d7eb`); bu çalışma `18da1d91d` üzerinden `analiz/build-network-v2` dalında. Eski iskelet `analiz/build-network` / `e5595b222` üzerinden taşındı; eski kod yolları yeni tabana uygulanmaz. Göç ayrıntısı: [MIGRATION.md](MIGRATION.md).

## M0 — Analiz ve dokümantasyon / taban geçişi

| Görev | Sahip | Durum ve kanıt |
| --- | --- | --- |
| MIG-1 — dallar/varsayılan dal/submodule/worktree düzeni | Kullanıcı | Yerel main ve iki v2 worktree mevcut; uzak varsayılan dal/yayımlama bu görevde doğrulanmadı |
| MIG-2 — yeni build ortamı | Codex | Tamamlandı: .NET 10 Release win-x64, recursive submodule'ler, `build.ps1`, BUILD.md; self-contained managed exe çalıştı. PowerShell 7 ve temiz yerel klonda PowerShell 5.1 ile build/publish geçti. NativeAOT için C++ bağlayıcısı eksik, ayrı seçenek olarak belgelendi |
| MIG-3 — render haritası | Claude Code | Paralel `analiz/render-v2` çalışması; bu dalda henüz güncel çıktı doğrulanmadı |
| MIG-4 — ışık planını uyarlama | Claude Code | MIG-3 sonrası; bu dalda henüz güncel çıktı doğrulanmadı |
| MIG-5 — veri/protokol testi | Codex + kullanıcı | **Kısmi:** 7.0.112.75 + 7.0.20.0 açılış, Nimloth'a giriş, yürüme, koşma, ışınlanma geçti. Kalan oyun testleri ve shard özel asset eşlemeleri aşağıda |
| MIG-6 — yeni ağ/asset haritası | Codex | Tamamlandı: `docs/arch/network-assets.md`; yeni paket/taşıma ve Assets/IO/Renderer ayrımı |
| MIG-7 — çapraz inceleme | Claude Code + Codex | Bekliyor: Claude BUILD/network-assets; Codex güncel rendering/LIGHTING_PLAN |

- [x] Eski `.gitignore` eklemeleri `e5595b222` üzerinden aynen taşındı; yeni tabanın mevcut kuralları korundu.
- [x] Oyun/protokol/asset/shader kaynakları değiştirilmeden build ve açılış doğrulandı.
- [ ] MIG-1 uzak işlemleri, MIG-3/4, kalan MIG-5 ve MIG-7 tamamlanınca M0 kapanışı; plan/analiz dalları ana dalda uzlaştırılsın.

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
- [ ] İstenirse NativeAOT doğrulaması: Visual C++ bağlayıcısı ve Windows SDK sağlandıktan sonra `build.ps1 -NativeAot`. Managed build/yayın başarılıdır.

## M1 — Dinamik ışık sistemi

Görevler, sahipleri ve kabul kriterleri MIG-4/MIG-7 sonrasında güncel `docs/LIGHTING_PLAN.md` üzerinden aktarılacak; eski L görevleri olduğu gibi uygulanmaz. Hedef: renkli ışık, yumuşak geçiş, gece/gündüz, aç/kapa ve kalite seviyeleri; sunucu ışığının `World.Light.Real*` değerleri korunmalı.

Yeni taban girdileri: `LightLevelType` profil davranışı, mevcut RGB ışık renk texture'ı ve atlaslar dikkate alınmalı; `SetTime(0x5B)` hâlâ boş. D-003 için eksik oyun testleri görünür şekilde izlenir; doğrulanmamış protokol/asset bağımsızlığı varsayımı üzerine tasarım kurulmaz.

## M2 — Gölgeler

- [ ] Claude Code: yumuşak gölge/occlusion tekniği; oynanış, seçim, sıralama ve performans ölçütleri; net alt işler Codex'e.

## M3 — Shader efektleri

- [ ] Claude Code: su, hava durumu ve bitki salınımı; `World.Weather` / `0x65` uyumu; bağımsız aç/kapa ve kabul kriterleri.

## M4 — Akıcılık

- [ ] Kamera, kademesiz zoom, interpolasyon; önce mevcut davranış/ölçüm, sonra görev bazında sahip ataması.

## M5 — Post-processing

- [ ] Renk düzenleme ve bloom; ışık/gölge geçiş sırası, performans ve kapalı durum davranışı.

## M6 — Değiştirilebilir art paketleri

**Taşınan öneri, yeni konumu:** CPU sağlayıcı sınırı `ClassicUO.Assets.ArtLoader.GetArt()` çevresinde olmalı; GPU upload/cache/atlas `ClassicUO.Renderer.Arts.Art` içinde kalmalı. Eski `UOFileLoader<T>.GetTexture()` önerisi geçersizdir. Yeni tabanın özel `.art` → MUL/UOP fallback yolu ve `UOFilesOverrideMap` zaten vardır; bunları tekrar icat etmeden genişletin.

- [ ] Claude Code: mevcut `ArtInfo` (uint pikseller, boyut) sözleşmesine mantıksal orijin/boyut/ölçek, seçim maskesi, hue/materyal ve gerekirse emissive/normal metadata'sı ekleme tasarımı.
- [ ] Mevcut statik/land özel `.art` desteği ile HD bitmap desteğini ayrı kapsamlar olarak değerlendir; paket kapalı/ID eksik durumda legacy dönüş, cursor gibi CPU tüketicileri ve atlas/cache invalidation davranışı tanımlansın.
- [ ] Art, gump, texmap ve animasyon sağlayıcılarını küçük test edilebilir işlere böl; net işler Codex'e. `Art.Reload()` atlas sayfalarını boşaltmadığından sık paket değişimindeki bellek politikası ayrıca tasarlansın.

Detay: [ağ/asset haritası](arch/network-assets.md). Bunlar öneridir; yeni sağlayıcı veya oyun kodu bu görevde uygulanmadı.

## Çalışma düzeni

Codex'in bu aşamadaki dosyaları `.gitignore`, `build.ps1`, `docs/BUILD.md`, `docs/PLAN.md`, `docs/arch/network-assets.md`. `rendering.md`, `LIGHTING_PLAN.md`, `DECISIONS.md` ve oyun kodu bu görevde değişmez. Her iş diğer araç tarafından incelenir; tamamlanmamış MIG-5/MIG-7 kabulü verilmez.
