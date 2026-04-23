using System;
using System.ComponentModel;
using System.Windows;
using YiboFile.Models;
using YiboFile.Services.FileNotes;

namespace YiboFile.Windows
{
    public partial class NotesEditWindow : Window, INotifyPropertyChanged
    {
        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        private string _notes;
        public string Notes
        {
            get => _notes;
            set
            {
                _notes = value;
                OnPropertyChanged(nameof(Notes));
            }
        }

        private readonly string _filePath;
        public bool NotesSaved { get; private set; }

        public NotesEditWindow(string filePath, string fileName, string currentNotes)
        {
            InitializeComponent();
            DataContext = this;

            _filePath = filePath;
            FileName = fileName;
            Notes = currentNotes ?? "";
            NotesSaved = false;

            Loaded += (s, e) => NotesTextBox.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileNotesService.SetFileNotes(_filePath, Notes);
                NotesSaved = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存笔记失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
