# Windows 배포

일반 사용자는 credentials.json이나 서비스 계정 키를 준비하지 않습니다. 앱에 포함된 데스크톱 OAuth 설정으로 본인의 Google 계정에 로그인하고, 본인 드라이브의 사본에 작업합니다.

## 빌드

`dotnet publish wpf.csproj -p:PublishProfile=Windows-x64 -o <새 배포 폴더>`

생성된 폴더 전체를 배포합니다. bin/Debug, bin/Release 폴더를 그대로 압축하지 마세요. 기존 로그인 토큰이 남아 있을 수 있습니다. 현재 결과는 자체 실행 구성요소가 포함된 Windows x64 폴더이며, 서명된 Setup.exe 설치 프로그램은 아닙니다.

모든 빌드는 Prepare-Settings.ps1을 통해 허용된 설정만 포함합니다. 서비스 계정 키와 웹 OAuth 설정은 거부하며 개인 토큰, HolidayApiKey는 포함하지 않습니다. 공휴일 API 키는 배포 파일에 포함하지 않습니다. 각 운영 PC에서 앱을 사용하는 Windows 계정으로 다음 설정 도구를 실행합니다.

```powershell
powershell -NoProfile -File .\Deployment\Set-HolidayApiKey.ps1
```

공공데이터포털의 **디코딩된 서비스 키**를 숨김 입력합니다. 키는 `Baebulttugi/HolidayApiKey`라는 이름으로 Windows 자격 증명 관리자에 저장되며 현재 사용자만 이용합니다. 키를 인수로 전달하거나 appsettings.json에 넣지 마세요. 앱은 해당 JSON의 HolidayApiKey를 읽지 않습니다. 제거는 같은 명령에 `-Remove`를 붙입니다. 공용 키를 모든 사용자에게 배포해야 하는 경우에는 별도 서버 중계가 필요합니다.

키가 없거나 API 조회에 실패하면 기존 공휴일 기본 자료를 유지하고 달력 머리말에 상태를 표시합니다. 실제 API 조회에는 운영 PC의 네트워크 연결과 유효한 서비스 키가 필요합니다.

런타임에서 사용하는 Sheets 설정은 Menu, WeeklyMenu, Recipe, TrackA, TrackB, Diet, Event, Patient, Dashboard, Facility, Cost이며 기존 허용 목록에 모두 포함됩니다. 기존 예약 항목 MealCount, AllergyPatient, DietSave, Ingredient도 유지합니다. 현재 코드가 읽지 않는 User, Allergy, MenuDatabase, FoodNutritionBasic 키를 새로 허용하지 않습니다. SpreadsheetId, TemplateSpreadsheetId와 installed 방식 OAuth 설정은 기존처럼 포함됩니다.

## 공개 배포 전 확인

- 공용 템플릿에는 공개 가능한 레시피만 두세요. 개인정보, 피급식자 명단, 개인 일정, 운영 메모는 별도 비공개 시트에 보관하세요. 현재 최초 설정은 템플릿 전체를 복사합니다. 복사 후 일부 예시를 지우는 동작은 정보 유출 방지가 아닙니다.
- 원본은 일반 사용자에게 보기 권한만 부여하고, 상위 폴더에서 편집 권한을 상속받는지도 확인하세요. 앱의 원본 쓰기 차단은 보조 수단이며 Google 공유 권한을 대신하지 못합니다.
- Google Cloud OAuth 동의 화면의 공개 상태, 테스트 사용자 제한, 사용하는 Drive/Sheets 권한의 검증 필요 여부를 확인하세요. 현재 전체 Drive 및 Sheets 접근 권한을 요청합니다. 권한을 더 줄이려면 파일 선택/복사 흐름 변경과 별도 검증이 필요합니다.
- 로그인 토큰은 각 Windows 사용자 LocalAppData/Baebulttugi/GoogleSignIn에 저장됩니다. 현재 파일 저장 방식이므로 해당 Windows 계정이 침해되면 안전을 보장할 수 없습니다. 원격 지원 시 이 폴더를 전달하지 마세요.
- 설치 프로그램 및 코드 서명은 별도 단계입니다. 공개 공유 권한과 신규 계정 로그인은 실제 환경에서 확인해야 합니다.
