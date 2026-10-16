# Taban Geçişi: 2020 snapshot → güncel upstream

Karar: `docs/DECISIONS.md` D-002 (taban) ve D-003 (client verisi/protokol).
Bu görevler M0'ın parçasıdır (M0.5); bitince `docs/PLAN.md`'de işaretlenir ve bu dosya arşivlenir.

## Git düzeni

| Dal | İçerik |
|---|---|
| `main` | **classicUO-T** `main` (upstream `ee79d7eb` + classicUO-T özellikleri) + AGENTS/CLAUDE/docs + Codex build/ağ/asset işi (D-004). `origin` = classicUO-T |
| `old/main` | Eski repo'daki (`old-origin`) upstream tabanlı `main`, yalnızca referans |
| `legacy/2020` | Eski `master` (`2bd06713`), yalnızca referans |
| `master` | Geçiş tamamlanınca `main`'e yönlendirilir veya silinir (MIG-1) |
| `analiz/render`, `analiz/build-network` | Eski tabanda açılmış analiz dalları; yeni tabanda `-v2` ile yeniden açılır |

Upstream senkronu: `git fetch upstream && git merge upstream/main` (main üzerinde, düzenli; rebase yok).

## Görevler

### MIG-1 – Dalları yayımla ve varsayılan dalı değiştir · Kullanıcı · K · ✅ 2026-10-16 (Claude Code çalıştırdı)
- `git push origin main legacy/2020`; GitHub'da varsayılan dalı `main` yap.
- Ana klasörü `main`'e geçir, submodule'leri çek: `git switch main && git submodule update --init --recursive`.
- Worktree'leri yeni tabanda aç: `git worktree add ../cuo-claude-v2 -b analiz/render-v2 main`, `git worktree add ../cuo-codex-v2 -b analiz/build-network-v2 main`.
- Kabul: `origin/main` = yerel `main`; iki worktree `main` üzerinde.

### MIG-2 – Build ortamı (yeni taban) · **Codex** · O
- .NET 10 SDK, submodule'ler, `dotnet build`/`publish` (Release, Windows x64). `docs/BUILD.md`, kökte `build.ps1`, `.gitignore` (UO verisi `*.mul *.uop *.idx *.def`, `settings.json`, profiller, build çıktıları).
- Kabul: Temiz klon + `build.ps1` ile çalışan exe; `settings.json` örneği veri yolu `D:\Games\Electronic Arts\Ultima Online Classic`, `clientversion` `7.0.20.0`.

### MIG-3 – Render haritasını yenile · **Claude Code** · O · ✅ 2026-10-16
- `docs/arch/rendering.md`'yi yeni koda göre yeniden yaz (eski sürüm `legacy/2020` dalında kalır).
- Kabul: Tüm yollar/metotlar `main` ile eşleşiyor; upstream'deki ışık değişiklikleri (atlas, `LightShaderData`, `RenderTargets`) belgelenmiş.

### MIG-4 – Işık planını yeni tabana uyarla · **Claude Code** · K–O · ✅ 2026-10-16
- `docs/LIGHTING_PLAN.md`: dosya yolları, kancalar; upstream'in zaten yaptığı işler varsa görevlerden düş (ör. L8 renk verisi kısmen `LightShaderData` ile mevcut olabilir).
- Kabul: Her görevdeki dosya yolu `main`'de mevcut ya da “yeni dosya” olarak işaretli; görev sayısı ve sahipler güncel.

### MIG-5 – Protokol/veri doğrulaması (D-003) · **Codex** + Kullanıcı · K
- 7.0.112 verisi + `clientversion 7.0.20.0` ile Nimloth'a bağlan: giriş, karakter seçimi, yürüme, eşya/konteyner, gump, harita geçişi, büyü efektleri, gece/gündüz ışık paketleri (0x4E/0x4F), hava (0x65).
- Kabul: Kontrol listesi `docs/BUILD.md` veya `docs/PLAN.md`'de; sorun varsa D-003 güncellenir.

### MIG-6 – Ağ/asset haritası (yeni taban) · **Codex** · O
- Önceki Codex görevi (Adım 1B'nin 2–3. maddeleri) aynen, ama `main` üzerinde → `docs/arch/network-assets.md`.

### MIG-7 – Çapraz inceleme · Her ikisi · K
- Claude Code: BUILD.md + network-assets.md; Codex: rendering.md + LIGHTING_PLAN.md (Adım 2'deki gibi).

### MIG-8 – classicUO-T özelliklerini haritalara işle · **Claude Code** · O
- `rendering.md`: HD world/karakter paketleri, Modern look, nature effects, zoom xBR'nin render yoluna etkisi (`GameScene`, `RenderTargets`, `ChunkMesh`, `IsometricWorld.fx`, `Batcher2D`).
- `LIGHTING_PLAN.md`: kancaları bu koda göre güncelle; ışık kompoziti ↔ Modern look sırası.
- Kabul: Haritadaki yollar/metotlar `origin/main` ile eşleşiyor.

### MIG-9 – PLAN.md'yi classicUO-T'ye göre güncelle · **Codex** · K
- M3/M4/M5/M6'da classicUO-T'de zaten yapılmış işleri işaretle (nature effects, smooth movement, Modern look, HD paketleri); launcher ve CI'ı BUILD.md'ye ekle; `build.ps1`'in mevcut CI ile çakışmadığını doğrula.

**Sıra:** MIG-1 → (MIG-2, MIG-3, MIG-6 paralel) → MIG-4, MIG-5 → MIG-7 → M1 başlar.

## Codex'e verilecek güncel prompt

```
Taban değişti: AGENTS.md, docs/DECISIONS.md (D-002, D-003) ve docs/MIGRATION.md'yi oku.
Eski tabanda (analiz/build-network) yaptığın iş varsa yalnızca genel bulguları al; kod yolları geçersiz.
Görevlerin: MIG-2, MIG-6, MIG-5 (bu sırayla), çalışma dalı analiz/build-network-v2.
Adım 1B'deki kurallar geçerli: oyun kodunu değiştirme; docs/arch/rendering.md, docs/LIGHTING_PLAN.md,
docs/DECISIONS.md'ye dokunma. docs/PLAN.md'yi oluştur; M0 altına MIG görevlerini ekle, bitenleri işaretle.
Bitince commit'le ve özet ver: build durumu, protokol/veri testi sonucu, ağ/asset bulguları.
```
