// SPDX-License-Identifier: BSD-2-Clause

using System.Globalization;

namespace ClassicUO.Launcher
{
    // Turkish when the OS UI language is Turkish, English otherwise.
    internal static class Strings
    {
        private static readonly bool _tr = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";

        private static string T(string tr, string en) => _tr ? tr : en;

        public static string WindowTitle => "classicUO-T Launcher";
        public static string TabServers => T("Sunucular", "Servers");
        public static string TabSettings => T("Ayarlar", "Settings");
        public static string Add => T("Yeni", "New");
        public static string Duplicate => T("Kopyala", "Copy");
        public static string Delete => T("Sil", "Delete");
        public static string ProfileName => T("Profil adı", "Profile name");
        public static string Host => T("Sunucu adresi", "Server address");
        public static string Port => "Port";
        public static string UoDirectory => T("Ultima Online klasörü", "Ultima Online folder");
        public static string Browse => T("Gözat", "Browse");
        public static string Detect => T("Bul", "Detect");
        public static string ClientVersion => T("Client sürümü (boş = otomatik)", "Client version (empty = auto)");
        public static string Username => T("Kullanıcı adı", "Username");
        public static string Password => T("Şifre", "Password");
        public static string SavePassword => T("Şifreyi kaydet", "Save password");
        public static string AutoLogin => T("Otomatik giriş", "Auto login");
        public static string Encryption => T("Client şifrelemesi (sunucu istiyorsa)", "Client encryption (if the server requires it)");
        public static string Plugins => T("Eklentiler (satır başına bir .dll, ör. Razor)", "Plugins (one .dll per line, e.g. Razor)");
        public static string ExtraArgs => T("Ek argümanlar", "Extra arguments");
        public static string Play => T("OYNA", "PLAY");
        public static string UpdateAndPlay => T("GÜNCELLE VE OYNA", "UPDATE AND PLAY");
        public static string CheckUpdates => T("Güncellemeleri denetle", "Check for updates");
        public static string ClientDirectory => T("ClassicUO klasörü", "ClassicUO folder");
        public static string ManifestUrl => T("Güncelleme manifest adresi (https)", "Update manifest URL (https)");
        public static string DownloadBaseUrl => T("İndirme adresi (boş = manifest ile aynı yer)", "Download URL (empty = next to the manifest)");
        public static string CheckOnStart => T("Açılışta güncellemeleri denetle", "Check for updates on start");
        public static string CloseAfterLaunch => T("Oyun açılınca launcher'ı kapat", "Close the launcher after starting the game");
        public static string OpenClientFolder => T("Klasörü aç", "Open folder");
        public static string SelectUoFolder => T("Ultima Online klasörünü seçin", "Select the Ultima Online folder");

        public static string Ready => T("Hazır.", "Ready.");
        public static string Checking => T("Güncellemeler denetleniyor...", "Checking for updates...");
        public static string UpToDate(string v) => T($"ClassicUO güncel ({v}).", $"ClassicUO is up to date ({v}).");
        public static string UpdateAvailable(int n, string v) => T($"Güncelleme var: {n} dosya ({v}).", $"Update available: {n} file(s) ({v}).");
        public static string Downloading => T("İndiriliyor", "Downloading");
        public static string Verifying => T("Doğrulanıyor", "Verifying");
        public static string Installing => T("Kuruluyor", "Installing");
        public static string Updated(string v) => T($"Güncellendi ({v}).", $"Updated ({v}).");
        public static string Starting => T("Oyun başlatılıyor...", "Starting the game...");
        public static string Started => T("Oyun başlatıldı.", "Game started.");
        public static string UpdatesDisabled => T("Güncelleme adresi ayarlı değil.", "No update URL configured.");

        public static string ErrUoDirectory => T("Geçerli bir Ultima Online klasörü seçin (tiledata.mul, hues.mul, art ve map dosyaları gerekli).", "Select a valid Ultima Online folder (tiledata.mul, hues.mul, art and map files are required).");
        public static string ErrUoNotFound => T("Ultima Online kurulumu bulunamadı. Klasörü Gözat ile seçin.", "No Ultima Online installation found. Use Browse to pick the folder.");
        public static string ErrHost => T("Sunucu adresi ve port (1-65535) gerekli.", "Server address and port (1-65535) are required.");
        public static string ErrClientMissing(string dir) => T($"ClassicUO bulunamadı: {dir}. Güncelleme adresini ayarlayın ya da ClassicUO'yu bu klasöre koyun.", $"ClassicUO not found in {dir}. Set the update URL or place ClassicUO in that folder.");
        public static string ErrClientRunning => T("ClassicUO açıkken güncelleme yapılamaz. Oyunu kapatıp tekrar deneyin.", "Cannot update while ClassicUO is running. Close the game and try again.");
        public static string ErrUpdate(string msg) => T($"Güncelleme başarısız: {msg}", $"Update failed: {msg}");
        public static string ErrUpdateOffline(string msg) => T($"Güncellemeler denetlenemedi ({msg}). Mevcut sürümle devam ediliyor.", $"Could not check for updates ({msg}). Continuing with the installed version.");
        public static string ErrStart(string msg) => T($"Oyun başlatılamadı: {msg}", $"Could not start the game: {msg}");
        public static string ErrMono => T("Linux/macOS'ta ClassicUO'yu çalıştırmak için mono gerekli.", "mono is required to run ClassicUO on Linux/macOS.");
    }
}
