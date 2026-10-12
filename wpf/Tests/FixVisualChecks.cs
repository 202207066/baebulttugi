using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using wpf;

internal static class FixVisualChecks
{
    public static void Run(string folder)
    {
        System.IO.Directory.CreateDirectory(folder);
        var app=new App();app.InitializeComponent();AppServices.Sheets=new GoogleSheetsService(null!);
        void Render(FrameworkElement element,string name,int width,int height)=>FeedbackChecks.Render(element,System.IO.Path.Combine(folder,name+".png"),width,height);
        void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
        foreach(var size in new[]{new Size(1080,660),new Size(1600,960)})
        {
            var allergy=new AllergyManagementPage();Render(allergy,"patients-"+size.Width,(int)size.Width,(int)size.Height);
            var delete=(Button)allergy.FindName("BtnDeletePatient");
            Check(delete.TransformToAncestor(allergy).Transform(new Point(0,delete.ActualHeight)).Y<=size.Height,"작은 창에서도 명단 삭제 버튼이 페이지 안에 있습니다");
            Check(Children<TextBlock>(allergy).Any(t=>t.Text.StartsWith("아직 등록된 피급식자")&&t.Visibility==Visibility.Visible),"빈 피급식자 명단 안내가 표시됩니다");
            Children<ScrollViewer>(allergy).FirstOrDefault(v=>v.ScrollableHeight>100)?.ScrollToEnd();
            Render(allergy,"patients-bottom-"+size.Width,(int)size.Width,(int)size.Height);
        }
        var ingredients=new Ingredients();Render(ingredients,"ingredients",1200,800);
        Check(Children<TextBlock>(ingredients).Any(t=>t.Text.StartsWith("위에서 CSV")&&t.Visibility==Visibility.Visible),"빈 메뉴 자료 안내가 표시됩니다");
        var cost=new CalculatePage();Render(cost,"cost",1250,1250);
        var calendar=new EventCalendar();Render(calendar,"calendar",1200,800);
        var menuPage=new MenuInputPage();Render(menuPage,"recipes",1200,1000);
        var start=new DateTime(2026,10,4);
        var days=WeekSchedule.Build(start,new List<IList<object>>(),new Dictionary<DateTime,string>());
        var meals=Enumerable.Range(0,7).Select(i=>{
            var saved=ManagementChecks.Fixture();saved.Raw[1]=start.AddDays(i).ToString("yyyy-MM-dd");
            saved=new StoredMeal{Date=start.AddDays(i),Raw=saved.Raw};
            return new MealDisplayColumn("",Enumerable.Repeat("돼지고기가지볶음 · 120 kcal",6).ToArray(),720,saved);
        }).ToArray();
        foreach(int width in new[]{940,1500})
        {
            var grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false};WeekMealTable.ShowDashboard(grid,meals,days);
            Render(new Page{Content=grid},"dashboard-"+width,width,1000);
            Check(grid.Columns.Sum(c=>c.ActualWidth)<=width+1,"대시보드 열이 창 폭 안에 들어갑니다: "+width);
            Check(Children<DataGridCell>(grid).All(c=>!MealTableEditing.GetIsEditable(c)),"대시보드 셀에는 편집 반응이 없습니다");
        }
        var planner=new Menu_table();var table=(DataGrid)planner.FindName("GridDietResult");WeekMealTable.Show(table,meals,days);Render(planner,"planner",1250,1100);
        var editor=new MealEditWindow(meals[0].Stored!,[],2);var content=(FrameworkElement)editor.Content;editor.Content=null;Render(new Page{Content=content},"meal-edit",620,720);
        Check(Children<Button>(content).Any(b=>Equals(b.Content,"이 끼니 전체 삭제"))&&Children<Button>(content).Any(b=>Equals(b.Content,"변경한 식단 저장")),"통합 창에 저장과 삭제가 함께 표시됩니다");
        app.Dispatcher.InvokeShutdown();
    }
    private static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T item)yield return item;foreach(var nested in Children<T>(child))yield return nested;}
    }
}
