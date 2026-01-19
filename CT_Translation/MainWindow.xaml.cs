using CT_Translation.ViewModels;
using System.Windows;

namespace CT_Translation;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var filePath = files[0];
                if (System.IO.Path.GetExtension(filePath).ToLower() == ".ct")
                {
                    if (DataContext is MainViewModel vm)
                    {
                        vm.LoadFile(filePath);
                    }
                }
            }
        }
    }
}
