using System.Windows;
using System.Windows.Controls;
using Wysicraft.Models;
namespace Wysicraft.Designer;
public partial class MainWindow
{
    void BuildAnchorFields(Element e) {
        void Choice(string label,string[] values,string value,Action<string> set) {
            Properties.Children.Add(new TextBlock {Text=label,Margin=new Thickness(4)});
            var pick=new ComboBox {ItemsSource=values,SelectedItem=value};
            pick.SelectionChanged+=(_,_)=>{if(pick.SelectedItem is string selected){Change();set(selected);Draw();}};
            Properties.Children.Add(pick);
        }
        Choice("Horizontal anchor",["left","center","right","stretch"],e.HorizontalAnchor,v=>e.HorizontalAnchor=v);
        Choice("Vertical anchor",["top","center","bottom","stretch"],e.VerticalAnchor,v=>e.VerticalAnchor=v);
        Field(Properties,"Minimum width",e,"MinWidth");Field(Properties,"Minimum height",e,"MinHeight");
        Properties.Children.Add(new TextBlock {Text="Anchors follow the parent panel, or the screen for top-level controls. Stretch keeps both edge margins. Resize the parent here to see them work (Ctrl-drag resizes it alone). In Minecraft they apply when the screen's Responsive layout is on.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)});
    }
}
