using Google.Apis.Drive.v3.Data;
using Google.Apis.Sheets.v4.Data;
using System.IO;

namespace wpf;

public partial class GoogleSheetsService
{
    public async Task PreparePersonalDatabaseAsync(IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        progress.Report("내 구글 드라이브에 저장된 식단 자료를 확인하는 중입니다…");
        string? id = null;
        var lookup = _driveService.Files.List();
        lookup.Q = "trashed = false and 'me' in owners and mimeType = 'application/vnd.google-apps.spreadsheet' and appProperties has { key='baebulttugi' and value='meal-db-v1' }";
        lookup.Fields = "files(id),nextPageToken";
        lookup.OrderBy = "modifiedTime desc";
        lookup.PageSize = 1;
        id = (await lookup.ExecuteAsync(cancellationToken)).Files?.FirstOrDefault()?.Id;

        // Adopt legacy data only if it is a writable spreadsheet owned by this account.
        if (id == null && System.IO.File.Exists(AppConfig.UserSheetIdFilePath))
        {
            string legacyId = System.IO.File.ReadAllText(AppConfig.UserSheetIdFilePath).Trim();
            if (legacyId.Length > 0 && legacyId != AppConfig.TemplateSpreadsheetId && legacyId != AppConfig.SpreadsheetId)
            {
                try
                {
                    var get = _driveService.Files.Get(legacyId); get.Fields = "id,mimeType,ownedByMe,trashed,capabilities(canEdit)";
                    var legacy = await get.ExecuteAsync(cancellationToken);
                    if (legacy.OwnedByMe == true && legacy.Trashed != true && legacy.Capabilities?.CanEdit == true && legacy.MimeType == "application/vnd.google-apps.spreadsheet")
                    {
                        await _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { AppProperties = new Dictionary<string,string> { ["baebulttugi"] = "meal-db-v1" } }, legacyId).ExecuteAsync(cancellationToken);
                        id = legacyId;
                    }
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound) { }
            }
        }
        if (id != null)
        {
            SetUserSpreadsheetId(id);
            progress.Report("기존 자료를 확인하는 중입니다. 저장된 식단은 유지합니다…");
            await GetSheetTitlesAsync(cancellationToken: cancellationToken);
            return;
        }
        progress.Report("처음 사용할 내 식단 자료를 구글 드라이브에 준비하는 중입니다…");
        cancellationToken.ThrowIfCancellationRequested();
        id = await SetupUserDatabaseAsync(cancellationToken);
        // Mark only after the blank workbook was created successfully. Never copy source people or examples.
        await _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { AppProperties = new Dictionary<string,string> { ["baebulttugi"] = "meal-db-v1", ["empty-meal-start"] = "2" } }, id).ExecuteAsync(cancellationToken);
        progress.Report("내 식단 자료 준비가 끝났습니다. 시작 화면을 여는 중입니다…");
    }
}
