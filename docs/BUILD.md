# Windows derleme ve çalıştırma — .NET 10

16 Ekim 2026; D-004 tabanı `Yunusolcay/classicUO-T` (`origin`), upstream `ee79d7eb` + classicUO-T özellikleri. MIG-9 inceleme tabanı `fe0244de4`, çalışma dalı `codex/dev`. MIG-2/MIG-5 sonuçları aşağıda tarihsel kayıt olarak korunur. Kaynak haritası: [ağ ve asset](arch/network-assets.md); görevler: [PLAN.md](PLAN.md).

## Gereksinimler ve bağımlılıklar

- Windows x64, Git, Windows PowerShell 5.1 veya PowerShell 7 ve **.NET 10 SDK**. Burada SDK `10.0.113` kullanıldı. `dotnet --version` ile seçili SDK'nın `10.x` olduğunu kontrol edin.
- İlk geri yükleme için GitHub ve NuGet erişimi. Ana kaynak projeleri `src/Directory.Build.props` ile `net10.0`, preview C# ve unsafe kod kullanır.
- `external/FNA` (kayıtlı sürüm 25.09, `ed62a28d`), `external/MP3Sharp` (`e64af846`) ve `external/FileEmbed` (`941fe67e`) gerçek submodule'lerdir. FNA'nın FAudio, FNA3D/MojoShader, SDL2-CS, SDL3-CS ve Theorafile alt submodule'leri de recursive alınmalı.
- `external/FNA/FNA.Core.csproj` `net8.0` hedefler; .NET 10 istemcisinin proje referansıdır. Ayrıca .NET 8 SDK veya çalışma zamanı kurulması bu doğrulamada gerekmedi. MP3Sharp ve FileEmbed netstandard2.0 hedefler; FileEmbed kaynak üreteci olarak kullanılır.
- Windows native dosyaları repodaki `external/x64/`: **SDL3.dll, FNA3D.dll, FAudio.dll, libtheorafile.dll, zlib.dll**. `external/cuoapi/cuoapi.dll` eklenti API referansıdır. Bunlar build/publish hedefleriyle çıktı klasörüne kopyalanır; FNA3D native kaynaklarını ayrıca derlemek gerekmedi.
- NuGet bağımlılıkları proje referanslarından geri yüklenir. `ClassicUO.Utility` içindeki ImageSharp `3.1.11` için mevcut `NU1902/NU1903` uyarıları görüldü; bu görev bağımlılık/kod güncellemesi yapmaz.

NativeAOT **isteğe bağlıdır**: VS C++ masaüstü araçları, MSVC x64 bağlayıcısı ve Windows SDK gerekir. Bu makinedeki ilk AOT publish denemesi `Platform linker not found` ile durdu. Varsayılan betik .NET çalışma zamanını içeren, doğrudan çalıştırılabilir managed yayın üretir. Çalıştırılan managed yayın için ayrıca .NET kurulması gerekmez; derleme için SDK gerekir.

## Temiz klondan tek komutla derleme

```powershell
git clone --recursive https://github.com/Yunusolcay/classicUO-T.git
Set-Location classicUO-T
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1`, bulunduğu repo kökünde çalışır, SDK'yı kontrol eder, `git submodule update --init --recursive` ile **kayıtlı commit'leri** alır; `--remote` kullanmaz. Eski `git://` adreslerini yalnız bu komutta HTTPS'e eşler, global Git ayarı değiştirmez. Eksik/uyuşmayan/conflict'li submodule durumunda veya native dosya eksikliğinde durur.

Sonra istemci projesine `dotnet build` ve `dotnet publish` uygular: Release, `win-x64`, `PublishAot=false`, publish için `--self-contained true`. Günlükler `artifacts/build/build-managed.log`, `publish-managed.log`; çıktı:

```text
bin/dist/win-x64-managed/cuo.exe
```

Dağıtımda **klasörün tamamını** taşıyın; tek başına exe yeterli değildir. Betik ayar dosyası veya UO verisi oluşturmaz/kopyalamaz. Başarısız dış komutta hata verir; çalışan istemci aynı çıktı klasörünü kilitleyebileceğinden yeniden yayınlamadan önce onu kapatın.

NativeAOT araçları kurulmuş bir makinede:

```powershell
.\build.ps1 -NativeAot
```

Bu modun ayrı çıktısı `bin/dist/win-x64-nativeaot/`, günlükleri `*-nativeaot.log` olur. Burada AOT başarıyla doğrulanmadı. `ClassicUO.Bootstrap` net472 **istemci başlatıcısı** ile Avalonia tabanlı **ClassicUO.Launcher** ayrı projelerdir. Deploy akışı bootstrap + native shared client kitaplığı, ayrıca oyuncunun kullandığı launcher paketini üretir; aşağıdaki bölümler bu ayrımı açıklar.

## classicUO-T launcher

`src/ClassicUO.Launcher/ClassicUO.Launcher.csproj`, .NET 10 / Avalonia 11.3.22 ile `ClassicUOLauncher` uygulamasını üretir. Sunucu profilleri, UO klasörü tespiti, isteğe bağlı parola saklama ve manifest üzerinden istemci güncellemesi içerir. Windows'ta kayıtlı parola DPAPI ile kullanıcı hesabına bağlanır.

Yerel Windows x64 yayını (repo kökünden, deploy'daki launcher seçenekleriyle):

```powershell
dotnet publish src/ClassicUO.Launcher/ClassicUO.Launcher.csproj -c Release -r win-x64 -o artifacts/launcher/win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

RID verildiğinde launcher csproj'si trimming'i etkinleştirir; globalization açık kalır (TR/EN arayüz). `build.ps1` launcher'ı derlemez. `ClassicUOLauncher.exe`, yayın klasöründeki diğer dosyalarla kullanılır; deploy ayrıca `launcher.defaults.json` ekler.

`Core/LauncherConfig.cs` ayar önceliği: launcher yanındaki `launcher.json` → `%APPDATA%/classicUO-T/launcher.json` → launcher yanındaki `launcher.defaults.json`. Kaydetme konumu launcher klasörü yazılabiliyorsa portable dosya, değilse AppData'dır. `ClientDirectory` varsayılanı launcher'a göre `ClassicUO`; `UpdateManifestUrl` boşsa güncelleme kapalıdır. Profil ayarları istemci altında `Data/Launcher/settings_<profil ID>.json` dosyasına yazılır.

**Çalıştırma sözleşmesi:** `Core/ClientStarter.cs`, istemci klasöründe `ClassicUO.exe` arar; Windows'ta onu doğrudan, Linux/macOS'ta Mono ile çalıştırır. Bu dosya CI'nin `ClassicUO.Bootstrap` çıktısıdır. Yerel `build.ps1` çıktısı `cuo.exe` ise doğrudan çalıştırma içindir; launcher'ın beklediği bootstrap/native dağıtımının yerine konmamalıdır. Launcher ile kullanımda deploy'un ürettiği istemci paketi gerekir.

D-003 profili için UO yolu `D:\Games\Electronic Arts\Ultima Online Classic`, `ClientVersion` **7.0.20.0**, host `166.1.94.99`, port `2593`, `UseEncryption=false` ayarlanmalı. Launcher profilindeki sürüm boşsa UO `client.exe` dosyasından 7.0.112.75 seçilir; bu D-003 ayarından farklıdır. Deploy'un varsayılan profilinde client sürümü ayrıca verilmediğinden ilk kullanımda kontrol edilmelidir. Yerel launcher yapılandırmaları/kimlik bilgileri repo dışında tutulur; bu görev launcher UI veya otomatik güncelleme indirmesi çalıştırmaz.

## GitHub Actions: Build-Test ve Deploy

Bu bölüm `.github/workflows/build-test.yml` ve `deploy.yml` kaynaklarına dayanır; uzaktaki son Actions sonucunu temsil etmez.

| Akış | Tetikleme / ortam | İşlem ve çıktı |
| --- | --- | --- |
| Build-Test | Tüm dallarda push/PR (`**`); README vb. path istisnaları; macos-15-intel, ubuntu-latest, windows-latest | Checkout, recursive submodule, .NET 10.0.x, repo kökünde `dotnet restore`, `dotnet build`, `dotnet test`; `contents: read` |
| Deploy | Aynı repo `main` push'ının başarılı Build-Test sonucu veya `workflow_dispatch` | Kaynak SHA checkout'u; `ClassicUO-<dal>-release` tag/release; Windows x64, Linux x64 ve macOS x64 paketleri |

Deploy otomatiği PR sonucundan yayın yapmaz: `workflow_run` için başarılı sonuç, `event=push` ve kaynak repo eşitliği kontrol edilir. Dal başına concurrency grubunda aynı anda bir deploy çalışır. Release ve yükleme işleri `contents: write` kullanır; normal Build-Test salt okuma yetkilidir.

Deploy paket sırası:

1. `ClassicUO.Bootstrap` Release publish → `dist/ClassicUO.exe` (.NET Framework 4.7.2 başlatıcı).
2. `ClassicUO.Client` Release/RID publish, `NativeLib=Shared`, `OutputType=Library`, `IS_DEV_BUILD=true` → aynı `dist/` içindeki native istemci kitaplığı ve bağımlılıklar. NativeAOT/platform derleme araçları gerekir.
3. `tools/ManifestCreator` → dosya listesi + MD5 manifesti; `upload/manifest-<rid>.xml` ve `upload/ClassicUO-<rid>-release.zip`.
4. Ayrı launcher publish → `launcher/`; PDB'ler paket dışı; `launcher.defaults.json` ve `upload/ClassicUOLauncher-<rid>.zip`.
5. Üç dosya release'e `gh release upload --clobber` ile yüklenir. Normal yerel build bu adımların tag/release/yükleme işlemlerini yapmaz.

Launcher varsayılanları Actions **repository variables** içindeki `SHARD_NAME`, `SHARD_HOST`, `SHARD_PORT` ile seçilir; yoksa `SphereNet`, `127.0.0.1`, `2593` kullanılır. Nimloth dağıtımı için host değişkeni `166.1.94.99` olmalı; bu görev GitHub değişkenlerini değiştirmez. Varsayılan manifest adresi `https://github.com/Yunusolcay/classicUO-T/releases/download/ClassicUO-main-release/manifest-{rid}.xml`; `{rid}` çalışırken `win-x64`, `linux-x64` veya `osx-x64` olur.

### Yerel betik ile CI uyumu

Workflow dosyaları `build.ps1` çağırmaz. Betik yalnız kendi komut satırına `PublishAot=false/true` verir; proje varsayılanlarını, `Directory.Build.props` dosyasını veya workflow'ları değiştirmez. Bu yüzden deploy'un native shared library seçimi korunur. Çıktılar da ayrıdır: yerel istemci `bin/dist/win-x64-managed/` veya `win-x64-nativeaot/`; deploy `dist/`, `launcher/`, `upload/` kullanır. Ortak `obj` ara dosyaları nedeniyle aynı checkout'ta eşzamanlı restore/build/publish yerine ayrı klon/iş çalıştırın.

Yerel Windows CI eşdeğeri, repo kökünden sırayla:

```powershell
dotnet restore
dotnet build
dotnet test
```

MIG-9'da betik, bu komutlar ve launcher publish temiz bir yerel klonda doğrulandı; aşağıdaki kayıt sonuçları ve sınırları içerir. `build.ps1` için kaynak değişikliği gerekmedi.

## MIG-9 doğrulama kaydı — classicUO-T

16 Ekim 2026, `fe0244de4` kaynakları, SDK `10.0.113`, Windows x64. Açık oyun oturumuyla dosya kilidi/çıktı çakışmasını önlemek için doğrulama `artifacts/build/mig9-check` temiz yerel klonunda yapıldı; NuGet makine önbelleği kullanıldı.

| Kontrol | Sonuç |
| --- | --- |
| Mevcut `build.ps1` / Windows PowerShell 5.1 | Recursive submodule kurulumu, Release win-x64 build ve self-contained publish başarılı; exe/native DLL kontrolleri geçti |
| Betikten sonra CI sırası | Kök dizinde `dotnet restore`, `dotnet build`, `dotnet test` başarılı; **279 başarılı, 0 başarısız, 0 atlanan** |
| Launcher publish | Deploy'daki Windows x64 self-contained/single-file seçenekleriyle başarılı; `ClassicUOLauncher.exe` üretildi |
| Ayar/çıktı çakışması | Betik sonrası varsayılan CI build/test başarılı; AOT tercihi komuta özel, deploy klasörleri ayrı. `build.ps1`, csproj/props ve `.github/workflows` değişmedi |
| Yayın uyarıları | Mevcut ImageSharp `NU1902/NU1903`; trimmed launcher publish'te `ClientStarter.MergeSettings()` / `JsonArray.Add<T>()` için **IL2026**. Trimming sonrası launcher ayar/plugin yazma davranışı çalıştırılarak doğrulanmadı |
| Doğrulama sınırı | Linux/macOS matrisi, NativeAOT shared client publish, uzak Actions/deploy ve launcher UI/updater bu görevde çalıştırılmadı; etiket/release/yükleme yapılmadı |

Loglar Git dışındaki `artifacts/build/mig9-script.log`, `mig9-ci-restore.log`, `mig9-ci-build.log`, `mig9-ci-test.log`, `mig9-launcher-publish.log` dosyalarında. Bu sonuç yerel Windows CI eşdeğerini doğrular; uzaktaki tam release pipeline'ının başarı iddiası değildir. MIG-9 yalnız PLAN/BUILD belgelerini güncelledi; mevcut özellikler için yeni oyun içi görsel kabul verilmedi.

## Nimloth ayarları ve çalıştırma

`bin/dist/win-x64-managed/settings.nimloth.json`:

```json
{
  "ultimaonlinedirectory": "D:\\Games\\Electronic Arts\\Ultima Online Classic",
  "clientversion": "7.0.20.0",
  "ip": "166.1.94.99",
  "port": 2593,
  "encryption": 0,
  "plugins": [],
  "autologin": false,
  "saveaccount": false,
  "login_music": false,
  "lang": "enu"
}
```

Veri klasöründeki orijinal `client.exe` dosya sürümü **7.0.112.75**. D-003 uyarınca protokol ayarı **7.0.20.0**; şifreleme 0 ile kullanıcı oyuna giriş yaptı. `plugins: []`, varsayılan Razor yüklemesini kapatır. Referans veri dosyalarını repoya veya yayın klasörüne kopyalamayın.

```powershell
Set-Location .\bin\dist\win-x64-managed
.\cuo.exe -settings settings.nimloth.json
```

Bu tabanda eski `-skipupdate` argümanı gerekli değildir ve `Main.cs` içinde böyle bir seçenek yoktur. `-settings` mutlak veya exe'ye göre bağıl yol kabul eder; varsayılan `settings.json` da exe'nin bulunduğu klasördedir. `-uopath`, `-clientversion`, `-ip`, `-port`, `-encryption`, `-profilespath`, `-filesoverride` desteklenir; komut satırı ayarlara üstün gelir. Argümanlar günlüklenir; parolayı komut satırından vermeyin. `saveaccount:false` kimlik bilgilerinin ayara yazılmasını engeller.

Varsayılan profiller `Data/Profiles/<hesap>/<sunucu>/<karakter>/profile.json`; ayrıca `globalprofile.json`/`default.json` kullanılabilir. `profilespath` ile kök değiştirilebilir. `files_override`, `dosyaadı=tam_yol` satırları içeren dosyaya yönelir; bu görevde override kullanılmadı. `ignore_relay_ip` varsayılan false; sunucunun `0x8C` ile döndürdüğü oyun adresi/portu kullanılır. Keyfî olarak açmak yerine relay erişimini doğrulayın.

`clientversion`, ağ tablosuna ek olarak bazı loader format seçimlerine de girer. Bu ayarın bütün asset yüklemelerinden bağımsız olduğu varsayılmamalı; ayrıntı [haritada](arch/network-assets.md).

## MIG-2 ve MIG-5 doğrulama kaydı

16 Ekim 2026, Windows x64:

| Kontrol | Sonuç / kanıt |
| --- | --- |
| Release x64 build | Başarılı, .NET 10; mevcut derleyici/analiz ve NuGet uyarıları var |
| Self-contained managed publish | `build.ps1` ile başarılı; `cuo.exe` ve beş native DLL çıktı kontrolü geçti |
| Temiz klon / PowerShell 5.1 | Çıktı ve submodule içermeyen temiz yerel Git klonunda `build.ps1`, recursive indirme + Release build + publish işlemlerini başarıyla tamamladı; NuGet makine önbelleği kullanıldı. Ana worktree denemesi PowerShell 7 ile yapıldı |
| NativeAOT publish | C++ bağlayıcısı eksik; doğrulanmadı, standart yayını engellemiyor |
| 7.0.112.75 veri + 7.0.20.0 ayarı | Dosyalar yaklaşık 1085 ms'de yüklendi; 20 saniyelik testte süreç yanıt verdi; eski Cliloc/gump çöküşleri tekrarlanmadı |
| Ağ erişimi | `166.1.94.99:2593` TCP bağlantısı başarılı; bu tek başına protokol testi değildir |
| Kullanıcı ile oyun girişi | Kullanıcı oyuna girdiğini doğruladı; logda login ve relay bağlantısı, karakter profili yüklemesi ve karakter pencere başlığı görüldü |
| Yürüme / koşma / ışınlanma | Kullanıcı doğruladı; gözle görülür hata bildirmedi. Işınlanma ayrı facet/harita geçişiyle aynı kabul edilmedi |
| Eksik shard asset'leri | İlk testte `Texture not found` mesajları: art index `127, 1006, 1007, 1008, 1010, 1011, 1012`. Kullanıcı, bazı shard MUL'larının `C:\UOSoft\Spherenet` paketine göre olduğunu ve orijinal sette görünmediğini belirtti; bu testte paketler birleştirilmedi |

İlk kontrollü süreç test sonunda kapatıldı; ardından kullanıcı testi için aynı ayarlarla görünür istemci açıldı. Deneme ayarları ve loglar yalnız Git dışındaki `bin/` ve `artifacts/` altında tutulur. Kaynak UO kurulumu değiştirilmedi.

D-003 kabul kontrol listesi:

- [x] 7.0.112.75 verisi ve 7.0.20.0 ile giriş ekranı/açılış; kullanıcı oyuna girdi.
- [x] Kullanıcı: yürüme, koşma, ışınlanma.
- [ ] Eşya alma/bırakma, konteyner ve shard gump'ları.
- [ ] Ayrı harita/facet geçişi ve dönüş.
- [ ] Büyü efektleri/animasyonlar.
- [ ] Sunucu `0x4E/0x4F` ışık değişiklikleri.
- [ ] Sunucu `0x65` hava değişiklikleri.
- [ ] Shard'a özel eksik asset'lerin eşlenmesi ve görsel kontrolü.

MIG-5 **kısmen tamamlandı**; D-003'ün tam kabulü için kalan liste kullanıcıyla doğrulanmalı. Sorun halinde sürüm kararını güncelleme değerlendirmesi DECISIONS sahibine bırakılır; bu görev o dosyayı değiştirmez.

## Sık sorunlar

| Belirti | Kontrol |
| --- | --- |
| SDK 10 bulunamadı / NETSDK1045 | `dotnet --version`, `dotnet --list-sdks`; .NET 10 SDK seçilmeli |
| Submodule kaynakları bulunamadı | Betik recursive init yapar; ağ, erişim ve `git submodule status --recursive` kontrolü |
| `Platform linker not found` | AOT için MSVC/Windows SDK gerekir; varsayılan managed yayını kullanın |
| Native DLL hatası | Yayın klasörünün tamamını kullanın; eski SDL2/MojoShader veya başka istemcinin DLL'leriyle karıştırmayın |
| Publish dosya kilidi | Aynı çıktı klasöründeki istemciyi kapatın; betik kullanıcı süreçlerini otomatik sonlandırmaz |
| NU1902/NU1903 | Bu tabanın ImageSharp uyarıları; ayrı bakım değerlendirmesi, eski Newtonsoft göreviyle aynı değil |
| Eksik texture / yanlış shard görseli | Orijinal veri seti shard'ın özel art'ını içermez; ID/asset eşlemesini inceleyin. Girişin çalışması bütün shard içeriğini doğrulamaz |
| Login sonrası bağlantı hatası | Relay IP/portu ve `ignore_relay_ip` davranışı; ilk port erişiminin yeterli olduğunu varsaymayın |
