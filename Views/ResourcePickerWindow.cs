using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyBook.Views
{
    public class ResourcePickerWindow : Window
    {
        public string? SelectedResource { get; private set; }
        public ResourcePickerWindow(string title, IEnumerable<string> resources)
        {
            Title = title;
            Width = 400;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            var listBox = new ListBox
            {
                ItemsSource = resources.ToList(),
                Margin = new Avalonia.Thickness(20, 20, 20, 10)
            };
            listBox.DoubleTapped += (s, e) =>
            {
                SelectedResource = listBox.SelectedItem as string;
                Close(SelectedResource);
            };

            var okButton = new Button
            {
                Content = "确定",
                Width = 100,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };
            okButton.Click += (s, e) =>
            {
                SelectedResource = listBox.SelectedItem as string;
                Close(SelectedResource);
            };

            var panel = new StackPanel();
            panel.Children.Add(listBox);
            panel.Children.Add(okButton);
            Content = panel;
        }
    }
}
