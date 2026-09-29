using System;
using System.ComponentModel;

namespace KingR9Tools.Core
{
    public class Tweak : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Recommended { get; set; }
        public Action<Logger> ApplyAction { get; set; }
        public Action<Logger> UndoAction { get; set; }
        public bool NeedsRestart { get; set; }

        private bool _sel;
        public bool IsSelected
        {
            get => _sel;
            set { _sel = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class CategoryItem
    {
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public string Icon { get; set; }
        public int Count { get; set; }
    }
}