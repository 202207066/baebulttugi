using System.Data;
using System.Windows;
using System.Windows.Controls;
namespace wpf;
public sealed class OfficialCatalogWindow:Window
{
    public OfficialCatalogWindow(PublicSheet sheet)
    {
        Title=sheet.Title;Width=1060;Height=700;MinWidth=640;MinHeight=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new Thickness(18)};Content=root;
        var head=new StackPanel();DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
        head.Children.Add(new TextBlock{Text=sheet.Title,FontSize=24,FontWeight=FontWeights.Bold});
        head.Children.Add(new TextBlock{Text="공식 자료 "+OfficialCatalog.Current.Version+" · 반찬은 100g당 단백질로 1차 분류하며, 검토 승인된 고단백 메뉴만 주찬 자동 생성에 사용합니다. · 수정은 메뉴·레시피 관리에서 내 자료로 저장하세요.",Margin=new Thickness(0,8,0,8),TextWrapping=TextWrapping.Wrap});
        var search=new TextBox{Margin=new Thickness(0,0,0,12),ToolTip="메뉴명·식재료명 검색"};head.Children.Add(search);
        var table=new DataTable();var data=OfficialCatalog.Current;
        if(sheet.Id is 1 or 2){
            foreach(var title in new[]{"메뉴ID","메뉴명","분류","열량(kcal)","탄수화물(g)","단백질(g)","지방(g)","알레르기","1인분 제공량(g)","100g당 단백질(g)","단백질 등급","검토 상태","제공량 근거","아몬드","참깨","콩류(기타)","출처·확인사항"})table.Columns.Add(title);
            foreach(var entry in data.Menus.Where(x=>x.Track==(sheet.Id==1?"A":"B"))){var m=MenuClassification.Official(entry.Menu);table.Rows.Add(m.Id,m.Name,m.Category,m.Calories?.ToString()??"미확인",m.Carb?.ToString()??"미확인",m.Protein?.ToString()??"미확인",m.Fat?.ToString()??"미확인",string.IsNullOrWhiteSpace(m.Allergens)?"미표기 · 확인 필요":MenuIngredientReview.EffectiveAllergens(m),m.ServingGrams?.ToString("0.##")??"미확인",m.ProteinPer100g?.ToString("0.##")??"미확인",m.ProteinGrade,m.ProteinReviewed?"검토 승인":"검토 대기",m.ServingBasis,m.Almond,m.Sesame,m.OtherBeans,m.Source+" · "+m.ProteinReview+" · "+m.AllergyReview);}
        }else if(sheet.Id is 3 or 4){
            foreach(var title in new[]{"메뉴명","메뉴키","분류","식재료","1인분량","단위","출처·확인사항"})table.Columns.Add(title);
            foreach(var r in data.Recipes.Where(x=>x.Track==(sheet.Id==3?"A":"B")))foreach(var i in r.Ingredients)table.Rows.Add(r.Name,r.Key,r.Category,i.Name,i.Amount,i.Unit,r.Source);
        }else{
            table.Columns.Add("메뉴명");table.Columns.Add("조리방법");foreach(var item in data.Cooking)table.Rows.Add(item.Key,item.Value);
        }
        var grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,CanUserDeleteRows=false,EnableRowVirtualization=true,EnableColumnVirtualization=true,ItemsSource=table.DefaultView};root.Children.Add(grid);
        search.TextChanged+=(_,_)=>{string q=search.Text.Replace("'","''").Replace("[","[[]").Replace("%","[%]").Replace("*","[*]");table.DefaultView.RowFilter=string.Join(" OR ",table.Columns.Cast<DataColumn>().Where(c=>c.ColumnName is "메뉴명" or "식재료").Select(c=>"["+c.ColumnName+"] LIKE '%"+q+"%'"));};
    }
}
