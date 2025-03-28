using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace FModel.Views.Resources.Controls
{
    public static class AccessibilityHelper
    {
        public static void SetAccessibleName(UIElement element, string name)
        {
            AutomationProperties.SetName(element, name);
        }

        public static void SetAccessibleDescription(UIElement element, string description)
        {
            AutomationProperties.SetHelpText(element, description);
        }

        public static void MakeElementAccessible(UIElement element, string name, string description = null)
        {
            AutomationProperties.SetName(element, name);
            if (!string.IsNullOrEmpty(description))
                AutomationProperties.SetHelpText(element, description);

            // Ensure the element can be accessed by keyboard navigation
            KeyboardNavigation.SetIsTabStop(element, true);
        }

        public static void EnhanceListBoxItem(ListBoxItem item, string name, string description = null)
        {
            // No need to set IsControlElement as ListBoxItems are already control elements by default
            AutomationProperties.SetName(item, name);
            if (!string.IsNullOrEmpty(description))
                AutomationProperties.SetHelpText(item, description);
        }

        public static void EnhanceTreeViewItem(TreeViewItem item, string name, string description = null)
        {
            // No need to set IsControlElement as TreeViewItems are already control elements by default
            AutomationProperties.SetName(item, name);
            if (!string.IsNullOrEmpty(description))
                AutomationProperties.SetHelpText(item, description);
        }

        public static void AddFocusExitKeyboardSupport(UIElement element, UIElement focusTarget, Key exitKey = Key.F6)
        {
            element.KeyDown += (s, e) => {
                if (e.Key == exitKey)
                {
                    e.Handled = true;
                    focusTarget.Focus();
                }
            };

            // Add automation properties to explain the exit key
            string currentHelp = AutomationProperties.GetHelpText(element) ?? "";
            if (!currentHelp.Contains($"Press {exitKey}"))
            {
                AutomationProperties.SetHelpText(element,
                    currentHelp + (string.IsNullOrEmpty(currentHelp) ? "" : " ") +
                    $"Press {exitKey} to exit this area.");
            }
        }

        public static void MakeEditorAccessible(FrameworkElement editor)
        {
            // Set properties specific to rich text/code editors
            KeyboardNavigation.SetTabNavigation(editor, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetControlTabNavigation(editor, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetIsTabStop(editor, true);

            // Ensure screen reader can announce state
            AutomationProperties.SetIsColumnHeader(editor, false);
            AutomationProperties.SetItemStatus(editor, "Press F6 to exit editor");
        }

        public static void EnhanceTabControl(TabControl tabControl, UIElement focusTarget)
        {
            // Make sure tab navigation works properly
            KeyboardNavigation.SetTabNavigation(tabControl, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(tabControl, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetIsTabStop(tabControl, true);

            // Add help text for screen readers
            string currentHelp = AutomationProperties.GetHelpText(tabControl) ?? "";
            if (!currentHelp.Contains("Press F6"))
            {
                AutomationProperties.SetHelpText(tabControl,
                    currentHelp + (string.IsNullOrEmpty(currentHelp) ? "" : " ") +
                    "Press F6 to exit this area. Use Ctrl+Tab and Ctrl+Shift+Tab to navigate between tabs.");
            }

            // Add key handler
            AddFocusExitKeyboardSupport(tabControl, focusTarget);
        }

        public static void EnhanceComboBox(ComboBox comboBox, string name, string description = null)
        {
            MakeElementAccessible(comboBox, name, description);

            // Ensure it's accessible to keyboard navigation
            comboBox.IsTabStop = true;
            KeyboardNavigation.SetIsTabStop(comboBox, true);

            // Make sure popup is accessible
            AutomationProperties.SetIsDialog(comboBox, false);

            // Ensure proper keyboard access
            comboBox.KeyDown += (s, e) => {
                if (e.Key == Key.Down || e.Key == Key.Up)
                {
                    // Let users navigate dropdown with arrow keys even when not opened
                    if (!comboBox.IsDropDownOpen)
                    {
                        comboBox.IsDropDownOpen = true;
                        e.Handled = true;
                    }
                }
            };
        }

        public static void EnhanceButton(Button button, string name, string description = null)
        {
            MakeElementAccessible(button, name, description);

            // Ensure it's accessible to keyboard navigation
            button.IsTabStop = true;
            KeyboardNavigation.SetIsTabStop(button, true);
        }
    }
}
