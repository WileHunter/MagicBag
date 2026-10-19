using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace WpfApp3.Models
{
    public class Tool : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int _id;
        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value ?? string.Empty;
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                _description = value ?? string.Empty;
                OnPropertyChanged(nameof(Description));
            }
        }

        /// <summary>
        /// 0 = 命令行方式启动，1 = 直接启动 EXE
        /// </summary>
        private int _cmd;
        public int Cmd
        {
            get => _cmd;
            set
            {
                _cmd = value;
                OnPropertyChanged(nameof(Cmd));
                OnPropertyChanged(nameof(ModeText));
            }
        }

        private string _toolPath = string.Empty;
        [JsonProperty("ToolPath")]
        public string ToolPath
        {
            get => _toolPath;
            set
            {
                _toolPath = value ?? string.Empty;
                OnPropertyChanged(nameof(ToolPath));
            }
        }

        private string _run = string.Empty;
        [JsonProperty("RUN")]
        public string RUN
        {
            get => _run;
            set
            {
                _run = value ?? string.Empty;
                OnPropertyChanged(nameof(RUN));
            }
        }

        private string _commond = string.Empty;
        [JsonProperty("Commond")]
        public string Commond
        {
            get => _commond;
            set
            {
                _commond = value ?? string.Empty;
                OnPropertyChanged(nameof(Commond));
            }
        }

        [JsonIgnore]
        public string ModeText => Cmd == 1 ? "EXE" : "CMD";

        /// <summary>程序自带的图标；取不到时为 null，界面显示默认图标。</summary>
        [JsonIgnore]
        public System.Windows.Media.ImageSource? Icon => Services.ToolIconProvider.Get(this);

        internal void NotifyIconChanged() => OnPropertyChanged(nameof(Icon));

        [JsonIgnore]
        public Category? ParentCategory { get; set; }
    }

    public class LaunchModeOption
    {
        public int Value { get; init; }
        public string Name { get; init; } = "";

        public static List<LaunchModeOption> All { get; } = new()
        {
            new LaunchModeOption { Value = 0, Name = "命令行" },
            new LaunchModeOption { Value = 1, Name = "EXE" }
        };
    }
}
