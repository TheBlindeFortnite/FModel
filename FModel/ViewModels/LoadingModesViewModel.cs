using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using FModel.Framework;
using FModel.ViewModels.Commands;

namespace FModel.ViewModels;

public class LoadingModesViewModel : ViewModel
{
    private LoadCommand _loadCommand;
    public LoadCommand LoadCommand => _loadCommand ??= new LoadCommand(this);
    public ReadOnlyObservableCollection<ELoadingMode> Modes { get; }

    public LoadingModesViewModel()
    {
        Modes = new ReadOnlyObservableCollection<ELoadingMode>(new ObservableCollection<ELoadingMode>(EnumerateLoadingModes()));

        // Register to initialized components to ensure accessibility
        Application.Current.Dispatcher.BeginInvoke(new Action(() => {
            EnhanceAccessibility();
        }));
    }

    private IEnumerable<ELoadingMode> EnumerateLoadingModes() => Enum.GetValues<ELoadingMode>();

    private void EnhanceAccessibility()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            // Find the ComboBox and Button in the visual tree
            var comboBox = FindComboBox(mainWindow);
            var loadButton = FindLoadButton(mainWindow);

            if (comboBox != null)
            {
                // Make sure it can be tabbed to
                comboBox.IsTabStop = true;
                comboBox.TabIndex = 1;
                AutomationProperties.SetName(comboBox, "Loading Mode Selector");
                AutomationProperties.SetHelpText(comboBox, "Choose how to load game archives");
                KeyboardNavigation.SetTabNavigation(comboBox, KeyboardNavigationMode.Local);
                KeyboardNavigation.SetIsTabStop(comboBox, true);
            }

            if (loadButton != null)
            {
                // Make sure it can be tabbed to
                loadButton.IsTabStop = true;
                loadButton.TabIndex = 2;
                AutomationProperties.SetName(loadButton, "Load Archives Button");
                AutomationProperties.SetHelpText(loadButton, "Load selected archives using the chosen loading mode");
                KeyboardNavigation.SetTabNavigation(loadButton, KeyboardNavigationMode.Local);
                KeyboardNavigation.SetIsTabStop(loadButton, true);
            }
        }
    }

    private ComboBox FindComboBox(DependencyObject parent)
    {
        // Find the ComboBox containing loading modes
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

            if (child is ComboBox comboBox && comboBox.ItemsSource == Modes)
                return comboBox;

            var result = FindComboBox(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private Button FindLoadButton(DependencyObject parent)
    {
        // Find the Load button
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

            if (child is Button button && button.Command == LoadCommand)
                return button;

            var result = FindLoadButton(child);
            if (result != null)
                return result;
        }

        return null;
    }
}
