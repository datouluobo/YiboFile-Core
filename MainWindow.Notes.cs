using System;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile
{
    /// <summary>
    /// MainWindow 备注功能
    /// </summary>
    public partial class MainWindow
    {
        #region 备注功能

        internal void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _fileNotesUIHandler?.NotesTextBox_TextChanged(sender, e);
        }

        internal void NotesAutoSaved_Handler(object sender, RoutedEventArgs e)
        {
            _fileNotesUIHandler?.NotesAutoSaved_Handler(sender, e);
        }

        #endregion
    }
}

