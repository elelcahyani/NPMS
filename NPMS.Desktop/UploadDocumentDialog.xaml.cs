using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NPMS.Desktop
{
    public partial class UploadDocumentDialog : Window
    {
        public string DocumentName { get; private set; } = "";
        public string DocumentType { get; private set; } = "";
        public string Revision { get; private set; } = "";
        public string? ProcessName { get; private set; }

        public UploadDocumentDialog(string defaultName = "", IEnumerable<string>? processNames = null)
        {
            InitializeComponent();
            TxtDocName.Text = defaultName;
            foreach (var processName in (processNames ?? Enumerable.Empty<string>())
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                CmbProcess.Items.Add(new ComboBoxItem { Content = processName, Tag = processName });
            }
            CmbProcess.SelectedIndex = 0;
            TxtDocName.Focus();
            TxtDocName.SelectAll();
            TxtDocName.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) BtnConfirm_Click(s, e); };
            TxtRevision.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) BtnConfirm_Click(s, e); };
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtDocName.Text))
            {
                TxtError.Text = "Document name harus diisi.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            DocumentName = TxtDocName.Text.Trim();
            DocumentType = ((ComboBoxItem)CmbDocType.SelectedItem).Content.ToString()!;
            Revision = string.IsNullOrWhiteSpace(TxtRevision.Text) ? "Rev.01" : TxtRevision.Text.Trim();
            ProcessName = (CmbProcess.SelectedItem as ComboBoxItem)?.Tag as string;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
