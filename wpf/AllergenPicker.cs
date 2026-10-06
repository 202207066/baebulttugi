using System.Windows;
using System.Windows.Controls;
namespace wpf;
public sealed class AllergenPicker : WrapPanel
{
 public static readonly string[] Names = ["난류","우유","메밀","땅콩","대두","밀","고등어","게","새우","돼지고기","복숭아","토마토","아황산류","호두","닭고기","쇠고기","오징어","조개류","잣"];
 public AllergenPicker() { for(int i=0;i<Names.Length;i++) Children.Add(new CheckBox { Content=$"{i+1}. {Names[i]}",Tag=(i+1).ToString(),MinWidth=120,Margin=new Thickness(0,6,12,6) }); }
 public string Text {
 get => string.Join(",",Children.OfType<CheckBox>().Where(c=>c.IsChecked==true).Select(c=>c.Tag));
 set {
 foreach(var c in Children.OfType<CheckBox>().Where(c=>!int.TryParse(c.Tag?.ToString(),out int n)||n<1||n>19).ToList()) Children.Remove(c);
 string expanded=value??""; for(int i=0;i<19;i++) expanded=expanded.Replace(((char)('①'+i)).ToString(),$"{i+1},");
 var tokens=System.Text.RegularExpressions.Regex.Split(expanded,@"[,;/\s]+").Where(t=>t.Length>0).ToHashSet();
 for(int i=0;i<19;i++) { bool code=tokens.Remove((i+1).ToString());bool name=tokens.Remove(Names[i]); if(i==0) name=tokens.Remove("달걀")|tokens.Remove("계란")|name; if(i==15)name=tokens.Remove("소고기")|name; ((CheckBox)Children[i]).IsChecked=code||name; }
 foreach(string token in tokens) Children.Add(new CheckBox{Content="기타: "+token,Tag=token,IsChecked=true,Margin=new Thickness(0,6,12,6)});
 }}
 public void Clear()=>Text="";
}
