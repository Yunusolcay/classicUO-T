# Yüzey ışığı planı (M9)

Hedef: dünyayı "yeni bir oyun" gibi göstermenin motor tarafı. Yeni 2.5D çizimler (yapay zekâ ile, kullanıcı
tarafında) ne kadar iyi olursa olsun düz sprite hep aynı yönden aydınlanır; yüzey ışığı taş, tahta ve yaprağa
piksel başına kabartma ve girinti gölgesi verir. Ayrı dosya gerekmez: normal haritası HD resimden yüklenirken
üretilir, yeni çizimler gelince kendiliğinden onlara da uygulanır.

## Uygulama (2026-10-17)

- **Üretim:** `Game/Lighting/SurfaceNormalBuilder.cs`. Parlaklık yükseklik sayılır (boyanmış taşın üstü açık,
  derzi koyu). 3×3 yumuşatılmış yüksekliğin eğimi normal (`RELIEF` 3), 7×7 çevre ortalamasının altında kalan
  derinlik girinti (cavity, en fazla 0.6). Saydam komşular düz sayılır: silüet kenarı ve land karosu sınırı
  kabartma almaz (karolar arası ızgara oluşmaz). Texel: r,g = normal x,y (128 düz), b = z, a = açıklık (255 açık).
- **Yükleme:** `HdWorldPack.Decode` iş parçacığında, yarıya indirmeden sonra üretir; `Update` normal haritasını
  atlas sayfasının yanındaki aynı boyutlu sayfaya aynı dikdörtgene yazar (`_normalPages`). Ayar ya da zoom ölçeği
  değişince her şey yeniden yüklenir.
- **Bağlama:** `Renderer/Lighting/SurfaceNormals`: renk dokusu → normal sayfası kaydı. Batcher temel efektle her
  çizimde (`InternalDraw`, `DrawDirectIndexed`; chunk mesh dahil) s3'e normal sayfasını, yoksa 1×1 düz texel'i
  bağlar. Özel efektlerde s3'e dokunmaz.
- **Shader:** `IsometricWorld.fx` `surface_light`: `1 + SurfaceLight × ((N·L − L.z) × 1.6 − cavity × 0.9)`.
  Düz texel tam 1 verir: HD olmayan art, UI ve yazılar değişmez. Yalnız doku/hue/land/çimen/su modlarında.
- **Sahne:** `GameSceneSurfaceLight.cs`. Güneş ekranın sol üstünden (UO art'ına boyanmış ışıkla aynı yön),
  güç 0.6; gece %35'e iner (`IsometricLevel`). Dünya çiziminden sonra 0'a çekilir.
- Ayar: `SurfaceLighting` (varsayılan açık), Options → Video → "Surface lighting". Yalnız HD world açıkken etkili.

## Sonraki adımlar

- [ ] Oyun içi kabul: güç, kumda/çimende gürültü, zoom 1×/2×.
- [ ] Gece meşale/mangal ışıklarının da normal haritasını kullanması (en yakın birkaç ışık shader'a).
- [ ] Gündüz güneş yönünün saate göre dönmesi: gölgeler döndü (SHADOWS_PLAN S7, `SunPath`), yüzey ışığı henüz sabit.
- [ ] İsteğe bağlı: pakette elle/yapay zekâ ile üretilmiş `_n.png` varsa onu kullanmak.
