using CommunityToolkit.Mvvm.ComponentModel;

namespace ED9FontCreator.ViewModels
{
    public partial class FntChar : ViewModelBase
    {
        [ObservableProperty] private long _offset;
        [ObservableProperty] private char _Char;
        [ObservableProperty] private int _type; //0: Loose spacing; 1: Compact
        [ObservableProperty] private short _x;
        [ObservableProperty] private short _y;
        [ObservableProperty] private short _pixelWidth; // Pixel width
        [ObservableProperty] private short _pixelHeight;
        [ObservableProperty] private short _colorChannel;
        [ObservableProperty] private short _xOffset;
        [ObservableProperty] private short _yOffset;
        [ObservableProperty] private short _width;  // Occupied width

        public char ReplacedChar;
        public int Code => Char;
        public short MaxWidth;
    }
}