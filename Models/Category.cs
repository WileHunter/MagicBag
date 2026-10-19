using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp3.Models
{
    public class Category : INotifyPropertyChanged
    {
        private string? _name;
        private List<Category>? _subCategories;
        private List<Tool>? _tools;
        private Category? _parentCategory;
        private bool _isExpanded;

        [JsonProperty("Name")]
        public string? Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonProperty("SubCategories")]
        public List<Category>? SubCategories
        {
            get => _subCategories;
            set
            {
                if (_subCategories != value)
                {
                    _subCategories = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonProperty("Tools")]
        public List<Tool>? Tools
        {
            get => _tools;
            set
            {
                if (_tools != value)
                {
                    _tools = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public Category? ParentCategory
        {
            get => _parentCategory;
            set
            {
                if (_parentCategory != value)
                {
                    _parentCategory = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 绑定 TreeViewItem 的展开/折叠状态
        /// </summary>
        [JsonIgnore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isSelected;

        /// <summary>
        /// 绑定 TreeViewItem 的选中状态（刷新树后仍能保持选中）
        /// </summary>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string Summary
        {
            get
            {
                int tools = Tools?.Count ?? 0;
                int subs = SubCategories?.Count ?? 0;
                return subs > 0 ? $"{tools} 个工具 · {subs} 个子目录" : $"{tools} 个工具";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}