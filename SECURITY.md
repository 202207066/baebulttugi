# 보안 안내 (반드시 먼저 읽어주세요)

## 🔴 즉시 조치가 필요한 사항

이 저장소의 **git 히스토리에 구글 서비스 계정 개인키가 남아 있습니다.**

커밋 `2d68cdf "구글키 제거"` 에서 `wpf/Google_key.json` 파일을 삭제했지만, 삭제 커밋은
파일을 "이후 시점에서 없앨" 뿐 **이전 커밋에 담긴 내용은 그대로 보존**합니다.
공개 저장소이므로 누구나 아래 명령 한 줄로 개인키 전문을 꺼낼 수 있습니다.

```bash
git show 2d68cdf~1:wpf/Google_key.json
```

노출된 항목: `type: service_account`, `project_id`, `private_key_id`,
`private_key`(PEM 전문), `client_email`, `client_id`.

### 해야 할 일 (순서대로)

1. **키 폐기 — 이것이 가장 중요합니다.**
   Google Cloud Console → *IAM 및 관리자* → *서비스 계정* → 해당 계정 →
   *키* 탭에서 노출된 키를 **삭제**합니다.
   해당 서비스 계정을 더 이상 쓰지 않는다면 계정 자체를 삭제하세요.

   > 파일을 지우거나 히스토리를 정리해도 **키 자체는 무효화되지 않습니다.**
   > 폐기하지 않으면 유출된 키는 계속 유효합니다.

2. **공공데이터포털 서비스 키 재발급.**
   `EventCalendar.xaml.cs`에 하드코딩되어 있던 특일정보 API 키도 공개된 상태였습니다.
   [공공데이터포털](https://www.data.go.kr) 마이페이지에서 재발급받으세요.

3. **(선택) 히스토리 정리.**
   1번을 마쳤다면 실질적 위험은 사라집니다. 그래도 히스토리에서 파일을 지우고 싶다면
   [`git filter-repo`](https://github.com/newren/git-filter-repo)를 사용하세요.

   ```bash
   git filter-repo --path wpf/Google_key.json --invert-paths
   git push --force --all
   ```

   모든 팀원이 저장소를 다시 클론해야 하고, 이미 저장소를 본 사람이 로컬에 남긴 사본까지는
   지울 수 없습니다. **1번의 대체재가 아니라 보완재입니다.**

## 앞으로의 비밀정보 관리

비밀정보는 코드에 두지 않고 `appsettings.json` 한 곳에 모읍니다.
이 파일과 인증 파일들은 `.gitignore`에 등록되어 커밋되지 않습니다.

| 파일 | 내용 | 커밋 여부 |
|---|---|---|
| `wpf/appsettings.sample.json` | 설정 항목의 예시(값 없음) | ✅ 커밋함 |
| `wpf/appsettings.json` | 실제 스프레드시트 ID·API 키 | ❌ 각자 로컬에만 |
| `wpf/credentials.json` | 구글 OAuth 클라이언트 비밀 | ❌ 각자 로컬에만 |
| `wpf/token.json/` | 로그인 후 저장되는 토큰 | ❌ 각자 로컬에만 |

### 처음 받았을 때

```bash
cd wpf
cp appsettings.sample.json appsettings.json
# appsettings.json 을 열어 SpreadsheetId 와 HolidayApiKey 를 채웁니다.
# credentials.json 은 팀 내부에서 안전한 경로로 전달받아 같은 폴더에 둡니다.
```

### 커밋 전 확인 습관

```bash
git status                       # 추적되지 않아야 할 파일이 올라와 있지 않은지
git diff --cached                # 실제로 무엇이 커밋되는지
```

키처럼 보이는 문자열(`-----BEGIN`, `AIza`, `serviceKey=`)이 diff에 보이면 커밋하지 마세요.
