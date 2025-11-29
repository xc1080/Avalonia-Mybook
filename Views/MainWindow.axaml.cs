using Avalonia.Controls;
using Avalonia.Interactivity;
using MyBook.ViewModels;
using MyBook.Models;
using System.Threading.Tasks;
using Avalonia.Data;
using System.Linq;

namespace MyBook.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnNextChapterClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not StoryViewModelRuntime vm) return;
        // Refresh chapters via VM helper to ensure latest
        await vm.RefreshAvailableChaptersAsync();

        var list = vm.AvailableChapters;
        if (list == null || list.Count == 0) return;

        var dialog = new Window
        {
            Title = "选择章节",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var listBox = new ListBox
        {
            ItemsSource = list,
            DisplayMemberBinding = new Binding("Title"),
            Margin = new Avalonia.Thickness(20, 20, 20, 10)
        };

        var okButton = new Button
        {
            Content = "开始章节",
            Width = 120,
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
            IsEnabled = false
        };

        listBox.SelectionChanged += (s, ev) => okButton.IsEnabled = listBox.SelectedItem != null;
        okButton.Click += (s, ev) => dialog.Close(listBox.SelectedItem);

        var panel = new StackPanel();
        panel.Children.Add(listBox);
        panel.Children.Add(okButton);
        dialog.Content = panel;

        var selected = await dialog.ShowDialog<object>(this);
        if (selected is Chapter chapter)
        {
            await vm.LoadChapterAsync(chapter.Id);
        }
    }
}