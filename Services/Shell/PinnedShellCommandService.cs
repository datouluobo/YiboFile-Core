using System;
using System.Collections.Generic;
using System.Linq;
using YiboFile.Services.Config;

namespace YiboFile.Services.Shell
{
    public interface IPinnedShellCommandService
    {
        bool IsPinned(string verb, string text);
        bool IsHidden(string verb, string text);
        void Pin(string verb, string text);
        void Unpin(string verb, string text);
        void Hide(string verb, string text);
        void Unhide(string verb, string text);
        IEnumerable<string> GetPinnedCommands();
        IEnumerable<string> GetHiddenCommands();
    }

    public class PinnedShellCommandService : IPinnedShellCommandService
    {
        private readonly ConfigurationService _configService;
        private readonly HashSet<string> _pinned;
        private readonly HashSet<string> _hidden;

        public PinnedShellCommandService(ConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            
            var config = _configService.Config;
            _pinned = new HashSet<string>(config.PinnedShellVerbs ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            _hidden = new HashSet<string>(config.HiddenShellVerbs ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public bool IsPinned(string verb, string text)
        {
            if (string.IsNullOrEmpty(verb)) return _pinned.Contains(text);
            return _pinned.Contains(verb);
        }

        public bool IsHidden(string verb, string text)
        {
            if (string.IsNullOrEmpty(verb)) return _hidden.Contains(text);
            return _hidden.Contains(verb);
        }

        public void Pin(string verb, string text)
        {
            string id = !string.IsNullOrEmpty(verb) ? verb : text;
            if (string.IsNullOrEmpty(id)) return;

            if (_pinned.Add(id))
            {
                _hidden.Remove(id);
                Save();
            }
        }

        public void Unpin(string verb, string text)
        {
            string id = !string.IsNullOrEmpty(verb) ? verb : text;
            if (string.IsNullOrEmpty(id)) return;

            if (_pinned.Remove(id))
            {
                Save();
            }
        }

        public void Hide(string verb, string text)
        {
            string id = !string.IsNullOrEmpty(verb) ? verb : text;
            if (string.IsNullOrEmpty(id)) return;

            if (_hidden.Add(id))
            {
                _pinned.Remove(id);
                Save();
            }
        }

        public void Unhide(string verb, string text)
        {
            string id = !string.IsNullOrEmpty(verb) ? verb : text;
            if (string.IsNullOrEmpty(id)) return;

            if (_hidden.Remove(id))
            {
                Save();
            }
        }

        public IEnumerable<string> GetPinnedCommands() => _pinned;
        public IEnumerable<string> GetHiddenCommands() => _hidden;

        private void Save()
        {
            _configService.Update(cfg => 
            {
                cfg.PinnedShellVerbs = _pinned.ToList();
                cfg.HiddenShellVerbs = _hidden.ToList();
            });
        }
    }
}
