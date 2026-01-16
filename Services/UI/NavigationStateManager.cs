using System;
using YiboFile.Services.Tabs;

namespace YiboFile.Services
{
    /// <summary>
    /// 导航状态管理器
    /// 负责管理路径/库的导航状态，提供状态保护和恢复功能
    /// </summary>
    public class NavigationStateManager
    {
        public enum NavigationMode
        {
            Path,
            Library
        }

        /// <summary>
        /// 导航状态
        /// </summary>
        public class NavigationState
        {
            public NavigationMode Mode { get; set; }
            public string CurrentPath { get; set; }
            public Library CurrentLibrary { get; set; }

            public void Clear()
            {
                CurrentPath = null;
                CurrentLibrary = null;
            }

            public void SetFromTab(PathTab tab)
            {
                Clear();
                switch (tab.Type)
                {
                    case TabType.Path:
                        Mode = NavigationMode.Path;
                        CurrentPath = tab.Path;
                        break;
                    case TabType.Library:
                        Mode = NavigationMode.Library;
                        CurrentLibrary = tab.Library;
                        break;
                }
            }

            public NavigationState Clone()
            {
                return new NavigationState
                {
                    Mode = this.Mode,
                    CurrentPath = this.CurrentPath,
                    CurrentLibrary = this.CurrentLibrary
                };
            }
        }

        private NavigationState _currentState = new NavigationState();

        /// <summary>
        /// 当前导航状态
        /// </summary>
        public NavigationState CurrentState => _currentState;

        /// <summary>
        /// 从标签页更新状态
        /// </summary>
        public void UpdateFromTab(PathTab tab)
        {
            if (tab == null)
            {
                _currentState.Clear();
                return;
            }
            _currentState.SetFromTab(tab);
        }

        /// <summary>
        /// 保存当前状态（用于模式切换时保护状态）
        /// </summary>
        public NavigationState SaveState()
        {
            return _currentState.Clone();
        }

        /// <summary>
        /// 恢复状态
        /// </summary>
        public void RestoreState(NavigationState state)
        {
            if (state != null)
            {
                _currentState = state.Clone();
            }
        }

        /// <summary>
        /// 根据模式保存对应状态
        /// </summary>
        public NavigationState SaveStateForMode(NavigationMode mode)
        {
            var saved = new NavigationState();
            switch (mode)
            {
                case NavigationMode.Path:
                    saved.Mode = NavigationMode.Path;
                    saved.CurrentPath = _currentState.CurrentPath;
                    break;
                case NavigationMode.Library:
                    saved.Mode = NavigationMode.Library;
                    saved.CurrentLibrary = _currentState.CurrentLibrary;
                    break;
            }
            return saved;
        }

        /// <summary>
        /// 清除状态
        /// </summary>
        public void Clear()
        {
            _currentState.Clear();
        }
    }
}


