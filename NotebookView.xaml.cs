using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System;
using HandyControl.Controls;

namespace WpfApp3
{
    public partial class NotebookView : UserControl
    {
        private const string NotebookPath = "Notebooks";
        private bool isContentChanged = false;

        public NotebookView()
        {
            InitializeComponent();
            Loaded += NotebookView_Loaded;

            // ע�� Ctrl+S ��ݼ�
            var saveGesture = new KeyGesture(Key.S, ModifierKeys.Control);
            var saveCommand = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(saveCommand, SaveNoteShortcut_Executed));
            InputBindings.Add(new InputBinding(saveCommand, saveGesture));
        }

        private void SaveNoteShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveNote_Click(null, null); // ֱ�Ӹ���ԭ���ı����߼�
        }

        private void NotebookView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadNotes();
        }

        private void LoadNotes()
        {
            if (!Directory.Exists(NotebookPath))
            {
                Directory.CreateDirectory(NotebookPath);
            }

            NotesListBox.Items.Clear();
            var files = Directory.GetFiles(NotebookPath, "*.rtf");
            foreach (var file in files)
            {
                NotesListBox.Items.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesListBox.SelectedItem != null)
            {
                string fileName = NotesListBox.SelectedItem.ToString() + ".rtf";
                string fullPath = Path.Combine(NotebookPath, fileName);

                if (File.Exists(fullPath))
                {
                    TextRange range = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd);
                    using (FileStream fStream = new FileStream(fullPath, FileMode.Open))
                    {
                        range.Load(fStream, DataFormats.Rtf);
                    }
                }
            }
            UpdateSaveStatus(true);
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Window parentWindow = System.Windows.Window.GetWindow(this);
            InputBox frm = new InputBox(InputBoxMode.NewNote, "�½��ʼ�");
            frm.Owner = parentWindow;
            frm.accept += new EventHandler(NewNote_frm_accept);
            frm.Show();
        }

        void NewNote_frm_accept(object sender, EventArgs e)
        {
            InputBox frm = (InputBox)sender;
            string newFileName = frm.InputValue.Trim();

            if (string.IsNullOrEmpty(newFileName))
            {
                HandyControl.Controls.MessageBox.Show("�ʼ����Ʋ���Ϊ�ա�", "����", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string fullPath = Path.Combine(NotebookPath, newFileName + ".rtf");
            if (File.Exists(fullPath))
            {
                HandyControl.Controls.MessageBox.Show("�ñʼ������Ѵ��ڣ�������������", "�ļ����Ѵ���", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NoteRichTextBox.Document.Blocks.Clear();
            NotesListBox.Items.Add(newFileName);
            NotesListBox.SelectedItem = newFileName;
            SaveNote(newFileName);
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListBox.SelectedItem != null)
            {
                string selectedNoteName = NotesListBox.SelectedItem.ToString();
                System.Windows.Window parentWindow = System.Windows.Window.GetWindow(this);
                InputBox frm = new InputBox(InputBoxMode.DeleteNote, "ɾ���ʼ�", $"��ȷ��Ҫɾ���ʼ� ��{selectedNoteName}�� �𣿴˲������ɳ�����");
                frm.Owner = parentWindow;
                frm.accept += new EventHandler(DeleteNote_frm_accept);
                frm.Show();
            }
            else
            {
                HandyControl.Controls.MessageBox.Show("����ѡ��Ҫɾ���ıʼǡ�", "��ʾ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        void DeleteNote_frm_accept(object sender, EventArgs e)
        {
            if (NotesListBox.SelectedItem != null)
            {
                string fileName = NotesListBox.SelectedItem.ToString() + ".rtf";
                string fullPath = Path.Combine(NotebookPath, fileName);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    NoteRichTextBox.Document.Blocks.Clear();
                    LoadNotes();
                    HandyControl.Controls.MessageBox.Show("�ʼ���ɾ����", "�����ɹ�", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void SaveNote(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                HandyControl.Controls.MessageBox.Show("�ļ�����Ч���޷����档", "����", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string fullPath = Path.Combine(NotebookPath, fileName + ".rtf");
            TextRange range = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd);
            using (FileStream fStream = new FileStream(fullPath, FileMode.Create))
            {
                range.Save(fStream, DataFormats.Rtf);
            }
        }

        private void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListBox.SelectedItem != null)
            {
                SaveNote(NotesListBox.SelectedItem.ToString());
                UpdateSaveStatus(true);
            }
            else
            {
                HandyControl.Controls.MessageBox.Show("���ȴ���һ���±ʼǻ�ѡ��һ�����бʼǡ�", "��ʾ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            var selection = NoteRichTextBox.Selection;
            if (!selection.IsEmpty)
            {
                TextRange textRange = new TextRange(selection.Start, selection.End);
                if (textRange.GetPropertyValue(TextElement.FontWeightProperty).Equals(FontWeights.Bold))
                {
                    textRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                }
                else
                {
                    textRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                }
            }
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            var selection = NoteRichTextBox.Selection;
            if (!selection.IsEmpty)
            {
                TextRange textRange = new TextRange(selection.Start, selection.End);
                if (textRange.GetPropertyValue(TextElement.FontStyleProperty).Equals(FontStyles.Italic))
                {
                    textRange.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                }
                else
                {
                    textRange.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                }
            }
        }

        private void Heading1_Click(object sender, RoutedEventArgs e)
        {
            ToggleHeading(32.0);
        }

        private void Heading2_Click(object sender, RoutedEventArgs e)
        {
            ToggleHeading(24.0);
        }

        private void ToggleHeading(double fontSize)
        {
            var selection = NoteRichTextBox.Selection;
            if (selection.IsEmpty)
            {
                return;
            }

            TextRange textRange = new TextRange(selection.Start, selection.End);

            if (textRange.GetPropertyValue(TextElement.FontSizeProperty) is double currentSize && currentSize == fontSize)
            {
                textRange.ApplyPropertyValue(TextElement.FontSizeProperty, 14.0);
                textRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            }
            else
            {
                textRange.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
                textRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            }
        }

        private void RichTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);

                DataObject dataObject = new DataObject(DataFormats.Text, text);
                e.DataObject = dataObject;
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void NoteRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isContentChanged)
            {
                isContentChanged = true;
                UpdateSaveStatus(false);
            }
        }

        private void UpdateSaveStatus(bool isSaved)
        {
            if (isSaved)
            {
                StatusTextBlock.Text = "�ѱ���";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                isContentChanged = false;
            }
            else
            {
                StatusTextBlock.Text = "δ����";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        // ���������������ܣ�ͨ���Ҽ��˵�����
        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListBox.SelectedItem != null)
            {
                string oldName = NotesListBox.SelectedItem.ToString();
                System.Windows.Window parentWindow = System.Windows.Window.GetWindow(this);
                InputBox frm = new InputBox(InputBoxMode.NewNote, "�������ʼ�", oldName);
                frm.Owner = parentWindow;
                frm.accept += (s, args) =>
                {
                    string newName = frm.InputValue.Trim();
                    if (string.IsNullOrEmpty(newName))
                    {
                        HandyControl.Controls.MessageBox.Show("�ʼ����Ʋ���Ϊ�ա�", "����", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (newName == oldName)
                    {
                        return;
                    }
                    if (File.Exists(Path.Combine(NotebookPath, newName + ".rtf")))
                    {
                        HandyControl.Controls.MessageBox.Show("�ñʼ������Ѵ��ڣ�������������", "�ļ����Ѵ���", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    RenameNote(oldName, newName);
                };
                frm.Show();
            }
            else
            {
                HandyControl.Controls.MessageBox.Show("����ѡ��Ҫ�������ıʼǡ�", "��ʾ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RenameNote(string oldName, string newName)
        {
            try
            {
                string oldPath = Path.Combine(NotebookPath, oldName + ".rtf");
                string newPath = Path.Combine(NotebookPath, newName + ".rtf");
                File.Move(oldPath, newPath);
                LoadNotes();
                NotesListBox.SelectedItem = newName;
                HandyControl.Controls.MessageBox.Show("�������ɹ���", "�����ɹ�", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"������ʧ��: {ex.Message}", "����", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}