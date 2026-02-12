using System.Windows;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Input;
using Microsoft.Win32;

namespace MacroRecorder.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(new GlobalInputHookService(), new InputPlaybackService());
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void SaveMacro_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Macro Recorder (*.mcr1)|*.mcr1|All Files (*.*)|*.*",
            DefaultExt = ".mcr1",
            FileName = "macro.mcr1"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SaveToFile(dialog.FileName);
        }
    }

    private void LoadMacro_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Macro Recorder (*.mcr1)|*.mcr1|All Files (*.*)|*.*",
            DefaultExt = ".mcr1"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.LoadFromFile(dialog.FileName);
        }
    }
}
