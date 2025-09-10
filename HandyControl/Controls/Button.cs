using System.Windows;

namespace HandyControl.Controls
{
    internal class Button : UIElement
    {
        public string Content { get; set; }
        public Thickness Margin { get; set; }
        public Action<object, object> Click { get; internal set; }
    }
}