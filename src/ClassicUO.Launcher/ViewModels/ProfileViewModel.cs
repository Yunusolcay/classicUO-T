// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Linq;
using ClassicUO.Launcher.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassicUO.Launcher.ViewModels
{
    internal sealed partial class ProfileViewModel : ObservableObject
    {
        public ProfileViewModel(ServerProfile model)
        {
            Model = model;
            _name = model.Name;
            _host = model.Host;
            _port = model.Port;
            _ultimaOnlineDirectory = model.UltimaOnlineDirectory;
            _clientVersion = model.ClientVersion;
            _username = model.Username;
            _savePassword = model.SavePassword;
            _password = model.SavePassword ? PasswordProtector.Unprotect(model.ProtectedPassword) : string.Empty;
            _autoLogin = model.AutoLogin;
            _useEncryption = model.UseEncryption;
            _plugins = string.Join(Environment.NewLine, model.Plugins);
            _extraArguments = model.ExtraArguments;
        }

        public ServerProfile Model { get; }

        [ObservableProperty] private string _name;
        [ObservableProperty] private string _host;
        [ObservableProperty] private int _port;
        [ObservableProperty] private string _ultimaOnlineDirectory;
        [ObservableProperty] private string _clientVersion;
        [ObservableProperty] private string _username;
        [ObservableProperty] private bool _savePassword;
        // plain text only in memory while the launcher is open
        [ObservableProperty] private string _password;
        [ObservableProperty] private bool _autoLogin;
        [ObservableProperty] private bool _useEncryption;
        [ObservableProperty] private string _plugins;
        [ObservableProperty] private string _extraArguments;

        partial void OnSavePasswordChanged(bool value)
        {
            if (!value)
            {
                Password = string.Empty;
                AutoLogin = false;
            }
        }

        // Copies the edited values back into the persisted model.
        public void Apply()
        {
            bool wasSaved = Model.SavePassword;

            Model.Name = string.IsNullOrWhiteSpace(Name) ? Host : Name.Trim();
            Model.Host = Host?.Trim() ?? string.Empty;
            Model.Port = Port;
            Model.UltimaOnlineDirectory = UltimaOnlineDirectory?.Trim() ?? string.Empty;
            Model.ClientVersion = ClientVersion?.Trim() ?? string.Empty;
            Model.Username = Username?.Trim() ?? string.Empty;
            Model.SavePassword = SavePassword;
            Model.ProtectedPassword = SavePassword ? PasswordProtector.Protect(Password ?? string.Empty) : string.Empty;
            Model.AutoLogin = SavePassword && AutoLogin;
            Model.UseEncryption = UseEncryption;
            Model.Plugins = (Plugins ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            Model.ExtraArguments = ExtraArguments?.Trim() ?? string.Empty;

            if (wasSaved && !SavePassword)
            {
                Model.ClearClientPassword = true;
            }
        }

        public override string ToString() => Name;
    }
}
