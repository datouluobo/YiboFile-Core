using System.Windows;
using System.Windows.Input;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Windows
{
    public partial class HotkeyEditWindow : Window
    {
        private HotkeyItemViewModel _item;
        private bool _hasMainKey;

        public HotkeyEditWindow(HotkeyItemViewModel item)
        {
            InitializeComponent();
            _item = item;
            _hasMainKey = !string.IsNullOrEmpty(item.KeyCombination) && !item.KeyCombination.EndsWith("...");

            DescText.Text = $"当前功能: {item.Description}";
            KeyDisplayText.Text = string.IsNullOrEmpty(item.KeyCombination) ? "请按下组合键..." : item.KeyCombination;

            Loaded += (s, a) => { Activate(); Focus(); Keyboard.Focus(this); };
            ContentRendered += (s, a) => Keyboard.Focus(this);
            MouseDown += (s, a) => Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            if (modifiers == ModifierKeys.None)
            {
                if (key == Key.Enter && _hasMainKey) { _item.KeyCombination = KeyDisplayText.Text; DialogResult = true; return; }
                if (key == Key.Escape) { DialogResult = false; return; }
            }

            var parts = new System.Collections.Generic.List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            bool isModifier = key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                              key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin;

            if (!isModifier)
            {
                var keyStr = key.ToString();
                if (key >= Key.D0 && key <= Key.D9) keyStr = (key - Key.D0).ToString();
                else if (key >= Key.NumPad0 && key <= Key.NumPad9) keyStr = (key - Key.NumPad0).ToString();
                else if (key == Key.OemPlus) keyStr = "=";
                else if (key == Key.OemMinus) keyStr = "-";
                else if (key == Key.OemPeriod) keyStr = ".";
                else if (key == Key.OemComma) keyStr = ",";

                parts.Add(keyStr);
                _hasMainKey = true;
            }
            else { parts.Add("... "); _hasMainKey = false; }

            KeyDisplayText.Text = string.Join("+", parts);
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_hasMainKey) { _item.KeyCombination = KeyDisplayText.Text; DialogResult = true; }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
