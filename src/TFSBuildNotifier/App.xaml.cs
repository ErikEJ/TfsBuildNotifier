﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using TFSBuildNotifier.TfsBuildStatusProvider;

namespace TFSBuildNotifier
{
    public partial class App
    {
        private bool _isExit;
        private TaskbarIcon _taskbarIcon;
        private ContextMenu _contextMenu = new ContextMenu();
        private readonly DispatcherTimer _dispatcherTimer = new DispatcherTimer();

        private List<BuildStatus> _buildStatuses;
        private List<Uri> _uriList = new List<Uri>();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow = new MainWindow();
            MainWindow.Closing += MainWindow_Closing;
            BuildTaskBarIcon();
            IBuildStatusProvider statusProvider;
            //TODO Use prod provider
            statusProvider = new DebugStatusProvider();
            if (e.Args.Length == 0)
            {
                MessageBox.Show("Invalid command line", "TFS Build Notifier", MessageBoxButton.OK);
            }
            try
            {
                foreach (var arg in e.Args)
                {
                    _uriList.Add(new Uri(arg));
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Invalid command line", "TFS Build Notifier", MessageBoxButton.OK);
            }

            AddMenuItems(statusProvider, _uriList);
            StartTimer();
        }

        private void StartTimer()
        {
            _dispatcherTimer.Tick += _dispatcherTimer_Tick;
            _dispatcherTimer.Interval = new TimeSpan(0, 1, 0);
            _dispatcherTimer.Start();
        }

        private void _dispatcherTimer_Tick(object sender, EventArgs e)
        {
            _dispatcherTimer.Stop();

            //TODO Use prod provider
            var statusProvider = new DebugStatusProvider();
            var latestStatuses = statusProvider.GetStatusList(_uriList);

            foreach (var newStatus in latestStatuses)
            {
                var oldStatus = _buildStatuses.Single(b => b.Key == newStatus.Key);
                if (newStatus.BuildId != oldStatus.BuildId)
                {
                    var title = newStatus.BuildName;
                    var text = newStatus.RequestedBy;
                    _taskbarIcon.ShowBalloonTip(title, text, ResourceHelper.GetIcon(newStatus));
                }

                //Update the Menu Item icon if status has changed
                if (oldStatus.Status != newStatus.Status)
                {
                    foreach (var contextMenuItem in _contextMenu.Items)
                    {
                        var menuItem = contextMenuItem as MenuItem;
                        if (menuItem != null)
                        {
                            var tag = menuItem.Tag as Uri;
                            if (tag != null && tag == oldStatus.Key)
                            {
                                menuItem.Icon = ResourceHelper.GetIcon(newStatus);
                            }
                        }
                    }
                }
                oldStatus.Status = newStatus.Status;
                oldStatus.BuildId = newStatus.BuildId;
                oldStatus.RequestedBy = newStatus.RequestedBy;
            }
            _dispatcherTimer.Start();
        }

        private void AddMenuItems(IBuildStatusProvider statusProvider, List<Uri> uriList)
        {
            _buildStatuses = statusProvider.GetStatusList(uriList);
            foreach (var buildStatus in _buildStatuses)
            {
                var imageName = ResourceHelper.GetImageName(buildStatus);
                var item = new MenuItem
                {
                    Icon = new Image
                    {
                        Source = new BitmapImage(new Uri(imageName, UriKind.Relative))
                    },
                    Header = string.Format("{0} - {1}", buildStatus.BuildName, buildStatus.RequestedBy),
                    Tag = buildStatus.Key
                };
                item.Click += ItemOnClick;
                _contextMenu.Items.Insert(0, item);
            }
        }


        private void ItemOnClick(object sender, RoutedEventArgs e)
        {
            //TODO Find correct link from buildstatuses!
            Process.Start("http://www.google.dk");
        }

        private void BuildTaskBarIcon()
        {
            _taskbarIcon = new TaskbarIcon
            {
                Icon = Resource.BuildSolution,
                ToolTipText = "TFS Build Notifier",
                MenuActivation = PopupActivationMode.LeftOrRightClick
            };

            var exitItem = new MenuItem {Header = "Exit"};
            exitItem.Click += ExitItem_Click;
            _contextMenu.Items.Add(exitItem);
            _taskbarIcon.ContextMenu = _contextMenu;
        }

        private void ExitItem_Click(object sender, RoutedEventArgs e)
        {
            _isExit = true;
            Current.Shutdown();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isExit)
            {
                e.Cancel = true;
                MainWindow.Hide(); // A hidden window can be shown again, a closed one not
            }
        }
    }
}