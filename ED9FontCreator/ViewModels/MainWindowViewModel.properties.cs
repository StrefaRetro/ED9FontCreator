using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ED9FontCreator.Views;
using System;
using System.Collections.Generic;

namespace ED9FontCreator.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly string OutDir;
        //GAME FNT
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Analysed))]
        [NotifyCanExecuteChangedFor(nameof(GenerateCharsCommand))]
        private Fnt? _fnt;
        [ObservableProperty] private string _fntPath = "";
        [ObservableProperty] private FntChar? _searchedFntChar;
        public bool Analysed => this.Fnt != null;
        //font settings
        [ObservableProperty] private FontSettings _fontSettings = new();
        public string[] FontWeights => Enum.GetNames(typeof(FontWeight));
        public string[] FontStyles => Enum.GetNames(typeof(FontStyle));

        //char settings
        [ObservableProperty] private bool _isSimplifiedChinese = true;
        [ObservableProperty] private bool _addPolishChars;

        [ObservableProperty] private string _replaceText = "";
        private string ReplaceTxtFile => System.IO.Path.Combine(Environment.CurrentDirectory, "replace.txt");
        //display canvas
        [ObservableProperty] private List<FntChar>? _drawChars;
        [ObservableProperty] private List<FntChar>? _previewChars;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(ExportFontCommand))] private bool _canExportFont;
        public CharsCanvas DrawCanvas;
        //info
        [ObservableProperty] private string _infoText;
        [ObservableProperty] private InfoBarState _infoState;
    }
}