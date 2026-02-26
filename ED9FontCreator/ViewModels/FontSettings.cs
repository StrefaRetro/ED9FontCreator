using CommunityToolkit.Mvvm.ComponentModel;

namespace ED9FontCreator.ViewModels
{
    public partial class FontSettings : ViewModelBase
    {
        [ObservableProperty] private string _fontName = "Segoe UI";
        [ObservableProperty] private short _fontSize = 42;
        [ObservableProperty] private string _fontWeight = nameof(Avalonia.Media.FontWeight.Medium);
        [ObservableProperty] private string _fontStyle = nameof(Avalonia.Media.FontStyle.Normal);
        [ObservableProperty] private int _padding = 1;
        [ObservableProperty] private int _topPadding = 4;
        [ObservableProperty] private int _lineHeightAdjustment = 0;
        [ObservableProperty] private int _widthAdjustment = 40;
    }
}