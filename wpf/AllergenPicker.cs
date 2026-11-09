using System.Windows;
using System.Windows.Controls;
namespace wpf;
public sealed class AllergenPicker:WrapPanel
{
 public static readonly string[] Names=["난류","우유","메밀","땅콩","대두","밀","고등어","게","새우","돼지고기","복숭아","토마토","아황산류","호두","닭고기","쇠고기","오징어","조개류","잣"];
 public static readonly string[] ExtraNames=["아몬드","참깨","콩류(기타)"];
 public AllergenPicker()=>ResetOptions();
 private void Add(string label,string token,bool selected=false)=>Children.Add(new CheckBox{Content=label,Tag=token,IsChecked=selected,MinWidth=120,Margin=new Thickness(0,6,12,6)});
 private void ResetOptions(){Children.Clear();for(int i=0;i<Names.Length;i++)Add($"{i+1}. {Names[i]}",(i+1).ToString());foreach(var name in ExtraNames)Add("기타 · "+name,name);}
 public string Text {
 get=>string.Join(",",Children.OfType<CheckBox>().Where(c=>c.IsChecked==true).Select(c=>c.Tag));
 set{
  ResetOptions();var merged=PatientAllergyInfo.Merge(new[]{value??""});
  var tokens=GoogleSheetsService.SplitAllergens(merged.Value).Where(t=>!PatientAllergyInfo.IsNone(t)).Select(PatientAllergyInfo.Key).ToHashSet();
  foreach(var box in Children.OfType<CheckBox>())box.IsChecked=tokens.Remove(box.Tag.ToString()!);
  foreach(var token in tokens)Add("기타: "+token,token,true);
 }}
 public void Clear()=>Text="";
}
