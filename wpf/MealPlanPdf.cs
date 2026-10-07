using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace wpf;

/// <summary>A4 portrait menu and recipe pages. Embedded images preserve Korean fonts in print.</summary>
public static class MealPlanPdf
{
    public const double Width=1120, Height=1584;
    const double Inner=1056;
    static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(32,60,68));
    static readonly Brush Line=new SolidColorBrush(Color.FromRgb(159,193,205));
    static readonly Brush Tint=new SolidColorBrush(Color.FromRgb(222,236,241));
    static TextBlock Text(string value,double size=17,bool bold=false) => new() {Text=value,FontSize=size,FontFamily=new FontFamily("Malgun Gothic"),Foreground=Ink,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};
    static Border Cell(string value,bool heading=false) => new() {Padding=new Thickness(6),BorderBrush=Line,BorderThickness=new Thickness(0,0,1,1),Background=heading?Tint:Brushes.White,Child=new TextBlock {Text=value,FontFamily=new FontFamily("Malgun Gothic"),Foreground=Ink,FontSize=heading?17:17,VerticalAlignment=VerticalAlignment.Center,LineHeight=26,FontWeight=heading?FontWeights.Bold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center}};
    static Grid Row(string label,IEnumerable<string> values,bool heading=false,double minHeight=0)
    {
        var grid=new Grid {Width=Inner,MinHeight=minHeight};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(80)});
        for(int i=0;i<7;i++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength((Inner-80)/7)});
        grid.Children.Add(Cell(label,true));int col=1;
        foreach(string value in values) {var cell=Cell(value,heading);Grid.SetColumn(cell,col++);grid.Children.Add(cell);}
        return grid;
    }
    public static IReadOnlyList<FrameworkElement> CreatePages(string facility,DateTime week,IReadOnlyList<MealDisplayColumn> source,IReadOnlyDictionary<string,string>? cooking=null,IReadOnlyDictionary<string,string>? allergens=null,bool sundayFirst=false,IReadOnlyList<WeekDayNotice>? schedule=null)
    {
        week=sundayFirst?WeeklyMealPlanner.Sunday(week):WeeklyMealPlanner.Monday(week);
        var meals=source.Where(x=>x.Stored!=null&&x.Stored.Date>=week&&x.Stored.Date<week.AddDays(7)).ToList();
        if(meals.Count==0)throw new InvalidOperationException("출력할 저장된 식단이 없습니다.");
        var days=Enumerable.Range(0,7).Select(i=>week.AddDays(i)).Select(d=>schedule?.FirstOrDefault(n=>n.Date.Date==d)??new WeekDayNotice(d,CalendarDates.Holidays(d.Year).GetValueOrDefault(d,""),"")).ToArray();
        var pages=new List<FrameworkElement>();
        foreach(var age in meals.GroupBy(x=>x.Stored!.AgeGroup))
        {
            StackPanel body=null!;double used=0;
            StackPanel? mealTable=null;
            string recipeContext="";
            void NewPage(bool recipe=false)
            {
                var page=new Grid {Width=Width,Height=Height,Background=Brushes.White};
                var accent=new Border {Background=Tint,Height=116,VerticalAlignment=VerticalAlignment.Top};page.Children.Add(accent);
                body=new StackPanel {Margin=new Thickness(32,20,32,0)};page.Children.Add(body);
                var title=Text($"{week:yyyy년 M월} "+(recipe?"조리방법":"식단표"),38,true);title.TextAlignment=TextAlignment.Center;title.Foreground=new SolidColorBrush(Color.FromRgb(61,134,157));body.Children.Add(title);
                var subtitle=Text($"{facility} · {(age.Key.Length==0?"연령 미지정":age.Key+"세")} | {week:M월 d일} ~ {week.AddDays(6):M월 d일}",19,true);subtitle.TextAlignment=TextAlignment.Center;subtitle.Margin=new Thickness(0,6,0,10);body.Children.Add(subtitle);
                var meta=Text($"발행: {facility}    작성일: {DateTime.Today:yyyy.MM.dd}    배불뚝이 SMART MEAL CARE",12);meta.VerticalAlignment=VerticalAlignment.Bottom;meta.Margin=new Thickness(32,0,32,42);page.Children.Add(meta);
                if(recipe && recipeContext.Length>0)
                {
                    var context=Text(recipeContext,22,true);context.Margin=new Thickness(0,18,0,8);body.Children.Add(context);
                }
                body.Measure(new Size(Width,double.PositiveInfinity));used=body.DesiredSize.Height;
                var footer=Text($"저장된 식단 · 1인분 기준 · 미확인 영양값은 0으로 계산하지 않습니다.                         {pages.Count+1}",12);footer.VerticalAlignment=VerticalAlignment.Bottom;footer.Margin=new Thickness(32,0,32,20);page.Children.Add(footer);pages.Add(page);
            }
            void Add(FrameworkElement item,bool recipe=false)
            {
                if(mealTable!=null && !recipe){mealTable.Children.Add(item);return;}
                item.Measure(new Size(Inner,double.PositiveInfinity));
                if(used+item.DesiredSize.Height>Height-65)NewPage(recipe);
                if(used+item.DesiredSize.Height>Height-65)throw new InvalidOperationException("한 항목의 내용이 너무 깁니다. 내용을 확인해 주세요.");
                body.Children.Add(item);used+=item.DesiredSize.Height;
            }
            NewPage();
            mealTable=new StackPanel {Width=Inner};
            var dates=Row("일자",days.Select(d=>$"{d.Date:%d}일 ({"일월화수목금토"[(int)d.Date.DayOfWeek]})"+(d.Description.Length>0?"\n"+d.Description:"")),true);
            for(int i=0;i<7;i++)((TextBlock)((Border)dates.Children[i+1]).Child).Foreground=(Brush)new BrushConverter().ConvertFromString(days[i].DateColor)!;
            Add(dates);
            foreach(var group in age.GroupBy(x=>x.Stored!.Meal).OrderBy(g=>g.Key=="조식"?0:g.Key=="중식"?1:g.Key=="석식"?2:3))
            {
                string[] PerDay(Func<MealDisplayColumn,string> display)=>Enumerable.Range(0,7).Select(i=>string.Join("\n",group.Where(x=>x.Stored!.Date==week.AddDays(i)).Select(display))).Select((x,i)=>x.Length==0?(days[i].IsClosed?"휴무":"-"):x).ToArray();
                string Menu(MealDisplayColumn meal)
                {
                    var menuLines=meal.Stored!.Menus.Where(n=>!string.IsNullOrWhiteSpace(n)).Select(n=>
                        n+(allergens?.TryGetValue(CookingMethods.Key(n),out var code)==true && AllergyCodes(code).Length>0?" ("+AllergyCodes(code)+")":""));
                    return string.Join("\n",menuLines)+"\n\n"+N(meal.Calories,"kcal")+
                        "\n탄 "+N(meal.Stored.Carb,"g")+"\n단 "+N(meal.Stored.Protein,"g")+"\n지 "+N(meal.Stored.Fat,"g");
                }
                Add(Row(group.Key,PerDay(Menu),minHeight:270));
            }
            var legend=Text("알레르기: "+string.Join(" · ",AllergenPicker.Names.Select((n,i)=>$"{i+1} {n}"))+"\n코드가 비어 있는 메뉴는 알레르기 없음이 아니라 확인이 필요한 항목입니다.",12);legend.Margin=new Thickness(0,10,0,14);Add(legend);
            mealTable.Measure(new Size(Inner,double.PositiveInfinity));
            var table=mealTable;mealTable=null;
            double available=Height-80-used;
            Add(new Viewbox {Width=Inner,Height=Math.Min(available,table.DesiredSize.Height),Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Child=table});
            if(cooking!=null)
            {
                NewPage(true);
                var heading=Text("이번 식단의 조리방법",23,true);heading.Margin=new Thickness(0,14,0,10);Add(heading,true);
                Add(Text("자료: test 구글 시트 · 조리방법 | 메뉴명 일치 기준 · 날짜·끼니별 안내",12),true);
                foreach(var day in age.GroupBy(m=>m.Stored!.Date.Date).OrderBy(g=>g.Key))
                foreach(var meal in day.GroupBy(m=>m.Stored!.Meal).OrderBy(g=>g.Key=="조식"?0:g.Key=="중식"?1:g.Key=="석식"?2:3))
                {
                    recipeContext=$"{day.Key:M월 d일} ({"일월화수목금토"[(int)day.Key.DayOfWeek]}) · {meal.Key}";
                    if(Height-65-used<180)NewPage(true);
                    else
                    {
                        var context=Text(recipeContext,22,true);context.Margin=new Thickness(0,22,0,8);Add(context,true);
                    }
                    foreach(var name in meal.SelectMany(m=>m.Stored!.Menus).Where(n=>n.Length>0).DistinctBy(CookingMethods.Key))
                    {
                    if(!cooking.TryGetValue(CookingMethods.Key(name),out string? method) || string.IsNullOrWhiteSpace(method))
                    {
                        var missing=Text(name+" · 조리방법 미등록",14);missing.Margin=new Thickness(12,6,0,6);Add(missing,true);continue;
                    }
                    // Split long methods by measured character ranges, preserving every character.
                    bool continued=false;
                    while(method.Length>0)
                    {
                        double remaining=Height-65-used;
                        if(remaining<100){NewPage(true);remaining=Height-65-used;}
                        int low=1,high=method.Length,best=0;
                        Border Block(int length)
                        {
                            var content=Text("",16);content.LineHeight=27;
                            content.Inlines.Add(new Run(name+(continued?" (계속)":"")) {FontSize=20,FontWeight=FontWeights.Bold});
                            content.Inlines.Add(new Run("\n"+method[..length]));
                            return new Border {Padding=new Thickness(12,14,12,14),Margin=new Thickness(0,10,0,0),CornerRadius=new CornerRadius(8),Background=new SolidColorBrush(Color.FromRgb(246,249,249)),BorderBrush=Line,BorderThickness=new Thickness(3,0,0,0),Child=content};
                        }
                        while(low<=high){int mid=(low+high)/2;var test=Block(mid);test.Measure(new Size(Inner,double.PositiveInfinity));if(test.DesiredSize.Height<=remaining){best=mid;low=mid+1;}else high=mid-1;}
                        if(best==0){NewPage(true);continue;}
                        // Avoid splitting a UTF-16 surrogate pair across pages.
                        if(best<method.Length&&char.IsHighSurrogate(method[best-1]))best--;
                        Add(Block(best),true);method=method[best..];continued=true;
                    }
                }
                }
            }
        }
        return pages;
    }
    static string AllergyCodes(string raw)
    {
        for(int i=1;i<=19;i++)raw=raw.Replace(((char)('①'+i-1)).ToString(),i+",");
        return string.Join(",",Regex.Matches(raw,@"(?<!\d)\d{1,2}(?!\d)").Select(m=>int.Parse(m.Value)).Where(n=>n>=1&&n<=19).Distinct().OrderBy(n=>n));
    }
    static string N(double? value,string unit)=>value.HasValue?value.Value.ToString("0.#",CultureInfo.InvariantCulture)+" "+unit:"미확인";
    public static void Save(string path,string facility,DateTime week,IReadOnlyList<MealDisplayColumn> meals, IReadOnlyDictionary<string,string>? cooking=null, IReadOnlyDictionary<string,string>? allergens=null,bool sundayFirst=false,IReadOnlyList<WeekDayNotice>? schedule=null)
    {
        var images=new List<byte[]>();
        foreach(var page in CreatePages(facility,week,meals,cooking,allergens,sundayFirst,schedule))
        {
            page.Measure(new Size(Width,Height));page.Arrange(new Rect(0,0,Width,Height));page.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Width*2,(int)Height*2,192,192,PixelFormats.Pbgra32);bitmap.Render(page);
            var encoder=new JpegBitmapEncoder {QualityLevel=96};encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var image=new MemoryStream();encoder.Save(image);images.Add(image.ToArray());
        }
        // Build fully in memory before replacing the user's chosen output file.
        using var pdf=new MemoryStream();
        void Write(string text) {var data=Encoding.ASCII.GetBytes(text);pdf.Write(data);}
        var offsets=new List<long>{0};
        void Object(int number,string value,byte[]? stream=null) {offsets.Add(pdf.Position);Write($"{number} 0 obj\n{value}\n");if(stream!=null){Write("stream\n");pdf.Write(stream);Write("\nendstream\n");}Write("endobj\n");}
        Write("%PDF-1.4\n");Object(1,"<< /Type /Catalog /Pages 2 0 R >>");
        Object(2,$"<< /Type /Pages /Count {images.Count} /Kids ["+string.Join(" ",Enumerable.Range(0,images.Count).Select(i=>$"{3+i*3} 0 R"))+"] >>");
        for(int i=0;i<images.Count;i++)
        {
            int id=3+i*3;
            Object(id,$"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /XObject << /Img {id+2} 0 R >> >> /Contents {id+1} 0 R >>");
            var content=Encoding.ASCII.GetBytes("q 595.28 0 0 841.89 0 0 cm /Img Do Q");Object(id+1,$"<< /Length {content.Length} >>",content);
            Object(id+2,$"<< /Type /XObject /Subtype /Image /Width {(int)Width*2} /Height {(int)Height*2} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {images[i].Length} >>",images[i]);
        }
        long xref=pdf.Position;Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");foreach(long offset in offsets.Skip(1))Write(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(path,pdf.ToArray());
    }
}
