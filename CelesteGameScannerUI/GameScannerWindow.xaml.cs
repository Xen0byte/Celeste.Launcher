﻿using Celeste_Launcher_Gui;
using Celeste_Launcher_Gui.Helpers;
using Celeste_Launcher_Gui.Windows;
using Celeste_Public_Api.Helpers;
using Celeste_Public_Api.Logging;
using ProjectCeleste.GameFiles.GameScanner;
using ProjectCeleste.GameFiles.GameScanner.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CelesteGameScannerUI
{
    /// <summary>
    /// Interaction logic for GameScannerWindow.xaml
    /// </summary>
    public partial class GameScannerWindow : Window
    {
        private readonly GameScannerManager GameScanner;
        private static readonly ILogger Logger = LoggerFactory.GetLogger();

        public GameScannerWindow()
        {
            var gameFilesPath = LegacyBootstrapper.UserConfig.GameFilesPath;
            var isSteam = LegacyBootstrapper.UserConfig.IsSteamVersion;

            InitializeComponent();

            Logger.Information($"Initialized game scanner to directory {gameFilesPath}");

            if (!Directory.Exists(gameFilesPath))
                Directory.CreateDirectory(gameFilesPath);

            GameScanner = new GameScannerManager(gameFilesPath, isSteam);
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnMoveWindow(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void OpenPatchNotes(object sender, RoutedEventArgs e)
        {
            Process.Start("https://forums.projectceleste.com/forums/announcements.12/");
        }

        private void OpenDiscord(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/xXFUvWA");
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            GameScanner.Dispose();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SetupScenarioSymlink();
                await GameScanner.InitializeFromCelesteManifest();
                var progress = new Progress<ScanProgress>();
                var subProgress = new Progress<ScanSubProgress>();

                progress.ProgressChanged += ProgressChanged;
                subProgress.ProgressChanged += SubProgressChanged;

                if (await Task.Run(async() => await GameScanner.ScanAndRepair(progress, subProgress, 1)))
                {
                    CurrentFileLabel.Content = string.Empty;
                    MainProgressLabel.Content =  Celeste_Launcher_Gui.Properties.Resources.GameScannerDoneLabel;
                    FileProgress.ProgressBar.IsIndeterminate = false;
                    if (!LegacyBootstrapper.GameScannerNoConfirmDialog)
                        GenericMessageDialog.Show(Celeste_Launcher_Gui.Properties.Resources.GameScannerDoneMessage, DialogIcon.None, DialogOptions.Ok);
                }
                else
                {
                    FailGameScan(Celeste_Launcher_Gui.Properties.Resources.GameScannerDidNotPass);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
                FailGameScan(Celeste_Launcher_Gui.Properties.Resources.GameScannerFailed);
            }
            finally
            {
                Close();
            }
        }

        private void SetupScenarioSymlink()
        {
            var gamePath = LegacyBootstrapper.UserConfig.GameFilesPath;

            //SymLink CustomScn Folder
            var myDocumentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var customScnGamePath = Path.Combine(gamePath, "Scenario", "CustomScn");
            var scenarioUserPath = Path.Combine(myDocumentsDir, "Spartan", "Scenario");

            Logger.Information("CustomScn directory: {@customScnPath}", customScnGamePath);
            Logger.Information("Scenario directory: {@scenarioPath}", scenarioUserPath);

            if (!Directory.Exists(scenarioUserPath))
                Directory.CreateDirectory(scenarioUserPath);

            if (Directory.Exists(customScnGamePath) &&
                (!Misc.IsSymLink(customScnGamePath, Misc.SymLinkFlag.Directory) ||
                 !string.Equals(Misc.GetRealPath(customScnGamePath), scenarioUserPath, StringComparison.OrdinalIgnoreCase)))
            {
                Directory.Delete(customScnGamePath, true);
                Misc.CreateSymbolicLink(customScnGamePath, scenarioUserPath, Misc.SymLinkFlag.Directory);
            }
            else
            {
                Misc.CreateSymbolicLink(customScnGamePath, scenarioUserPath, Misc.SymLinkFlag.Directory);
            }
        }

        private void ProgressChanged(object sender, ScanProgress e)
        {
            var wrappedFileName = e.File.WrapIfLengthIsLongerThan(35, "...");
            CurrentFileLabel.Content = $"{wrappedFileName} ({e.Index}/{e.TotalIndex})";
            ScanTotalProgress.ProgressBar.Value = e.ProgressPercentage;
            TaskbarItemInfo.ProgressValue = (e.ProgressPercentage / 100);
        }

        private void FailGameScan(string reason)
        {
            FileProgress.ProgressBar.Foreground = Brushes.Red;
            ScanTotalProgress.ProgressBar.Foreground = Brushes.Red;
            CurrentFileLabel.Content = string.Empty;
            MainProgressLabel.Content = string.Empty;
            TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
            if (!LegacyBootstrapper.GameScannerNoConfirmDialog)
                GenericMessageDialog.Show(reason, DialogIcon.Error, DialogOptions.Ok);
        }

        private void SubProgressChanged(object sender, ScanSubProgress e)
        {
            switch (e.Step)
            {
                case ScanSubProgressStep.Check:
                    MainProgressLabel.Content = Celeste_Launcher_Gui.Properties.Resources.GameScannerVerifying;
                    FileProgress.ProgressBar.IsIndeterminate = false;
                    FileProgress.ProgressBar.Value = e.ProgressPercentage;
                    break;
                case ScanSubProgressStep.Download:
                    if (e.DownloadProgress != null)
                    {
                        if (e.DownloadProgress.Size == 0)
                        {
                            MainProgressLabel.Content = Celeste_Launcher_Gui.Properties.Resources.GameScannerDownloadStarting;
                        }
                        else
                        {
                            var downloaded = BytesSizeExtension.FormatToBytesSizeThreeNonZeroDigits(e.DownloadProgress.SizeCompleted);
                            var leftToDownload = BytesSizeExtension.FormatToBytesSizeThreeNonZeroDigits(e.DownloadProgress.Size);

                            var downloadSpeed = double.IsInfinity(e.DownloadProgress.Speed) ?
                                string.Empty : $"({BytesSizeExtension.FormatToBytesSizeThreeNonZeroDigits(e.DownloadProgress.Speed)}/s)";

                            MainProgressLabel.Content = $"{Celeste_Launcher_Gui.Properties.Resources.GameScannerDownloading} {downloaded}/{leftToDownload} {downloadSpeed}";
                        }
                    }

                    FileProgress.ProgressBar.Value = e.ProgressPercentage;
                    FileProgress.ProgressBar.IsIndeterminate = false;
                    break;
                case ScanSubProgressStep.CheckDownload:
                    MainProgressLabel.Content = Celeste_Launcher_Gui.Properties.Resources.GameScannerVerifyingDownloadedFile;
                    FileProgress.ProgressBar.IsIndeterminate = true;
                    break;
                case ScanSubProgressStep.ExtractDownload:
                    MainProgressLabel.Content = Celeste_Launcher_Gui.Properties.Resources.GameScannerExtracting;
                    FileProgress.ProgressBar.Value = e.ProgressPercentage;
                    FileProgress.ProgressBar.IsIndeterminate = false;
                    break;
                case ScanSubProgressStep.CheckExtractDownload:
                    MainProgressLabel.Content = Celeste_Launcher_Gui.Properties.Resources.GameScannerVerifyingExtractedFile;
                    FileProgress.ProgressBar.IsIndeterminate = true;
                    break;
                case ScanSubProgressStep.Finalize:
                    MainProgressLabel.Content = Celeste_Launcher_Gui.Properties.Resources.GameScannerFinalizing;
                    FileProgress.ProgressBar.IsIndeterminate = true;
                    break;
                case ScanSubProgressStep.End:
                    FileProgress.ProgressBar.Value = 100;
                    ScanTotalProgress.ProgressBar.Value = 100;
                    TaskbarItemInfo.ProgressValue = 100;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(e.Step), e.Step, null);
            }
        }
    }
}
