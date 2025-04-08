using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Data.Converters;
using Avalonia.VisualTree; // For GetVisualDescendants()
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;  // For CancelEventArgs
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShadowKeys
{
    // Model representing a dynamic field (key-value pair).
    public class FieldEntry
    {
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
    }

    // Model representing a record composed of multiple fields.
    public class Record
    {
        // New property for the record name.
        public string Name { get; set; } = "Untitled Record";

        public ObservableCollection<FieldEntry> Fields { get; set; } = new ObservableCollection<FieldEntry>();

        // For display in the list we now show the configured record name.
        public override string ToString()
        {
            return Name;
        }
    }

    // Converter to hide password values (used in the list display).
    public class FieldValueMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FieldEntry field)
            {
                if (!string.IsNullOrEmpty(field.FieldName) &&
                    field.FieldName.Equals("password", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "********";
                }
                return field.FieldValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : Window
    {
        private const string RecordsFilePath = "records.enc";

        private ObservableCollection<Record> _records;
        private ObservableCollection<FieldEntry> _currentFields;
        private Record _editingRecord; // Holds the record currently being edited

        public MainWindow()
        {
            InitializeComponent();

            // Load persisted records if available.
            _records = LoadRecords();
            RecordsListBox.ItemsSource = _records;

            _currentFields = new ObservableCollection<FieldEntry>();
            FieldsItemsControl.ItemsSource = _currentFields;

            // Save records when window is closing.
            this.Closing += OnWindowClosing;
        }

        private ObservableCollection<Record> LoadRecords()
        {
            if (File.Exists(RecordsFilePath))
            {
                try
                {
                    string encryptedData = File.ReadAllText(RecordsFilePath);
                    string json = EncryptionHelper.DecryptString(encryptedData);
                    var importedRecords = JsonSerializer.Deserialize<ObservableCollection<Record>>(json);
                    if (importedRecords != null)
                    {
                        return importedRecords;
                    }
                }
                catch (Exception ex)
                {
                    Notify("Failed to load records: " + ex.Message, true);
                }
            }
            return new ObservableCollection<Record>();
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            SaveRecords();
        }

        private void SaveRecords()
        {
            try
            {
                string json = JsonSerializer.Serialize(_records);
                string encryptedData = EncryptionHelper.EncryptString(json);
                File.WriteAllText(RecordsFilePath, encryptedData);
            }
            catch (Exception ex)
            {
                Notify("Failed to save records: " + ex.Message, true);
            }
        }

        // Add a new field row in the editing panel.
        private void OnAddField(object sender, RoutedEventArgs e)
        {
            _currentFields.Add(new FieldEntry { FieldName = "Field", FieldValue = "" });
        }

        // Remove a field row.
        private void OnRemoveField(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FieldEntry field)
            {
                _currentFields.Remove(field);
            }
        }

        // Handler to copy a specific field value in the display (list) mode.
        private async void OnCopyField(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FieldEntry field)
            {
                await this.Clipboard.SetTextAsync(field.FieldValue);
                Notify($"Copied '{field.FieldName}' field value.");
            }
        }

        // Add a new record based on the current record name and fields.
        private void AddRecordButton_Click(object sender, RoutedEventArgs e)
        {
            // Require at least one field.
            if (_currentFields.Count == 0)
            {
                Notify("Please add at least one field.", true);
                return;
            }

            // Ensure the record name field has a value.
            if (string.IsNullOrWhiteSpace(RecordNameTextBox.Text))
            {
                Notify("Please enter a record name.", true);
                return;
            }

            var record = new Record
            {
                Name = RecordNameTextBox.Text.Trim()
            };

            foreach (var field in _currentFields)
            {
                record.Fields.Add(new FieldEntry { FieldName = field.FieldName, FieldValue = field.FieldValue });
            }
            _records.Add(record);
            ClearCurrentFields();
            RecordNameTextBox.Text = "";
            Notify("Record added.");
        }

        // Save changes to the currently editing record.
        private void SaveRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editingRecord == null)
            {
                Notify("No record selected for editing.", true);
                return;
            }

            // Update record name from TextBox.
            if (string.IsNullOrWhiteSpace(RecordNameTextBox.Text))
            {
                Notify("Please enter a record name.", true);
                return;
            }
            _editingRecord.Name = RecordNameTextBox.Text.Trim();

            _editingRecord.Fields.Clear();
            foreach (var field in _currentFields)
            {
                _editingRecord.Fields.Add(new FieldEntry { FieldName = field.FieldName, FieldValue = field.FieldValue });
            }
            // Refresh the ListBox.
            RecordsListBox.ItemsSource = null;
            RecordsListBox.ItemsSource = _records;
            ClearCurrentFields();
            RecordNameTextBox.Text = "";
            _editingRecord = null;
            Notify("Record updated.");
        }

        // Load the selected record into the editing panel.
        private void EditRecordButton_Click(object sender, RoutedEventArgs e)
        {
            // Allow selection by Tag or from the ListBox's selected item.
            Record record = null;
            if (sender is Button btn && btn.Tag is Record taggedRecord)
            {
                record = taggedRecord;
            }
            else if (RecordsListBox.SelectedItem is Record selectedRecord)
            {
                record = selectedRecord;
            }

            if (record != null)
            {
                _editingRecord = record;
                RecordNameTextBox.Text = record.Name;
                _currentFields.Clear();
                foreach (var field in record.Fields)
                {
                    _currentFields.Add(new FieldEntry { FieldName = field.FieldName, FieldValue = field.FieldValue });
                }
                Notify("Editing record.");
            }
            else
            {
                Notify("Please select a record to edit.", true);
            }
        }

        // Remove the selected record.
        private void RemoveRecordButton_Click(object sender, RoutedEventArgs e)
        {
            Record record = null;
            if (sender is Button btn && btn.Tag is Record taggedRecord)
            {
                record = taggedRecord;
            }
            else if (RecordsListBox.SelectedItem is Record selectedRecord)
            {
                record = selectedRecord;
            }
            if (record != null)
            {
                _records.Remove(record);
                ClearCurrentFields();
                _editingRecord = null;
                Notify("Record removed.");
            }
            else
            {
                Notify("Please select a record to remove.", true);
            }
        }

        // Import records from an encrypted file.
        private async void OnImport(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filters = { new FileDialogFilter { Name = "Encrypted Files", Extensions = { "enc" } } },
                AllowMultiple = false
            };
            var result = await openFileDialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                try
                {
                    string encryptedData = await File.ReadAllTextAsync(result[0]);
                    string json = EncryptionHelper.DecryptString(encryptedData);
                    var importedRecords = JsonSerializer.Deserialize<ObservableCollection<Record>>(json);
                    if (importedRecords != null)
                    {
                        _records = importedRecords;
                        RecordsListBox.ItemsSource = _records;
                        Notify("Records imported successfully.");
                    }
                }
                catch (Exception ex)
                {
                    Notify("Failed to import records: " + ex.Message, true);
                }
            }
        }

        // Export records to an encrypted file.
        private async void OnExport(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filters = { new FileDialogFilter { Name = "Encrypted Files", Extensions = { "enc" } } },
                InitialFileName = "records.enc"
            };
            var result = await saveFileDialog.ShowAsync(this);
            if (!string.IsNullOrWhiteSpace(result))
            {
                try
                {
                    string json = JsonSerializer.Serialize(_records);
                    string encryptedData = EncryptionHelper.EncryptString(json);
                    await File.WriteAllTextAsync(result, encryptedData);
                    Notify("Records exported successfully.");
                }
                catch (Exception ex)
                {
                    Notify("Failed to export records: " + ex.Message, true);
                }
            }
        }

        // Helper method to clear the dynamic fields.
        private void ClearCurrentFields()
        {
            _currentFields.Clear();
        }

        // Display a temporary notification message.
        private void Notify(string message, bool isError = false)
        {
            NotificationTextBlock.Text = message;
            NotificationTextBlock.Foreground = isError ? Avalonia.Media.Brushes.Red : Avalonia.Media.Brushes.Green;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, args) =>
            {
                NotificationTextBlock.Text = string.Empty;
                timer.Stop();
            };
            timer.Start();
        }

        // Event handler to ensure that when one Expander is opened, all others collapse.
        private void Expander_Expanded(object? sender, RoutedEventArgs e)
        {
            if (sender is Expander expandedExpander)
            {
                // Loop through all items by their index.
                for (int i = 0; i < RecordsListBox.Items.Count; i++)
                {
                    var container = RecordsListBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                    if (container != null)
                    {
                        // Locate the Expander in this container using the visual tree.
                        var expander = container.GetVisualDescendants().OfType<Expander>().FirstOrDefault();
                        if (expander != null && expander != expandedExpander)
                        {
                            expander.IsExpanded = false;
                        }
                    }
                }
            }
        }
    }
}
