using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace wpf;

public partial class Main
{
    private readonly DispatcherTimer requestBadgeTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool requestBadgeBusy, requestBadgeClosed, requestBadgePending;
    private int requestBadgeFailures;
    private DateTimeOffset requestBadgeNext = DateTimeOffset.MinValue;
    private void ScheduleRequestBadgeCheck(TimeSpan delay)
    {
        requestBadgeTimer.Stop();
        if(requestBadgeClosed || _sheetsService.IsLocal || WindowState==WindowState.Minimized)return;
        requestBadgeTimer.Interval=delay<TimeSpan.FromSeconds(1)?TimeSpan.FromSeconds(1):delay;
        requestBadgeTimer.Start();
    }
    private List<CatalogRequestItem> badgeRows = new();
    private string badgeAccount = "";
    private bool badgeAdmin;
    private void InitializeRequestNotifications()
    {
        requestBadgeTimer.Tick += async (_,_) => await RefreshRequestBadgesAsync();
        Loaded += (_,_) => ScheduleRequestBadgeCheck(TimeSpan.FromMinutes(1));
        StateChanged += async (_,_) => {
            if(WindowState==WindowState.Minimized)requestBadgeTimer.Stop();
            else await RefreshRequestBadgesAsync();
        };
        CatalogRequestReadState.Changed += RequestReadChanged;
        Closed += (_,_) => { requestBadgeClosed=true;requestBadgeTimer.Stop();CatalogRequestReadState.Changed-=RequestReadChanged; };
    }
    private void RequestReadChanged(object? sender, EventArgs e) => RenderRequestBadges();
    private void RenderRequestBadges()
    {
        void Show(Border badge,TextBlock text,int count,string label) {
            text.Text=count>99?"99+":count.ToString();badge.Visibility=count>0?Visibility.Visible:Visibility.Collapsed;
            badge.ToolTip=$"{label} {count}건 · 확인 상태는 이 PC의 로그인 계정별로 보관합니다";
        }
        Show(RequestBadge,RequestBadgeCount,CatalogRequestReadState.CountUnread(badgeAccount,false,
            badgeAdmin?badgeRows.Where(x=>string.Equals(x.Email,badgeAccount,StringComparison.OrdinalIgnoreCase)):badgeRows),"확인하지 않은 답변·상태 변경");
        Show(AdminRequestBadge,AdminRequestBadgeCount,badgeAdmin?CatalogRequestReadState.CountUnread(badgeAccount,true,badgeRows):0,"확인하지 않은 요청");
    }
    private async Task RefreshRequestBadgesAsync(bool force = false)
    {
        if(requestBadgeClosed || _sheetsService.IsLocal || WindowState==WindowState.Minimized)return;
        if(requestBadgeBusy){if(force)requestBadgePending=true;return;}
        if(!force && DateTimeOffset.UtcNow<requestBadgeNext){ScheduleRequestBadgeCheck(requestBadgeNext-DateTimeOffset.UtcNow);return;}
        var service=_sheetsService.FacilityRegistry;
        var account=service.CurrentUser?.Email??"";
        if(account.Length==0)return;
        requestBadgeBusy=true;
        requestBadgeTimer.Stop();
        try {
            bool admin=string.Equals(account,CatalogRequestsClient.AdminEmail,StringComparison.OrdinalIgnoreCase);
            var client=new CatalogRequestsClient(service);var collected=new List<CatalogRequestItem>();
            string? cursor=null;var visited=new HashSet<string>();
            do {
                var batch=await client.ListAsync(admin,cursor);
                if(requestBadgeClosed)return;
                if(admin&&!batch.IsAdmin)throw new InvalidOperationException("관리자 권한 확인 실패");
                collected.AddRange(batch.Items);cursor=batch.NextBefore;
                if(cursor!=null&&!visited.Add(cursor))throw new InvalidOperationException("요청 목록 확인 실패");
            } while(cursor!=null);
            requestBadgeFailures=0;
            badgeAccount=account;badgeAdmin=admin;badgeRows=collected.DistinctBy(x=>x.Id).ToList();RenderRequestBadges();
        } catch {
            requestBadgeFailures=Math.Min(requestBadgeFailures+1,3);
            RequestBadge.ToolTip="새 알림을 확인하지 못했습니다. 이전 표시를 유지하며 다음 확인 때 다시 시도합니다.";
            AdminRequestBadge.ToolTip=RequestBadge.ToolTip;
        } finally {
            requestBadgeBusy=false;
            var delay=requestBadgePending?TimeSpan.FromSeconds(1):TimeSpan.FromMinutes(requestBadgeFailures==0?1:Math.Min(5,Math.Pow(2,requestBadgeFailures)));
            requestBadgePending=false;requestBadgeNext=DateTimeOffset.UtcNow+delay;
            ScheduleRequestBadgeCheck(delay);
        }
    }
}
