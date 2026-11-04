using System.Data;
using System.Windows;
using System.Windows.Controls;
namespace wpf;
public sealed class OfficialCatalogWindow:Window
{
    public OfficialCatalogWindow(PublicSheet sheet)
    {
        Title=sheet.Title;Width=1180;Height=700;MinWidth=640;MinHeight=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new Thickness(18)};Content=root;
        var head=new StackPanel();DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
        head.Children.Add(new TextBlock{Text=sheet.Title,FontSize=24,FontWeight=FontWeights.Bold});
        head.Children.Add(new TextBlock{Text="공식 자료 "+OfficialCatalog.Current.Version+" · 주찬·부찬은 100g당 단백질 11g 이상 주찬, 7g 이상 중단백, 3g 이상 저단백, 3g 미만 채소 메뉴로 자동 분류합니다. 별도 승인은 필요하지 않습니다.",Margin=new Thickness(0,8,0,4),TextWrapping=TextWrapping.Wrap});
        head.Children.Add(new TextBlock{Text="김치·밥·국·후식은 기존 분류를 유지합니다. 제공량·단백질이 없거나 불명확하면 자료 확인 필요로 표시합니다. 원본 값은 보존하며, 수정은 메뉴·레시피 관리에서 내 자료로 저장하세요.",Margin=new Thickness(0,0,0,8),TextWrapping=TextWrapping.Wrap});
        var search=new TextBox{Margin=new Thickness(0,0,0,12),ToolTip="메뉴명·식재료명 검색"};head.Children.Add(search);
        var table=new DataTable();var data=OfficialCatalog.Current;
        if(sheet.Id is 1 or 2){
            foreach(var title in new[]{"메뉴ID","메뉴명","분류","단백질 등급","1인분 제공량(g)","100g당 단백질(g)","분류 상태","열량(kcal)","탄수화물(g)","단백질(g)","지방(g)","알레르기","제공량 근거","아몬드","참깨","콩류(기타)","출처·확인사항"})table.Columns.Add(title);
            foreach(var entry in data.Menus.Where(x=>x.Track==(sheet.Id==1?"A":"B"))){
                var m=MenuClassification.Official(entry.Menu);
                string allergens=MenuIngredientReview.EffectiveAllergens(m);
                if(string.IsNullOrWhiteSpace(m.Allergens))allergens+=(allergens.Length>0?" · ":"")+"기본 코드 미표기 · 확인 필요";
                table.Rows.Add(m.Id,m.Name,m.Category,m.ProteinGrade,m.ServingGrams?.ToString("0.##")??"미확인",m.ProteinPer100g?.ToString("0.##")??"미확인",MenuClassification.Status(m),m.Calories?.ToString()??"미확인",m.Carb?.ToString()??"미확인",m.Protein?.ToString()??"미확인",m.Fat?.ToString()??"미확인",allergens,m.ServingBasis,m.Almond,m.Sesame,m.OtherBeans,m.Source+" · "+m.ProteinReview+" · "+m.AllergyReview);
            }
        }else if(sheet.Id is 3 or 4){
            foreach(var title in new[]{"메뉴명","메뉴키","분류","식재료","1인분량","단위","출처·확인사항"})table.Columns.Add(title);
            var categories=data.Menus.ToDictionary(m=>m.Track+":"+m.Menu.Key,m=>MenuClassification.Official(m.Menu).Category);
            foreach(var r in data.Recipes.Where(x=>x.Track==(sheet.Id==3?"A":"B")))
                foreach(var i in r.Ingredients)table.Rows.Add(r.Name,r.Key,categories.GetValueOrDefault(r.Track+":"+r.Key,r.Category),i.Name,i.Amount,i.Unit,r.Source);
        }else{
            table.Columns.Add("메뉴명");table.Columns.Add("조리방법");foreach(var item in data.Cooking)table.Rows.Add(item.Key,item.Value);
        }
        var grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,CanUserDeleteRows=false,MinColumnWidth=70,EnableRowVirtualization=true,EnableColumnVirtualization=true,ItemsSource=table.DefaultView};root.Children.Add(grid);
        search.TextChanged+=(_,_)=>{string q=search.Text.Replace("'","''").Replace("[","[[]").Replace("%","[%]").Replace("*","[*]");table.DefaultView.RowFilter=string.Join(" OR ",table.Columns.Cast<DataColumn>().Where(c=>c.ColumnName is "메뉴명" or "식재료").Select(c=>"["+c.ColumnName+"] LIKE '%"+q+"%'"));};
    }
}
