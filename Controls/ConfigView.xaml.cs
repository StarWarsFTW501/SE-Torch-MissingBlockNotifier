using NLog.LayoutRenderers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VRage.ObjectBuilders.Voxels;

namespace TorchPlugin
{
    // ReSharper disable once UnusedType.Global
    // ReSharper disable once RedundantExtendsListEntry
    public partial class ConfigView : UserControl
    {
        public ICommand ApplyChangesCommand { get; }
        public ICommand ManageGroupsCommand { get; }
        public ConfigView()
        {
            InitializeComponent();
            DataContext = Plugin.Instance.Config;
            ApplyChangesCommand = new MyRelayCommand(_ => ApplyFired(), _ => Plugin.Instance.Config.HasChanges);
            ManageGroupsCommand = new MyRelayCommand(_ => ManageFired());
        }

        void ApplyFired()
        {
            var applicator = Plugin.Instance.GetConfigApplicator(out bool needsRecompute, out bool typeProblems);
            if (needsRecompute)
            {
                var result = MessageBox.Show(
                    "Applying these changes will require a full recompute of all tracking data. This may take a while depending on the size of your world. Do you want to proceed?",
                    "Apply changes?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
            }
            if (typeProblems)
            {
                var result = MessageBox.Show(
                    "One or more block definitions specified in the configuration could not be parsed. Check the log for specifics. Are you sure you wish to continue?",
                    "Block parsing issues!",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
            }
            applicator();
        }

        void ManageFired() => Dialog.CreateModalBlocking(Plugin.Instance.GetGroupManagerControl(), Window.GetWindow(this));
    }
}