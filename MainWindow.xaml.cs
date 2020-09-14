using Microsoft.Win32;
using PS3TRPViewer.TRPFormat;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace PS3TRPViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public static TrophyFile TrophyFile { get; set; }
        public ObservableCollection<TabItem> Tabs {get; set;}
        public MainWindow()
        {
            Tabs = new ObservableCollection<TabItem>();

            InitializeComponent();
            DataContext = this;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                DefaultExt = ".trp",
                Filter = "Trophy File|*.trp",
                Multiselect = false
            };
            if (!fileDialog.ShowDialog(this).GetValueOrDefault(false)) return;
            TrophyFile = new TrophyFile(fileDialog.FileName);

            int i = 0;
            Tabs.Clear();
            foreach(SFMFile configFile in TrophyFile.ConfigFiles)
            {
                Tabs.Add(new TabItem { Header = "Config #" + i, ConfigFile = configFile });
                i++;
            }
            DataContext = null;
            DataContext = this;
            ProfileTab.SelectedIndex = 0;

        }

        private void Extract_Click(object sender, RoutedEventArgs e)
        {
            if(TrophyFile == null)
            {
                OpenFileDialog fileDialog = new OpenFileDialog
                {
                    DefaultExt = ".trp",
                    Filter = "Trophy File|*.trp",
                    Multiselect = false
                };
                if (!fileDialog.ShowDialog(this).GetValueOrDefault(false)) return;
                TrophyFile = new TrophyFile(fileDialog.FileName);
            }

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var dlg = new CommonOpenFileDialog();
                dlg.IsFolderPicker = true;
                if(dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    TrophyFile.ExportExtractedFiles(dlg.FileName);
                    Process.Start("explorer.exe", dlg.FileName);
                }
            }
        }
    }

    public sealed class TabItem
    {
        public string Header { get; set; }
        public SFMFile ConfigFile { get; set; }
    }
}
