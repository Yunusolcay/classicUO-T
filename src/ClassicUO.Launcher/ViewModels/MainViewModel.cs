// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Launcher.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassicUO.Launcher.ViewModels
{
    internal sealed partial class MainViewModel : ObservableObject
    {
        private readonly LauncherConfig _config;
        private UpdateCheck? _pendingUpdate;
        private CancellationTokenSource? _cts;

        public MainViewModel(LauncherConfig config)
        {
            _config = config;

            foreach (ServerProfile p in config.Profiles)
            {
                Profiles.Add(new ProfileViewModel(p));
            }

            ServerProfile? selected = config.GetSelectedProfile();
            _selectedProfile = selected != null ? FindViewModel(selected) : null;

            _clientDirectory = config.ClientDirectory;
            _updateManifestUrl = config.UpdateManifestUrl;
            _updateDownloadBaseUrl = config.UpdateDownloadBaseUrl;
            _checkUpdatesOnStart = config.CheckUpdatesOnStart;
            _closeAfterLaunch = config.CloseAfterLaunch;
            _statusText = Strings.Ready;

            // first run: fill in the UO folder if it can be found
            if (_selectedProfile != null && string.IsNullOrWhiteSpace(_selectedProfile.UltimaOnlineDirectory))
            {
                _selectedProfile.UltimaOnlineDirectory = UoPathDetector.Detect() ?? string.Empty;
            }
        }

        // set by the view: folder picker and window close
        public Func<Task<string?>>? PickFolder { get; set; }
        public Action? RequestClose { get; set; }

        public ObservableCollection<ProfileViewModel> Profiles { get; } = new();

        [ObservableProperty] private ProfileViewModel? _selectedProfile;
        [ObservableProperty] private string _statusText;
        [ObservableProperty] private bool _isError;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private bool _showProgress;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(CheckUpdatesCommand), nameof(DeleteProfileCommand))]
        private bool _isBusy;
        [ObservableProperty] private string _playText = Strings.Play;

        [ObservableProperty] private string _clientDirectory;
        [ObservableProperty] private string _updateManifestUrl;
        [ObservableProperty] private string _updateDownloadBaseUrl;
        [ObservableProperty] private bool _checkUpdatesOnStart;
        [ObservableProperty] private bool _closeAfterLaunch;

        private bool UpdatesConfigured => !string.IsNullOrWhiteSpace(UpdateManifestUrl);

        public async Task OnStartedAsync()
        {
            if (CheckUpdatesOnStart && UpdatesConfigured)
            {
                await CheckUpdatesAsync();
            }
        }

        [RelayCommand]
        private void AddProfile()
        {
            var vm = new ProfileViewModel(new ServerProfile { Name = "Server", UltimaOnlineDirectory = SelectedProfile?.UltimaOnlineDirectory ?? string.Empty });
            Profiles.Add(vm);
            SelectedProfile = vm;
        }

        [RelayCommand]
        private void DuplicateProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            SelectedProfile.Apply();
            ServerProfile copy = SelectedProfile.Model.Clone();
            copy.Name += " (2)";
            // a saved password belongs to one profile only
            copy.SavePassword = false;
            copy.ProtectedPassword = string.Empty;
            copy.AutoLogin = false;

            var vm = new ProfileViewModel(copy);
            Profiles.Add(vm);
            SelectedProfile = vm;
        }

        private bool CanDeleteProfile() => !IsBusy && Profiles.Count > 1;

        [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
        private void DeleteProfile()
        {
            if (SelectedProfile == null || Profiles.Count <= 1)
            {
                return;
            }

            int index = Profiles.IndexOf(SelectedProfile);
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles[Math.Min(index, Profiles.Count - 1)];
            Save();
        }

        [RelayCommand]
        private async Task BrowseUoFolderAsync()
        {
            if (SelectedProfile == null || PickFolder == null)
            {
                return;
            }

            string? folder = await PickFolder();

            if (folder != null)
            {
                SelectedProfile.UltimaOnlineDirectory = folder;
                ValidateUoFolder();
            }
        }

        [RelayCommand]
        private void DetectUoFolder()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            string? found = UoPathDetector.Detect();

            if (found == null)
            {
                SetError(Strings.ErrUoNotFound);

                return;
            }

            SelectedProfile.UltimaOnlineDirectory = found;
            SetStatus(found);
        }

        [RelayCommand]
        private void OpenClientFolder()
        {
            string dir = GetClientDirectory();
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }

        private bool CanRun() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanRun))]
        private async Task CheckUpdatesAsync()
        {
            Save();

            if (!UpdatesConfigured)
            {
                SetStatus(Strings.UpdatesDisabled);

                return;
            }

            await RunBusyAsync(async token =>
            {
                SetStatus(Strings.Checking);
                _pendingUpdate = await new ClientUpdater(GetClientDirectory()).CheckAsync(UpdateManifestUrl, UpdateDownloadBaseUrl, token);
                ShowUpdateState();
            }, ex => SetError(Strings.ErrUpdate(ex.Message)));
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private async Task PlayAsync()
        {
            Save();
            ProfileViewModel? profile = SelectedProfile;

            if (profile == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.Host) || profile.Port is < 1 or > 65535)
            {
                SetError(Strings.ErrHost);

                return;
            }

            if (!ValidateUoFolder())
            {
                return;
            }

            await RunBusyAsync(async token =>
            {
                string clientDir = GetClientDirectory();

                // Everyone on the shard should run the same client build, so update before every
                // launch when an update source is configured.
                if (UpdatesConfigured)
                {
                    try
                    {
                        var updater = new ClientUpdater(clientDir);
                        SetStatus(Strings.Checking);
                        _pendingUpdate = await updater.CheckAsync(UpdateManifestUrl, UpdateDownloadBaseUrl, token);

                        if (!_pendingUpdate.UpToDate)
                        {
                            ShowProgress = true;
                            await updater.ApplyAsync(_pendingUpdate, new Progress<UpdateProgress>(ReportProgress), token);
                            _config.InstalledVersion = _pendingUpdate.Manifest.Version;
                            _config.Save();
                            SetStatus(Strings.Updated(_pendingUpdate.Manifest.Version));
                            _pendingUpdate = null;
                            PlayText = Strings.Play;
                        }
                    }
                    catch (ClientRunningException)
                    {
                        SetError(Strings.ErrClientRunning);

                        return;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
                    {
                        // bad URL, manifest or archive: nothing was installed, do not start
                        LauncherLog.Write($"Update failed: {ex}");
                        SetError(Strings.ErrUpdate(ex.Message));

                        return;
                    }
                    catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException
                                                   && File.Exists(Path.Combine(clientDir, ClientStarter.BOOTSTRAP_EXE)))
                    {
                        // offline or update host down: the installed client still works
                        LauncherLog.Write($"Update check failed: {ex}");
                        SetError(Strings.ErrUpdateOffline(ex.Message));
                    }
                }

                if (!File.Exists(Path.Combine(clientDir, ClientStarter.BOOTSTRAP_EXE)))
                {
                    SetError(Strings.ErrClientMissing(clientDir));

                    return;
                }

                SetStatus(Strings.Starting);

                try
                {
                    ClientStarter.Start(clientDir, profile.Model);
                }
                catch (System.ComponentModel.Win32Exception) when (!OperatingSystem.IsWindows())
                {
                    SetError(Strings.ErrMono);

                    return;
                }

                // the starter may have consumed a one-shot flag (ClearClientPassword)
                _config.Save();

                if (!IsError)
                {
                    SetStatus(Strings.Started);
                }

                if (CloseAfterLaunch)
                {
                    RequestClose?.Invoke();
                }
            }, ex => SetError(Strings.ErrStart(ex.Message)));
        }

        public void Save()
        {
            foreach (ProfileViewModel p in Profiles)
            {
                p.Apply();
            }

            _config.Profiles = new(Profiles.Count);

            foreach (ProfileViewModel p in Profiles)
            {
                _config.Profiles.Add(p.Model);
            }

            _config.SelectedProfileId = SelectedProfile?.Model.Id ?? string.Empty;
            _config.ClientDirectory = string.IsNullOrWhiteSpace(ClientDirectory) ? "ClassicUO" : ClientDirectory.Trim();
            _config.UpdateManifestUrl = UpdateManifestUrl?.Trim() ?? string.Empty;
            _config.UpdateDownloadBaseUrl = UpdateDownloadBaseUrl?.Trim() ?? string.Empty;
            _config.CheckUpdatesOnStart = CheckUpdatesOnStart;
            _config.CloseAfterLaunch = CloseAfterLaunch;

            try
            {
                _config.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetError(ex.Message);
            }
        }

        public void Cancel() => _cts?.Cancel();

        partial void OnSelectedProfileChanged(ProfileViewModel? value)
        {
            DeleteProfileCommand.NotifyCanExecuteChanged();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName is nameof(UpdateManifestUrl) or nameof(UpdateDownloadBaseUrl) or nameof(ClientDirectory))
            {
                // a different source or folder invalidates the last check
                _pendingUpdate = null;
                PlayText = Strings.Play;
            }
        }

        private bool ValidateUoFolder()
        {
            if (SelectedProfile == null || !UoPathDetector.IsValidUoDirectory(SelectedProfile.UltimaOnlineDirectory))
            {
                SetError(Strings.ErrUoDirectory);

                return false;
            }

            return true;
        }

        private void ShowUpdateState()
        {
            if (_pendingUpdate == null)
            {
                return;
            }

            if (_pendingUpdate.UpToDate)
            {
                SetStatus(Strings.UpToDate(_pendingUpdate.Manifest.Version));
                PlayText = Strings.Play;
            }
            else
            {
                SetStatus(Strings.UpdateAvailable(_pendingUpdate.Outdated.Count, _pendingUpdate.Manifest.Version));
                PlayText = Strings.UpdateAndPlay;
            }
        }

        private void ReportProgress(UpdateProgress p)
        {
            string stage = p.Stage switch
            {
                "download" => Strings.Downloading,
                "verify" => Strings.Verifying,
                _ => Strings.Installing
            };

            StatusText = $"{stage}... {p.Fraction:P0}";
            IsError = false;
            Progress = p.Fraction * 100;
        }

        private async Task RunBusyAsync(Func<CancellationToken, Task> action, Action<Exception> onError)
        {
            IsBusy = true;
            _cts = new CancellationTokenSource();

            try
            {
                await action(_cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                SetStatus(Strings.Ready);
            }
            catch (Exception ex)
            {
                LauncherLog.Write(ex.ToString());
                onError(ex);
            }
            finally
            {
                ShowProgress = false;
                Progress = 0;
                IsBusy = false;
                _cts.Dispose();
                _cts = null;
            }
        }

        private string GetClientDirectory()
        {
            _config.ClientDirectory = string.IsNullOrWhiteSpace(ClientDirectory) ? "ClassicUO" : ClientDirectory.Trim();

            return _config.GetClientDirectory();
        }

        private ProfileViewModel? FindViewModel(ServerProfile model)
        {
            foreach (ProfileViewModel vm in Profiles)
            {
                if (vm.Model == model)
                {
                    return vm;
                }
            }

            return null;
        }

        private void SetStatus(string text)
        {
            StatusText = text;
            IsError = false;
        }

        private void SetError(string text)
        {
            StatusText = text;
            IsError = true;
        }
    }
}
