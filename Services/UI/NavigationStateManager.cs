using System;
using OoiMRR.Controls;
using TagType = OoiMRR.Tag;

namespace OoiMRR.Services
{
    /// <summary>
    /// 导航状态管理器
    /// 负责管理路径/库/标签的导航状态，提供状态保护和恢复功能
    /// </summary>
    public class NavigationStateManager
    {
        public enum NavigationMode
        {
            Path,
            Library,
            Tag
        }

        /// <summary>
        /// 导航状态
        /// </summary>
        public class NavigationState
        {
            public NavigationMode Mode { get; set; }
            public string CurrentPath { get; set; }
            public Library CurrentLibrary { get; set; }
            public TagType CurrentTagFilter { get; set; }

            public void Clear()
            {
                CurrentPath = null;
                CurrentLibrary = null;
                CurrentTagFilter = null;
            }

            public void SetFromTab(TabManagerControl.TabInfo tab)
            {
                Clear();
                switch (tab.Type)
                {
                    case TabManagerControl.TabType.Path:
                        Mode = NavigationMode.Path;
                        CurrentPath = tab.Identifier;
                        break;
                    case TabManagerControl.TabType.Library:
                        Mode = NavigationMode.Library;
                        CurrentLibrary = tab.Data as Library;
                        break;
                    case TabManagerControl.TabType.Tag:
                        Mode = NavigationMode.Tag;
                        if (tab.Data is TagType tag)
                        {
                            CurrentTagFilter = tag;
                        }
                        else if (!string.IsNullOrEmpty(tab.Identifier) && int.TryParse(tab.Identifier, out int tagId))
                        {
                            // 如果没有Tag对象，尝试从ID创建
                            CurrentTagFilter = new TagType { Id = tagId, Name = tab.Title };
                        }
                        break;
                }
            }

            public NavigationState Clone()
            {
                return new NavigationState
                {
                    Mode = this.Mode,
                    CurrentPath = this.CurrentPath,
                    CurrentLibrary = this.CurrentLibrary,
                    CurrentTagFilter = this.CurrentTagFilter != null ? new TagType
                    {
                        Id = this.CurrentTagFilter.Id,
                        Name = this.CurrentTagFilter.Name,
                        Color = this.CurrentTagFilter.Color
                    } : null
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
        public void UpdateFromTab(TabManagerControl.TabInfo tab)
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
                case NavigationMode.Tag:
                    saved.Mode = NavigationMode.Tag;
                    saved.CurrentTagFilter = _currentState.CurrentTagFilter != null ? new TagType
                    {
                        Id = _currentState.CurrentTagFilter.Id,
                        Name = _currentState.CurrentTagFilter.Name,
                        Color = _currentState.CurrentTagFilter.Color
                    } : null;
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

