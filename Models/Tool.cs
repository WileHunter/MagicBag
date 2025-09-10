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
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        private int _cmd;
        public int Cmd
        {
            get => _cmd;
            set
            {
                _cmd = value;
                OnPropertyChanged(nameof(Cmd));
            }
        }

        private string _toolPath = string.Empty;
        [JsonProperty("ToolPath")]
        public string ToolPath
        {
            get => _toolPath;
            set
            {
                _toolPath = value;
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
                _run = value;
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
                _commond = value;
                OnPropertyChanged(nameof(Commond));
            }
        }

        [JsonIgnore]
        public Category? ParentCategory { get; set; }
    }
}