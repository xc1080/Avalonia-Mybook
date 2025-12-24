using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MyBook.ViewModels;

namespace MyBook.Views;

public partial class SaveLoadWindow : Window
{
    public SaveLoadWindow()
    {
        InitializeComponent();
        this.Opened += SaveLoadWindow_Opened;
    }

    private async void SaveLoadWindow_Opened(object? sender, EventArgs e)
    {
        if (DataContext is StoryViewModelRuntime vm)
        {
            await vm.RefreshSaveSlotsAsync();
            SlotsList.ItemsSource = vm.SaveSlots;
        }
    }

    private async void OnSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StoryViewModelRuntime vm)
        {
            var name = NameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"slot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            }
            // create a slot id based on timestamp
            var slot = $"slot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            await vm.SaveToSlotAsync(slot, name);
            await vm.RefreshSaveSlotsAsync();
            SlotsList.ItemsSource = vm.SaveSlots;
        }
    }

    private async void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StoryViewModelRuntime vm)
        {
            await vm.RefreshSaveSlotsAsync();
            SlotsList.ItemsSource = vm.SaveSlots;
        }
    }

    private async void OnLoadClicked(object? sender, RoutedEventArgs e)
    {
        if (SlotsList.SelectedItem is MyBook.Models.SaveEntryMetadata meta && DataContext is StoryViewModelRuntime vm)
        {
            await vm.LoadSlotAsync(meta.Slot);
            Close();
        }
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (SlotsList.SelectedItem is MyBook.Models.SaveEntryMetadata meta && DataContext is StoryViewModelRuntime vm)
        {
            var dlg = new Window
            {
                Title = "确定删除",
                Width = 380,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = Avalonia.Media.Brushes.White,
                Content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Children =
                    {
                        new Avalonia.Controls.TextBlock { Text = $"确认删除存档 '{meta.Name}' 吗？", Margin = new Avalonia.Thickness(0,0,0,15), Foreground = Avalonia.Media.Brushes.Black, FontSize = 14 },
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Spacing = 15,
                            Children =
                            {
                                new Avalonia.Controls.Button { Content = "删除", Width=100, Height=36, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F44336")), Foreground = Avalonia.Media.Brushes.White },
                                new Avalonia.Controls.Button { Content = "取消", Width=100, Height=36, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#757575")), Foreground = Avalonia.Media.Brushes.White }
                            }
                        }
                    }
                }
            };
            var panel = (Avalonia.Controls.StackPanel)dlg.Content!;
            var buttonsPanel = (Avalonia.Controls.StackPanel)panel.Children[1];
            var deleteBtn = (Avalonia.Controls.Button)buttonsPanel.Children[0];
            var cancelBtn = (Avalonia.Controls.Button)buttonsPanel.Children[1];
            deleteBtn.Click += async (s, ev) =>
            {
                await vm.DeleteSaveSlotAsync(meta.Slot);
                dlg.Close();
                await vm.RefreshSaveSlotsAsync();
                SlotsList.ItemsSource = vm.SaveSlots;
            };
            cancelBtn.Click += (s, ev) => dlg.Close();
            await dlg.ShowDialog(this);
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
