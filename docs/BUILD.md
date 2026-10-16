# Windows derleme ve çalıştırma — .NET 10

16 Ekim 2026; D-002 tabanı `main` / `18da1d91d` (upstream `ee79d7eb`). Eski .NET Framework 4.7/FNA DLL/targeting pack adımları kullanılmaz. Kaynak haritası: [ağ ve asset](arch/network-assets.md); görevler: [PLAN.md](PLAN.md).

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
git clone --recursive <fork-depo-adresi> ClassicUO
Set-Location ClassicUO
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

Bu modun ayrı çıktısı `bin/dist/win-x64-nativeaot/`, günlükleri `*-nativeaot.log` olur. Burada AOT başarıyla doğrulanmadı. Upstream'deki `scripts/build-naot.sh`/deploy iş akışı ayrıca `ClassicUO.Bootstrap` net472 launcher + native shared library üretir. Bu betik doğrudan `cuo.exe` üretir; launcher/assistant plugin dağıtımı bu doğrulamanın kapsamı değildir.

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
