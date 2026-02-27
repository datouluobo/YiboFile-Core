using System;
using System.Windows.Input;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 布局管理模块
    /// </summary>
    public class LayoutModule : ModuleBase
    {
        private string _currentLayoutMode = "Work"; // Default
        private bool _isDualListMode;
        private bool _isSecondPaneFocused;
        private bool _isLeftPanelCollapsed;
        private bool _isRightPanelCollapsed;

        /// <summary>
        /// 进入备份管理等强制布局模式的特殊标签页之前，保存的原始布局模式。
        /// 离开时用于恢复。null 表示当前没有被特殊标签页覆盖。
        /// </summary>
        private string _savedLayoutModeBeforeSpecialTab;
        /// <summary>
        /// 进入特殊标签页之前的双列表模式状态。
        /// </summary>
        private bool _savedDualListModeBeforeSpecialTab;

        public override string Name => "LayoutModule";


        /// <summary>
        /// 当前布局模式 (Focus, Work, Full)
        /// </summary>
        public string CurrentLayoutMode
        {
            get => _currentLayoutMode;
            private set
            {
                if (_currentLayoutMode != value)
                {
                    _currentLayoutMode = value;
                    Publish(new LayoutModeChangedMessage(_currentLayoutMode));

                    // 持久化状态
                    if (!_isTemporaryLayoutSwitch)
                    {
                        YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.LayoutMode, value);
                        YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
                    }
                }
            }
        }

        /// <summary>
        /// 是否为双列表模式
        /// </summary>
        public bool IsDualListMode
        {
            get => _isDualListMode;
            internal set
            {
                if (_isDualListMode != value)
                {
                    _isDualListMode = value;
                    Publish(new DualListModeChangedMessage(_isDualListMode));
                    OnPropertyChanged(nameof(IsDualListEffectivelyVisible));

                    // 如果开启双列表，确保右侧面板是不折叠的（让出空间给副列表）
                    if (_isDualListMode)
                    {
                        IsRightPanelCollapsed = false;
                    }
                    else
                    {
                        // 关闭双列表时，根据当前布局模式和预览可见性恢复右侧面板折叠状态
                        IsRightPanelCollapsed = ShouldRightPanelCollapse();
                    }

                    // 持久化状态
                    if (!_isTemporaryLayoutSwitch)
                    {
                        YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.IsDualListMode, value);
                        YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
                    }
                }
            }
        }

        /// <summary>
        /// 左面板是否已折叠
        /// </summary>
        public bool IsLeftPanelCollapsed
        {
            get => _isLeftPanelCollapsed;
            set
            {
                if (SetProperty(ref _isLeftPanelCollapsed, value))
                {
                    if (!_isTemporaryLayoutSwitch)
                    {
                        YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.IsSidebarCollapsed, value);
                        YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
                    }
                }
            }
        }

        /// <summary>
        /// 右面板是否已折叠
        /// </summary>
        public bool IsRightPanelCollapsed
        {
            get => _isRightPanelCollapsed;
            set
            {
                if (SetProperty(ref _isRightPanelCollapsed, value))
                {
                    // 当右侧面板折叠状态改变时，通知配置
                    // 注意：因为右面板可以对应预览区/属性区，我们同时更新相关的可见性标记
                    if (!_isTemporaryLayoutSwitch)
                    {
                        YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.IsRightPanelVisible, !value);
                        YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.IsPreviewCollapsed, value);
                        YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
                    }
                }
            }
        }

        /// <summary>
        /// 副列表实际可见性（考虑双列表开关）
        /// </summary>
        public bool IsDualListEffectivelyVisible => IsDualListMode;

        /// <summary>
        /// 是否为副面板获得焦点 (双列表模式)
        /// </summary>
        public bool IsSecondPaneFocused
        {
            get => _isSecondPaneFocused;
            internal set
            {
                if (_isSecondPaneFocused != value)
                {
                    _isSecondPaneFocused = value;
                    // 发布状态变更通知（不是请求）
                    Publish(new FocusedPaneChangedMessage(_isSecondPaneFocused));
                }
            }
        }

        public ICommand SwitchLayoutModeCommand { get; private set; }
        public ICommand ToggleDualListModeCommand { get; private set; }
        public ICommand SwitchFocusedPaneCommand { get; private set; }

        public LayoutModule(IMessageBus messageBus) : base(messageBus)
        {
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            SwitchLayoutModeCommand = new RelayCommand<string>(mode => SwitchLayoutMode(mode));
            ToggleDualListModeCommand = new RelayCommand(() => ToggleDualListMode());
            SwitchFocusedPaneCommand = new RelayCommand(() => SwitchFocusedPane());
        }

        /// <summary>
        /// 初始化状态（不发布消息）
        /// </summary>
        public void InitializeState(string layoutMode, bool isDualListMode, bool isSecondPaneFocused, bool isLeftCollapsed, bool isRightCollapsed)
        {
            CurrentLayoutMode = layoutMode;
            IsDualListMode = isDualListMode;
            IsSecondPaneFocused = isSecondPaneFocused;
            IsLeftPanelCollapsed = isLeftCollapsed;
            IsRightPanelCollapsed = isRightCollapsed;
        }

        protected override void OnInitialize()
        {
            // 订阅焦点切换请求（外部请求切换焦点时触发）
            Subscribe<SwitchFocusedPaneMessage>(m =>
            {
                if (IsDualListMode)
                {
                    // 直接修改内部字段并发布通知，避免递归
                    _isSecondPaneFocused = !_isSecondPaneFocused;
                    Publish(new FocusedPaneChangedMessage(_isSecondPaneFocused));
                }
            });

            Subscribe<SetFocusedPaneMessage>(m =>
            {
                // Only allow setting focus if dual list mode is active, OR if we want to allow setting primary (0) always?
                // Actually even in Single mode, Primary is focused.
                // If Single mode and request Secondary, ignore.
                if (!IsDualListMode && m.IsSecondPane) return;

                if (_isSecondPaneFocused != m.IsSecondPane)
                {
                    _isSecondPaneFocused = m.IsSecondPane;
                    Publish(new FocusedPaneChangedMessage(_isSecondPaneFocused));
                }
            });

            // 订阅布局变更请求
            Subscribe<RequestLayoutModeMessage>(m => SwitchLayoutMode(m.Mode));
            Subscribe<RequestDualListToggleMessage>(m => ToggleDualListMode());

        // 导航模式变更（现已无特殊处理）
            Subscribe<NavigationModeChangedMessage>(m =>
            {
            });

            // 当某个标签页被激活时触发（仅处理主面板）
            Subscribe<TabActiveChangedMessage>(m =>
            {
                if (m.ActiveTab == null || m.Pane != YiboFile.Services.Navigation.PaneId.Main) return;

                bool isSpecialLayoutTab = !string.IsNullOrEmpty(m.ActiveTab.ContentTypeId) && 
                                          !YiboFile.Services.Tabs.TabContentTypes.IsFileBrowserType(m.ActiveTab.ContentTypeId) &&
                                          m.ActiveTab.ContentTypeId != YiboFile.Services.Tabs.TabContentTypes.Settings;

                if (isSpecialLayoutTab)
                {
                    // 进入需要强制布局的特殊标签页：保存当前状态，然后切换
                    if (_savedLayoutModeBeforeSpecialTab == null)
                    {
                        _savedLayoutModeBeforeSpecialTab = _currentLayoutMode;
                        _savedDualListModeBeforeSpecialTab = _isDualListMode;
                        Services.Core.FileLogger.Log($"[LayoutModule] 进入系统页 → 保存布局 '{_currentLayoutMode}', 双栏={_isDualListMode}");
                    }
                    
                    _isTemporaryLayoutSwitch = true;
                    try
                    {
                        if (IsDualListMode)
                        {
                            ToggleDualListMode(false);
                        }
                        SwitchLayoutMode("Work", true);
                    }
                    finally
                    {
                        _isTemporaryLayoutSwitch = false;
                    }
                }
                else if (_savedLayoutModeBeforeSpecialTab != null)
                {
                    // 离开特殊标签页：恢复之前保存的布局模式和双列表状态
                    var savedMode = _savedLayoutModeBeforeSpecialTab;
                    var savedDualList = _savedDualListModeBeforeSpecialTab;
                    _savedLayoutModeBeforeSpecialTab = null;
                    _savedDualListModeBeforeSpecialTab = false;
                    Services.Core.FileLogger.Log($"[LayoutModule] 离开系统页 → 恢复布局 '{savedMode}', 双栏={savedDualList} (当前: '{_currentLayoutMode}')");
                    // 先恢复布局模式
                    ForceApplyLayoutMode(savedMode);
                    // 再恢复双列表模式（如果之前是开启的）
                    if (savedDualList && !_isDualListMode)
                    {
                        ToggleDualListMode(true);
                    }
                }
            });

            // 订阅高度和视图模式变更，用于持久化
            Subscribe<ViewModeChangedMessage>(m =>
            {
                // 目前配置是全局的，所以不区分 pane
                YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.FileViewMode, m.Mode);
                YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
            });

            Subscribe<NotesHeightChangedMessage>(m =>
            {
                YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.RightPanelNotesHeight, m.NewHeight);
                YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
            });


            Subscribe<SplitterDragCompletedMessage>(OnSplitterDragCompleted);
        }

        private void OnSplitterDragCompleted(SplitterDragCompletedMessage message)
        {
            if (message.SplitterName == "Left")
            {
                YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.LeftPanelWidth, message.NewValue);
            }
            else if (message.SplitterName == "Right")
            {
                YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.RightPanelWidth, message.NewValue);
            }
            YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
        }

        /// <summary>
        /// 判断右侧面板是否应该折叠。
        /// 统一决策逻辑：只有在双列表模式激活，或者 Full 模式且预览面板已启用时，右侧列才展开。
        /// </summary>
        private bool ShouldRightPanelCollapse()
        {
            // 双列表模式下，右侧列始终展开（显示副文件列表）
            if (_isDualListMode) return false;

            // Full 模式下，仅当预览面板启用时展开
            if (_currentLayoutMode == "Full")
            {
                var isPreviewVis = YiboFile.Services.Config.ConfigurationService.Instance.Config.IsRightPanelVisible;
                return !isPreviewVis;
            }

            // Focus/Work 模式下，右侧列始终折叠
            return true;
        }

        private bool _isTemporaryLayoutSwitch = false;

        /// <summary>
        /// 强制应用布局模式。
        /// </summary>
        private void ForceApplyLayoutMode(string mode)
        {
            // 强制更新字段
            _currentLayoutMode = mode;
            Publish(new LayoutModeChangedMessage(_currentLayoutMode));

            // 恢复原始布局时，应该持久化
            YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.LayoutMode, mode);
            YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();

            // 强制应用面板折叠状态 (恢复时不拦截保存侧栏)
            ApplyPanelCollapseForMode(mode);
        }

        /// <summary>
        /// 切换布局模式
        /// </summary>
        public void SwitchLayoutMode(string mode, bool isTemporary = false)
        {
            _isTemporaryLayoutSwitch = isTemporary;
            try
            {
                CurrentLayoutMode = mode;
                ApplyPanelCollapseForMode(mode);
            }
            finally
            {
                _isTemporaryLayoutSwitch = false;
            }
        }

        /// <summary>
        /// 根据布局模式应用面板折叠/展开状态。
        /// </summary>
        private void ApplyPanelCollapseForMode(string mode)
        {
            switch (mode)
            {
                case "Focus":
                    IsLeftPanelCollapsed = true;
                    IsRightPanelCollapsed = true;
                    break;
                case "Work":
                    IsLeftPanelCollapsed = false;
                    IsRightPanelCollapsed = true;
                    break;
                case "Full":
                    IsLeftPanelCollapsed = false;
                    IsRightPanelCollapsed = ShouldRightPanelCollapse();
                    break;
            }
        }

        /// <summary>
        /// 切换双列表模式
        /// </summary>
        public void ToggleDualListMode(bool? forcedValue = null)
        {
            IsDualListMode = forcedValue ?? !IsDualListMode;
        }

        /// <summary>
        /// 切换焦点面板 (从主列表到副列表，反之亦然)
        /// </summary>
        public void SwitchFocusedPane()
        {
            IsSecondPaneFocused = !IsSecondPaneFocused;
        }

        /// <summary>
        /// 设置焦点面板
        /// </summary>
        public void SetFocusedPane(bool isSecondPane)
        {
            IsSecondPaneFocused = isSecondPane;
        }
    }
}
